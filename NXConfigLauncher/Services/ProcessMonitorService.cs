using System.Diagnostics;
using System.IO;
using System.Management;
using NXConfigLauncher.Helpers;

namespace NXConfigLauncher.Services
{
    public interface IProcessMonitorService : IDisposable
    {
        void StartMonitoring(string basePath);
        void StopMonitoring();
        bool IsMonitoring { get; }
        string MonitoredPath { get; }
        event EventHandler<ProcessBlockedEventArgs>? ProcessBlocked;
        List<string> GetBlockedProcesses();
    }

    public class ProcessBlockedEventArgs : EventArgs
    {
        public string ProcessName { get; }
        public string ProcessPath { get; }
        public int ProcessId { get; }

        public ProcessBlockedEventArgs(string processName, string processPath, int processId)
        {
            ProcessName = processName;
            ProcessPath = processPath;
            ProcessId = processId;
        }
    }

    public class ProcessMonitorService : IProcessMonitorService
    {
        private readonly IFirewallService _firewallService;
        private ManagementEventWatcher? _processWatcher;
        private string _monitoredPath = string.Empty;
        private bool _isMonitoring;
        private readonly object _lock = new();
        private readonly HashSet<string> _blockedProcessPaths = new(StringComparer.OrdinalIgnoreCase);

        // Siemens 기본 설치 경로들
        private static readonly string[] DefaultSiemensPaths =
        {
            @"C:\Program Files\Siemens",
            @"C:\Program Files (x86)\Siemens",
            @"D:\Program Files\Siemens",
            @"D:\Siemens"
        };

        public bool IsMonitoring => _isMonitoring;
        public string MonitoredPath => _monitoredPath;

        public event EventHandler<ProcessBlockedEventArgs>? ProcessBlocked;

        public ProcessMonitorService(IFirewallService firewallService)
        {
            _firewallService = firewallService;
        }

        public ProcessMonitorService() : this(new FirewallService())
        {
        }

        /// <summary>
        /// Siemens 설치 기본 경로를 자동 감지
        /// </summary>
        public static string? DetectSiemensBasePath()
        {
            // 환경 변수에서 먼저 확인
            var ugiiBasePath = Environment.GetEnvironmentVariable("UGII_BASE_DIR");
            if (!string.IsNullOrEmpty(ugiiBasePath))
            {
                // UGII_BASE_DIR은 보통 "C:\Program Files\Siemens\NX2312" 형태
                // 상위 Siemens 폴더를 반환
                var parent = Path.GetDirectoryName(ugiiBasePath);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    return parent;
                }
            }

