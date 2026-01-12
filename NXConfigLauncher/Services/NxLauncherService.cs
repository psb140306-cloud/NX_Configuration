using System.Diagnostics;
using NXConfigLauncher.Helpers;
using NXConfigLauncher.Models;

namespace NXConfigLauncher.Services
{
    public interface INxLauncherService
    {
        Task<(bool Success, string Message)> LaunchNxAsync(NxVersionInfo version, string licensePort, string language, bool blockNetwork);
        (bool Success, string Message) LaunchNxSimple(NxVersionInfo version);
        void CleanupOnExit(bool wasNetworkBlocked);
    }

    public class NxLauncherService : INxLauncherService
    {
        private readonly IEnvironmentService _environmentService;
        private readonly IFirewallService _firewallService;
        private readonly IHostsService _hostsService;
        private readonly IProcessMonitorService _processMonitorService;
        private readonly IRegistryPathService _registryPathService;

        private const string LmgrdExe = "lmgrd.exe";
        private const string LicenseFile = "splm8.lic";

        // 라이선스 서버 시작 후 대기 시간 (밀리초)
        private const int LicenseServerStartupDelayMs = 2000;

        public NxLauncherService(IEnvironmentService environmentService, IFirewallService firewallService, IHostsService hostsService, IProcessMonitorService processMonitorService, IRegistryPathService registryPathService)
        {
            _environmentService = environmentService;
            _firewallService = firewallService;
            _hostsService = hostsService;
            _processMonitorService = processMonitorService;
            _registryPathService = registryPathService;
        }

        public NxLauncherService()
        {
            _registryPathService = new RegistryPathService();
            _environmentService = new EnvironmentService();
            _firewallService = new FirewallService(_registryPathService);
            _hostsService = new HostsService();
            _processMonitorService = new ProcessMonitorService(_firewallService);
        }

        private bool IsLicenseServerRunning()
        {
            var lmgrdProcesses = Process.GetProcessesByName("lmgrd");
            var ugslmdProcesses = Process.GetProcessesByName("ugslmd");
            return lmgrdProcesses.Length > 0 || ugslmdProcesses.Length > 0;
        }

