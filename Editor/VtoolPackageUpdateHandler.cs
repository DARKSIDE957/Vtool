using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.IO;
using System.Text.RegularExpressions;

namespace XVR.Tools
{
    [InitializeOnLoad]
    public static class VtoolPackageUpdateHandler
    {
        private const string LoadedVersionKey = "com.vtool.autofixer.loadedVersion";
        private const string PendingVersionKey = "com.vtool.autofixer.pendingVersion";
        private const string PackageStampKey = "com.vtool.autofixer.packageStamp";
        private const double PollIntervalSeconds = 2.0;

        private static string packageJsonPath;
        private static double lastPollTime;
        private static bool reloadScheduled;

        public static bool HasPendingUpdate =>
            !string.IsNullOrEmpty(EditorPrefs.GetString(PendingVersionKey, string.Empty));

        public static string PendingVersion =>
            EditorPrefs.GetString(PendingVersionKey, string.Empty);

        static VtoolPackageUpdateHandler()
        {
            EditorApplication.delayCall += OnEditorReady;
            EditorApplication.focusChanged += OnFocusChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private static void OnProjectChanged() => CheckForPackageUpdate(silent: true);

        private static void OnEditorReady()
        {
            packageJsonPath = FindPackageJsonPath();
            string diskVersion = ReadVersionFromDisk();
            string diskStamp = ReadPackageStamp();

            if (string.IsNullOrEmpty(diskVersion))
                return;

            string loadedVersion = EditorPrefs.GetString(LoadedVersionKey, string.Empty);
            string loadedStamp = EditorPrefs.GetString(PackageStampKey, string.Empty);

            if (string.IsNullOrEmpty(loadedVersion))
            {
                EditorPrefs.SetString(LoadedVersionKey, diskVersion);
                EditorPrefs.SetString(PackageStampKey, diskStamp);
                EditorPrefs.DeleteKey(PendingVersionKey);
                return;
            }

            if (diskVersion == loadedVersion && diskStamp == loadedStamp)
            {
                EditorPrefs.DeleteKey(PendingVersionKey);
                return;
            }

            EditorPrefs.SetString(LoadedVersionKey, diskVersion);
            EditorPrefs.SetString(PackageStampKey, diskStamp);
            EditorPrefs.DeleteKey(PendingVersionKey);
            Debug.Log("[Vtool] Package update applied.");
        }

        private static void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                CheckForPackageUpdate(silent: false);
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - lastPollTime < PollIntervalSeconds)
                return;

            lastPollTime = EditorApplication.timeSinceStartup;
            CheckForPackageUpdate(silent: true);
        }

        [MenuItem("Vtool/Apply Package Update (Reload)", false, 100)]
        public static void ApplyUpdateFromMenu()
        {
            CheckForPackageUpdate(silent: false, force: true);
        }

        public static void CheckForPackageUpdate(bool silent, bool force = false)
        {
            if (reloadScheduled)
                return;

            if (string.IsNullOrEmpty(packageJsonPath))
                packageJsonPath = FindPackageJsonPath();

            string diskVersion = ReadVersionFromDisk();
            if (string.IsNullOrEmpty(diskVersion))
                return;

            string diskStamp = ReadPackageStamp();
            string loadedVersion = EditorPrefs.GetString(LoadedVersionKey, string.Empty);
            string loadedStamp = EditorPrefs.GetString(PackageStampKey, string.Empty);

            if (!force)
            {
                if (string.IsNullOrEmpty(loadedVersion))
                    return;

                if (diskVersion == loadedVersion && diskStamp == loadedStamp)
                    return;
            }
            else if (diskVersion == loadedVersion && diskStamp == loadedStamp)
            {
                EditorUtility.DisplayDialog("Vtool", "Already on the latest installed package.", "OK");
                return;
            }

            EditorPrefs.SetString(PendingVersionKey, diskVersion);
            ApplyPackageUpdate(silent);
        }

        private static void ApplyPackageUpdate(bool silent)
        {
            if (reloadScheduled)
                return;

            reloadScheduled = true;

            Debug.Log("[Vtool] Package update detected. Refreshing and reloading…");

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "Vtool Update",
                    "A new Vtool package was installed while Unity was open.\n\n" +
                    "Unity will refresh and reload now so the update takes effect.",
                    "OK");
            }

            EditorApplication.delayCall += () =>
            {
                CompilationPipeline.RequestScriptCompilation();
                EditorUtility.RequestScriptReload();
                EditorApplication.delayCall += () => { reloadScheduled = false; };
            };
        }

        private static string ReadVersionFromDisk()
        {
            if (string.IsNullOrEmpty(packageJsonPath))
                packageJsonPath = FindPackageJsonPath();

            if (string.IsNullOrEmpty(packageJsonPath) || !File.Exists(packageJsonPath))
                return string.Empty;

            var match = Regex.Match(File.ReadAllText(packageJsonPath), "\"version\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string ReadPackageStamp()
        {
            if (string.IsNullOrEmpty(packageJsonPath))
                packageJsonPath = FindPackageJsonPath();

            if (string.IsNullOrEmpty(packageJsonPath) || !File.Exists(packageJsonPath))
                return string.Empty;

            var pkgDir = Path.GetDirectoryName(packageJsonPath);
            if (string.IsNullOrEmpty(pkgDir) || !Directory.Exists(pkgDir))
                return string.Empty;

            long newestTicks = File.GetLastWriteTimeUtc(packageJsonPath).Ticks;
            foreach (var file in Directory.EnumerateFiles(pkgDir, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta"))
                    continue;

                long ticks = File.GetLastWriteTimeUtc(file).Ticks;
                if (ticks > newestTicks)
                    newestTicks = ticks;
            }

            return newestTicks.ToString();
        }

        private static string FindPackageJsonPath()
        {
            const string vpmPath = "Packages/com.vtool.autofixer/package.json";
            if (File.Exists(vpmPath))
                return vpmPath;

            var guids = AssetDatabase.FindAssets("VRCAvatarAutoFixer t:MonoScript");
            foreach (var guid in guids)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!scriptPath.EndsWith("VRCAvatarAutoFixer.cs"))
                    continue;

                var pkgPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(scriptPath) ?? string.Empty, "..", "package.json"));
                if (File.Exists(pkgPath))
                    return pkgPath;
            }

            return string.Empty;
        }
    }
}
