namespace NXConfigLauncher.Models
{
    public class NxVersionInfo
    {
        public string VersionName { get; set; } = string.Empty;
        public string InstallPath { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{VersionName} - {InstallPath}";
        }
    }
}
