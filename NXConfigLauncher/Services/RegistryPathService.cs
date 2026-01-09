using Microsoft.Win32;
using NXConfigLauncher.Helpers;

namespace NXConfigLauncher.Services
{
    /// <summary>
    /// Siemens/NX 설치 경로를 레지스트리에서 동적으로 조회하는 서비스
    /// </summary>
    public interface IRegistryPathService
    {
        string? GetSiemensInstallPath();
        string? GetLicenseServerPath();
        string? GetNxInstallPath(string versionKey);
        IEnumerable<string> GetAllNxVersionKeys();
    }

    public class RegistryPathService : IRegistryPathService
    {
        // 레지스트리 경로 상수
        private const string SiemensRegistryPath = @"SOFTWARE\Siemens";
        private const string LicenseServerRegistryPath = @"SOFTWARE\FLEXlm License Manager";
        private const string UninstallRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string Wow64UninstallPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        // 폴백 경로 (레지스트리에서 찾지 못할 경우)
        private const string FallbackSiemensPath = @"C:\Program Files\Siemens";
        private const string FallbackLicenseServerPath = @"C:\Program Files\Siemens\PLMLicenseServer";

        // 캐시
        private string? _cachedSiemensPath;
        private string? _cachedLicenseServerPath;
        private readonly object _cacheLock = new();

        /// <summary>
        /// Siemens 기본 설치 경로를 레지스트리에서 조회
        /// </summary>
        public string? GetSiemensInstallPath()
        {
            lock (_cacheLock)
            {
                if (_cachedSiemensPath != null)
                    return _cachedSiemensPath;

                _cachedSiemensPath = FindSiemensInstallPath();
                return _cachedSiemensPath;
            }
        }

        /// <summary>
        /// PLM 라이선스 서버 설치 경로를 레지스트리에서 조회
        /// </summary>
        public string? GetLicenseServerPath()
        {
            lock (_cacheLock)
            {
                if (_cachedLicenseServerPath != null)
                    return _cachedLicenseServerPath;

                _cachedLicenseServerPath = FindLicenseServerPath();
                return _cachedLicenseServerPath;
            }
        }

