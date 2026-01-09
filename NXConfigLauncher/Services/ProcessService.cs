using System.Diagnostics;
using System.Text.RegularExpressions;
using NXConfigLauncher.Helpers;
using NXConfigLauncher.Models;

namespace NXConfigLauncher.Services
{
    public interface IProcessService
    {
        List<ProcessStatus> GetAllProcessStatus();
        ProcessStatus GetProcessStatus(string processName);
        ProcessStatus GetSplmProcessStatus();
        bool IsAnyProcessRunning();
        (int Killed, int Failed, List<string> FailedProcesses) KillProcess(string processName);
        (int TotalKilled, int TotalFailed, List<string> FailedProcesses) KillAllNxProcesses();
        List<string> GetRunningProcessNames();
    }

    public class ProcessService : IProcessService
    {
        // 대상 프로세스 목록 (확장자 제외)
        private static readonly string[] TargetProcesses =
        {
            "ugraf",
            "lmgrd",
            "ugslmd",
            "ugs_router",
            "nxsession",
            "nxtask",
            "javaw",
            "java",
            "ugtopv"
        };

        // 프로세스 종료 대기 시간 (밀리초)
        private const int ProcessExitTimeoutMs = 3000;

        // 와일드카드 패턴 (splm*.exe) - Compiled 옵션으로 성능 최적화
        private static readonly Regex SplmRegex = new("^splm.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public List<ProcessStatus> GetAllProcessStatus()
        {
            var statusList = new List<ProcessStatus>();

            // 일반 프로세스 확인
            foreach (var processName in TargetProcesses)
            {
                var status = GetProcessStatus(processName);
                statusList.Add(status);
            }

            // splm* 패턴 프로세스 확인
            var splmStatus = GetSplmProcessStatus();
            statusList.Add(splmStatus);

            return statusList;
        }

        public ProcessStatus GetProcessStatus(string processName)
        {
            Process[]? processes = null;
            try
            {
                processes = Process.GetProcessesByName(processName);
                return new ProcessStatus
                {
                    ProcessName = $"{processName}.exe",
                    IsRunning = processes.Length > 0,
                    ProcessCount = processes.Length
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get process status for '{processName}': {ex.Message}");
                return new ProcessStatus
                {
                    ProcessName = $"{processName}.exe",
                    IsRunning = false,
                    ProcessCount = 0
                };
            }
            finally
            {
                // Process 객체 리소스 해제
                if (processes != null)
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
        }

        public ProcessStatus GetSplmProcessStatus()
        {
            Process[]? allProcesses = null;
            try
            {
                allProcesses = Process.GetProcesses();
                var splmProcesses = allProcesses
                    .Where(p => SplmRegex.IsMatch(p.ProcessName))
                    .ToList();

                return new ProcessStatus
                {
                    ProcessName = "splm*.exe",
                    IsRunning = splmProcesses.Count > 0,
                    ProcessCount = splmProcesses.Count
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get SPLM process status: {ex.Message}");
                return new ProcessStatus
                {
                    ProcessName = "splm*.exe",
                    IsRunning = false,
                    ProcessCount = 0
                };
            }
            finally
            {
                // Process 객체 리소스 해제
                if (allProcesses != null)
                {
                    foreach (var process in allProcesses)
                    {
                        process.Dispose();
                    }
                }
            }
        }

        public bool IsAnyProcessRunning()
        {
            var statusList = GetAllProcessStatus();
            return statusList.Any(s => s.IsRunning);
        }

        public (int Killed, int Failed, List<string> FailedProcesses) KillProcess(string processName)
        {
            int killed = 0;
            int failed = 0;
            var failedProcesses = new List<string>();

            Process[]? processes = null;
            try
            {
                processes = Process.GetProcessesByName(processName);

                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(ProcessExitTimeoutMs);
                        killed++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to kill process '{processName}' (PID: {process.Id}): {ex.Message}");
                        failed++;
                        failedProcesses.Add($"{processName} (PID: {process.Id})");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get processes for '{processName}': {ex.Message}");
                failed++;
                failedProcesses.Add(processName);
            }
            finally
            {
                // Process 객체 리소스 해제
                if (processes != null)
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            }

            return (killed, failed, failedProcesses);
        }

        public (int TotalKilled, int TotalFailed, List<string> FailedProcesses) KillAllNxProcesses()
        {
            int totalKilled = 0;
            int totalFailed = 0;
            var allFailedProcesses = new List<string>();

            // 일반 프로세스 종료
            foreach (var processName in TargetProcesses)
            {
                var (killed, failed, failedList) = KillProcess(processName);
                totalKilled += killed;
                totalFailed += failed;
                allFailedProcesses.AddRange(failedList);
            }

            // splm* 패턴 프로세스 종료
            var (splmKilled, splmFailed, splmFailedList) = KillSplmProcesses();
            totalKilled += splmKilled;
            totalFailed += splmFailed;
            allFailedProcesses.AddRange(splmFailedList);

            return (totalKilled, totalFailed, allFailedProcesses);
        }

        private (int Killed, int Failed, List<string> FailedProcesses) KillSplmProcesses()
        {
            int killed = 0;
            int failed = 0;
            var failedProcesses = new List<string>();

            Process[]? allProcesses = null;
            try
            {
                allProcesses = Process.GetProcesses();
                var splmProcesses = allProcesses
                    .Where(p => SplmRegex.IsMatch(p.ProcessName))
                    .ToList();

                foreach (var process in splmProcesses)
                {
                    try
                    {
                        var processName = process.ProcessName;
                        process.Kill();
                        process.WaitForExit(ProcessExitTimeoutMs);
                        killed++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to kill SPLM process '{process.ProcessName}' (PID: {process.Id}): {ex.Message}");
                        failed++;
                        failedProcesses.Add($"{process.ProcessName} (PID: {process.Id})");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get SPLM processes: {ex.Message}");
                failed++;
                failedProcesses.Add("splm*.exe");
            }
            finally
            {
                // Process 객체 리소스 해제
                if (allProcesses != null)
                {
                    foreach (var process in allProcesses)
                    {
                        process.Dispose();
                    }
                }
            }

            return (killed, failed, failedProcesses);
        }

        public List<string> GetRunningProcessNames()
        {
            var runningProcesses = new List<string>();

            foreach (var processName in TargetProcesses)
            {
                var status = GetProcessStatus(processName);
                if (status.IsRunning)
                {
                    runningProcesses.Add(status.ProcessName);
                }
            }

            var splmStatus = GetSplmProcessStatus();
            if (splmStatus.IsRunning)
            {
                runningProcesses.Add(splmStatus.ProcessName);
            }

            return runningProcesses;
        }
    }
}
