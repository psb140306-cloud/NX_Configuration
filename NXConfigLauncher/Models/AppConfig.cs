namespace NXConfigLauncher.Models
{
    public class AppConfig
    {
        public string SelectedNxVersion { get; set; } = string.Empty;
        public string SelectedNxPath { get; set; } = string.Empty;
        public bool IsNetworkBlocked { get; set; } = false;
        public string LicensePort { get; set; } = "28000";
        public string Language { get; set; } = "english";

        public static AppConfig Default => new AppConfig
        {
            SelectedNxVersion = string.Empty,
            SelectedNxPath = string.Empty,
            IsNetworkBlocked = false,
            LicensePort = "28000",
            Language = "english"
        };
    }
}