        private async Task<(bool Success, string Message)> StartLicenseServerAsync()
        {
            try
            {
                if (IsLicenseServerRunning())
                {
                    return (true, "라이선스 서버가 이미 실행 중입니다.");
                }

                var licenseServerPath = _registryPathService.GetLicenseServerPath();
                if (string.IsNullOrEmpty(licenseServerPath))
                {
                    return (false, "라이선스 서버 경로를 찾을 수 없습니다. Siemens PLMLicenseServer가 설치되어 있는지 확인하세요.");
                }

                var lmgrdPath = System.IO.Path.Combine(licenseServerPath, LmgrdExe);
                var licFilePath = System.IO.Path.Combine(licenseServerPath, LicenseFile);

                if (!System.IO.File.Exists(lmgrdPath))
                {
                    return (false, $"라이선스 서버를 찾을 수 없습니다: {lmgrdPath}");
                }

                if (!System.IO.File.Exists(licFilePath))
                {
                    return (false, $"라이선스 파일을 찾을 수 없습니다: {licFilePath}");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = lmgrdPath,
                    Arguments = $"-c \"{licFilePath}\"",
                    WorkingDirectory = licenseServerPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(startInfo);

                if (process == null)
                {
                    return (false, "라이선스 서버 프로세스를 시작할 수 없습니다.");
                }

                // 비동기 대기로 UI 블로킹 방지
                await Task.Delay(LicenseServerStartupDelayMs);

                if (IsLicenseServerRunning())
                {
                    return (true, "라이선스 서버가 시작되었습니다.");
                }

                return (false, "라이선스 서버 시작에 실패했습니다.");
            }
            catch (Exception ex)
            {
                return (false, $"라이선스 서버 시작 중 오류: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> LaunchNxAsync(
            NxVersionInfo version,
            string licensePort,
            string language,
            bool blockNetwork)
        {
            try
            {
                if (version == null || string.IsNullOrEmpty(version.ExePath))
                {
                    return (false, "NX 버전이 선택되지 않았습니다.");
                }

                if (!System.IO.File.Exists(version.ExePath))
                {
                    return (false, $"NX 실행 파일을 찾을 수 없습니다: {version.ExePath}");
                }

                Logger.Info($"LaunchNx start: version='{version.VersionName}', exe='{version.ExePath}', port='{licensePort}', lang='{language}', block={blockNetwork}");

                var (licenseSuccess, licenseMessage) = await StartLicenseServerAsync();
                if (!licenseSuccess)
                {
                    Logger.Error($"License server start failed: {licenseMessage}");
                    return (false, licenseMessage);
                }

                var warnings = new List<string>();

                if (!string.IsNullOrEmpty(licensePort))
                {
                    var (portSet, portError) = _environmentService.SetLicensePort(licensePort);
                    if (!portSet && !string.IsNullOrWhiteSpace(portError))
                    {
                        warnings.Add(portError);
                    }
                }

                if (!string.IsNullOrEmpty(language))
                {
                    var (langSet, langError) = _environmentService.SetLanguage(language);
                    if (!langSet && !string.IsNullOrWhiteSpace(langError))
                    {
                        warnings.Add(langError);
                    }
                }

                if (blockNetwork)
                {
                    // UI 블로킹 방지를 위해 모든 네트워크 차단 작업을 백그라운드에서 실행
                    var installPath = version.InstallPath;
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            // 방화벽 규칙 추가
                            var firewallSet = _firewallService.AddBlockRules(installPath);
                            if (!firewallSet)
                            {
                                Logger.Error("Firewall rules add failed.");
                            }

                            // 도메인 차단 (hosts 파일)
                            var hostsSet = _hostsService.AddDomainBlocks();
                            if (!hostsSet)
                            {
                                Logger.Error("Domain blocks add failed.");
                            }

                            // 프로세스 실시간 감시 시작
                            var siemensBasePath = ProcessMonitorService.GetSiemensBasePathFromNxPath(installPath)
                                                  ?? ProcessMonitorService.DetectSiemensBasePath();
                            if (!string.IsNullOrEmpty(siemensBasePath))
                            {
                                _processMonitorService.StartMonitoring(siemensBasePath);
                                Logger.Info($"Process monitoring started for: {siemensBasePath}");
                            }

                            Logger.Info("Network blocking setup completed");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to setup network blocking: {ex.Message}");
                        }
                    });
                }
                else
                {
                    _firewallService.RemoveBlockRules();
                    _hostsService.RemoveDomainBlocks();
                    _processMonitorService.StopMonitoring();
                }

                var (_, server) = _environmentService.GetLicenseServer();
                var licenseValue = $"{licensePort}@{server}";

                Logger.Info($"Launch NX: exe='{version.ExePath}', license='{licenseValue}', lang='{language}'");

                var startInfo = new ProcessStartInfo
                {
                    FileName = version.ExePath,
                    UseShellExecute = false,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(version.ExePath)
                };

                // 환경변수를 프로세스에 직접 설정
                startInfo.Environment["SPLM_LICENSE_SERVER"] = licenseValue;
                startInfo.Environment["UGII_LANG"] = language;

                var process = Process.Start(startInfo);

                if (process == null)
                {
                    Logger.Error("Process.Start returned null");
                    return (false, "NX 프로세스를 시작할 수 없습니다.");
                }

                Logger.Info($"NX started with PID: {process.Id}");

                var message = $"NX {version.VersionName}가(이) 실행되었습니다.";
                if (warnings.Count > 0)
                {
                    message += $" 경고: {string.Join(" / ", warnings)}";
                }

                return (true, message);
            }
            catch (Exception ex)
            {
                Logger.Error($"LaunchNx failed: {ex.Message}");
                return (false, $"NX 실행 중 오류 발생: {ex.Message}");
            }
        }

        public (bool Success, string Message) LaunchNxSimple(NxVersionInfo version)
        {
            try
            {
                if (version == null || string.IsNullOrEmpty(version.ExePath))
                {
                    return (false, "NX 버전이 선택되지 않았습니다.");
                }

                if (!System.IO.File.Exists(version.ExePath))
                {
                    return (false, $"NX 실행 파일을 찾을 수 없습니다: {version.ExePath}");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = version.ExePath,
                    UseShellExecute = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(version.ExePath)
                };

                var process = Process.Start(startInfo);

                if (process == null)
                {
                    return (false, "NX 프로세스를 시작할 수 없습니다.");
                }

                return (true, $"NX {version.VersionName}가(이) 실행되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error($"LaunchNxSimple failed: {ex.Message}");
                return (false, $"NX 실행 중 오류 발생: {ex.Message}");
            }
        }

        public void CleanupOnExit(bool wasNetworkBlocked)
        {
            if (wasNetworkBlocked)
            {
                _firewallService.RemoveBlockRules();
                _hostsService.RemoveDomainBlocks();
                _processMonitorService.StopMonitoring();
            }
        }
    }
}
