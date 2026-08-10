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
            DrawRollbackBanner();

            if (targetAvatar == null)
            {
                EditorGUILayout.HelpBox(L.T("assign.avatar"), MessageType.Info);
                DrawSupportFooter();
                EditorGUILayout.EndScrollView();
                return;
            }

            GUILayout.Space(8);
            tabIndex = GUILayout.Toolbar(tabIndex, new[]
            {
                L.T("tab.check"),
                L.T("tab.fix"),
                L.T("tab.textures")
            }, GUILayout.Height(26));
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
            captionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = Muted } };
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
            GUILayout.Label(L.T("header.title"), headerStyle);
            GUILayout.Label(L.T("header.subtitle"), subStyle);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.Width(160));
            GUILayout.Label(L.T("lang.label"), EditorStyles.miniLabel);
            int lang = (int)L.Language;
            int next = EditorGUILayout.Popup(lang, L.LanguageDisplayNames);
            if (next != lang)
            {
                L.Language = (VtoolLanguage)next;
                Repaint();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            var line = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, Accent);
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField(L.T("header.safety"), EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawSupportFooter()
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("☕ " + L.T("support.coffee")), linkStyle))
                Application.OpenURL(SupportUrl);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawUpdateBanner()
        {
            if (!VtoolPackageUpdateHandler.HasPendingUpdate) return;
            EditorGUILayout.HelpBox(L.T("update.detected"), MessageType.Info);
            if (GUILayout.Button(L.T("btn.apply_update")))
                VtoolPackageUpdateHandler.CheckForPackageUpdate(silent: false, force: true);
        }

        private void DrawAvatarPicker()
        {
            EditorGUILayout.BeginVertical(panelStyle);
            targetAvatar = (GameObject)EditorGUILayout.ObjectField(L.T("field.avatar"), targetAvatar, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(L.T("btn.use_selected"), L.T("tip.use_selected")), GUILayout.Width(120)))
            {
                if (Selection.activeGameObject != null)
                    targetAvatar = Selection.activeGameObject;
            }
            if (GUILayout.Button(new GUIContent(L.T("btn.auto_detect"), L.T("tip.auto_detect")), GUILayout.Width(120)))
                AutoDetectAvatar();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawRollbackBanner()
        {
            if (targetAvatar == null || !VtoolAvatarRollback.HasRollback(targetAvatar)) return;

            EditorGUILayout.BeginVertical(panelStyle);
            EditorGUILayout.LabelField(L.T("rollback.banner"), EditorStyles.wordWrappedMiniLabel);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.85f, 0.45f, 0.2f);
            if (GUILayout.Button(new GUIContent(L.T("btn.rollback"), L.T("tip.rollback")), GUILayout.Height(30)))
                RunRollback();
            GUI.backgroundColor = prev;
            Caption("cap.rollback");
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
            GUILayout.Label(label, GUILayout.Width(150));
            GUILayout.Label(value, style ?? EditorStyles.label);
            EditorGUILayout.EndHorizontal();
        }

        private void Caption(string key)
        {
            EditorGUILayout.LabelField(L.T(key), captionStyle);
            GUILayout.Space(2);
        }

        private bool ActionButton(string labelKey, string tipKey, string capKey = null, float height = 0f)
        {
            var content = new GUIContent(L.T(labelKey), L.T(tipKey));
            bool clicked = height > 0f
                ? GUILayout.Button(content, GUILayout.Height(height))
                : GUILayout.Button(content);

            if (!string.IsNullOrEmpty(capKey))
                Caption(capKey);

            return clicked;
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
            DrawSection(L.T("sec.status"), () =>
            {
                EditorGUILayout.HelpBox(scan.Summary,
                    scan.BlockerCount > 0 ? MessageType.Error : scan.WarningCount > 0 ? MessageType.Warning : MessageType.Info);
            });

            if (scan.BlockerCount > 0)
            {
                DrawSection(L.TF("sec.blockers", scan.BlockerCount), () =>
                {
                    foreach (var i in scan.Issues)
                        if (i.Severity == IssueSeverity.Blocker) IssueRow(i);
                });
            }

            if (scan.WarningCount > 0)
            {
                DrawSection(L.TF("sec.warnings", scan.WarningCount), () =>
                {
                    foreach (var i in scan.Issues)
                        if (i.Severity == IssueSeverity.Warning) IssueRow(i);
                });
            }

            if (scan.BlockerCount == 0 && scan.WarningCount == 0)
            {
                DrawSection(L.T("sec.result"), () => GUILayout.Label(L.T("result.all_ok"), okStyle));
            }

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
                EditorGUILayout.LabelField(L.T("fix.intro"), EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(6);

                if (ActionButton("btn.backup", "tip.backup", "cap.backup", 28f))
                    BackupAvatar();

                if (VtoolAvatarRollback.HasRollback(targetAvatar))
                {
                    var prevRollback = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.85f, 0.45f, 0.2f);
                    if (GUILayout.Button(new GUIContent(L.T("btn.rollback"), L.T("tip.rollback")), GUILayout.Height(28)))
                        RunRollback();
                    GUI.backgroundColor = prevRollback;
                    Caption("cap.rollback");
                }

                GUILayout.Space(4);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = scan.BlockerCount > 0 ? new Color(0.28f, 0.72f, 0.38f) : new Color(0.4f, 0.55f, 0.45f);
                if (GUILayout.Button(new GUIContent(L.T("btn.fix_all"), L.T("tip.fix_all")), GUILayout.Height(36)))
                    RunFixAll();
                GUI.backgroundColor = prev;
                Caption("cap.fix_all");

                GUILayout.Space(6);
                showIndividualFixes = EditorGUILayout.Foldout(showIndividualFixes, L.T("fold.individual"), true);
                if (showIndividualFixes)
                {
                    EditorGUI.indentLevel++;
                    if (ActionButton("btn.fix_mats", "tip.fix_mats"))
                        WithUndo(() => VtoolAvatarFixes.FixMissingMaterials(targetAvatar, allowPlaceholder: false));
                    if (ActionButton("btn.add_pipeline", "tip.add_pipeline"))
                        WithUndo(() => VtoolAvatarFixes.EnsurePipelineManager(targetAvatar));
                    if (ActionButton("btn.fix_bounds", "tip.fix_bounds"))
                        WithUndo(() => VtoolAvatarFixes.FixMeshBounds(targetAvatar));
                    if (ActionButton("btn.fix_audio", "tip.fix_audio"))
                        WithUndo(() => { int p; VtoolAvatarFixes.FixAudioSources(targetAvatar, out p); });
                    if (ActionButton("btn.view_pos", "tip.view_pos"))
                        WithUndo(() => VtoolAvatarFixes.AlignViewPosition(targetAvatar, onlyIfUnset: true));
                    if (ActionButton("btn.lip_sync", "tip.lip_sync"))
                        WithUndo(() => VtoolAvatarFixes.SetupLipSync(targetAvatar, onlyIfUnset: true));

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(L.T("label.optional"), EditorStyles.miniLabel);
                    if (scan.PhysBoneCount > 256)
                    {
                        var prevPb = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.85f, 0.35f, 0.3f);
                        if (GUILayout.Button(new GUIContent(L.TF("btn.reduce_pb_n", scan.PhysBoneCount), L.T("tip.reduce_pb"))))
                            RunReducePhysBones();
                        GUI.backgroundColor = prevPb;
                    }
                    else if (GUILayout.Button(new GUIContent(L.T("btn.reduce_pb"), L.T("tip.reduce_pb"))))
                        RunReducePhysBones();
                    Caption("cap.reduce_pb");

                    if (ActionButton("btn.remove_missing", "tip.remove_missing"))
                        RunRemoveMissingScripts();
                    if (ActionButton("btn.placeholder_mats", "tip.placeholder_mats"))
                        RunPlaceholderMaterials();
                    if (ActionButton("btn.disable_others", "tip.disable_others"))
                        RunDisableOtherAvatars();
                    if (ActionButton("btn.clear_blueprint", "tip.clear_blueprint"))
                        RunClearBlueprintId();
                    EditorGUI.indentLevel--;
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
                textureCapSize = EditorGUILayout.IntPopup(L.T("field.cap_to"), textureCapSize,
                    new[] { "512", "1024", L.T("cap.vrchat_max") }, new[] { 512, 1024, 2048 });

                var prev = GUI.backgroundColor;
                GUI.backgroundColor = Accent;
                if (GUILayout.Button(new GUIContent(L.TF("btn.reduce_tex", textureCapSize), L.T("tip.reduce_tex")), GUILayout.Height(32)))
                {
                    if (EditorUtility.DisplayDialog(L.T("dlg.tex.reduce_title"),
                        L.TF("dlg.tex.reduce_body", textureCapSize),
                        L.T("dlg.reduce"), L.T("dlg.cancel")))
                    {
                        var textures = VtoolAvatarFixes.CollectTextures(targetAvatar);
                        VtoolAvatarRollback.EnsureCapture(targetAvatar);
                        VtoolAvatarRollback.RecordTextures(targetAvatar, textures);
                        int n = VtoolAvatarFixes.CapTextureSizes(targetAvatar, textureCapSize);
                        EditorUtility.DisplayDialog(L.T("dlg.done"), L.TF("dlg.tex.reduce_done", n), L.T("dlg.ok"));
                        Repaint();
                    }
                }
                GUI.backgroundColor = prev;
                Caption("cap.reduce_tex");

                if (GUILayout.Button(new GUIContent(L.T("btn.restore_tex"), L.T("tip.restore_tex")), GUILayout.Height(26)))
                {
                    if (EditorUtility.DisplayDialog(L.T("dlg.tex.restore_title"),
                        L.T("dlg.tex.restore_body"),
                        L.T("dlg.restore"), L.T("dlg.cancel")))
                    {
                        var textures = VtoolAvatarFixes.CollectTextures(targetAvatar);
                        VtoolAvatarRollback.EnsureCapture(targetAvatar);
                        VtoolAvatarRollback.RecordTextures(targetAvatar, textures);
                        int n = VtoolAvatarFixes.RestoreTextureSizes(targetAvatar);
                        EditorUtility.DisplayDialog(L.T("dlg.done"), L.TF("dlg.tex.restore_done", n), L.T("dlg.ok"));
                        Repaint();
                    }
                }

                if (GUILayout.Button(new GUIContent(L.T("btn.mipmaps"), L.T("tip.mipmaps")), GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog(L.T("dlg.tex.mip_title"),
                        L.T("dlg.tex.mip_body"),
                        L.T("dlg.enable"), L.T("dlg.cancel")))
                        WithUndo(() => VtoolAvatarFixes.EnableTextureMipmaps(targetAvatar), trackTextures: true);
                }
            });

            DrawSection(L.T("sec.quest"), () =>
            {
                EditorGUILayout.LabelField(L.T("quest.intro"), EditorStyles.wordWrappedMiniLabel);
                Stat(L.T("stat.non_quest"), scan.QuestBadShaders.ToString(), scan.QuestBadShaders > 0 ? warnStyle : okStyle);

                if (GUILayout.Button(new GUIContent(L.T("btn.quest_convert"), L.T("tip.quest_convert")), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog(L.T("dlg.quest.title"),
                        L.T("dlg.quest.body"),
                        L.T("dlg.convert"), L.T("dlg.cancel")))
                    {
                        VtoolAvatarRollback.EnsureCapture(targetAvatar);
                        int n = VtoolAvatarFixes.ConvertToQuestShaders(targetAvatar, true);
                        EditorUtility.DisplayDialog(L.T("dlg.done"), L.TF("dlg.quest.done", n), L.T("dlg.ok"));
                        Repaint();
                    }
                }
                Caption("cap.quest_convert");
            });
        }

        #endregion

        #region Actions

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