        /// <summary>
        /// 특정 NX 버전의 설치 경로를 레지스트리에서 조회
        /// </summary>
        public string? GetNxInstallPath(string versionKey)
        {
            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(SiemensRegistryPath);
                if (baseKey == null) return null;

                using var nxKey = baseKey.OpenSubKey(versionKey);
                if (nxKey == null) return null;

                // 다양한 키 이름 시도
                var installPath = nxKey.GetValue("InstallDir") as string
                               ?? nxKey.GetValue("UGII_BASE_DIR") as string
                               ?? nxKey.GetValue("Path") as string
                               ?? nxKey.GetValue("InstallLocation") as string;

                if (!string.IsNullOrEmpty(installPath))
                    return installPath;

                // 하위 키에서 검색
                foreach (var subKeyName in nxKey.GetSubKeyNames())
                {
                    using var subKey = nxKey.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    installPath = subKey.GetValue("InstallDir") as string
                               ?? subKey.GetValue("UGII_BASE_DIR") as string
                               ?? subKey.GetValue("Path") as string;

                    if (!string.IsNullOrEmpty(installPath))
                        return installPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get NX install path for '{versionKey}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 레지스트리에 등록된 모든 NX 버전 키 목록 반환
        /// </summary>
        public IEnumerable<string> GetAllNxVersionKeys()
        {
            var keys = new List<string>();

            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(SiemensRegistryPath);
                if (baseKey == null) return keys;

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    if (subKeyName.StartsWith("NX", StringComparison.OrdinalIgnoreCase))
                    {
                        // PLMLicenseServer 등 제외
                        if (!subKeyName.Contains("License", StringComparison.OrdinalIgnoreCase))
                        {
                            keys.Add(subKeyName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to enumerate NX version keys: {ex.Message}");
            }

            return keys;
        }

        private string? FindSiemensInstallPath()
        {
            try
            {
                // 1. Siemens 레지스트리 키에서 공통 설치 경로 찾기
                using var siemensKey = Registry.LocalMachine.OpenSubKey(SiemensRegistryPath);
                if (siemensKey != null)
                {
                    var installDir = siemensKey.GetValue("InstallDir") as string
                                  ?? siemensKey.GetValue("InstallPath") as string;

                    if (!string.IsNullOrEmpty(installDir) && System.IO.Directory.Exists(installDir))
                    {
                        Logger.Info($"Found Siemens install path from registry: {installDir}");
                        return installDir;
                    }

                    // NX 설치 경로에서 상위 Siemens 폴더 추론
                    foreach (var nxKey in GetAllNxVersionKeys().Take(1))
                    {
                        var nxPath = GetNxInstallPath(nxKey);
                        if (!string.IsNullOrEmpty(nxPath))
                        {
                            // NX 경로에서 Siemens 폴더 추출
                            var siemensPath = ExtractSiemensPath(nxPath);
                            if (!string.IsNullOrEmpty(siemensPath))
                            {
                                Logger.Info($"Inferred Siemens path from NX install: {siemensPath}");
                                return siemensPath;
                            }
                        }
                    }
                }

                // 2. Uninstall 레지스트리에서 Siemens 제품 찾기
                var uninstallPath = FindSiemensFromUninstall();
                if (!string.IsNullOrEmpty(uninstallPath))
                {
                    Logger.Info($"Found Siemens path from uninstall registry: {uninstallPath}");
                    return uninstallPath;
                }

                // 3. Program Files에서 Siemens 폴더 존재 확인
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var siemensProgramFilesPath = System.IO.Path.Combine(programFiles, "Siemens");
                if (System.IO.Directory.Exists(siemensProgramFilesPath))
                {
                    Logger.Info($"Found Siemens path in Program Files: {siemensProgramFilesPath}");
                    return siemensProgramFilesPath;
                }

                // 4. 폴백 경로
                if (System.IO.Directory.Exists(FallbackSiemensPath))
                {
                    Logger.Info($"Using fallback Siemens path: {FallbackSiemensPath}");
                    return FallbackSiemensPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to find Siemens install path: {ex.Message}");
            }

            return null;
        }

        private string? FindLicenseServerPath()
        {
            try
            {
                // 1. FLEXlm 레지스트리에서 찾기
                using var flexKey = Registry.LocalMachine.OpenSubKey(LicenseServerRegistryPath);
                if (flexKey != null)
                {
                    foreach (var subKeyName in flexKey.GetSubKeyNames())
                    {
                        using var subKey = flexKey.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var path = subKey.GetValue("LMGRD_PATH") as string
                                ?? subKey.GetValue("LM_LICENSE_FILE") as string;

                        if (!string.IsNullOrEmpty(path))
                        {
                            var dir = System.IO.Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                            {
                                Logger.Info($"Found license server path from FLEXlm registry: {dir}");
                                return dir;
                            }
                        }
                    }
                }

                // 2. Siemens PLMLicenseServer 레지스트리에서 찾기
                using var plmKey = Registry.LocalMachine.OpenSubKey($@"{SiemensRegistryPath}\PLMLicenseServer");
                if (plmKey != null)
                {
                    var installDir = plmKey.GetValue("InstallDir") as string
                                  ?? plmKey.GetValue("Path") as string;

                    if (!string.IsNullOrEmpty(installDir) && System.IO.Directory.Exists(installDir))
                    {
                        Logger.Info($"Found license server path from PLM registry: {installDir}");
                        return installDir;
                    }
                }

                // 3. Siemens 설치 경로 하위에서 찾기
                var siemensPath = GetSiemensInstallPath();
                if (!string.IsNullOrEmpty(siemensPath))
                {
                    var plmServerPath = System.IO.Path.Combine(siemensPath, "PLMLicenseServer");
                    if (System.IO.Directory.Exists(plmServerPath))
                    {
                        Logger.Info($"Found license server path under Siemens: {plmServerPath}");
                        return plmServerPath;
                    }
                }

                // 4. 폴백 경로
                if (System.IO.Directory.Exists(FallbackLicenseServerPath))
                {
                    Logger.Info($"Using fallback license server path: {FallbackLicenseServerPath}");
                    return FallbackLicenseServerPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to find license server path: {ex.Message}");
            }

            return null;
        }

        private string? FindSiemensFromUninstall()
        {
            var uninstallPaths = new[] { UninstallRegistryPath, Wow64UninstallPath };

            foreach (var regPath in uninstallPaths)
            {
                try
                {
                    using var uninstallKey = Registry.LocalMachine.OpenSubKey(regPath);
                    if (uninstallKey == null) continue;

                    foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        using var subKey = uninstallKey.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var publisher = subKey.GetValue("Publisher") as string;
                        var displayName = subKey.GetValue("DisplayName") as string;

                        if ((publisher?.Contains("Siemens", StringComparison.OrdinalIgnoreCase) == true ||
                             displayName?.Contains("NX", StringComparison.OrdinalIgnoreCase) == true) &&
                            displayName?.Contains("License", StringComparison.OrdinalIgnoreCase) != true)
                        {
                            var installLocation = subKey.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrEmpty(installLocation))
                            {
                                var siemensPath = ExtractSiemensPath(installLocation);
                                if (!string.IsNullOrEmpty(siemensPath))
                                    return siemensPath;
                            }
                        }
                    }
                }
                catch
                {
                    // 무시하고 다음 경로 시도
                }
            }

            return null;
        }

        private static string? ExtractSiemensPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var normalized = path.Replace('/', '\\');
            var siemensIndex = normalized.IndexOf("Siemens", StringComparison.OrdinalIgnoreCase);

            if (siemensIndex > 0)
            {
                var endIndex = normalized.IndexOf('\\', siemensIndex + 7);
                if (endIndex > 0)
                    return normalized.Substring(0, endIndex);
                else
                    return normalized;
            }

            return null;
        }

        /// <summary>
        /// 캐시 초기화 (테스트 또는 수동 갱신 시 사용)
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cachedSiemensPath = null;
                _cachedLicenseServerPath = null;
            }
        }
    }
}
