using UnityEngine;
using UnityEditor;
using System.IO;

namespace XVR.Tools
{
    public class VRCAvatarAutoFixer : EditorWindow
    {
        private const string SupportUrl = "https://buymeacoffee.com/Omv1";
        private static readonly Color Accent = new Color(0.78f, 0.18f, 0.24f);
        private static readonly Color Muted = new Color(0.62f, 0.62f, 0.62f);

        private GameObject targetAvatar;
        private Vector2 scrollPos;
        private int tabIndex;
        private int textureCapSize = 2048;
        private bool showIndividualFixes;
        private Texture2D logoTexture;

        private GUIStyle headerStyle, subStyle, sectionStyle, panelStyle;
        private GUIStyle okStyle, warnStyle, errStyle, linkStyle;
        private bool stylesReady;

        private readonly string[] tabs = { "Check", "Fix", "Textures" };

        [MenuItem("Vtool/Avatar Auto-Fixer Pro")]
        public static void ShowWindow()
        {
            var w = GetWindow<VRCAvatarAutoFixer>("Vtool");
            w.minSize = new Vector2(440, 640);
            w.Show();
        }

        private void OnEnable()
        {
            AutoDetectAvatar();
            LoadLogo();
        }

        private void OnSelectionChange() { if (targetAvatar == null) Repaint(); }

        private void OnGUI()
        {
            InitStyles();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();
            DrawUpdateBanner();
            DrawAvatarPicker();

            if (targetAvatar == null)
            {
                EditorGUILayout.HelpBox("Assign an avatar root to run checks and fixes.", MessageType.Info);
                DrawSupportFooter();
                EditorGUILayout.EndScrollView();
                return;
            }

            GUILayout.Space(8);
            tabIndex = GUILayout.Toolbar(tabIndex, tabs, GUILayout.Height(26));
            GUILayout.Space(8);

            var scan = VtoolAvatarScan.Scan(targetAvatar);

            switch (tabIndex)
            {
                case 0: DrawCheckTab(scan); break;
                case 1: DrawFixTab(scan); break;
                case 2: DrawTexturesTab(scan); break;
            }

            DrawSupportFooter();
            EditorGUILayout.EndScrollView();
        }

