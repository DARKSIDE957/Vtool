using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace XVR.Tools
{
    public class VRCAvatarAutoFixer : EditorWindow
    {
        private const string FallbackVersion = "2.0.1";

        private GameObject targetAvatar;
        private Vector2 scrollPos;
        private int textureCapSize = 2048;
        private bool showIndividualFixes;
        private Material placeholderMaterial;

        private GUIStyle headerStyle;
        private GUIStyle subStyle;
        private GUIStyle versionStyle;
        private GUIStyle panelStyle;
        private GUIStyle okStyle;
        private GUIStyle warnStyle;
        private GUIStyle errStyle;
        private bool stylesReady;
        private static string cachedVersion;

        private static readonly string[] VisemeSuffixes =
        {
            "sil", "pp", "ff", "th", "dd", "kk", "ch", "ss", "nn", "rr", "aa", "e", "ih", "oh", "ou"
        };

        private static readonly string[] TextureCapOptions = { "512", "1024", "2048 (VRChat max)" };

        [MenuItem("Vtool/Avatar Auto-Fixer Pro")]
        public static void ShowWindow()
        {
            var w = GetWindow<VRCAvatarAutoFixer>("Vtool Pre-Upload");
            w.minSize = new Vector2(440, 680);
            w.Show();
        }

        private void OnEnable() => AutoDetectAvatar();
        private void OnSelectionChange() { if (targetAvatar == null) Repaint(); }

        #region UI

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

            var report = BuildUploadReport();

            DrawUploadStatus(report);
            DrawFixSection(report);
            DrawTextureSection(report);

            EditorGUILayout.EndScrollView();
        }

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 19, margin = new RectOffset(4, 4, 2, 0) };
            subStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.68f, 0.68f, 0.68f) } };
            versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
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
            GUILayout.Label("Fix upload errors  •  Reduce texture size", subStyle);
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
            if (!VtoolPackageUpdateHandler.HasPendingUpdate)
                return;

            EditorGUILayout.HelpBox(
                $"Vtool v{VtoolPackageUpdateHandler.PendingVersion} is installing — Unity is reloading scripts…",
                MessageType.Info);

            if (GUILayout.Button("Apply Update Now (Reload Scripts)", GUILayout.Height(26)))
                VtoolPackageUpdateHandler.CheckForPackageUpdate(silent: false, force: true);
        }

        private void DrawDisclaimer()
        {
            EditorGUILayout.HelpBox(
                "DISCLAIMER: Back up your avatar first. DARKSIDE957 is NOT responsible if this tool breaks your avatar or causes upload failures. Use at your own risk.",
                MessageType.Warning);
        }

        private void DrawAvatarPicker()
        {
            EditorGUILayout.BeginVertical(panelStyle);
            EditorGUILayout.BeginHorizontal();
            targetAvatar = (GameObject)EditorGUILayout.ObjectField("Avatar Root", targetAvatar, typeof(GameObject), true);
            if (targetAvatar == null && Selection.activeGameObject != null && GUILayout.Button("Use Selected", GUILayout.Width(96)))
                targetAvatar = Selection.activeGameObject;
            EditorGUILayout.EndHorizontal();

            if (targetAvatar == null && GUILayout.Button("Auto-Detect Avatar in Scene", GUILayout.Height(28)))
            {
                AutoDetectAvatar();
                if (targetAvatar == null)
                    Debug.LogWarning("[Vtool] No VRCAvatarDescriptor found in scene.");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawUploadStatus(UploadReport report)
        {
            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("Pre-Upload Check", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (report.Blockers.Count == 0)
            {
                EditorGUILayout.HelpBox("No upload blockers found. Review warnings below, then upload via VRChat SDK.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"{report.Blockers.Count} blocker(s) must be fixed before upload.", MessageType.Error);
            }

            foreach (var issue in report.Blockers)
                DrawIssueRow(issue, true);
            foreach (var issue in report.Warnings)
                DrawIssueRow(issue, false);

            if (report.Blockers.Count == 0 && report.Warnings.Count == 0)
                GUILayout.Label("✓  All checks passed", okStyle);

            EditorGUILayout.Space(6);
            DrawStat("Polygons", report.PolyCount.ToString("N0"), report.PolyCount > 70000 ? warnStyle : okStyle);
            DrawStat("Material slots", report.MaterialSlots.ToString(), report.MaterialSlots > 16 ? warnStyle : EditorStyles.label);
            DrawStat("Textures", $"{report.TextureCount}  ({report.TexturesOver2K} over 2K)", report.TexturesOver2K > 0 ? warnStyle : okStyle);
            DrawStat("Est. texture memory", $"~{report.TextureMemoryMB:F0} MB", report.TextureMemoryMB > 100 ? warnStyle : EditorStyles.label);

            EditorGUILayout.EndVertical();
        }

        private void DrawIssueRow(UploadIssue issue, bool isBlocker)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(isBlocker ? "✗" : "!", isBlocker ? errStyle : warnStyle, GUILayout.Width(14));
            GUILayout.Label(issue.Message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStat(string label, string value, GUIStyle valueStyle)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130));
            GUILayout.Label(value, valueStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFixSection(UploadReport report)
        {
            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("Fix Upload Errors", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These fixes do not change textures or how your avatar looks (hair-safe material fix).", MessageType.None);

            GUI.backgroundColor = new Color(0.18f, 0.55f, 0.88f);
            if (GUILayout.Button("Backup Avatar", GUILayout.Height(30)))
                BackupAvatar();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            GUI.backgroundColor = report.Blockers.Count > 0 ? new Color(0.2f, 0.78f, 0.28f) : new Color(0.35f, 0.65f, 0.4f);
            if (GUILayout.Button("FIX ALL UPLOAD ERRORS", GUILayout.Height(42)))
                FixAllUploadErrors();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            showIndividualFixes = EditorGUILayout.Foldout(showIndividualFixes, "Individual fixes", true);
            if (showIndividualFixes)
            {
                EditorGUI.indentLevel++;
                if (GUILayout.Button("Remove missing scripts")) { UndoAvatar(); RemoveMissingScripts(); Done(); }
                if (GUILayout.Button("Fix missing material slots")) { UndoAvatar(); CleanMissingMaterials(); Done(); }
                if (GUILayout.Button("Assign dummy animator controller")) CreateDummyController(targetAvatar.GetComponent<Animator>());
                if (GUILayout.Button("Fix skinned mesh bounds")) { UndoAvatar(); FixMeshBounds(); Done(); }
                if (GUILayout.Button("Fix audio (3D + volume)")) { UndoAvatar(); FixAudioSources(); Done(); }
                if (GUILayout.Button("Align view position")) { UndoAvatar(); AutoAlignViewPosition(); Done(); }
                if (GUILayout.Button("Setup lip sync")) { UndoAvatar(); AutoSetupLipSync(); Done(); }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTextureSection(UploadReport report)
        {
            EditorGUILayout.BeginVertical(panelStyle);
            GUILayout.Label("Reduce Texture Size", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Lowers Unity import Max Size only — your original image files are NOT deleted. " +
                "Use Restore to put sizes back to the source file resolution.",
                MessageType.Info);

            DrawStat("Textures on avatar", report.TextureCount.ToString(), EditorStyles.label);
            DrawStat("4K textures", report.Textures4K.ToString(), report.Textures4K > 0 ? errStyle : okStyle);
            DrawStat("Over 2K", report.TexturesOver2K.ToString(), report.TexturesOver2K > 0 ? warnStyle : okStyle);
            DrawStat("Memory (estimate)", $"~{report.TextureMemoryMB:F0} MB", report.TextureMemoryMB > 100 ? warnStyle : EditorStyles.label);

            EditorGUILayout.Space(6);
            textureCapSize = EditorGUILayout.IntPopup("Cap import size to", textureCapSize, TextureCapOptions, new[] { 512, 1024, 2048 });

            GUI.backgroundColor = new Color(0.72f, 0.14f, 0.2f);
            if (GUILayout.Button($"REDUCE TEXTURES TO {textureCapSize}px", GUILayout.Height(38)))
                CapTextures(textureCapSize);
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Restore Original Texture Sizes", GUILayout.Height(30)))
                RestoreTextures();

            if (GUILayout.Button("Enable Mipmaps (performance)", GUILayout.Height(26)))
            {
                UndoAvatar();
                int n = FixTextureMipmaps();
                Done($"Enabled mipmaps on {n} texture(s).");
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Upload report

        private struct UploadIssue
        {
            public string Message;
        }

        private struct UploadReport
        {
            public List<UploadIssue> Blockers;
            public List<UploadIssue> Warnings;
            public int PolyCount;
            public int MaterialSlots;
            public int TextureCount;
            public int Textures4K;
            public int TexturesOver2K;
            public float TextureMemoryMB;
        }

        private UploadReport BuildUploadReport()
        {
            var report = new UploadReport
            {
                Blockers = new List<UploadIssue>(),
                Warnings = new List<UploadIssue>()
            };

            if (targetAvatar == null) return report;

            var meshes = new HashSet<Mesh>();
            var textures = CollectAvatarTextures();
            int nullMats = 0, brokenShaders = 0, missingScripts = 0;

            foreach (var smr in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null && smr.sharedMesh != null && meshes.Add(smr.sharedMesh))
                    report.PolyCount += smr.sharedMesh.triangles.Length / 3;

            foreach (var mf in targetAvatar.GetComponentsInChildren<MeshFilter>(true))
                if (mf != null && mf.sharedMesh != null && meshes.Add(mf.sharedMesh))
                    report.PolyCount += mf.sharedMesh.triangles.Length / 3;

            foreach (var r in targetAvatar.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                report.MaterialSlots += mats.Length;
                foreach (var m in mats)
                {
                    if (m == null) { nullMats++; continue; }
                    if (IsBrokenShader(m.shader)) brokenShaders++;
                }
            }

            foreach (var t in targetAvatar.GetComponentsInChildren<Transform>(true))
                if (t != null)
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);

            report.TextureCount = textures.Count;
            long mem = 0;
            foreach (var tex in textures)
            {
                if (tex == null) continue;
                int dim = Mathf.Max(tex.width, tex.height);
                if (dim >= 4096) report.Textures4K++;
                if (dim > 2048) report.TexturesOver2K++;
                mem += EstimateTextureBytes(tex);
            }
            report.TextureMemoryMB = mem / (1024f * 1024f);

            var descType = GetVRCDescriptorType();
            bool hasDesc = descType != null && targetAvatar.GetComponent(descType) != null;
            var anim = targetAvatar.GetComponent<Animator>();

            if (!hasDesc)
                report.Blockers.Add(new UploadIssue { Message = "Missing VRCAvatarDescriptor on avatar root" });
            if (anim == null || !anim.isHuman)
                report.Blockers.Add(new UploadIssue { Message = "Missing humanoid Animator on avatar root" });
            else if (anim.runtimeAnimatorController == null)
                report.Blockers.Add(new UploadIssue { Message = "Animator has no controller (causes T-Pose)" });
            if (missingScripts > 0)
                report.Blockers.Add(new UploadIssue { Message = $"{missingScripts} missing script reference(s)" });
            if (nullMats > 0)
                report.Blockers.Add(new UploadIssue { Message = $"{nullMats} null material slot(s)" });
            if (brokenShaders > 0)
                report.Blockers.Add(new UploadIssue { Message = $"{brokenShaders} broken/missing shader(s) — fix manually" });

            if (report.PolyCount > 100000)
                report.Warnings.Add(new UploadIssue { Message = $"Very high polygon count ({report.PolyCount:N0})" });
            else if (report.PolyCount > 70000)
                report.Warnings.Add(new UploadIssue { Message = $"High polygon count ({report.PolyCount:N0})" });

            if (report.MaterialSlots > 16)
                report.Warnings.Add(new UploadIssue { Message = $"High material slot count ({report.MaterialSlots})" });
            if (report.Textures4K > 0)
                report.Warnings.Add(new UploadIssue { Message = $"{report.Textures4K} texture(s) are 4K+ — reduce before upload" });
            if (report.TexturesOver2K > 0)
                report.Warnings.Add(new UploadIssue { Message = $"{report.TexturesOver2K} texture(s) over 2K — VRChat recommends 2K max" });
            if (report.TextureMemoryMB > 150)
                report.Warnings.Add(new UploadIssue { Message = $"High texture memory (~{report.TextureMemoryMB:F0} MB) — risk of security check failure" });

            if (CountOtherAvatarsInScene() > 0)
                report.Warnings.Add(new UploadIssue { Message = "Other avatars active in scene — disable before upload" });

            int badAudio = 0;
            foreach (var a in targetAvatar.GetComponentsInChildren<AudioSource>(true))
                if (a != null && (a.volume > 0.8f || a.spatialBlend < 1f)) badAudio++;
            if (badAudio > 0)
                report.Warnings.Add(new UploadIssue { Message = $"{badAudio} audio source(s) need 3D spatialization" });

            return report;
        }

        #endregion

        #region Fix all

        private void FixAllUploadErrors()
        {
            if (!EditorUtility.DisplayDialog("Fix Upload Errors",
                "Applies all safe pre-upload fixes.\n\nBack up your avatar first. Continue?",
                "Fix", "Cancel"))
                return;

            UndoAvatar();
            int scripts = RemoveMissingScripts();
            int mats = CleanMissingMaterials();
            var anim = targetAvatar.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController == null)
                CreateDummyController(anim);
            int bounds = FixMeshBounds();
            int audio = FixAudioSources();
            bool view = AutoAlignViewPosition(silent: true);
            bool lip = AutoSetupLipSync(silent: true);
            MarkDirty();

            EditorUtility.DisplayDialog("Done",
                $"Missing scripts removed: {scripts}\n" +
                $"Material slots fixed: {mats}\n" +
                $"Bounds fixed: {bounds}\n" +
                $"Audio fixed: {audio}\n" +
                $"View position: {(view ? "OK" : "skipped")}\n" +
                $"Lip sync: {(lip ? "OK" : "skipped")}\n\n" +
                "Re-check the list above. Fix broken shaders manually.",
                "OK");
            Repaint();
        }

        private void UndoAvatar() => Undo.RegisterFullObjectHierarchyUndo(targetAvatar, "Vtool Fix");
        private void Done(string msg = null)
        {
            MarkDirty();
            if (!string.IsNullOrEmpty(msg))
                EditorUtility.DisplayDialog("Vtool", msg, "OK");
            Repaint();
        }

        private void BackupAvatar()
        {
            var backup = Instantiate(targetAvatar);
            backup.name = targetAvatar.name + "_Backup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            backup.SetActive(false);
            Undo.RegisterCreatedObjectUndo(backup, "Backup Avatar");
            MarkDirty();
            EditorUtility.DisplayDialog("Backup", $"Created hidden backup:\n{backup.name}", "OK");
        }

        #endregion

        #region Upload fixes

        private int RemoveMissingScripts()
        {
            int n = 0;
            foreach (var go in targetAvatar.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject))
                if (go != null) n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            return n;
        }

        private int CleanMissingMaterials()
        {
            int fixedSlots = 0;
            var renderers = targetAvatar.GetComponentsInChildren<Renderer>(true);
            Undo.RecordObjects(renderers, "Fix Materials");

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats.Length == 0) continue;

                int subCount = GetSubMeshCount(r);
                var newMats = (Material[])mats.Clone();
                bool changed = false;

                for (int i = 0; i < newMats.Length; i++)
                {
                    if (newMats[i] != null) continue;
                    var fb = FindFallbackMaterial(newMats, i) ?? GetPlaceholderMaterial();
                    if (fb == null) continue;
                    newMats[i] = fb;
                    fixedSlots++;
                    changed = true;
                }

                if (subCount > 0 && newMats.Length < subCount)
                {
                    var expanded = new Material[subCount];
                    for (int i = 0; i < subCount; i++)
                        expanded[i] = i < newMats.Length && newMats[i] != null
                            ? newMats[i]
                            : FindFallbackMaterial(newMats, i) ?? GetPlaceholderMaterial();
                    newMats = expanded;
                    changed = true;
                }

                if (changed) r.sharedMaterials = newMats;
            }
            return fixedSlots;
        }

        private void CreateDummyController(Animator anim)
        {
            if (anim == null) return;
            EnsureFolder("Assets/Vtool");
            string path = "Assets/Vtool/DummyController.controller";
            var ctrl = File.Exists(path)
                ? AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path)
                : UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            Undo.RecordObject(anim, "Assign Controller");
            anim.runtimeAnimatorController = ctrl;
            MarkDirty();
        }

        private int FixMeshBounds()
        {
            var smrs = targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Undo.RecordObjects(smrs, "Fix Bounds");
            int n = 0;
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                var b = smr.sharedMesh.bounds;
                b.Expand(Mathf.Max(b.size.magnitude * 0.15f, 0.1f));
                smr.localBounds = b;
                n++;
            }
            return n;
        }

        private int FixAudioSources()
        {
            var sources = targetAvatar.GetComponentsInChildren<AudioSource>(true);
            Undo.RecordObjects(sources, "Fix Audio");
            int n = 0;
            foreach (var a in sources)
            {
                if (a == null) continue;
                bool c = false;
                if (a.spatialBlend < 1f) { a.spatialBlend = 1f; c = true; }
                if (a.volume > 0.8f) { a.volume = 0.8f; c = true; }
                if (c) n++;
            }
            return n;
        }

        private bool AutoAlignViewPosition(bool silent = false)
        {
            var anim = targetAvatar.GetComponent<Animator>();
            if (anim == null || !anim.isHuman) return false;

            var le = anim.GetBoneTransform(HumanBodyBones.LeftEye);
            var re = anim.GetBoneTransform(HumanBodyBones.RightEye);
            Vector3 local;

            if (le != null && re != null)
            {
                local = targetAvatar.transform.InverseTransformPoint((le.position + re.position) * 0.5f);
                local.z += 0.015f;
            }
            else
            {
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head == null) return false;
                local = targetAvatar.transform.InverseTransformPoint(head.position);
                local.y += 0.06f;
                local.z += 0.08f;
            }

            var descType = GetVRCDescriptorType();
            var desc = descType != null ? targetAvatar.GetComponent(descType) : null;
            if (desc == null) return false;

            Undo.RecordObject(desc, "View Position");
            if (!TrySetMember(desc, descType, "ViewPosition", local)) return false;
            EditorUtility.SetDirty(desc);
            return true;
        }

        private bool AutoSetupLipSync(bool silent = false)
        {
            var descType = GetVRCDescriptorType();
            var desc = descType != null ? targetAvatar.GetComponent(descType) : null;
            if (desc == null) return false;

            SkinnedMeshRenderer face = null;
            foreach (var smr in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null) continue;
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    var nm = smr.sharedMesh.GetBlendShapeName(i).ToLowerInvariant();
                    if (nm.Contains("vrc.v_aa") || nm.Contains("vrc.v_sil")) { face = smr; break; }
                }
                if (face != null) break;
            }
            if (face == null) return false;

            var names = new string[VisemeSuffixes.Length];
            int mapped = 0;
            for (int i = 0; i < VisemeSuffixes.Length; i++)
                names[i] = MapViseme(face.sharedMesh, VisemeSuffixes[i], ref mapped);
            if (mapped == 0) return false;

            Undo.RecordObject(desc, "Lip Sync");
            TrySetMember(desc, descType, "VisemeSkinnedMesh", face);
            TrySetMember(desc, descType, "VisemeBlendShapes", names);
            TrySetEnumMember(desc, descType, "lipSync", "VisemeBlendShape");
            EditorUtility.SetDirty(desc);
            return true;
        }

        private static string MapViseme(Mesh mesh, string suffix, ref int mapped)
        {
            foreach (var p in new[] { "vrc.v_", "VRC.v_" })
            {
                string c = p + suffix;
                if (mesh.GetBlendShapeIndex(c) >= 0) { mapped++; return c; }
            }
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                var n = mesh.GetBlendShapeName(i);
                if (n.ToLowerInvariant().EndsWith(suffix)) { mapped++; return n; }
            }
            return "";
        }

        #endregion

        #region Textures

        private void CapTextures(int maxSize)
        {
            if (!EditorUtility.DisplayDialog("Reduce Textures",
                $"Set Max Size to {maxSize}px on avatar textures.\n\nOriginal files are kept. Continue?",
                "Reduce", "Cancel"))
                return;

            int n = 0;
            foreach (var tex in CollectAvatarTextures())
            {
                string path = AssetDatabase.GetAssetPath(tex);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null || imp.maxTextureSize <= maxSize) continue;
                imp.maxTextureSize = maxSize;
                imp.SaveAndReimport();
                n++;
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Textures Reduced", $"Updated {n} texture(s) to {maxSize}px import size.\n\nUse Restore to undo.", "OK");
            Repaint();
        }

        private void RestoreTextures()
        {
            if (!EditorUtility.DisplayDialog("Restore Textures",
                "Restore import Max Size to each texture's original source resolution?",
                "Restore", "Cancel"))
                return;

            int n = 0;
            foreach (var tex in CollectAvatarTextures())
            {
                string path = AssetDatabase.GetAssetPath(tex);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                int target = Mathf.Clamp(Mathf.Max(w, h), 32, 8192);
                if (imp.maxTextureSize == target) continue;
                imp.maxTextureSize = target;
                imp.SaveAndReimport();
                n++;
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Restored", $"Restored {n} texture(s) to source resolution.", "OK");
            Repaint();
        }

        private int FixTextureMipmaps()
        {
            int n = 0;
            foreach (var tex in CollectAvatarTextures())
            {
                var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
                if (imp == null || imp.mipmapEnabled) continue;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
                n++;
            }
            if (n > 0) AssetDatabase.SaveAssets();
            return n;
        }

        private HashSet<Texture> CollectAvatarTextures()
        {
            var set = new HashSet<Texture>();
            foreach (var r in targetAvatar.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null) continue;
                    for (int i = 0; i < ShaderUtil.GetPropertyCount(m.shader); i++)
                    {
                        if (ShaderUtil.GetPropertyType(m.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv) continue;
                        var t = m.GetTexture(ShaderUtil.GetPropertyName(m.shader, i));
                        if (t != null) set.Add(t);
                    }
                }
            }
            return set;
        }

        private static long EstimateTextureBytes(Texture tex)
        {
            return (long)(Mathf.Max(tex.width, 1) * Mathf.Max(tex.height, 1) * 4 * 1.33f);
        }

        #endregion

        #region Helpers

        private void AutoDetectAvatar()
        {
            if (targetAvatar != null) return;
            if (Selection.activeGameObject != null && HasDescriptor(Selection.activeGameObject))
            { targetAvatar = Selection.activeGameObject; return; }

            var type = GetVRCDescriptorType();
            if (type == null) return;
            var found = FindObjectsCompat(type);
            if (found.Length > 0) targetAvatar = ((Component)found[0]).gameObject;
        }

        private int CountOtherAvatarsInScene()
        {
            var type = GetVRCDescriptorType();
            if (type == null) return 0;
            int n = 0;
            foreach (var o in FindObjectsCompat(type))
            {
                if (o == null) continue;
                var go = ((Component)o).gameObject;
                if (go != targetAvatar && go.activeInHierarchy) n++;
            }
            return n;
        }

        private static int GetSubMeshCount(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) return smr.sharedMesh.subMeshCount;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : 0;
        }

        private static Material FindFallbackMaterial(Material[] mats, int idx)
        {
            for (int i = idx - 1; i >= 0; i--) if (mats[i] != null) return mats[i];
            for (int i = idx + 1; i < mats.Length; i++) if (mats[i] != null) return mats[i];
            return null;
        }

        private Material GetPlaceholderMaterial()
        {
            if (placeholderMaterial != null) return placeholderMaterial;
            EnsureFolder("Assets/Vtool");
            string path = "Assets/Vtool/MissingMaterialPlaceholder.mat";
            placeholderMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (placeholderMaterial != null) return placeholderMaterial;
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            placeholderMaterial = new Material(shader) { name = "MissingMaterialPlaceholder" };
            AssetDatabase.CreateAsset(placeholderMaterial, path);
            AssetDatabase.SaveAssets();
            return placeholderMaterial;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (!AssetDatabase.IsValidFolder("Assets")) return;
            AssetDatabase.CreateFolder("Assets", path.Replace("Assets/", ""));
        }

        private static bool HasDescriptor(GameObject go) =>
            GetVRCDescriptorType() != null && go.GetComponent(GetVRCDescriptorType()) != null;

        private static bool IsBrokenShader(Shader s) =>
            s == null || s.name.Contains("InternalErrorShader");

        private static void MarkDirty()
        {
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static Object[] FindObjectsCompat(System.Type type)
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType(type, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType(type);
#endif
        }

        private static System.Type GetVRCDescriptorType() =>
            GetTypeSafe("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");

        private static System.Type GetTypeSafe(string name)
        {
            var t = System.Type.GetType(name);
            if (t != null) return t;
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(name);
                if (t != null) return t;
            }
            return null;
        }

        private static bool TrySetMember(object obj, System.Type type, string name, object value)
        {
            const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(name, f);
            if (field != null) { field.SetValue(obj, value); return true; }
            var prop = type.GetProperty(name, f);
            if (prop != null && prop.CanWrite) { prop.SetValue(obj, value); return true; }
            return false;
        }

        private static bool TrySetEnumMember(object obj, System.Type type, string name, string enumName)
        {
            const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(name, f);
            if (field == null || !field.FieldType.IsEnum) return false;
            try { field.SetValue(obj, System.Enum.Parse(field.FieldType, enumName)); return true; }
            catch { return false; }
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
