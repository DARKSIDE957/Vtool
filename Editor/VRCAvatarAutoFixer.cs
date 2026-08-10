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

        private GUIStyle headerStyle, subStyle, sectionStyle, panelStyle, captionStyle;
        private GUIStyle okStyle, warnStyle, errStyle, linkStyle;
        private bool stylesReady;

        // Snapshot on EventType.Layout so Layout/Repaint/Input draw the same control tree.
        private AvatarScanResult layoutScan;
        private bool layoutScanValid;
        private bool layoutHasAvatar;
        private bool layoutHasRollback;
        private bool layoutHasPendingUpdate;
        private bool layoutShowLogo;
        private bool layoutShowIndividualFixes;
        private int layoutLang;

        [MenuItem("Vtool/Avatar Auto-Fixer Pro")]
        public static void ShowWindow()
        {
            var w = GetWindow<VRCAvatarAutoFixer>("Vtool");
            w.minSize = new Vector2(440, 640);
            w.Show();
        }

        private void OnEnable()
        {
            stylesReady = false;
            AutoDetectAvatar();
            LoadLogo();
            RefreshLayoutCache();
        }

        private void OnSelectionChange() { if (targetAvatar == null) Repaint(); }

        private void RefreshLayoutCache()
        {
            layoutHasAvatar = targetAvatar != null;
            layoutHasRollback = layoutHasAvatar && VtoolAvatarRollback.HasRollback(targetAvatar);
            layoutHasPendingUpdate = VtoolPackageUpdateHandler.HasPendingUpdate;
            layoutShowLogo = logoTexture != null;
            layoutShowIndividualFixes = showIndividualFixes;
            layoutLang = (int)L.Language;
            if (layoutHasAvatar)
            {
                layoutScan = VtoolAvatarScan.Scan(targetAvatar);
                layoutScanValid = true;
            }
            else
            {
                layoutScan = default;
                layoutScanValid = false;
            }
        }

        private void Defer(System.Action action)
        {
            if (action == null) return;
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                try { action(); }
                finally { Repaint(); }
            };
        }

        private void OnGUI()
        {
            InitStyles();

            // Must refresh only on Layout — otherwise control counts diverge across events.
            if (Event.current.type == EventType.Layout)
                RefreshLayoutCache();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            try
            {
                DrawHeader();
                DrawUpdateBanner();
                DrawAvatarPicker();
                DrawRollbackBanner();

                if (!layoutHasAvatar)
                {
                    EditorGUILayout.HelpBox(L.T("assign.avatar") ?? string.Empty, MessageType.Info);
                    DrawSupportFooter();
                    return;
                }

                GUILayout.Space(8);
                tabIndex = GUILayout.Toolbar(tabIndex, new[]
                {
                    L.T("tab.check") ?? "Check",
                    L.T("tab.fix") ?? "Fix",
                    L.T("tab.textures") ?? "Textures"
                }, GUILayout.Height(26));
                GUILayout.Space(8);

                var scan = layoutScanValid ? layoutScan : VtoolAvatarScan.Scan(targetAvatar);

                switch (tabIndex)
                {
                    case 0: DrawCheckTab(scan); break;
                    case 1: DrawFixTab(scan); break;
                    case 2: DrawTexturesTab(scan); break;
                }

                DrawSupportFooter();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        #region UI chrome

        private void InitStyles()
        {
            // GUIStyles die after script reload; also EditorStyles can be null early in the frame.
            if (stylesReady &&
                headerStyle != null && subStyle != null && sectionStyle != null &&
                panelStyle != null && captionStyle != null &&
                okStyle != null && warnStyle != null && errStyle != null && linkStyle != null)
                return;

            try
            {
                var bold = EditorStyles.boldLabel ?? GUI.skin.label;
                var mini = EditorStyles.miniLabel ?? GUI.skin.label;
                var help = EditorStyles.helpBox ?? GUI.skin.box;
                var label = EditorStyles.label ?? GUI.skin.label;
                var wrapMini = EditorStyles.wordWrappedMiniLabel ?? mini;
                var link = EditorStyles.linkLabel ?? mini;

                headerStyle = new GUIStyle(bold) { fontSize = 17, margin = new RectOffset(0, 0, 2, 0) };
                subStyle = new GUIStyle(mini) { normal = { textColor = Muted }, margin = new RectOffset(0, 0, 0, 0) };
                sectionStyle = new GUIStyle(bold) { fontSize = 12, margin = new RectOffset(0, 0, 4, 6) };
                panelStyle = new GUIStyle(help) { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(4, 4, 4, 4) };
                captionStyle = new GUIStyle(wrapMini) { wordWrap = true, normal = { textColor = Muted } };
                okStyle = new GUIStyle(label) { normal = { textColor = new Color(0.35f, 0.82f, 0.48f) } };
                warnStyle = new GUIStyle(label) { normal = { textColor = new Color(1f, 0.68f, 0.2f) } };
                errStyle = new GUIStyle(label) { normal = { textColor = new Color(0.95f, 0.35f, 0.35f) } };
                linkStyle = new GUIStyle(link) { alignment = TextAnchor.MiddleRight };
                stylesReady = captionStyle != null;
            }
            catch
            {
                stylesReady = false;
                captionStyle = null;
            }
        }

        private GUIStyle CaptionStyle()
        {
            if (captionStyle != null) return captionStyle;
            if (EditorStyles.wordWrappedMiniLabel != null) return EditorStyles.wordWrappedMiniLabel;
            if (EditorStyles.miniLabel != null) return EditorStyles.miniLabel;
            if (EditorStyles.label != null) return EditorStyles.label;
            return GUI.skin != null ? GUI.skin.label : new GUIStyle();
        }

        private void Caption(string key)
        {
            // Always emit the same controls (never early-out) so Layout/Repaint stay matched.
            string text = string.IsNullOrEmpty(key) ? string.Empty : (L.T(key) ?? string.Empty);
            GUILayout.Label(text, CaptionStyle());
            GUILayout.Space(2);
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
            EditorGUILayout.BeginVertical(panelStyle ?? EditorStyles.helpBox ?? GUI.skin.box);
            try
            {
                EditorGUILayout.BeginHorizontal();
                try
                {
                    // Fixed logo slot every frame (texture or empty)
                    if (layoutShowLogo && logoTexture != null)
                        GUILayout.Label(logoTexture, GUILayout.Width(48), GUILayout.Height(48));
                    else
                        GUILayout.Label(GUIContent.none, GUILayout.Width(48), GUILayout.Height(48));

                    EditorGUILayout.BeginVertical();
                    try
                    {
                        GUILayout.Label(L.T("header.title") ?? "Vtool", headerStyle ?? CaptionStyle());
                        GUILayout.Label(L.T("header.subtitle") ?? string.Empty, subStyle ?? CaptionStyle());
                    }
                    finally
                    {
                        EditorGUILayout.EndVertical();
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.BeginVertical(GUILayout.Width(180));
                    try
                    {
                        GUILayout.Label(L.T("lang.label") ?? "Language", CaptionStyle());
                        int next = EditorGUILayout.Popup(layoutLang, L.LanguageDisplayNames);
                        if (next != layoutLang && next >= 0 && next <= 2)
                        {
                            int lang = next;
                            Defer(() => L.Language = (VtoolLanguage)lang);
                        }
                    }
                    finally
                    {
                        EditorGUILayout.EndVertical();
                    }
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }

                var line = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(line, Accent);
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }

            GUILayout.Label(L.T("header.safety") ?? string.Empty, CaptionStyle());
        }

        private void DrawSupportFooter()
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            try
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("☕ " + (L.T("support.coffee") ?? "Support")), linkStyle ?? CaptionStyle()))
                    Application.OpenURL(SupportUrl);
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Space(4);
        }

        private void DrawUpdateBanner()
        {
            if (!layoutHasPendingUpdate) return;
            EditorGUILayout.HelpBox(L.T("update.detected") ?? "Update detected.", MessageType.Info);
            if (GUILayout.Button(L.T("btn.apply_update") ?? "Apply Update"))
                Defer(() => VtoolPackageUpdateHandler.CheckForPackageUpdate(silent: false, force: true));
        }

        private void DrawAvatarPicker()
        {
            EditorGUILayout.BeginVertical(panelStyle ?? EditorStyles.helpBox ?? GUI.skin.box);
            try
            {
                targetAvatar = (GameObject)EditorGUILayout.ObjectField(
                    L.T("field.avatar") ?? "Avatar", targetAvatar, typeof(GameObject), true);

                EditorGUILayout.BeginHorizontal();
                try
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent(L.T("btn.use_selected") ?? "Use Selected", L.T("tip.use_selected") ?? string.Empty), GUILayout.Width(120)))
                    {
                        var sel = Selection.activeGameObject;
                        if (sel != null)
                            Defer(() => targetAvatar = sel);
                    }
                    if (GUILayout.Button(new GUIContent(L.T("btn.auto_detect") ?? "Auto-Detect", L.T("tip.auto_detect") ?? string.Empty), GUILayout.Width(120)))
                        Defer(AutoDetectAvatar);
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawRollbackBanner()
        {
            // Always reserve the same controls when an avatar is assigned.
            if (!layoutHasAvatar) return;

            EditorGUILayout.BeginVertical(panelStyle ?? EditorStyles.helpBox ?? GUI.skin.box);
            try
            {
                bool prevEnabled = GUI.enabled;
                GUI.enabled = layoutHasRollback;
                GUILayout.Label(layoutHasRollback
                    ? (L.T("rollback.banner") ?? string.Empty)
                    : (L.T("rollback.none") ?? "No rollback snapshot yet."), CaptionStyle());
                var prev = GUI.backgroundColor;
                if (layoutHasRollback)
                    GUI.backgroundColor = new Color(0.85f, 0.45f, 0.2f);
                if (GUILayout.Button(new GUIContent(L.T("btn.rollback") ?? "Rollback", L.T("tip.rollback") ?? string.Empty), GUILayout.Height(30)))
                    Defer(RunRollback);
                GUI.backgroundColor = prev;
                Caption("cap.rollback");
                GUI.enabled = prevEnabled;
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawSection(string title, System.Action body)
        {
            EditorGUILayout.BeginVertical(panelStyle ?? EditorStyles.helpBox ?? GUI.skin.box);
            try
            {
                GUILayout.Label(title ?? string.Empty, sectionStyle ?? EditorStyles.boldLabel ?? GUI.skin.label);
                body?.Invoke();
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void Stat(string label, string value, GUIStyle style = null)
        {
            EditorGUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label(label ?? string.Empty, GUILayout.Width(170));
                GUILayout.Label(value ?? string.Empty, style ?? EditorStyles.label ?? GUI.skin.label);
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private bool ActionButton(string labelKey, string tipKey, string capKey = null, float height = 0f)
        {
            var content = new GUIContent(L.T(labelKey) ?? string.Empty, L.T(tipKey) ?? string.Empty);
            bool clicked = height > 0f
                ? GUILayout.Button(content, GUILayout.Height(height))
                : GUILayout.Button(content);

            if (!string.IsNullOrEmpty(capKey))
                Caption(capKey);

            return clicked;
        }

        private void IssueRow(AvatarIssue issue)
        {
            GUIStyle icon = issue.Severity == IssueSeverity.Blocker ? (errStyle ?? CaptionStyle())
                : issue.Severity == IssueSeverity.Warning ? (warnStyle ?? CaptionStyle()) : CaptionStyle();
            string mark = issue.Severity == IssueSeverity.Blocker ? "✗" : issue.Severity == IssueSeverity.Warning ? "!" : "·";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox ?? GUI.skin.box);
            try
            {
                EditorGUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label(mark, icon, GUILayout.Width(14));
                    if (!string.IsNullOrEmpty(issue.Code))
                        GUILayout.Label(issue.Code, errStyle ?? CaptionStyle(), GUILayout.Width(140));
                    else
                        GUILayout.Label(string.Empty, CaptionStyle(), GUILayout.Width(140));
                    GUILayout.Label(issue.Message ?? string.Empty, EditorStyles.wordWrappedLabel ?? CaptionStyle());
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
                if (!string.IsNullOrEmpty(issue.FixHint))
                    GUILayout.Label(issue.FixHint, CaptionStyle());
                else
                    GUILayout.Label(string.Empty, CaptionStyle());
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        #endregion

        #region Tabs

        private void DrawCheckTab(AvatarScanResult scan)
        {
            DrawSection(L.T("sec.status"), () =>
            {
                EditorGUILayout.HelpBox(scan.Summary ?? string.Empty,
                    scan.BlockerCount > 0 ? MessageType.Error : scan.WarningCount > 0 ? MessageType.Warning : MessageType.Info);

                if (GUILayout.Button(new GUIContent(L.T("btn.copy_errors") ?? "Copy Error Codes", L.T("tip.copy_errors") ?? string.Empty), GUILayout.Height(28)))
                    Defer(() => CopyErrorCodes(scan));
                Caption("cap.copy_errors");
            });

            // Always draw the three outcome sections so Layout/Repaint stay matched.
            DrawSection(L.TF("sec.blockers", scan.BlockerCount) ?? "Blockers", () =>
            {
                if (scan.BlockerCount <= 0)
                {
                    GUILayout.Label(L.T("result.no_blockers") ?? "No blockers.", CaptionStyle());
                    return;
                }
                foreach (var i in scan.Issues)
                    if (i.Severity == IssueSeverity.Blocker) IssueRow(i);
            });

            DrawSection(L.TF("sec.warnings", scan.WarningCount) ?? "Warnings", () =>
            {
                if (scan.WarningCount <= 0)
                {
                    GUILayout.Label(L.T("result.no_warnings") ?? "No warnings.", CaptionStyle());
                    return;
                }
                foreach (var i in scan.Issues)
                    if (i.Severity == IssueSeverity.Warning) IssueRow(i);
            });

            DrawSection(L.T("sec.result") ?? "Result", () =>
            {
                if (scan.BlockerCount == 0 && scan.WarningCount == 0)
                    GUILayout.Label(L.T("result.all_ok") ?? "OK", okStyle ?? CaptionStyle());
                else
                    GUILayout.Label(L.T("result.has_issues") ?? "Issues listed above.", CaptionStyle());
            });

            DrawSection(L.T("sec.performance"), () =>
            {
                Stat(L.T("stat.polygons"), scan.PolyCount.ToString("N0"), scan.PolyCount > 70000 ? warnStyle : null);
                Stat(L.T("stat.skinned"), scan.SkinnedMeshCount.ToString(), scan.SkinnedMeshCount > 8 ? warnStyle : null);
                Stat(L.T("stat.mat_slots"), scan.MaterialSlots.ToString(), scan.MaterialSlots > 16 ? warnStyle : null);
                Stat(L.T("stat.bones"), scan.BoneCount.ToString());
                Stat(L.T("stat.height"), $"{scan.AvatarHeightMeters:F2} m");
                Stat(L.T("stat.physbones"), scan.PhysBoneCount.ToString(),
                    scan.PhysBoneCount > 256 ? errStyle : scan.PhysBoneCount > 32 ? warnStyle : null);
                Stat(L.T("stat.particles"), scan.ParticleCount.ToString(), scan.ParticleCount > 16 ? warnStyle : null);
            });

            DrawSection(L.T("sec.vrchat"), () =>
            {
                Stat(L.T("stat.descriptor"), scan.HasDescriptor ? L.T("stat.ok") : L.T("stat.missing"), scan.HasDescriptor ? okStyle : errStyle);
                Stat(L.T("stat.pipeline"), scan.HasPipelineManager ? L.T("stat.ok") : L.T("stat.missing"), scan.HasPipelineManager ? okStyle : errStyle);
                Stat(L.T("stat.humanoid"), scan.HasHumanoidAnimator ? L.T("stat.ok") : L.T("stat.missing"), scan.HasHumanoidAnimator ? okStyle : errStyle);
                Stat(L.T("stat.chest"), scan.HasChestBone ? L.T("stat.ok") : L.T("stat.missing"), scan.HasChestBone ? okStyle : warnStyle);
                Stat(L.T("stat.view"), scan.HasViewPosition ? L.T("stat.ok") : L.T("stat.not_set"), scan.HasViewPosition ? okStyle : warnStyle);
                Stat(L.T("stat.lipsync"), scan.HasLipSync ? L.T("stat.ok") : L.T("stat.not_set"), scan.HasLipSync ? okStyle : warnStyle);
            });

            DrawSection(L.T("sec.textures"), () =>
            {
                Stat(L.T("stat.count"), scan.TextureCount.ToString());
                Stat(L.T("stat.4k"), scan.Textures4K.ToString(), scan.Textures4K > 0 ? errStyle : okStyle);
                Stat(L.T("stat.over2k"), scan.TexturesOver2K.ToString(), scan.TexturesOver2K > 0 ? warnStyle : okStyle);
                Stat(L.T("stat.memory"), $"~{scan.TextureMemoryMB:F0} MB", scan.TextureMemoryMB > 100 ? warnStyle : null);
                Stat(L.T("stat.nomip"), scan.TexturesNoMipmaps.ToString(), scan.TexturesNoMipmaps > 0 ? warnStyle : okStyle);
            });
        }

        private void DrawFixTab(AvatarScanResult scan)
        {
            DrawSection(L.T("sec.quick"), () =>
            {
                GUILayout.Label(L.T("fix.intro") ?? string.Empty, CaptionStyle());
                GUILayout.Space(6);

                if (ActionButton("btn.backup", "tip.backup", "cap.backup", 28f))
                    Defer(BackupAvatar);

                // Always same rollback controls (enabled only when snapshot exists)
                {
                    bool prevEnabled = GUI.enabled;
                    GUI.enabled = layoutHasRollback;
                    var prevRollback = GUI.backgroundColor;
                    if (layoutHasRollback)
                        GUI.backgroundColor = new Color(0.85f, 0.45f, 0.2f);
                    if (GUILayout.Button(new GUIContent(L.T("btn.rollback") ?? "Rollback", L.T("tip.rollback") ?? string.Empty), GUILayout.Height(28)))
                        Defer(RunRollback);
                    GUI.backgroundColor = prevRollback;
                    Caption("cap.rollback");
                    GUI.enabled = prevEnabled;
                }

                GUILayout.Space(4);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = scan.BlockerCount > 0 ? new Color(0.28f, 0.72f, 0.38f) : new Color(0.4f, 0.55f, 0.45f);
                if (GUILayout.Button(new GUIContent(L.T("btn.fix_all") ?? "Fix All", L.T("tip.fix_all") ?? string.Empty), GUILayout.Height(36)))
                    Defer(RunFixAll);
                GUI.backgroundColor = prev;
                Caption("cap.fix_all");

                GUILayout.Space(6);
                showIndividualFixes = EditorGUILayout.Foldout(showIndividualFixes, L.T("fold.individual") ?? "Individual fixes", true);

                // Use Layout-time foldout snapshot so expand/collapse does not desync Layout vs input/Repaint.
                if (layoutShowIndividualFixes)
                {
                    EditorGUI.indentLevel++;
                    try
                    {
                        if (ActionButton("btn.fix_mats", "tip.fix_mats"))
                            Defer(() => WithUndo(() => VtoolAvatarFixes.FixMissingMaterials(targetAvatar, allowPlaceholder: false)));
                        if (ActionButton("btn.add_pipeline", "tip.add_pipeline"))
                            Defer(() => WithUndo(() => VtoolAvatarFixes.EnsurePipelineManager(targetAvatar)));
                        if (ActionButton("btn.fix_bounds", "tip.fix_bounds"))
                            Defer(() => WithUndo(() => VtoolAvatarFixes.FixMeshBounds(targetAvatar)));
                        if (ActionButton("btn.fix_audio", "tip.fix_audio"))
                            Defer(() => WithUndo(() => { int p; VtoolAvatarFixes.FixAudioSources(targetAvatar, out p); }));
                        if (ActionButton("btn.view_pos", "tip.view_pos"))
                            Defer(() => WithUndo(() => VtoolAvatarFixes.AlignViewPosition(targetAvatar, onlyIfUnset: true)));
                        if (ActionButton("btn.lip_sync", "tip.lip_sync"))
                            Defer(() => WithUndo(() => VtoolAvatarFixes.SetupLipSync(targetAvatar, onlyIfUnset: true)));

                        EditorGUILayout.Space(4);
                        GUILayout.Label(L.T("label.optional") ?? string.Empty, CaptionStyle());

                        string pbLabel = scan.PhysBoneCount > 256
                            ? (L.TF("btn.reduce_pb_n", scan.PhysBoneCount) ?? "Reduce PhysBones")
                            : (L.T("btn.reduce_pb") ?? "Reduce PhysBones");
                        var prevPb = GUI.backgroundColor;
                        if (scan.PhysBoneCount > 256)
                            GUI.backgroundColor = new Color(0.85f, 0.35f, 0.3f);
                        if (GUILayout.Button(new GUIContent(pbLabel, L.T("tip.reduce_pb") ?? string.Empty)))
                            Defer(RunReducePhysBones);
                        GUI.backgroundColor = prevPb;
                        Caption("cap.reduce_pb");

                        if (ActionButton("btn.remove_missing", "tip.remove_missing"))
                            Defer(RunRemoveMissingScripts);
                        if (ActionButton("btn.placeholder_mats", "tip.placeholder_mats"))
                            Defer(RunPlaceholderMaterials);
                        if (ActionButton("btn.disable_others", "tip.disable_others"))
                            Defer(RunDisableOtherAvatars);
                        if (ActionButton("btn.clear_blueprint", "tip.clear_blueprint"))
                            Defer(RunClearBlueprintId);
                    }
                    finally
                    {
                        EditorGUI.indentLevel--;
                    }
                }
            });
        }

        private void DrawTexturesTab(AvatarScanResult scan)
        {
            DrawSection(L.T("sec.tex_size"), () =>
            {
                Stat(L.T("stat.textures"), scan.TextureCount.ToString());
                Stat(L.T("stat.4k"), scan.Textures4K.ToString(), scan.Textures4K > 0 ? errStyle : okStyle);
                Stat(L.T("stat.over2k"), scan.TexturesOver2K.ToString(), scan.TexturesOver2K > 0 ? warnStyle : okStyle);
                Stat(L.T("stat.mem_short"), $"~{scan.TextureMemoryMB:F0} MB", scan.TextureMemoryMB > 100 ? warnStyle : null);

                GUILayout.Space(6);
                textureCapSize = EditorGUILayout.IntPopup(L.T("field.cap_to") ?? "Cap to", textureCapSize,
                    new[] { "512", "1024", L.T("cap.vrchat_max") ?? "2048" }, new[] { 512, 1024, 2048 });

                var prev = GUI.backgroundColor;
                GUI.backgroundColor = Accent;
                if (GUILayout.Button(new GUIContent(L.TF("btn.reduce_tex", textureCapSize) ?? "Reduce", L.T("tip.reduce_tex") ?? string.Empty), GUILayout.Height(32)))
                {
                    int cap = textureCapSize;
                    Defer(() =>
                    {
                        if (!EditorUtility.DisplayDialog(L.T("dlg.tex.reduce_title"),
                            L.TF("dlg.tex.reduce_body", cap),
                            L.T("dlg.reduce"), L.T("dlg.cancel")))
                            return;

                        var textures = VtoolAvatarFixes.CollectTextures(targetAvatar);
                        VtoolAvatarRollback.EnsureCapture(targetAvatar);
                        VtoolAvatarRollback.RecordTextures(targetAvatar, textures);
                        int n = VtoolAvatarFixes.CapTextureSizes(targetAvatar, cap);
                        EditorUtility.DisplayDialog(L.T("dlg.done"), L.TF("dlg.tex.reduce_done", n), L.T("dlg.ok"));
                    });
                }
                GUI.backgroundColor = prev;
                Caption("cap.reduce_tex");

                if (GUILayout.Button(new GUIContent(L.T("btn.restore_tex") ?? "Restore", L.T("tip.restore_tex") ?? string.Empty), GUILayout.Height(26)))
                {
                    Defer(() =>
                    {
                        if (!EditorUtility.DisplayDialog(L.T("dlg.tex.restore_title"),
                            L.T("dlg.tex.restore_body"),
                            L.T("dlg.restore"), L.T("dlg.cancel")))
                            return;

                        var textures = VtoolAvatarFixes.CollectTextures(targetAvatar);
                        VtoolAvatarRollback.EnsureCapture(targetAvatar);
                        VtoolAvatarRollback.RecordTextures(targetAvatar, textures);
                        int n = VtoolAvatarFixes.RestoreTextureSizes(targetAvatar);
                        EditorUtility.DisplayDialog(L.T("dlg.done"), L.TF("dlg.tex.restore_done", n), L.T("dlg.ok"));
                    });
                }

                if (GUILayout.Button(new GUIContent(L.T("btn.mipmaps") ?? "Mipmaps", L.T("tip.mipmaps") ?? string.Empty), GUILayout.Height(24)))
                {
                    Defer(() =>
                    {
                        if (!EditorUtility.DisplayDialog(L.T("dlg.tex.mip_title"),
                            L.T("dlg.tex.mip_body"),
                            L.T("dlg.enable"), L.T("dlg.cancel")))
                            return;
                        WithUndo(() => VtoolAvatarFixes.EnableTextureMipmaps(targetAvatar), trackTextures: true);
                    });
                }
            });

            DrawSection(L.T("sec.quest"), () =>
            {
                GUILayout.Label(L.T("quest.intro") ?? string.Empty, CaptionStyle());
                Stat(L.T("stat.non_quest"), scan.QuestBadShaders.ToString(), scan.QuestBadShaders > 0 ? warnStyle : okStyle);

                if (GUILayout.Button(new GUIContent(L.T("btn.quest_convert") ?? "Convert", L.T("tip.quest_convert") ?? string.Empty), GUILayout.Height(30)))
                {
                    Defer(() =>
                    {
                        if (!EditorUtility.DisplayDialog(L.T("dlg.quest.title"),
                            L.T("dlg.quest.body"),
                            L.T("dlg.convert"), L.T("dlg.cancel")))
                            return;

                        VtoolAvatarRollback.EnsureCapture(targetAvatar);
                        int n = VtoolAvatarFixes.ConvertToQuestShaders(targetAvatar, true);
                        EditorUtility.DisplayDialog(L.T("dlg.done"), L.TF("dlg.quest.done", n), L.T("dlg.ok"));
                    });
                }
                Caption("cap.quest_convert");
            });
        }

        #endregion

        #region Actions

        private static string ReadPackageVersion()
        {
            try
            {
                const string path = "Packages/com.vtool.autofixer/package.json";
                if (System.IO.File.Exists(path))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        System.IO.File.ReadAllText(path), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            catch { /* ignore */ }
            return "2.2.6";
        }

        private void CopyErrorCodes(AvatarScanResult scan)
        {
            if (scan.Issues == null)
                scan = VtoolAvatarScan.Scan(targetAvatar);

            string report = VtoolAvatarScan.BuildDiagnosticReport(targetAvatar, scan, ReadPackageVersion());
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log("[Vtool] Diagnostic report copied to clipboard.\n" + report);
            EditorUtility.DisplayDialog(
                L.T("dlg.copy_errors.title") ?? "Copied",
                L.T("dlg.copy_errors.body") ?? "Error codes and scan details were copied to the clipboard. Paste them when reporting a problem.",
                L.T("dlg.ok") ?? "OK");
        }

        private void RunFixAll()
        {
            if (!EditorUtility.DisplayDialog(L.T("dlg.fix_all.title"), L.T("dlg.fix_all.body"), L.T("dlg.fix"), L.T("dlg.cancel")))
                return;

            VtoolAvatarRollback.Capture(targetAvatar);
            var s = VtoolAvatarFixes.ApplyAllSafeFixes(targetAvatar);

            EditorUtility.DisplayDialog(L.T("dlg.fix_complete"),
                L.TF("dlg.fix_all.result",
                    s.MaterialSlots,
                    s.PipelineManager ? L.T("dlg.yes") : L.T("dlg.no"),
                    s.Bounds,
                    s.Audio,
                    s.AudioPlayOnAwake,
                    s.ViewPosition ? L.T("dlg.set") : L.T("dlg.skipped"),
                    s.LipSync ? L.T("dlg.set") : L.T("dlg.skipped")),
                L.T("dlg.ok"));
            Repaint();
        }

        private void RunReducePhysBones()
        {
            int count = VtoolAvatarScan.Scan(targetAvatar).PhysBoneCount;
            int excess = count - 256;
            if (excess <= 0)
            {
                EditorUtility.DisplayDialog(L.T("dlg.pb.title"), L.TF("dlg.pb.ok_body", count), L.T("dlg.ok"));
                return;
            }

            if (!EditorUtility.DisplayDialog(L.T("dlg.pb.reduce_title"),
                L.TF("dlg.pb.reduce_body", count, excess),
                L.T("dlg.reduce"), L.T("dlg.cancel")))
                return;

            WithUndo(() =>
            {
                int n = VtoolAvatarFixes.ReducePhysBoneComponents(targetAvatar, 256);
                int after = VtoolAvatarScan.Scan(targetAvatar).PhysBoneCount;
                string msg = L.TF("dlg.pb.done", n);
                if (after > 256)
                    msg += "\n\n" + L.TF("dlg.pb.head_kept", after);
                EditorUtility.DisplayDialog(L.T("dlg.done"), msg, L.T("dlg.ok"));
            });
        }

        private void RunRemoveMissingScripts()
        {
            if (!EditorUtility.DisplayDialog(L.T("dlg.missing.title"), L.T("dlg.missing.body"), L.T("dlg.remove"), L.T("dlg.cancel")))
                return;

            WithUndo(() =>
            {
                int n = VtoolAvatarFixes.RemoveMissingScripts(targetAvatar);
                EditorUtility.DisplayDialog(L.T("dlg.done"), L.TF("dlg.missing.done", n), L.T("dlg.ok"));
            });
        }

        private void RunPlaceholderMaterials()
        {
            if (!EditorUtility.DisplayDialog(L.T("dlg.placeholder.title"), L.T("dlg.placeholder.body"), L.T("dlg.continue"), L.T("dlg.cancel")))
                return;

            WithUndo(() =>
            {
                int n = VtoolAvatarFixes.FixMissingMaterials(targetAvatar, allowPlaceholder: true);
                EditorUtility.DisplayDialog(L.T("dlg.done"), L.TF("dlg.placeholder.done", n), L.T("dlg.ok"));
            });
        }

        private void RunDisableOtherAvatars()
        {
            if (!EditorUtility.DisplayDialog(L.T("dlg.disable.title"), L.T("dlg.disable.body"), L.T("dlg.disable"), L.T("dlg.cancel")))
                return;

            WithUndo(() =>
            {
                int n = VtoolAvatarFixes.DisableOtherAvatars(targetAvatar);
                EditorUtility.DisplayDialog(L.T("dlg.done"), L.TF("dlg.disable.done", n), L.T("dlg.ok"));
            });
        }

        private void RunClearBlueprintId()
        {
            if (!EditorUtility.DisplayDialog(L.T("dlg.blueprint.title"), L.T("dlg.blueprint.body"), L.T("dlg.clear"), L.T("dlg.cancel")))
                return;

            WithUndo(() =>
            {
                bool ok = VtoolAvatarFixes.ClearBlueprintId(targetAvatar);
                EditorUtility.DisplayDialog(L.T("dlg.done"),
                    ok ? L.T("dlg.blueprint.cleared") : L.T("dlg.blueprint.nothing"),
                    L.T("dlg.ok"));
            });
        }

        private void RunRollback()
        {
            if (targetAvatar == null || !VtoolAvatarRollback.HasRollback(targetAvatar)) return;

            if (!EditorUtility.DisplayDialog(L.T("dlg.rollback.title"), L.T("dlg.rollback.body"), L.T("dlg.rollback"), L.T("dlg.cancel")))
                return;

            targetAvatar = VtoolAvatarRollback.Restore(targetAvatar);
            EditorUtility.DisplayDialog(L.T("dlg.rollback.done_title"), L.T("dlg.rollback.done"), L.T("dlg.ok"));
            Repaint();
        }

        private void WithUndo(System.Action action, bool trackTextures = false)
        {
            VtoolAvatarRollback.EnsureCapture(targetAvatar);
            if (trackTextures)
                VtoolAvatarRollback.RecordTextures(targetAvatar, VtoolAvatarFixes.CollectTextures(targetAvatar));

            Undo.RegisterFullObjectHierarchyUndo(targetAvatar, "Vtool Fix");
            action();
            VtoolAvatarFixes.MarkDirty();
            Repaint();
        }

        private void BackupAvatar()
        {
            VtoolAvatarRollback.Capture(targetAvatar);
            var backup = Instantiate(targetAvatar);
            backup.name = targetAvatar.name + "_Backup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            backup.SetActive(false);
            Undo.RegisterCreatedObjectUndo(backup, "Backup");
            VtoolAvatarFixes.MarkDirty();
            EditorUtility.DisplayDialog(L.T("dlg.backup.title"), L.TF("dlg.backup.done", backup.name), L.T("dlg.ok"));
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

        // Short alias for localization calls in this window
        private static class L
        {
            public static VtoolLanguage Language
            {
                get => VtoolLocalization.Language;
                set => VtoolLocalization.Language = value;
            }

            public static string[] LanguageDisplayNames => VtoolLocalization.LanguageDisplayNames;
            public static string T(string key) => VtoolLocalization.T(key);
            public static string TF(string key, params object[] args) => VtoolLocalization.TF(key, args);
        }
    }
}
