using System.IO;
using System.Text.Json;
using System.Timers;
using NXConfigLauncher.Helpers;
using NXConfigLauncher.Models;

namespace NXConfigLauncher.Services
{
    public interface IConfigService
    {
        AppConfig Load();
        bool Save(AppConfig config);
        bool UpdateSelectedVersion(string versionName, string versionPath);
        bool UpdateNetworkBlocked(bool isBlocked);
        bool UpdateLicensePort(string port);
        bool UpdateLanguage(string language);
        string GetConfigFilePath();
        bool ConfigFileExists();
        bool DeleteConfig();
        void Flush();
    }

    /// <summary>
    /// 최적화된 설정 서비스
    /// - 메모리 캐싱으로 반복적인 파일 읽기 방지
    /// - FileSystemWatcher로 외부 변경 감지
    /// - 지연 저장(debounce)으로 연속적인 저장 요청 최적화
    /// </summary>
    public class ConfigService : IConfigService, IDisposable
    {
        private readonly string _configDirectory;
        private readonly string _configFilePath;
        private readonly object _lock = new();
        private readonly FileSystemWatcher? _fileWatcher;
        private readonly System.Timers.Timer _saveDebounceTimer;

        // 캐시
        private AppConfig? _cachedConfig;
        private DateTime _cacheTimestamp;
        private AppConfig? _pendingSaveConfig;
        private bool _isDisposed;

        // 설정
        private const int SaveDebounceMs = 500; // 저장 지연 시간
        private static readonly TimeSpan CacheValidDuration = TimeSpan.FromSeconds(30); // 캐시 유효 시간

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public ConfigService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _configDirectory = Path.Combine(appDataPath, "NXConfigLauncher");
            _configFilePath = Path.Combine(_configDirectory, "config.json");

            // 디렉토리 생성
            EnsureDirectoryExists();

            // 지연 저장 타이머 초기화
            _saveDebounceTimer = new System.Timers.Timer(SaveDebounceMs)
            {
                AutoReset = false
            };
            _saveDebounceTimer.Elapsed += OnSaveDebounceElapsed;

            // 파일 변경 감시 초기화
            _fileWatcher = InitializeFileWatcher();
        }