        #region UI chrome

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 17, margin = new RectOffset(0, 0, 2, 0) };
            subStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Muted }, margin = new RectOffset(0, 0, 0, 0) };
            sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, margin = new RectOffset(0, 0, 4, 6) };
            panelStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(4, 4, 4, 4) };
            okStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.35f, 0.82f, 0.48f) } };
            warnStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.68f, 0.2f) } };
            errStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.95f, 0.35f, 0.35f) } };
            linkStyle = new GUIStyle(EditorStyles.linkLabel) { alignment = TextAnchor.MiddleRight };
        }

        private void LoadLogo()
        {
            if (logoTexture != null) return;

            const string pkgPath = "Packages/com.vtool.autofixer/Editor/Resources/VtoolLogo.png";
            if (File.Exists(pkgPath))
                logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pkgPath);

            if (logoTexture == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("VtoolLogo t:Texture2D"))
                {
                    logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                    if (logoTexture != null) break;
                }
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(panelStyle);
            EditorGUILayout.BeginHorizontal();

            if (logoTexture != null)
                GUILayout.Label(logoTexture, GUILayout.Width(48), GUILayout.Height(48));

            EditorGUILayout.BeginVertical();
            GUILayout.Label("Pre-Upload Fixer", headerStyle);
            GUILayout.Label("VRChat avatar checks & safe fixes", subStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            var line = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, Accent);
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField(
                "Fix All never deletes meshes, objects, or materials. Back up first.",
                EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawSupportFooter()
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("☕ Support on Buy Me a Coffee", linkStyle))
                Application.OpenURL(SupportUrl);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawUpdateBanner()
        {
            if (!VtoolPackageUpdateHandler.HasPendingUpdate) return;
            EditorGUILayout.HelpBox("Update detected — reloading…", MessageType.Info);
            if (GUILayout.Button("Apply Update Now"))
                VtoolPackageUpdateHandler.CheckForPackageUpdate(silent: false, force: true);
        }

        private void DrawAvatarPicker()
        {
            EditorGUILayout.BeginVertical(panelStyle);
            targetAvatar = (GameObject)EditorGUILayout.ObjectField("Avatar", targetAvatar, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Use Selected", GUILayout.Width(100)))
            {
                if (Selection.activeGameObject != null)
                    targetAvatar = Selection.activeGameObject;
            }
            if (GUILayout.Button("Auto-Detect", GUILayout.Width(100)))
                AutoDetectAvatar();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSection(string title, System.Action body)
        {
            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label(title, sectionStyle);
            body();
            EditorGUILayout.EndVertical();
        }

        private void Stat(string label, string value, GUIStyle style = null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130));
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
                EditorGUILayout.LabelField(issue.FixHint, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Tabs

        private void DrawCheckTab(AvatarScanResult scan)
        {
            DrawSection("Status", () =>
            {
                EditorGUILayout.HelpBox(scan.Summary,
                    scan.BlockerCount > 0 ? MessageType.Error : scan.WarningCount > 0 ? MessageType.Warning : MessageType.Info);
            });

            if (scan.BlockerCount > 0)
            {
                DrawSection($"Blockers ({scan.BlockerCount})", () =>
                {
                    foreach (var i in scan.Issues)
                        if (i.Severity == IssueSeverity.Blocker) IssueRow(i);
                });
            }

            if (scan.WarningCount > 0)
            {
                DrawSection($"Warnings ({scan.WarningCount})", () =>
                {
                    foreach (var i in scan.Issues)
                        if (i.Severity == IssueSeverity.Warning) IssueRow(i);
                });
            }

            if (scan.BlockerCount == 0 && scan.WarningCount == 0)
            {
                DrawSection("Result", () => GUILayout.Label("All common checks passed.", okStyle));
            }

            DrawSection("Performance", () =>
            {
                Stat("Polygons", scan.PolyCount.ToString("N0"), scan.PolyCount > 70000 ? warnStyle : null);
                Stat("Skinned meshes", scan.SkinnedMeshCount.ToString(), scan.SkinnedMeshCount > 8 ? warnStyle : null);
                Stat("Material slots", scan.MaterialSlots.ToString(), scan.MaterialSlots > 16 ? warnStyle : null);
                Stat("Bones", scan.BoneCount.ToString());
                Stat("Height", $"{scan.AvatarHeightMeters:F2} m");
                Stat("PhysBones", scan.PhysBoneCount.ToString(), scan.PhysBoneCount > 256 ? warnStyle : null);
                Stat("Particles", scan.ParticleCount.ToString(), scan.ParticleCount > 16 ? warnStyle : null);
            });

            DrawSection("VRChat", () =>
            {
                Stat("Descriptor", scan.HasDescriptor ? "OK" : "Missing", scan.HasDescriptor ? okStyle : errStyle);
                Stat("PipelineManager", scan.HasPipelineManager ? "OK" : "Missing", scan.HasPipelineManager ? okStyle : errStyle);
                Stat("Humanoid rig", scan.HasHumanoidAnimator ? "OK" : "Missing", scan.HasHumanoidAnimator ? okStyle : errStyle);
                Stat("Chest bone", scan.HasChestBone ? "OK" : "Missing", scan.HasChestBone ? okStyle : warnStyle);
                Stat("View position", scan.HasViewPosition ? "OK" : "Not set", scan.HasViewPosition ? okStyle : warnStyle);
                Stat("Lip sync", scan.HasLipSync ? "OK" : "Not set", scan.HasLipSync ? okStyle : warnStyle);
            });

            DrawSection("Textures", () =>
            {
                Stat("Count", scan.TextureCount.ToString());
                Stat("4K+", scan.Textures4K.ToString(), scan.Textures4K > 0 ? errStyle : okStyle);
                Stat("Over 2K", scan.TexturesOver2K.ToString(), scan.TexturesOver2K > 0 ? warnStyle : okStyle);
                Stat("Est. memory", $"~{scan.TextureMemoryMB:F0} MB", scan.TextureMemoryMB > 100 ? warnStyle : null);
                Stat("No mipmaps", scan.TexturesNoMipmaps.ToString(), scan.TexturesNoMipmaps > 0 ? warnStyle : okStyle);
            });
        }

        private void DrawFixTab(AvatarScanResult scan)
        {
            DrawSection("Quick actions", () =>
            {
                EditorGUILayout.LabelField(
                    "Fix All only adds or adjusts settings. It does not remove GameObjects, meshes, or material slots.",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(6);

                if (GUILayout.Button("Backup Avatar", GUILayout.Height(28)))
                    BackupAvatar();

                GUILayout.Space(4);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = scan.BlockerCount > 0 ? new Color(0.28f, 0.72f, 0.38f) : new Color(0.4f, 0.55f, 0.45f);
                if (GUILayout.Button("Fix All Safe Upload Errors", GUILayout.Height(36)))
                    RunFixAll();
                GUI.backgroundColor = prev;

                GUILayout.Space(6);
                showIndividualFixes = EditorGUILayout.Foldout(showIndividualFixes, "Individual fixes", true);
                if (showIndividualFixes)
                {
                    EditorGUI.indentLevel++;
                    if (GUILayout.Button("Fix missing material slots (nearby material only)"))
                        WithUndo(() => VtoolAvatarFixes.FixMissingMaterials(targetAvatar, allowPlaceholder: false));
                    if (GUILayout.Button("Add PipelineManager")) WithUndo(() => VtoolAvatarFixes.EnsurePipelineManager(targetAvatar));
                    if (GUILayout.Button("Fix skinned mesh bounds")) WithUndo(() => VtoolAvatarFixes.FixMeshBounds(targetAvatar));
                    if (GUILayout.Button("Fix audio (3D, volume, playOnAwake)")) WithUndo(() => { int p; VtoolAvatarFixes.FixAudioSources(targetAvatar, out p); });
                    if (GUILayout.Button("Align view position (only if empty)")) WithUndo(() => VtoolAvatarFixes.AlignViewPosition(targetAvatar, onlyIfUnset: true));
                    if (GUILayout.Button("Setup lip sync (only if empty)")) WithUndo(() => VtoolAvatarFixes.SetupLipSync(targetAvatar, onlyIfUnset: true));

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Optional / changes more", EditorStyles.miniLabel);
                    if (GUILayout.Button("Remove missing script slots"))
                        RunRemoveMissingScripts();
                    if (GUILayout.Button("Fix materials with placeholder (last resort)"))
                        RunPlaceholderMaterials();
                    if (GUILayout.Button("Disable other avatars in scene"))
                        RunDisableOtherAvatars();
                    if (GUILayout.Button("Clear blueprint ID (new upload)"))
                        RunClearBlueprintId();
                    EditorGUI.indentLevel--;
                }
            });
        }

        private void DrawTexturesTab(AvatarScanResult scan)
        {
            DrawSection("Texture size", () =>
            {
                Stat("Textures", scan.TextureCount.ToString());
                Stat("4K+", scan.Textures4K.ToString(), scan.Textures4K > 0 ? errStyle : okStyle);
                Stat("Over 2K", scan.TexturesOver2K.ToString(), scan.TexturesOver2K > 0 ? warnStyle : okStyle);
                Stat("Memory", $"~{scan.TextureMemoryMB:F0} MB", scan.TextureMemoryMB > 100 ? warnStyle : null);

                GUILayout.Space(6);
                textureCapSize = EditorGUILayout.IntPopup("Cap to", textureCapSize,
                    new[] { "512", "1024", "2048 (VRChat max)" }, new[] { 512, 1024, 2048 });

                var prev = GUI.backgroundColor;
                GUI.backgroundColor = Accent;
                if (GUILayout.Button($"Reduce to {textureCapSize}px", GUILayout.Height(32)))
                {
                    if (EditorUtility.DisplayDialog("Reduce Textures", $"Cap avatar textures to {textureCapSize}px import size?", "Reduce", "Cancel"))
                    {
                        int n = VtoolAvatarFixes.CapTextureSizes(targetAvatar, textureCapSize);
                        EditorUtility.DisplayDialog("Done", $"Reduced {n} texture(s). Use Restore to undo.", "OK");
                        Repaint();
                    }
                }
                GUI.backgroundColor = prev;

                if (GUILayout.Button("Restore original sizes", GUILayout.Height(26)))
                {
                    if (EditorUtility.DisplayDialog("Restore", "Restore textures to source file resolution?", "Restore", "Cancel"))
                    {
                        int n = VtoolAvatarFixes.RestoreTextureSizes(targetAvatar);
                        EditorUtility.DisplayDialog("Done", $"Restored {n} texture(s).", "OK");
                        Repaint();
                    }
                }

                if (GUILayout.Button("Enable mipmaps", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog("Enable Mipmaps", "Changes texture import settings for textures on this avatar. Continue?", "Enable", "Cancel"))
                        WithUndo(() => VtoolAvatarFixes.EnableTextureMipmaps(targetAvatar));
                }
            });

            DrawSection("Quest / Android", () =>
            {
                EditorGUILayout.LabelField(
                    "Quest uploads need VRChat/Mobile shaders. Duplicate materials first to keep PC versions.",
                    EditorStyles.wordWrappedMiniLabel);
                Stat("Non-Quest materials", scan.QuestBadShaders.ToString(), scan.QuestBadShaders > 0 ? warnStyle : okStyle);

                if (GUILayout.Button("Convert to Quest shaders", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Quest Conversion", "Duplicate materials then convert to VRChat/Mobile/Toon Lit?", "Convert", "Cancel"))
                    {
                        int n = VtoolAvatarFixes.ConvertToQuestShaders(targetAvatar, true);
                        EditorUtility.DisplayDialog("Done", $"Converted {n} material slot(s).", "OK");
                        Repaint();
                    }
                }
            });
        }

        #endregion

        #region Actions

        private void RunFixAll()
        {
            if (!EditorUtility.DisplayDialog("Fix All",
                "Applies safe fixes only.\n\n" +
                "Does NOT remove scripts, meshes, objects, or materials.\n" +
                "Does NOT change view position or lip sync if already set.\n\n" +
                "Back up first. Continue?", "Fix", "Cancel"))
                return;

            var s = VtoolAvatarFixes.ApplyAllSafeFixes(targetAvatar);

            EditorUtility.DisplayDialog("Fix Complete",
                $"Material slots fixed: {s.MaterialSlots}\n" +
                $"PipelineManager added: {(s.PipelineManager ? "yes" : "no")}\n" +
                $"Bounds fixed: {s.Bounds}\n" +
                $"Audio fixed: {s.Audio} (playOnAwake: {s.AudioPlayOnAwake})\n" +
                $"View position: {(s.ViewPosition ? "set" : "skipped")}\n" +
                $"Lip sync: {(s.LipSync ? "set" : "skipped")}\n\n" +
                "Re-check the Check tab. Fix pink/broken shaders manually.",
                "OK");
            Repaint();
        }

        private void RunRemoveMissingScripts()
        {
            if (!EditorUtility.DisplayDialog("Remove Missing Scripts",
                "This removes broken empty script slots from GameObjects.\n\n" +
                "It does NOT delete meshes or child objects.\n" +
                "Only use if you know those scripts are gone for good.\n\nContinue?",
                "Remove", "Cancel"))
                return;

            WithUndo(() =>
            {
                int n = VtoolAvatarFixes.RemoveMissingScripts(targetAvatar);
                EditorUtility.DisplayDialog("Done", $"Removed {n} missing script slot(s).", "OK");
            });
        }

        private void RunPlaceholderMaterials()
        {
            if (!EditorUtility.DisplayDialog("Placeholder Materials",
                "Fills empty material slots with a gray placeholder.\n\n" +
                "This can change how parts look. Prefer fixing materials manually.\n\nContinue?",
                "Continue", "Cancel"))
                return;

            WithUndo(() =>
            {
                int n = VtoolAvatarFixes.FixMissingMaterials(targetAvatar, allowPlaceholder: true);
                EditorUtility.DisplayDialog("Done", $"Filled {n} slot(s).", "OK");
            });
        }

        private void RunDisableOtherAvatars()
        {
            if (!EditorUtility.DisplayDialog("Disable Other Avatars",
                "Hides other avatar roots in this scene.\n\n" +
                "Your selected avatar is not changed.\n\nContinue?",
                "Disable", "Cancel"))
                return;

            WithUndo(() =>
            {
                int n = VtoolAvatarFixes.DisableOtherAvatars(targetAvatar);
                EditorUtility.DisplayDialog("Done", $"Disabled {n} other avatar(s).", "OK");
            });
        }

        private void RunClearBlueprintId()
        {
            if (!EditorUtility.DisplayDialog("Clear Blueprint ID",
                "Clears the PipelineManager blueprint ID for a fresh upload.\n\nContinue?",
                "Clear", "Cancel"))
                return;

            WithUndo(() =>
            {
                bool ok = VtoolAvatarFixes.ClearBlueprintId(targetAvatar);
                EditorUtility.DisplayDialog("Done", ok ? "Blueprint ID cleared." : "Nothing to clear.", "OK");
            });
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

        #endregion
    }
}
