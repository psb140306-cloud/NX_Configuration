using System.IO;
using Microsoft.Win32;
using NXConfigLauncher.Helpers;
using NXConfigLauncher.Models;

namespace NXConfigLauncher.Services
{
    public interface INxDetectionService
    {
        List<NxVersionInfo> DetectInstalledVersions();
        bool ValidateVersion(NxVersionInfo version);
    }

    public class NxDetectionService : INxDetectionService
    {
        private readonly IRegistryPathService _registryPathService;

        private const string SiemensRegistryPath = @"SOFTWARE\Siemens";
        private const string NxBinFolder = "NXBIN";
        private const string UgrafExe = "ugraf.exe";

        public NxDetectionService() : this(new RegistryPathService())
        {
        }

        public NxDetectionService(IRegistryPathService registryPathService)
        {
            _registryPathService = registryPathService;
        }

        public List<NxVersionInfo> DetectInstalledVersions()
        {
            var versions = new List<NxVersionInfo>();

            // 1. 레지스트리에서 검색
            var registryVersions = DetectFromRegistry();
            versions.AddRange(registryVersions);

            // 2. 기본 설치 경로에서 검색
            var pathVersions = DetectFromInstallPath();

            // 중복 제거 (InstallPath 기준)
            foreach (var pv in pathVersions)
            {
                if (!versions.Any(v => v.InstallPath.Equals(pv.InstallPath, StringComparison.OrdinalIgnoreCase)))
                {
                    versions.Add(pv);
                }
            }

            return versions.OrderByDescending(v => v.VersionName).ToList();
        }

        private List<NxVersionInfo> DetectFromRegistry()
        {
            var versions = new List<NxVersionInfo>();

            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(SiemensRegistryPath);
                if (baseKey == null) return versions;

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    // NX로 시작하는 키 검색 (예: NX2306, NX2212 등)
                    if (!subKeyName.StartsWith("NX", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using var nxKey = baseKey.OpenSubKey(subKeyName);
                    if (nxKey == null) continue;

                    // 설치 경로 찾기
                    var installPath = nxKey.GetValue("InstallDir") as string
                                   ?? nxKey.GetValue("UGII_BASE_DIR") as string;

                    if (string.IsNullOrEmpty(installPath))
                    {
                        // 하위 키에서 검색
                        installPath = FindInstallPathInSubKeys(nxKey);
                    }

                    if (!string.IsNullOrEmpty(installPath))
                    {
                        var versionInfo = CreateVersionInfo(subKeyName, installPath);
                        if (versionInfo != null)
                        {
                            versions.Add(versionInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to detect NX versions from registry: {ex.Message}");
            }

            return versions;
        }

        private string? FindInstallPathInSubKeys(RegistryKey parentKey)
        {
            foreach (var subKeyName in parentKey.GetSubKeyNames())
            {
                using var subKey = parentKey.OpenSubKey(subKeyName);
                if (subKey == null) continue;

                var installPath = subKey.GetValue("InstallDir") as string
                               ?? subKey.GetValue("UGII_BASE_DIR") as string
                               ?? subKey.GetValue("Path") as string;

                if (!string.IsNullOrEmpty(installPath))
                    return installPath;
            }

            return null;
        }

        private List<NxVersionInfo> DetectFromInstallPath()
        {
            var versions = new List<NxVersionInfo>();
            var siemensInstallPath = _registryPathService.GetSiemensInstallPath();

            try
            {
                if (string.IsNullOrEmpty(siemensInstallPath) || !Directory.Exists(siemensInstallPath))
                    return versions;

                var directories = Directory.GetDirectories(siemensInstallPath);

                foreach (var dir in directories)
                {
                    var folderName = Path.GetFileName(dir);

                    // NX로 시작하는 폴더 검색 (예: "NX 10.0", "NX 12.0", "NX2306")
                    // 공백이 있는 버전명도 지원 ("NX " 또는 "NX")
                    if (!folderName.StartsWith("NX ", StringComparison.OrdinalIgnoreCase) &&
                        !folderName.StartsWith("NX", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // PLMLicenseServer 등 제외
                    if (folderName.Contains("License", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var versionInfo = CreateVersionInfo(folderName, dir);
                    if (versionInfo != null)
                    {
                        versions.Add(versionInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to detect NX versions from install path: {ex.Message}");
            }

            return versions;
        }

        private NxVersionInfo? CreateVersionInfo(string versionName, string installPath)
        {
            // 가능한 ugraf.exe 경로들 (다양한 NX 버전 구조 지원)
            var possiblePaths = new[]
            {
                // NX 최신 버전 구조
                Path.Combine(installPath, NxBinFolder, UgrafExe),
                // NX 구버전 구조 (UGII 폴더 하위)
                Path.Combine(installPath, "UGII", UgrafExe),
                Path.Combine(installPath, "UGII", NxBinFolder, UgrafExe),
                // 직접 하위
                Path.Combine(installPath, UgrafExe),
                // NX 10/11/12 등 구버전 구조
                Path.Combine(installPath, "NXBIN", UgrafExe),
                Path.Combine(installPath, "nxbin", UgrafExe),
            };

            var exePath = possiblePaths.FirstOrDefault(File.Exists);

            // 못 찾으면 하위 폴더에서 재귀 검색
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = FindUgrafExeRecursive(installPath);
            }

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return null;

            return new NxVersionInfo
            {
                VersionName = versionName,
                InstallPath = installPath,
                ExePath = exePath
            };
        }

        private string? FindUgrafExeRecursive(string basePath, int maxDepth = 3)
        {
            if (maxDepth <= 0 || !Directory.Exists(basePath))
                return null;

            try
            {
                // 현재 폴더에서 ugraf.exe 검색
                var ugrafPath = Path.Combine(basePath, UgrafExe);
                if (File.Exists(ugrafPath))
                    return ugrafPath;

                // 하위 폴더 검색
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    var found = FindUgrafExeRecursive(dir, maxDepth - 1);
                    if (!string.IsNullOrEmpty(found))
                        return found;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to search for ugraf.exe in '{basePath}': {ex.Message}");
            }

            return null;
        }

        public bool ValidateVersion(NxVersionInfo version)
        {
            if (version == null)
                return false;

            return File.Exists(version.ExePath);
        }
    }
}