        private void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_configDirectory))
                {
                    Directory.CreateDirectory(_configDirectory);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create config directory: {ex.Message}");
            }
        }

        private FileSystemWatcher? InitializeFileWatcher()
        {
            try
            {
                if (!Directory.Exists(_configDirectory))
                    return null;

                var watcher = new FileSystemWatcher(_configDirectory)
                {
                    Filter = "config.json",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                watcher.Changed += OnConfigFileChanged;
                watcher.Deleted += OnConfigFileDeleted;

                return watcher;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize file watcher: {ex.Message}");
                return null;
            }
        }

        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            lock (_lock)
            {
                // 외부에서 파일이 변경되면 캐시 무효화
                InvalidateCache();
                Logger.Info("Config file changed externally, cache invalidated.");
            }
        }

        private void OnConfigFileDeleted(object sender, FileSystemEventArgs e)
        {
            lock (_lock)
            {
                InvalidateCache();
                Logger.Info("Config file deleted externally, cache invalidated.");
            }
        }

        private void InvalidateCache()
        {
            _cachedConfig = null;
            _cacheTimestamp = DateTime.MinValue;
        }

        private bool IsCacheValid()
        {
            if (_cachedConfig == null)
                return false;

            return DateTime.Now - _cacheTimestamp < CacheValidDuration;
        }

        public AppConfig Load()
        {
            lock (_lock)
            {
                // 캐시가 유효하면 캐시에서 반환
                if (IsCacheValid())
                {
                    return CloneConfig(_cachedConfig!);
                }

                try
                {
                    if (!File.Exists(_configFilePath))
                    {
                        _cachedConfig = AppConfig.Default;
                        _cacheTimestamp = DateTime.Now;
                        return CloneConfig(_cachedConfig);
                    }

                    var json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

                    _cachedConfig = config ?? AppConfig.Default;
                    _cacheTimestamp = DateTime.Now;

                    return CloneConfig(_cachedConfig);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load config: {ex.Message}");
                    return AppConfig.Default;
                }
            }
        }

        public bool Save(AppConfig config)
        {
            lock (_lock)
            {
                try
                {
                    // 캐시 즉시 업데이트 (UI 반응성)
                    _cachedConfig = CloneConfig(config);
                    _cacheTimestamp = DateTime.Now;

                    // 지연 저장 예약
                    _pendingSaveConfig = CloneConfig(config);
                    _saveDebounceTimer.Stop();
                    _saveDebounceTimer.Start();

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to schedule config save: {ex.Message}");
                    return false;
                }
            }
        }

        private void OnSaveDebounceElapsed(object? sender, ElapsedEventArgs e)
        {
            FlushInternal();
        }

        private void FlushInternal()
        {
            AppConfig? configToSave;

            lock (_lock)
            {
                configToSave = _pendingSaveConfig;
                _pendingSaveConfig = null;
            }

            if (configToSave == null)
                return;

            try
            {
                EnsureDirectoryExists();

                var json = JsonSerializer.Serialize(configToSave, JsonOptions);

                // 임시 파일에 먼저 쓰고 이동 (원자적 쓰기)
                var tempFile = _configFilePath + ".tmp";
                File.WriteAllText(tempFile, json);

                // FileSystemWatcher 일시 중지
                if (_fileWatcher != null)
                    _fileWatcher.EnableRaisingEvents = false;

                try
                {
                    File.Move(tempFile, _configFilePath, overwrite: true);
                }
                finally
                {
                    if (_fileWatcher != null)
                        _fileWatcher.EnableRaisingEvents = true;
                }

                Logger.Info("Config saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save config: {ex.Message}");
            }
        }

        /// <summary>
        /// 대기 중인 저장을 즉시 실행 (프로그램 종료 시 호출)
        /// </summary>
        public void Flush()
        {
            _saveDebounceTimer.Stop();
            FlushInternal();
        }

        public bool UpdateSelectedVersion(string versionName, string versionPath)
        {
            var config = Load();
            config.SelectedNxVersion = versionName;
            config.SelectedNxPath = versionPath;
            return Save(config);
        }

        public bool UpdateNetworkBlocked(bool isBlocked)
        {
            var config = Load();
            config.IsNetworkBlocked = isBlocked;
            return Save(config);
        }

        public bool UpdateLicensePort(string port)
        {
            var config = Load();
            config.LicensePort = port;
            return Save(config);
        }

        public bool UpdateLanguage(string language)
        {
            var config = Load();
            config.Language = language;
            return Save(config);
        }

        public string GetConfigFilePath()
        {
            return _configFilePath;
        }

        public bool ConfigFileExists()
        {
            return File.Exists(_configFilePath);
        }

        public bool DeleteConfig()
        {
            lock (_lock)
            {
                try
                {
                    InvalidateCache();
                    _pendingSaveConfig = null;
                    _saveDebounceTimer.Stop();

                    if (File.Exists(_configFilePath))
                    {
                        File.Delete(_configFilePath);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to delete config: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// AppConfig 복제 (참조 공유 방지)
        /// </summary>
        private static AppConfig CloneConfig(AppConfig source)
        {
            return new AppConfig
            {
                SelectedNxVersion = source.SelectedNxVersion,
                SelectedNxPath = source.SelectedNxPath,
                IsNetworkBlocked = source.IsNetworkBlocked,
                LicensePort = source.LicensePort,
                Language = source.Language
            };
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                // 저장 대기 중인 설정 플러시
                Flush();

                _saveDebounceTimer.Stop();
                _saveDebounceTimer.Dispose();

                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Dispose();
                }
            }

            _isDisposed = true;
        }

        ~ConfigService()
        {
            Dispose(false);
        }
    }
}
