using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

namespace XVR.Tools
{
    public class VRCAvatarAutoFixer : EditorWindow
    {
        private const string FallbackVersion = "2.1.0";

        private GameObject targetAvatar;
        private Vector2 scrollPos;
        private int tabIndex;
        private int textureCapSize = 2048;
        private bool showIndividualFixes;

        private GUIStyle headerStyle, subStyle, versionStyle, panelStyle;
        private GUIStyle okStyle, warnStyle, errStyle;
        private bool stylesReady;
        private static string cachedVersion;

        private readonly string[] tabs = { "Pre-Upload Check", "Fix Errors", "Textures & Quest" };

        [MenuItem("Vtool/Avatar Auto-Fixer Pro")]
        public static void ShowWindow()
        {
            var w = GetWindow<VRCAvatarAutoFixer>("Vtool Pre-Upload");
            w.minSize = new Vector2(480, 720);
            w.Show();
        }

        private void OnEnable() => AutoDetectAvatar();
        private void OnSelectionChange() { if (targetAvatar == null) Repaint(); }

        private void OnGUI()
        {
            InitStyles();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();
            DrawUpdateBanner();
            DrawDisclaimer();
            DrawAvatarPicker();

            if (targetAvatar == null)
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            GUILayout.Space(4);
            tabIndex = GUILayout.Toolbar(tabIndex, tabs, GUILayout.Height(28));
            GUILayout.Space(4);

            var scan = VtoolAvatarScan.Scan(targetAvatar);

            switch (tabIndex)
            {
                case 0: DrawCheckTab(scan); break;
                case 1: DrawFixTab(scan); break;
                case 2: DrawTexturesTab(scan); break;
            }

            EditorGUILayout.EndScrollView();
        }

