using System.IO;
using System.Text;
using NXConfigLauncher.Helpers;

namespace NXConfigLauncher.Services
{
    public interface IHostsService
    {
        bool AddDomainBlocks();
        bool RemoveDomainBlocks();
        bool HasActiveBlocks();
        (int ActiveCount, List<string> DomainNames) GetBlockStatus();
        List<string> GetTargetDomains();
    }

    public class HostsService : IHostsService
    {
        private static readonly string HostsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "drivers", "etc", "hosts");

        private const string BlockMarkerStart = "# NXConfigLauncher Domain Block Start";
        private const string BlockMarkerEnd = "# NXConfigLauncher Domain Block End";
        private const string BlockedIp = "127.0.0.1";

        // 차단 대상 도메인 목록
        private static readonly string[] TargetDomains =
        {
            // *.siemens.com (메인)
            "siemens.com",
            "www.siemens.com",
            "license.siemens.com",
            "licensing.siemens.com",
            "support.siemens.com",
            "download.siemens.com",
            "update.siemens.com",
            "activation.siemens.com",
            "telemetry.siemens.com",
            "api.siemens.com",
            "cloud.siemens.com",
            "usage.siemens.com",
            "analytics.siemens.com",
            "metrics.siemens.com",
            "tracking.siemens.com",

            // *.sw.siemens.com
            "sw.siemens.com",
            "license.sw.siemens.com",
            "licensing.sw.siemens.com",
            "download.sw.siemens.com",
            "support.sw.siemens.com",
            "gtac.sw.siemens.com",
            "webkey.sw.siemens.com",
            "update.sw.siemens.com",
            "activation.sw.siemens.com",
            "entitlement.sw.siemens.com",
            "plm.sw.siemens.com",
            "www.sw.siemens.com",
            "telemetry.sw.siemens.com",
            "usage.sw.siemens.com",
            "analytics.sw.siemens.com",
            "metrics.sw.siemens.com",
            "tracking.sw.siemens.com",

            // *.plm.automation.siemens.com
            "plm.automation.siemens.com",
            "www.plm.automation.siemens.com",
            "support.plm.automation.siemens.com",
            "license.plm.automation.siemens.com",
            "licensing.plm.automation.siemens.com",
            "download.plm.automation.siemens.com",
            "gtac.plm.automation.siemens.com",
            "webkey.plm.automation.siemens.com",
            "update.plm.automation.siemens.com",
            "activation.plm.automation.siemens.com",
            "entitlement.plm.automation.siemens.com",
            "api.plm.automation.siemens.com",
            "cloud.plm.automation.siemens.com",
            "telemetry.plm.automation.siemens.com",
            "usage.plm.automation.siemens.com",
            "analytics.plm.automation.siemens.com",
            "metrics.plm.automation.siemens.com",
            "tracking.plm.automation.siemens.com"
        };

        public bool AddDomainBlocks()
        {
            try
            {
                // 이미 차단되어 있으면 스킵
                if (HasActiveBlocks())
                {
                    Logger.Info("Domain blocks already exist in hosts file");
                    return true;
                }

                var hostsContent = File.ReadAllText(HostsFilePath, Encoding.UTF8);
                var sb = new StringBuilder(hostsContent);

                // 마지막에 줄바꿈이 없으면 추가
                if (!hostsContent.EndsWith(Environment.NewLine))
                {
                    sb.AppendLine();
                }

                // 차단 블록 추가
                sb.AppendLine();
                sb.AppendLine(BlockMarkerStart);
                foreach (var domain in TargetDomains)
                {
                    sb.AppendLine($"{BlockedIp}\t{domain}");
                }
                sb.AppendLine(BlockMarkerEnd);

                File.WriteAllText(HostsFilePath, sb.ToString(), Encoding.UTF8);
                Logger.Info($"Added {TargetDomains.Length} domain blocks to hosts file");

                // DNS 캐시 플러시
                FlushDnsCache();

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error($"Access denied to hosts file: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add domain blocks: {ex.Message}");
                return false;
            }
        }

        public bool RemoveDomainBlocks()
        {
            try
            {
                if (!HasActiveBlocks())
                {
                    Logger.Info("No domain blocks to remove from hosts file");
                    return true;
                }

                var lines = File.ReadAllLines(HostsFilePath, Encoding.UTF8).ToList();
                var newLines = new List<string>();
                var inBlockSection = false;

                foreach (var line in lines)
                {
                    if (line.Trim() == BlockMarkerStart)
                    {
                        inBlockSection = true;
                        continue;
                    }

                    if (line.Trim() == BlockMarkerEnd)
                    {
                        inBlockSection = false;
                        continue;
                    }

                    if (!inBlockSection)
                    {
                        newLines.Add(line);
                    }
                }

                // 끝에 있는 빈 줄들 정리
                while (newLines.Count > 0 && string.IsNullOrWhiteSpace(newLines[^1]))
                {
                    newLines.RemoveAt(newLines.Count - 1);
                }

                File.WriteAllLines(HostsFilePath, newLines, Encoding.UTF8);
                Logger.Info("Removed domain blocks from hosts file");

                // DNS 캐시 플러시
                FlushDnsCache();

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error($"Access denied to hosts file: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to remove domain blocks: {ex.Message}");
                return false;
            }
        }

        public bool HasActiveBlocks()
        {
            try
            {
                var content = File.ReadAllText(HostsFilePath, Encoding.UTF8);
                return content.Contains(BlockMarkerStart) && content.Contains(BlockMarkerEnd);
            }
            catch
            {
                return false;
            }
        }

        public (int ActiveCount, List<string> DomainNames) GetBlockStatus()
        {
            var blockedDomains = new List<string>();

            try
            {
                if (!HasActiveBlocks())
                {
                    return (0, blockedDomains);
                }

                var lines = File.ReadAllLines(HostsFilePath, Encoding.UTF8);
                var inBlockSection = false;

                foreach (var line in lines)
                {
                    if (line.Trim() == BlockMarkerStart)
                    {
                        inBlockSection = true;
                        continue;
                    }

                    if (line.Trim() == BlockMarkerEnd)
                    {
                        break;
                    }

                    if (inBlockSection && !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
                    {
                        // "127.0.0.1    domain.com" 형식에서 도메인 추출
                        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            blockedDomains.Add(parts[1]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get domain block status: {ex.Message}");
            }

            return (blockedDomains.Count, blockedDomains);
        }

        public List<string> GetTargetDomains()
        {
            return TargetDomains.ToList();
        }

        private void FlushDnsCache()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit(5000);
                Logger.Info("DNS cache flushed");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to flush DNS cache: {ex.Message}");
            }
        }
    }
}
