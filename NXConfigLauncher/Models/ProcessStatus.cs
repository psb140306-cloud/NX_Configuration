namespace NXConfigLauncher.Models
{
    public class ProcessStatus
    {
        public string ProcessName { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public int ProcessCount { get; set; }

        public string StatusText => IsRunning ? "ON" : "OFF";

        public override string ToString()
        {
            return $"{ProcessName}: {(IsRunning ? "Running" : "Stopped")}";
        }
    }
}