            // 기본 경로들 중 존재하는 경로 반환
            foreach (var path in DefaultSiemensPaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// NX 설치 경로에서 Siemens 기본 경로 추출
        /// </summary>
        public static string? GetSiemensBasePathFromNxPath(string nxInstallPath)
        {
            if (string.IsNullOrEmpty(nxInstallPath))
                return null;

            // "C:\Program Files\Siemens\NX2312\NXBIN" -> "C:\Program Files\Siemens"
            var path = nxInstallPath;
            while (!string.IsNullOrEmpty(path))
            {
                var dirName = Path.GetFileName(path);
                if (dirName?.Equals("Siemens", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return path;
                }
                path = Path.GetDirectoryName(path);
            }

            return null;
        }

        public void StartMonitoring(string basePath)
        {
            lock (_lock)
            {
                if (_isMonitoring)
                {
                    StopMonitoring();
                }

                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                {
                    Logger.Error($"Invalid monitoring path: {basePath}");
                    return;
                }

                _monitoredPath = basePath;
                _blockedProcessPaths.Clear();

                try
                {
                    // 현재 실행 중인 Siemens 프로세스들 먼저 차단
                    BlockExistingProcesses();

                    // WMI 이벤트 구독 - 새 프로세스 생성 감시
                    var query = new WqlEventQuery(
                        "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'");

                    _processWatcher = new ManagementEventWatcher(query);
                    _processWatcher.EventArrived += OnProcessCreated;
                    _processWatcher.Start();

                    _isMonitoring = true;
                    Logger.Info($"Process monitoring started for path: {basePath}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to start process monitoring: {ex.Message}");
                    _isMonitoring = false;
                }
            }
        }

        public void StopMonitoring()
        {
            lock (_lock)
            {
                if (_processWatcher != null)
                {
                    try
                    {
                        _processWatcher.Stop();
                        _processWatcher.EventArrived -= OnProcessCreated;
                        _processWatcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error stopping process watcher: {ex.Message}");
                    }
                    finally
                    {
                        _processWatcher = null;
                    }
                }

                _isMonitoring = false;
                _monitoredPath = string.Empty;
                Logger.Info("Process monitoring stopped");
            }
        }

        public List<string> GetBlockedProcesses()
        {
            lock (_lock)
            {
                return _blockedProcessPaths.ToList();
            }
        }

        private void OnProcessCreated(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var targetInstance = e.NewEvent["TargetInstance"] as ManagementBaseObject;
                if (targetInstance == null)
                    return;

                var processId = Convert.ToInt32(targetInstance["ProcessId"]);
                var processName = targetInstance["Name"]?.ToString() ?? string.Empty;
                var executablePath = targetInstance["ExecutablePath"]?.ToString();

                if (string.IsNullOrEmpty(executablePath))
                {
                    // ExecutablePath가 없으면 프로세스에서 직접 가져오기 시도
                    try
                    {
                        using var process = Process.GetProcessById(processId);
                        executablePath = process.MainModule?.FileName;
                    }
                    catch
                    {
                        // 프로세스가 이미 종료되었거나 접근 불가
                        return;
                    }
                }

                if (string.IsNullOrEmpty(executablePath))
                    return;

                // 모니터링 경로 내의 프로세스인지 확인
                if (IsPathUnderMonitoredFolder(executablePath))
                {
                    BlockProcess(processName, executablePath, processId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing new process event: {ex.Message}");
            }
        }

        private bool IsPathUnderMonitoredFolder(string processPath)
        {
            if (string.IsNullOrEmpty(_monitoredPath) || string.IsNullOrEmpty(processPath))
                return false;

            return processPath.StartsWith(_monitoredPath, StringComparison.OrdinalIgnoreCase);
        }

        private void BlockProcess(string processName, string executablePath, int processId)
        {
            lock (_lock)
            {
                // 이미 차단된 경로인지 확인
                if (_blockedProcessPaths.Contains(executablePath))
                    return;

                // 방화벽 규칙 추가
                var ruleName = $"NXConfigLauncher_Monitor_{Path.GetFileNameWithoutExtension(processName)}_{processId}";

                try
                {
                    var result = AddOutboundBlockRule(ruleName, executablePath);
                    if (result)
                    {
                        _blockedProcessPaths.Add(executablePath);
                        Logger.Info($"Blocked new process: {processName} ({executablePath})");

                        // 이벤트 발생
                        ProcessBlocked?.Invoke(this, new ProcessBlockedEventArgs(processName, executablePath, processId));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to block process {processName}: {ex.Message}");
                }
            }
        }

        private void BlockExistingProcesses()
        {
            try
            {
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    try
                    {
                        var executablePath = process.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(executablePath) && IsPathUnderMonitoredFolder(executablePath))
                        {
                            BlockProcess(process.ProcessName, executablePath, process.Id);
                        }
                    }
                    catch
                    {
                        // 일부 시스템 프로세스는 접근 불가 - 무시
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error blocking existing processes: {ex.Message}");
            }
        }

        private bool AddOutboundBlockRule(string ruleName, string exePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block program=\"{exePath}\" enable=yes",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return false;

                process.WaitForExit(5000);
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add firewall rule: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
