using NXConfigLauncher.Helpers;

namespace NXConfigLauncher.Services
{
    public interface IEnvironmentService
    {
        (string Port, string Server) GetLicenseServer();
        (bool Success, string ErrorMessage) SetLicensePort(string port);
        (bool Success, string ErrorMessage) SetLicenseServer(string port, string server);
        string GetLanguage();
        (bool Success, string ErrorMessage) SetLanguage(string language);
        Dictionary<string, string> GetCurrentSettings();
        string[] GetAvailablePorts();
        string[] GetAvailableLanguages();
    }

    public class EnvironmentService : IEnvironmentService
    {
        private const string LicenseServerVar = "SPLM_LICENSE_SERVER";
        private const string LanguageVar = "UGII_LANG";

        /// <summary>
        /// NX가 지원하는 모든 언어 목록 (UGII_LANG 환경변수 값)
        /// </summary>
        private static readonly Dictionary<string, string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
        {
            { "english", "English" },
            { "korean", "한국어" },
            { "simpl_chinese", "简体中文 (Simplified Chinese)" },
            { "trad_chinese", "繁體中文 (Traditional Chinese)" },
            { "japanese", "日本語" },
            { "german", "Deutsch" },
            { "french", "Français" },
            { "italian", "Italiano" },
            { "spanish", "Español" },
            { "portuguese", "Português" },
            { "russian", "Русский" },
            { "polish", "Polski" },
            { "czech", "Čeština" },
            { "hungarian", "Magyar" }
        };

        public (string Port, string Server) GetLicenseServer()
        {
            var value = Environment.GetEnvironmentVariable(LicenseServerVar, EnvironmentVariableTarget.Machine)
                     ?? string.Empty;

            Logger.Info($"Read {LicenseServerVar} from Machine: '{value}'");
            return ParseLicenseServer(value);
        }

        public (bool Success, string ErrorMessage) SetLicensePort(string port)
        {
            try
            {
                var (_, server) = GetLicenseServer();

                if (string.IsNullOrEmpty(server))
                {
                    Logger.Error("SetLicensePort failed: license server address is empty.");
                    return (false, "라이선스 서버 주소가 비어 있어 포트를 변경할 수 없습니다.");
                }

                var newValue = $"{port}@{server}";
                Environment.SetEnvironmentVariable(LicenseServerVar, newValue, EnvironmentVariableTarget.Machine);

                Logger.Info($"SetLicensePort succeeded: {newValue}");
                return (true, string.Empty);
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Error("SetLicensePort failed: unauthorized.");
                return (false, "환경변수 수정 권한이 없습니다. 관리자 권한으로 실행해 주세요.");
            }
            catch (Exception ex)
            {
                Logger.Error($"SetLicensePort failed: {ex.Message}");
                return (false, $"라이선스 환경변수 설정 실패: {ex.Message}");
            }
        }

        public (bool Success, string ErrorMessage) SetLicenseServer(string port, string server)
        {
            try
            {
                var newValue = $"{port}@{server}";
                Environment.SetEnvironmentVariable(LicenseServerVar, newValue, EnvironmentVariableTarget.Machine);

                Logger.Info($"SetLicenseServer succeeded: {newValue}");
                return (true, string.Empty);
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Error("SetLicenseServer failed: unauthorized.");
                return (false, "환경변수 수정 권한이 없습니다. 관리자 권한으로 실행해 주세요.");
            }
            catch (Exception ex)
            {
                Logger.Error($"SetLicenseServer failed: {ex.Message}");
                return (false, $"라이선스 환경변수 설정 실패: {ex.Message}");
            }
        }

        public string GetLanguage()
        {
            var value = Environment.GetEnvironmentVariable(LanguageVar, EnvironmentVariableTarget.Machine)
                ?? "english";
            Logger.Info($"Read {LanguageVar} from Machine: '{value}'");
            return value;
        }

        public (bool Success, string ErrorMessage) SetLanguage(string language)
        {
            try
            {
                var normalizedLang = language.ToLowerInvariant();

                // 지원 언어 검증
                if (!SupportedLanguages.ContainsKey(normalizedLang))
                {
                    var supportedList = string.Join(", ", SupportedLanguages.Keys);
                    Logger.Error($"SetLanguage failed: invalid value '{language}'. Supported: {supportedList}");
                    return (false, $"지원하지 않는 언어입니다. 지원 언어: {supportedList}");
                }

                Environment.SetEnvironmentVariable(LanguageVar, normalizedLang, EnvironmentVariableTarget.Machine);

                Logger.Info($"SetLanguage succeeded: {normalizedLang}");
                return (true, string.Empty);
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Error("SetLanguage failed: unauthorized.");
                return (false, "환경변수 수정 권한이 없습니다. 관리자 권한으로 실행해 주세요.");
            }
            catch (Exception ex)
            {
                Logger.Error($"SetLanguage failed: {ex.Message}");
                return (false, $"언어 환경변수 설정 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 언어가 유효한지 검증
        /// </summary>
        public bool IsValidLanguage(string language)
        {
            return SupportedLanguages.ContainsKey(language);
        }

        /// <summary>
        /// 언어 코드에 대한 표시 이름 반환
        /// </summary>
        public string GetLanguageDisplayName(string languageCode)
        {
            return SupportedLanguages.TryGetValue(languageCode, out var displayName)
                ? displayName
                : languageCode;
        }

        /// <summary>
        /// 모든 지원 언어와 표시 이름 반환
        /// </summary>
        public IReadOnlyDictionary<string, string> GetAllSupportedLanguages()
        {
            return SupportedLanguages;
        }

        public Dictionary<string, string> GetCurrentSettings()
        {
            var (port, server) = GetLicenseServer();
            var language = GetLanguage();

            return new Dictionary<string, string>
            {
                { "LicensePort", port },
                { "LicenseServer", server },
                { "Language", language }
            };
        }

        private (string Port, string Server) ParseLicenseServer(string value)
        {
            if (string.IsNullOrEmpty(value))
                return (string.Empty, string.Empty);

            // Format: port@server (e.g. 28000@license-server)
            var parts = value.Split('@');

            if (parts.Length >= 2)
            {
                return (parts[0].Trim(), parts[1].Trim());
            }
            else if (parts.Length == 1)
            {
                // Port-only or server-only value.
                if (int.TryParse(parts[0].Trim(), out _))
                    return (parts[0].Trim(), string.Empty);
                else
                    return (string.Empty, parts[0].Trim());
            }

            return (string.Empty, string.Empty);
        }

        public string[] GetAvailablePorts()
        {
            return new[] { "28000", "27800", "29000" };
        }

        public string[] GetAvailableLanguages()
        {
            return SupportedLanguages.Keys.ToArray();
        }
    }
}
