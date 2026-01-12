using System.Diagnostics;
using System.IO;
using NXConfigLauncher.Helpers;

namespace NXConfigLauncher.Services
{
    public interface IFirewallService
    {
        bool AddBlockRules(string nxInstallPath);
        bool RemoveBlockRules();
        bool RuleExists(string ruleName);
        List<string> GetExistingRules();
        bool HasActiveBlockRules();
        (int ActiveCount, List<string> RuleNames) GetBlockStatus();
    }

    public class FirewallService : IFirewallService
    {
        private readonly IRegistryPathService _registryPathService;

        private const string RulePrefix = "NXConfigLauncher_Block_";

        // 차단 대상 프로세스 목록
        private static readonly string[] TargetProcesses =
        {
            // NX 메인 프로세스
            "ugraf.exe",
            "nx.exe",
            "nxcam.exe",
            "nxmanager.exe",
            "paxar.exe",

            // 라이선스 관련
            "ugslmd.exe",
            "lmgrd.exe",
            "instlck.exe",

            // NX 런처/유틸리티
            "nxquicklaunch.exe",
            "nxlauncher.exe",
            "ugs_router.exe",
            "run_journal.exe",

            // Siemens 클라우드/텔레메트리
            "Siemens.Discovery.Service.exe",
            "SIEMENS_PLM_Cloud_Agent.exe",
            "ProductExcellenceProgram.exe",
            "ugs_update.exe"
        };

        // splm*.exe 패턴 매칭을 위한 접두사
        private const string SplmPrefix = "splm";

        public FirewallService() : this(new RegistryPathService())
        {
        }

        public FirewallService(IRegistryPathService registryPathService)
        {
            _registryPathService = registryPathService;
        }

        public bool AddBlockRules(string nxInstallPath)
        {
            bool allSuccess = true;
            var searchPaths = new List<string> { nxInstallPath };

            // PLMLicenseServer 경로를 레지스트리에서 동적으로 조회
            var licenseServerPath = _registryPathService.GetLicenseServerPath();
            if (!string.IsNullOrEmpty(licenseServerPath) && Directory.Exists(licenseServerPath))
            {
                searchPaths.Add(licenseServerPath);
            }

            try
            {
                // 기본 프로세스들 차단
                foreach (var processName in TargetProcesses)
                {
                    foreach (var searchPath in searchPaths)
                    {
                        var exePath = FindExecutable(searchPath, processName);
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            var ruleName = GetRuleName(processName);
                            if (!RuleExists(ruleName))
                            {
                                var result = AddOutboundBlockRule(ruleName, exePath);
                                Logger.Info($"Firewall rule '{ruleName}' for '{exePath}': {(result ? "added" : "failed")}");
                                allSuccess &= result;
                            }
                            break; // 찾았으면 다음 프로세스로
                        }
                    }
                }

                // splm*.exe 패턴 차단 (모든 경로에서)
                foreach (var searchPath in searchPaths)
                {
                    var splmFiles = FindSplmExecutables(searchPath);
                    foreach (var splmPath in splmFiles)
                    {
                        var fileName = Path.GetFileName(splmPath);
                        var ruleName = GetRuleName(fileName);
                        if (!RuleExists(ruleName))
                        {
                            var result = AddOutboundBlockRule(ruleName, splmPath);
                            Logger.Info($"Firewall rule '{ruleName}' for '{splmPath}': {(result ? "added" : "failed")}");
                            allSuccess &= result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"AddBlockRules failed: {ex.Message}");
                allSuccess = false;
            }

            return allSuccess;
        }

        public bool RemoveBlockRules()
        {
            bool allSuccess = true;

            try
            {
                // 모든 NXConfigLauncher 규칙 제거
                var rules = GetExistingRules();
                foreach (var ruleName in rules)
                {
                    allSuccess &= RemoveRule(ruleName);
                }
            }
            catch (Exception)
            {
                allSuccess = false;
            }

            return allSuccess;
        }

        public bool RuleExists(string ruleName)
        {
            try
            {
                var result = ExecuteNetsh($"advfirewall firewall show rule name=\"{ruleName}\"");
                return result.ExitCode == 0 && !result.Output.Contains("No rules match");
            }
            catch
            {
                return false;
            }
        }

        public List<string> GetExistingRules()
        {
            var rules = new List<string>();

            // 모든 차단 대상 프로세스에 대한 규칙 확인
            foreach (var processName in TargetProcesses)
            {
                var ruleName = GetRuleName(processName);
                if (RuleExists(ruleName))
                {
                    rules.Add(ruleName);
                }
            }

            // splm*.exe 규칙도 확인 (splm8 등)
            for (int i = 1; i <= 20; i++)
            {
                var ruleName = $"{RulePrefix}splm{i}";
                if (RuleExists(ruleName))
                {
                    rules.Add(ruleName);
                }
            }

            return rules;
        }

        public bool HasActiveBlockRules()
        {
            return GetExistingRules().Count > 0;
        }

        /// <summary>
        /// 현재 활성화된 방화벽 규칙 상태를 반환
        /// </summary>
        public (int ActiveCount, List<string> RuleNames) GetBlockStatus()
        {
            var rules = GetExistingRules();
            return (rules.Count, rules);
        }

        private bool AddOutboundBlockRule(string ruleName, string exePath)
        {
            var args = $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block program=\"{exePath}\" enable=yes";
            var result = ExecuteNetsh(args);
            return result.ExitCode == 0;
        }

        private bool RemoveRule(string ruleName)
        {
            var args = $"advfirewall firewall delete rule name=\"{ruleName}\"";
            var result = ExecuteNetsh(args);
            return result.ExitCode == 0;
        }

        private string GetRuleName(string processName)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(processName);
            return $"{RulePrefix}{nameWithoutExt}";
        }

        private string? FindExecutable(string basePath, string fileName)
        {
            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                return null;

            try
            {
                var files = Directory.GetFiles(basePath, fileName, SearchOption.AllDirectories);
                return files.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private List<string> FindSplmExecutables(string basePath)
        {
            var splmFiles = new List<string>();

            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                return splmFiles;

            try
            {
                var files = Directory.GetFiles(basePath, $"{SplmPrefix}*.exe", SearchOption.AllDirectories);
                splmFiles.AddRange(files);
            }
            catch
            {
                // 무시
            }

            return splmFiles;
        }

        private (int ExitCode, string Output) ExecuteNetsh(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return (-1, string.Empty);

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output);
        }
    }
}
