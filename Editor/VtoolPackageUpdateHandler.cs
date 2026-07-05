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
        private const double PollIntervalSeconds = 3.0;

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

            if (string.IsNullOrEmpty(diskVersion))
                return;

            string loadedVersion = EditorPrefs.GetString(LoadedVersionKey, string.Empty);

            if (string.IsNullOrEmpty(loadedVersion))
            {
                EditorPrefs.SetString(LoadedVersionKey, diskVersion);
                EditorPrefs.DeleteKey(PendingVersionKey);
                return;
            }

            if (diskVersion == loadedVersion)
            {
                EditorPrefs.DeleteKey(PendingVersionKey);
                return;
            }

            // Domain reload finished — new package version is now active.
            EditorPrefs.SetString(LoadedVersionKey, diskVersion);
            EditorPrefs.DeleteKey(PendingVersionKey);
            Debug.Log($"[Vtool] Updated to v{diskVersion}.");
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

            string loadedVersion = EditorPrefs.GetString(LoadedVersionKey, string.Empty);

            if (!force)
            {
                if (string.IsNullOrEmpty(loadedVersion) || diskVersion == loadedVersion)
                    return;
            }
            else if (diskVersion == loadedVersion)
            {
                EditorUtility.DisplayDialog("Vtool", $"Already running v{diskVersion}.", "OK");
                return;
            }

            EditorPrefs.SetString(PendingVersionKey, diskVersion);
            ApplyPackageUpdate(diskVersion, silent);
        }

        private static void ApplyPackageUpdate(string newVersion, bool silent)
        {
            if (reloadScheduled)
                return;

            reloadScheduled = true;

            Debug.Log($"[Vtool] Package update detected (v{newVersion}). Refreshing and reloading scripts...");

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "Vtool Update Detected",
                    $"A new version of Vtool (v{newVersion}) was installed while Unity was open.\n\n" +
                    "Unity will refresh and reload scripts now so the update takes effect.",
                    "OK");
            }

            EditorApplication.delayCall += () =>
            {
                CompilationPipeline.RequestScriptCompilation();
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