        #region UI chrome

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 19 };
            subStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.68f, 0.68f, 0.68f) } };
            versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.82f, 0.22f, 0.28f) }
            };
            panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12), margin = new RectOffset(6, 6, 5, 5) };
            okStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.25f, 0.82f, 0.4f) }, fontStyle = FontStyle.Bold };
            warnStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.62f, 0.1f) }, fontStyle = FontStyle.Bold };
            errStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.95f, 0.28f, 0.28f) }, fontStyle = FontStyle.Bold };
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(panelStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Vtool Pre-Upload Fixer", headerStyle);
            GUILayout.Label("Fixes the most common VRChat upload errors", subStyle);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label("v" + GetVersion(), versionStyle, GUILayout.Width(56));
            EditorGUILayout.EndHorizontal();
            var line = GUILayoutUtility.GetRect(0, 2, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(0.72f, 0.14f, 0.2f));
            EditorGUILayout.EndVertical();
        }

        private void DrawUpdateBanner()
        {
            if (!VtoolPackageUpdateHandler.HasPendingUpdate) return;
            EditorGUILayout.HelpBox($"Vtool v{VtoolPackageUpdateHandler.PendingVersion} installing — reloading…", MessageType.Info);
            if (GUILayout.Button("Apply Update Now"))
                VtoolPackageUpdateHandler.CheckForPackageUpdate(silent: false, force: true);
        }

        private void DrawDisclaimer()
        {
            EditorGUILayout.HelpBox(
                "DISCLAIMER: Back up your avatar first. DARKSIDE957 is NOT responsible if this tool breaks your avatar or causes upload failures.",
                MessageType.Warning);
        }

        private void DrawAvatarPicker()
        {
            EditorGUILayout.BeginVertical(panelStyle);
            EditorGUILayout.BeginHorizontal();
            targetAvatar = (GameObject)EditorGUILayout.ObjectField("Avatar Root", targetAvatar, typeof(GameObject), true);
            if (targetAvatar == null && Selection.activeGameObject != null && GUILayout.Button("Use Selected", GUILayout.Width(96)))
                targetAvatar = Selection.activeGameObject;
            if (GUILayout.Button("Refresh", GUILayout.Width(64))) Repaint();
            EditorGUILayout.EndHorizontal();
            if (targetAvatar == null && GUILayout.Button("Auto-Detect Avatar in Scene", GUILayout.Height(26)))
                AutoDetectAvatar();
            EditorGUILayout.EndVertical();
        }

        private void Stat(string label, string value, GUIStyle style = null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140));
            GUILayout.Label(value, style ?? EditorStyles.label);
            EditorGUILayout.EndHorizontal();
        }

        private void IssueRow(AvatarIssue issue)
        {
            GUIStyle icon = issue.Severity == IssueSeverity.Blocker ? errStyle
                : issue.Severity == IssueSeverity.Warning ? warnStyle : EditorStyles.miniLabel;
            string mark = issue.Severity == IssueSeverity.Blocker ? "✗" : issue.Severity == IssueSeverity.Warning ? "!" : "·";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(mark, icon, GUILayout.Width(14));
            GUILayout.Label(issue.Message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(issue.FixHint))
                EditorGUILayout.LabelField("→ " + issue.FixHint, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Tabs

        private void DrawCheckTab(AvatarScanResult scan)
        {
            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("Upload Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(scan.Summary,
                scan.BlockerCount > 0 ? MessageType.Error : scan.WarningCount > 0 ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.EndVertical();

            if (scan.BlockerCount > 0)
            {
                EditorGUILayout.BeginVertical(panelStyle);
                GUILayout.Label($"Blockers ({scan.BlockerCount})", EditorStyles.boldLabel);
                foreach (var i in scan.Issues)
                    if (i.Severity == IssueSeverity.Blocker) IssueRow(i);
                EditorGUILayout.EndVertical();
            }

            if (scan.WarningCount > 0)
            {
                EditorGUILayout.BeginVertical(panelStyle);
                GUILayout.Label($"Warnings ({scan.WarningCount})", EditorStyles.boldLabel);
                foreach (var i in scan.Issues)
                    if (i.Severity == IssueSeverity.Warning) IssueRow(i);
                EditorGUILayout.EndVertical();
            }

            if (scan.BlockerCount == 0 && scan.WarningCount == 0)
            {
                EditorGUILayout.BeginVertical(panelStyle);
                GUILayout.Label("✓  All common checks passed", okStyle);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("Performance Snapshot", EditorStyles.boldLabel);
            Stat("Polygons", scan.PolyCount.ToString("N0"), scan.PolyCount > 70000 ? warnStyle : okStyle);
            Stat("Skinned meshes", scan.SkinnedMeshCount.ToString(), scan.SkinnedMeshCount > 8 ? warnStyle : null);
            Stat("Material slots", scan.MaterialSlots.ToString(), scan.MaterialSlots > 16 ? warnStyle : null);
            Stat("Bones", scan.BoneCount.ToString());
            Stat("Avatar height", $"{scan.AvatarHeightMeters:F2} m");
            Stat("PhysBones", scan.PhysBoneCount.ToString(), scan.PhysBoneCount > 256 ? warnStyle : null);
            Stat("Particles", scan.ParticleCount.ToString(), scan.ParticleCount > 16 ? warnStyle : null);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("VRChat Setup", EditorStyles.boldLabel);
            Stat("Descriptor", scan.HasDescriptor ? "OK" : "Missing", scan.HasDescriptor ? okStyle : errStyle);
            Stat("PipelineManager", scan.HasPipelineManager ? "OK" : "Missing", scan.HasPipelineManager ? okStyle : errStyle);
            Stat("Humanoid Animator", scan.HasHumanoidAnimator ? "OK" : "Missing", scan.HasHumanoidAnimator ? okStyle : errStyle);
            Stat("Animator Controller", scan.HasAnimatorController ? "OK" : "Missing", scan.HasAnimatorController ? okStyle : errStyle);
            Stat("Chest bone", scan.HasChestBone ? "OK" : "Missing", scan.HasChestBone ? okStyle : warnStyle);
            Stat("View position", scan.HasViewPosition ? "OK" : "Not set", scan.HasViewPosition ? okStyle : warnStyle);
            Stat("Lip sync", scan.HasLipSync ? "OK" : "Not set", scan.HasLipSync ? okStyle : warnStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("Textures", EditorStyles.boldLabel);
            Stat("Count", scan.TextureCount.ToString());
            Stat("4K+", scan.Textures4K.ToString(), scan.Textures4K > 0 ? errStyle : okStyle);
            Stat("Over 2K", scan.TexturesOver2K.ToString(), scan.TexturesOver2K > 0 ? warnStyle : okStyle);
            Stat("Est. memory", $"~{scan.TextureMemoryMB:F0} MB", scan.TextureMemoryMB > 100 ? warnStyle : null);
            Stat("No mipmaps", scan.TexturesNoMipmaps.ToString(), scan.TexturesNoMipmaps > 0 ? warnStyle : okStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawFixTab(AvatarScanResult scan)
        {
            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("Fix Upload Errors", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Fixes the issues most people hit: missing scripts, materials, T-Pose, bounds, audio, " +
                "view position, lip sync, PipelineManager, and other avatars in scene.\n\n" +
                "Does NOT change textures or shaders (use other tabs). Hair-safe material fix.",
                MessageType.Info);

            GUI.backgroundColor = new Color(0.18f, 0.55f, 0.88f);
            if (GUILayout.Button("Backup Avatar", GUILayout.Height(30))) BackupAvatar();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            GUI.backgroundColor = scan.BlockerCount > 0 ? new Color(0.2f, 0.78f, 0.28f) : new Color(0.35f, 0.65f, 0.4f);
            if (GUILayout.Button("FIX ALL UPLOAD ERRORS", GUILayout.Height(44)))
                RunFixAll();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            showIndividualFixes = EditorGUILayout.Foldout(showIndividualFixes, "Individual fixes", true);
            if (showIndividualFixes)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("Remove missing scripts")) WithUndo(() => VtoolAvatarFixes.RemoveMissingScripts(targetAvatar));
                if (GUILayout.Button("Fix missing material slots")) WithUndo(() => VtoolAvatarFixes.FixMissingMaterials(targetAvatar));
                if (GUILayout.Button("Add PipelineManager")) WithUndo(() => VtoolAvatarFixes.EnsurePipelineManager(targetAvatar));
                if (GUILayout.Button("Assign dummy animator controller")) WithUndo(() => VtoolAvatarFixes.AssignDummyController(targetAvatar));
                if (GUILayout.Button("Fix skinned mesh bounds")) WithUndo(() => VtoolAvatarFixes.FixMeshBounds(targetAvatar));
                if (GUILayout.Button("Fix audio (3D, volume, playOnAwake)")) WithUndo(() => { int p; VtoolAvatarFixes.FixAudioSources(targetAvatar, out p); });
                if (GUILayout.Button("Disable other avatars in scene")) WithUndo(() => VtoolAvatarFixes.DisableOtherAvatars(targetAvatar));
                if (GUILayout.Button("Align view position")) WithUndo(() => VtoolAvatarFixes.AlignViewPosition(targetAvatar));
                if (GUILayout.Button("Setup lip sync")) WithUndo(() => VtoolAvatarFixes.SetupLipSync(targetAvatar));
                if (GUILayout.Button("Clear blueprint ID (new upload)")) WithUndo(() => VtoolAvatarFixes.ClearBlueprintId(targetAvatar));
                if (GUILayout.Button("Normalize root scale (1,1,1) — changes size")) WithUndo(() => VtoolAvatarFixes.NormalizeRootScale(targetAvatar));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTexturesTab(AvatarScanResult scan)
        {
            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("Reduce Texture Size", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Changes import settings only — original files are NOT deleted. Restore anytime.", MessageType.Info);

            Stat("Textures", scan.TextureCount.ToString());
            Stat("4K+", scan.Textures4K.ToString(), scan.Textures4K > 0 ? errStyle : okStyle);
            Stat("Over 2K", scan.TexturesOver2K.ToString(), scan.TexturesOver2K > 0 ? warnStyle : okStyle);
            Stat("Memory", $"~{scan.TextureMemoryMB:F0} MB", scan.TextureMemoryMB > 100 ? warnStyle : null);

            EditorGUILayout.Space(6);
            textureCapSize = EditorGUILayout.IntPopup("Cap to", textureCapSize,
                new[] { "512", "1024", "2048 (VRChat max)" }, new[] { 512, 1024, 2048 });

            GUI.backgroundColor = new Color(0.72f, 0.14f, 0.2f);
            if (GUILayout.Button($"REDUCE TEXTURES TO {textureCapSize}px", GUILayout.Height(38)))
            {
                if (EditorUtility.DisplayDialog("Reduce Textures", $"Cap avatar textures to {textureCapSize}px import size?", "Reduce", "Cancel"))
                {
                    int n = VtoolAvatarFixes.CapTextureSizes(targetAvatar, textureCapSize);
                    EditorUtility.DisplayDialog("Done", $"Reduced {n} texture(s). Use Restore to undo.", "OK");
                    Repaint();
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Restore Original Texture Sizes", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Restore", "Restore textures to source file resolution?", "Restore", "Cancel"))
                {
                    int n = VtoolAvatarFixes.RestoreTextureSizes(targetAvatar);
                    EditorUtility.DisplayDialog("Done", $"Restored {n} texture(s).", "OK");
                    Repaint();
                }
            }

            if (GUILayout.Button("Enable Mipmaps", GUILayout.Height(26)))
            {
                WithUndo(() => VtoolAvatarFixes.EnableTextureMipmaps(targetAvatar));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("Quest / Android Shaders", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Quest uploads fail if materials don't use VRChat/Mobile shaders. " +
                "Duplicate materials first to keep PC versions.",
                MessageType.Warning);

            Stat("Non-Quest materials", scan.QuestBadShaders.ToString(), scan.QuestBadShaders > 0 ? warnStyle : okStyle);

            if (GUILayout.Button("Convert to Quest Shaders (duplicate materials)", GUILayout.Height(34)))
            {
                if (EditorUtility.DisplayDialog("Quest Conversion", "Duplicate materials then convert to VRChat/Mobile/Toon Lit?", "Convert", "Cancel"))
                {
                    int n = VtoolAvatarFixes.ConvertToQuestShaders(targetAvatar, true);
                    EditorUtility.DisplayDialog("Done", $"Converted {n} material slot(s).", "OK");
                    Repaint();
                }
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Actions

        private void RunFixAll()
        {
            if (!EditorUtility.DisplayDialog("Fix All",
                "Applies all safe pre-upload fixes.\n\nBack up first. Continue?", "Fix", "Cancel"))
                return;

            var s = VtoolAvatarFixes.ApplyAllSafeFixes(targetAvatar);

            EditorUtility.DisplayDialog("Fix Complete",
                $"Missing scripts removed: {s.MissingScripts}\n" +
                $"Material slots fixed: {s.MaterialSlots}\n" +
                $"PipelineManager added: {(s.PipelineManager ? "yes" : "no")}\n" +
                $"Animator controller: {(s.AnimatorController ? "assigned" : "skipped")}\n" +
                $"Bounds fixed: {s.Bounds}\n" +
                $"Audio fixed: {s.Audio} (playOnAwake: {s.AudioPlayOnAwake})\n" +
                $"Mipmaps enabled: {s.Mipmaps}\n" +
                $"Other avatars disabled: {s.OtherAvatarsDisabled}\n" +
                $"View position: {(s.ViewPosition ? "OK" : "skipped")}\n" +
                $"Lip sync: {(s.LipSync ? "OK" : "skipped")}\n\n" +
                "Re-check Pre-Upload Check tab. Fix pink/broken shaders manually.",
                "OK");
            Repaint();
        }

        private void WithUndo(System.Action action)
        {
            Undo.RegisterFullObjectHierarchyUndo(targetAvatar, "Vtool Fix");
            action();
            VtoolAvatarFixes.MarkDirty();
            Repaint();
        }

        private void BackupAvatar()
        {
            var backup = Instantiate(targetAvatar);
            backup.name = targetAvatar.name + "_Backup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            backup.SetActive(false);
            Undo.RegisterCreatedObjectUndo(backup, "Backup");
            VtoolAvatarFixes.MarkDirty();
            EditorUtility.DisplayDialog("Backup", $"Created:\n{backup.name}", "OK");
        }

        private void AutoDetectAvatar()
        {
            if (targetAvatar != null) return;
            var type = VtoolAvatarFixes.GetDescriptorType();
            if (type == null) return;

            if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent(type) != null)
            {
                targetAvatar = Selection.activeGameObject;
                return;
            }

            var found = VtoolAvatarFixes.FindObjects(type);
            if (found.Length > 0) targetAvatar = ((Component)found[0]).gameObject;
        }

        private static string GetVersion()
        {
            if (!string.IsNullOrEmpty(cachedVersion)) return cachedVersion;
            cachedVersion = FallbackVersion;
            if (File.Exists("Packages/com.vtool.autofixer/package.json"))
            {
                var m = Regex.Match(File.ReadAllText("Packages/com.vtool.autofixer/package.json"), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) cachedVersion = m.Groups[1].Value;
            }
            return cachedVersion;
        }

        #endregion
    }
}
