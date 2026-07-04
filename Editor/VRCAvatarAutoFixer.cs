using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace XVR.Tools
{
    public class VRCAvatarAutoFixer : EditorWindow
    {
        private GameObject targetAvatar;
        private Vector2 scrollPos;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle boxStyle;
        private GUIStyle warningStyle;
        private GUIStyle successStyle;
        private GUIStyle errorStyle;
        private int tabIndex;
        private readonly string[] tabs = { "Diagnostics", "Auto-Fixes", "Optimizations", "Quest/Android" };

        private static readonly string[] StandardVisemeSuffixes =
        {
            "sil", "pp", "ff", "th", "dd", "kk", "ch", "ss", "nn", "rr", "aa", "e", "ih", "oh", "ou"
        };

        private static readonly string[] QuestShaderNames =
        {
            "VRChat/Mobile/Toon Lit",
            "VRChat/Mobile/Standard Lite",
            "VRChat/Mobile/Diffuse"
        };

        [MenuItem("Vtool/Avatar Auto-Fixer Pro")]
        public static void ShowWindow()
        {
            var window = GetWindow<VRCAvatarAutoFixer>("VRC Auto-Fixer");
            window.minSize = new Vector2(480, 720);
            window.Show();
        }

        private void OnEnable()
        {
            AutoDetectAvatar();
        }

        private void OnSelectionChange()
        {
            if (targetAvatar == null && Selection.activeGameObject != null)
                Repaint();
        }

        private void AutoDetectAvatar()
        {
            if (targetAvatar != null) return;

            if (Selection.activeGameObject != null && HasVRCDescriptor(Selection.activeGameObject))
            {
                targetAvatar = Selection.activeGameObject;
                return;
            }

            var descriptorType = GetVRCDescriptorType();
            if (descriptorType == null) return;

            var descriptors = FindObjectsCompat(descriptorType);
            if (descriptors.Length > 0)
                targetAvatar = ((Component)descriptors[0]).gameObject;
        }

        private void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    margin = new RectOffset(10, 10, 10, 10)
                };
            }

            if (subHeaderStyle == null)
            {
                subHeaderStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.65f, 0.65f, 0.65f) }
                };
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(15, 15, 15, 15),
                    margin = new RectOffset(10, 10, 10, 10)
                };
            }

            if (warningStyle == null)
                warningStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.6f, 0f) }, fontStyle = FontStyle.Bold };

            if (successStyle == null)
                successStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.2f, 0.8f, 0.2f) }, fontStyle = FontStyle.Bold };

            if (errorStyle == null)
                errorStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.9f, 0.2f, 0.2f) }, fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            InitStyles();

            GUILayout.Label("VRChat Avatar Auto-Fixer Pro", headerStyle);
            GUILayout.Label("Diagnose, fix, and optimize avatars for VRChat upload", subHeaderStyle);

            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.BeginHorizontal();
            var newTarget = (GameObject)EditorGUILayout.ObjectField("Avatar Root", targetAvatar, typeof(GameObject), true);
            if (newTarget != targetAvatar)
            {
                targetAvatar = newTarget;
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(64)))
                Repaint();

            if (targetAvatar == null && Selection.activeGameObject != null)
            {
                if (GUILayout.Button("Use Selected", GUILayout.Width(100)))
                    targetAvatar = Selection.activeGameObject;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (targetAvatar == null)
            {
                EditorGUILayout.HelpBox("Select your avatar root GameObject to unlock features.", MessageType.Info);
                if (GUILayout.Button("Auto-Detect Avatar in Scene", GUILayout.Height(30)))
                {
                    AutoDetectAvatar();
                    if (targetAvatar == null)
                        Debug.LogWarning("[Vtool] No avatar with a VRCAvatarDescriptor was found in the active scene.");
                }
                return;
            }

            GUILayout.Space(5);
            tabIndex = GUILayout.Toolbar(tabIndex, tabs, GUILayout.Height(30));
            GUILayout.Space(5);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (tabIndex)
            {
                case 0: DrawDiagnosticsTab(); break;
                case 1: DrawAutoFixesTab(); break;
                case 2: DrawOptimizationsTab(); break;
                case 3: DrawQuestTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        #region UI Tabs

        private void DrawDiagnosticsTab()
        {
            var stats = GatherDiagnostics();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Overall Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(stats.OverallSummary, stats.CriticalIssues > 0 ? MessageType.Error : (stats.WarningIssues > 0 ? MessageType.Warning : MessageType.Info));
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Performance Metrics", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawStat("Polygons (Triangles):", stats.PolyCount.ToString("N0"), GetPolyStyle(stats.PolyCount));
            DrawStat("Skinned Meshes:", stats.SkinnedMeshCount.ToString(), stats.SkinnedMeshCount > 16 ? errorStyle : (stats.SkinnedMeshCount > 8 ? warningStyle : successStyle));
            DrawStat("Material Slots:", stats.MaterialSlotCount.ToString(), stats.MaterialSlotCount > 24 ? errorStyle : (stats.MaterialSlotCount > 16 ? warningStyle : successStyle));
            DrawStat("Unique Materials:", stats.UniqueMaterialCount.ToString(), stats.UniqueMaterialCount > 16 ? errorStyle : (stats.UniqueMaterialCount > 8 ? warningStyle : successStyle));
            DrawStat("Mesh Renderers:", stats.MeshRendererCount.ToString(), EditorStyles.label);
            DrawStat("Bones:", stats.BoneCount.ToString(), stats.BoneCount > 256 ? warningStyle : EditorStyles.label);

            EditorGUILayout.Space();
            if (stats.PolyCount > 100000)
                EditorGUILayout.HelpBox("Polygon count exceeds 100,000 (Very Poor on PC).", MessageType.Error);
            else if (stats.PolyCount > 70000)
                EditorGUILayout.HelpBox("Polygon count exceeds 70,000 (Poor on PC).", MessageType.Warning);
            else if (stats.PolyCount > 32000)
                EditorGUILayout.HelpBox("Polygon count is in the Medium range for PC.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Polygon count is Excellent/Good for PC.", MessageType.Info);

            if (stats.MaterialSlotCount > 16)
                EditorGUILayout.HelpBox("High material slot count. Consider atlasing or merging materials.", MessageType.Warning);

            if (stats.LargeTextureCount > 0)
                EditorGUILayout.HelpBox($"Detected {stats.LargeTextureCount} unique texture(s) larger than 2K. This increases VRAM usage.", MessageType.Warning);

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("VRChat Components", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawStat("VRC Avatar Descriptor:", stats.HasDescriptor ? "Present" : "Missing", stats.HasDescriptor ? successStyle : errorStyle);
            DrawStat("Humanoid Animator:", stats.HasHumanoidAnimator ? "Yes" : "No", stats.HasHumanoidAnimator ? successStyle : errorStyle);
            DrawStat("Animator Controller:", stats.HasAnimatorController ? "Assigned" : "Missing", stats.HasAnimatorController ? successStyle : errorStyle);
            DrawStat("View Position:", stats.HasViewPosition ? "Set" : "Not verified", stats.HasViewPosition ? successStyle : warningStyle);
            DrawStat("Lip Sync (Visemes):", stats.LipSyncConfigured ? "Configured" : "Not configured", stats.LipSyncConfigured ? successStyle : warningStyle);
            DrawStat("PhysBones:", stats.PhysBoneCount.ToString(), stats.PhysBoneCount > 256 ? warningStyle : EditorStyles.label);
            DrawStat("Contacts:", stats.ContactCount.ToString(), EditorStyles.label);
            DrawStat("Particle Systems:", stats.ParticleSystemCount.ToString(), stats.ParticleSystemCount > 16 ? warningStyle : EditorStyles.label);
            DrawStat("Audio Sources:", stats.AudioSourceCount.ToString(), EditorStyles.label);
            DrawStat("Missing Scripts:", stats.MissingScriptCount.ToString(), stats.MissingScriptCount > 0 ? errorStyle : successStyle);
            DrawStat("Non-Unit Scales:", stats.NonUnitScaleCount.ToString(), stats.NonUnitScaleCount > 0 ? warningStyle : successStyle);

            if (stats.LegacyDynamicBoneCount > 0)
                EditorGUILayout.HelpBox($"Found {stats.LegacyDynamicBoneCount} legacy Dynamic Bone component(s). Migrate to PhysBones for better performance.", MessageType.Warning);

            if (!stats.HasDescriptor)
                EditorGUILayout.HelpBox("No VRCAvatarDescriptor on the avatar root. Add one from the VRChat SDK.", MessageType.Error);

            if (stats.HasAnimator && !stats.HasAnimatorController)
            {
                EditorGUILayout.HelpBox("Animator has no controller assigned. This often causes a T-Pose in VRChat.", MessageType.Error);
                if (GUILayout.Button("Create & Assign Dummy Controller"))
                    CreateDummyController(targetAvatar.GetComponent<Animator>());
            }
            else if (!stats.HasAnimator)
            {
                EditorGUILayout.HelpBox("No Animator on the avatar root. VRChat requires a humanoid Animator.", MessageType.Error);
            }

            if (stats.BadAudioCount > 0)
                EditorGUILayout.HelpBox($"{stats.BadAudioCount} AudioSource(s) have high volume or are not fully 3D spatialized.", MessageType.Warning);

            if (stats.MissingScriptCount > 0)
                EditorGUILayout.HelpBox("Missing script references can block uploads. Use Remove Missing Scripts in Auto-Fixes.", MessageType.Error);

            if (stats.NonUnitScaleCount > 0)
                EditorGUILayout.HelpBox("Non-unit local scales can cause IK and upload issues. Consider Normalize Scale.", MessageType.Warning);

            EditorGUILayout.EndVertical();

            if (stats.QuestIncompatibleMaterials > 0)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                GUILayout.Label("Quest / Android", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox($"{stats.QuestIncompatibleMaterials} material(s) are not using a Quest-compatible mobile shader. See the Quest/Android tab.", MessageType.Warning);
                EditorGUILayout.EndVertical();
            }
        }

        private GUIStyle GetPolyStyle(int polyCount)
        {
            if (polyCount > 100000) return errorStyle;
            if (polyCount > 70000) return errorStyle;
            if (polyCount > 32000) return warningStyle;
            return successStyle;
        }

        private void DrawStat(string label, string value, GUIStyle style)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(160));
            GUILayout.Label(value, style);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAutoFixesTab()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Safety First", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Always back up your avatar before applying destructive fixes.", MessageType.Info);
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            if (GUILayout.Button("Backup Avatar", GUILayout.Height(30)))
                BackupAvatar();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("1-Click Master Fix", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Runs all safe, essential fixes: missing scripts, materials, bounds, audio, mesh import settings, scale, view position, and lip sync.", MessageType.Info);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("RUN ALL MASTER FIXES", GUILayout.Height(40)))
                RunAllFixes();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Individual Fixes", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Remove Missing Scripts", "Removes broken script references from all GameObjects.")))
                RemoveMissingScripts();
            if (GUILayout.Button(new GUIContent("Clean Missing Materials", "Removes null/missing materials from renderer slots.")))
                CleanMissingMaterials();
            if (GUILayout.Button(new GUIContent("Fix Skinned Mesh Bounds", "Expands skinned mesh bounds to reduce in-world culling issues.")))
                FixMeshBounds();
            if (GUILayout.Button(new GUIContent("Fix Audio Sources (Spatial Blend)", "Forces all audio sources to be 3D spatialized and caps volume.")))
                FixAudioSources();
            if (GUILayout.Button(new GUIContent("Fix Mesh Read/Write", "Enables Read/Write on meshes required for some VRC features.")))
                FixMeshReadWrite();
            if (GUILayout.Button(new GUIContent("Normalize Root Scale to (1,1,1)", "Sets root scale to 1 to prevent IK issues.")))
                NormalizeScale();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("VRChat Specific Auto-Setup", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Auto-Align View Position (Eyes)", "Positions the VRC Viewpoint between the avatar's eyes.")))
                AutoAlignViewPosition();
            if (GUILayout.Button(new GUIContent("Auto-Setup Lip Sync (Visemes)", "Finds vrc.v_* blendshapes and configures viseme lip sync.")))
                AutoSetupLipSync();
            if (GUILayout.Button(new GUIContent("Clear Blueprint ID (Detach)", "Clears the Blueprint ID so you can upload as a new avatar.")))
                ClearBlueprintID();
            EditorGUILayout.EndVertical();
        }

        private void DrawOptimizationsTab()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Prefab Utilities", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Unpacking a prefab completely disconnects it from the original file. This is often required before making deep structural changes.", MessageType.Info);
            if (GUILayout.Button("Unpack Prefab Completely", GUILayout.Height(30)))
                UnpackPrefab();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Hierarchy Cleanup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Removes empty GameObjects that are NOT bones. Back up first if you use constraints targeting empty objects.", MessageType.Warning);

            if (GUILayout.Button("Remove Unused Empty GameObjects", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Remove Empty GameObjects",
                    "This will permanently remove empty objects that are not part of the avatar rig. Continue?",
                    "Remove", "Cancel"))
                {
                    CleanupEmptyGameObjects();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawQuestTab()
        {
            var stats = GatherDiagnostics();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Quest / Android Conversion", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("VRChat on Quest/Android requires mobile shaders. This converts avatar materials to 'VRChat/Mobile/Toon Lit'.", MessageType.Warning);

            if (stats.QuestIncompatibleMaterials == 0)
                EditorGUILayout.HelpBox("All materials already use Quest-compatible shaders.", MessageType.Info);
            else
                EditorGUILayout.HelpBox($"{stats.QuestIncompatibleMaterials} material(s) need conversion.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Duplicate materials before converting", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Recommended: duplicate materials so your PC shaders are preserved in the project.", MessageType.Info);

            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            if (GUILayout.Button("Convert Materials to Quest Compatible", GUILayout.Height(40)))
                ConvertToQuest();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Diagnostics

        private struct AvatarDiagnostics
        {
            public int PolyCount;
            public int SkinnedMeshCount;
            public int MeshRendererCount;
            public int MaterialSlotCount;
            public int UniqueMaterialCount;
            public int LargeTextureCount;
            public int BoneCount;
            public int PhysBoneCount;
            public int ContactCount;
            public int ParticleSystemCount;
            public int AudioSourceCount;
            public int BadAudioCount;
            public int MissingScriptCount;
            public int NonUnitScaleCount;
            public int LegacyDynamicBoneCount;
            public int QuestIncompatibleMaterials;
            public int CriticalIssues;
            public int WarningIssues;
            public bool HasDescriptor;
            public bool HasAnimator;
            public bool HasHumanoidAnimator;
            public bool HasAnimatorController;
            public bool HasViewPosition;
            public bool LipSyncConfigured;
            public string OverallSummary;
        }

        private AvatarDiagnostics GatherDiagnostics()
        {
            var stats = new AvatarDiagnostics();
            if (targetAvatar == null) return stats;

            var countedMeshes = new HashSet<Mesh>();
            var uniqueMaterials = new HashSet<Material>();
            var checkedTextures = new HashSet<Texture>();
            var questShader = FindQuestShader();

            var smrs = targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var mfs = targetAvatar.GetComponentsInChildren<MeshFilter>(true);
            var renderers = targetAvatar.GetComponentsInChildren<Renderer>(true);
            var audioSources = targetAvatar.GetComponentsInChildren<AudioSource>(true);
            var transforms = targetAvatar.GetComponentsInChildren<Transform>(true);

            stats.SkinnedMeshCount = smrs.Length;
            stats.MeshRendererCount = targetAvatar.GetComponentsInChildren<MeshRenderer>(true).Length;

            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                if (countedMeshes.Add(smr.sharedMesh))
                    stats.PolyCount += smr.sharedMesh.triangles.Length / 3;
            }

            foreach (var mf in mfs)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (countedMeshes.Add(mf.sharedMesh))
                    stats.PolyCount += mf.sharedMesh.triangles.Length / 3;
            }

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                stats.MaterialSlotCount += mats.Length;
                foreach (var mat in mats)
                {
                    if (mat == null) continue;
                    uniqueMaterials.Add(mat);
                    if (questShader != null && mat.shader != questShader)
                        stats.QuestIncompatibleMaterials++;

                    var mainTex = mat.mainTexture;
                    if (mainTex != null && checkedTextures.Add(mainTex))
                    {
                        if (mainTex.width > 2048 || mainTex.height > 2048)
                            stats.LargeTextureCount++;
                    }
                }
            }

            stats.UniqueMaterialCount = uniqueMaterials.Count;
            stats.AudioSourceCount = audioSources.Length;

            foreach (var a in audioSources)
            {
                if (a == null) continue;
                if (a.volume > 0.8f || a.spatialBlend < 1f)
                    stats.BadAudioCount++;
            }

            foreach (var t in transforms)
            {
                if (t == null) continue;
                stats.MissingScriptCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (t.localScale != Vector3.one)
                    stats.NonUnitScaleCount++;
            }

            stats.BoneCount = CountBones();
            stats.PhysBoneCount = CountComponentsOfType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
            stats.ContactCount = CountComponentsOfType("VRC.SDK3.Dynamics.Contact.Components.ContactBase");
            stats.ParticleSystemCount = targetAvatar.GetComponentsInChildren<ParticleSystem>(true).Length;
            stats.LegacyDynamicBoneCount = CountComponentsOfType("DynamicBone");

            var descriptorType = GetVRCDescriptorType();
            stats.HasDescriptor = descriptorType != null && targetAvatar.GetComponent(descriptorType) != null;

            var anim = targetAvatar.GetComponent<Animator>();
            stats.HasAnimator = anim != null;
            stats.HasHumanoidAnimator = anim != null && anim.isHuman;
            stats.HasAnimatorController = anim != null && anim.runtimeAnimatorController != null;

            if (stats.HasDescriptor && descriptorType != null)
            {
                var descriptor = targetAvatar.GetComponent(descriptorType);
                if (TryGetMember(descriptor, descriptorType, "ViewPosition", out var viewPos) && viewPos is Vector3 v && v != Vector3.zero)
                    stats.HasViewPosition = true;

                if (TryGetMember(descriptor, descriptorType, "VisemeSkinnedMesh", out var visemeMesh) && visemeMesh != null)
                    stats.LipSyncConfigured = true;
            }

            stats.CriticalIssues = 0;
            stats.WarningIssues = 0;

            if (!stats.HasDescriptor) stats.CriticalIssues++;
            if (!stats.HasHumanoidAnimator) stats.CriticalIssues++;
            if (!stats.HasAnimatorController) stats.CriticalIssues++;
            if (stats.MissingScriptCount > 0) stats.CriticalIssues++;
            if (stats.PolyCount > 100000) stats.CriticalIssues++;

            if (stats.PolyCount > 70000) stats.WarningIssues++;
            if (stats.MaterialSlotCount > 16) stats.WarningIssues++;
            if (stats.BadAudioCount > 0) stats.WarningIssues++;
            if (stats.NonUnitScaleCount > 0) stats.WarningIssues++;
            if (stats.LegacyDynamicBoneCount > 0) stats.WarningIssues++;
            if (stats.QuestIncompatibleMaterials > 0) stats.WarningIssues++;
            if (!stats.LipSyncConfigured) stats.WarningIssues++;

            if (stats.CriticalIssues > 0)
                stats.OverallSummary = $"{stats.CriticalIssues} critical issue(s) and {stats.WarningIssues} warning(s) detected. Fix these before uploading.";
            else if (stats.WarningIssues > 0)
                stats.OverallSummary = $"No critical blockers, but {stats.WarningIssues} warning(s) should be reviewed.";
            else
                stats.OverallSummary = "Avatar looks healthy. Run a build test in VRChat SDK before uploading.";

            return stats;
        }

        private int CountBones()
        {
            var bones = new HashSet<Transform>();
            foreach (var smr in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                if (smr.bones != null)
                {
                    foreach (var bone in smr.bones)
                        if (bone != null) bones.Add(bone);
                }
                if (smr.rootBone != null) bones.Add(smr.rootBone);
            }
            return bones.Count;
        }

        private int CountComponentsOfType(string typeName)
        {
            var type = GetTypeSafe(typeName);
            if (type == null) return 0;
            return targetAvatar.GetComponentsInChildren(type, true).Length;
        }

        #endregion

        #region Logic Implementation

        private void BackupAvatar()
        {
            if (targetAvatar == null) return;

            GameObject backup = Instantiate(targetAvatar);
            backup.name = targetAvatar.name + "_Backup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            backup.SetActive(false);
            Undo.RegisterCreatedObjectUndo(backup, "Backup Avatar");
            MarkSceneDirty();
            Debug.Log($"[Vtool] Created backup: {backup.name}");
            EditorUtility.DisplayDialog("Backup Created", $"A backup of your avatar has been created and hidden in the scene:\n\n{backup.name}", "OK");
        }

        private void RunAllFixes()
        {
            if (!EditorUtility.DisplayDialog("Run Master Fixes",
                "This will apply all safe auto-fixes to your avatar. A backup is recommended first. Continue?",
                "Run Fixes", "Cancel"))
                return;

            Undo.RegisterFullObjectHierarchyUndo(targetAvatar, "Run All Auto-Fixes");
            int scripts = RemoveMissingScripts();
            int materials = CleanMissingMaterials();
            int bounds = FixMeshBounds();
            int audio = FixAudioSources();
            int meshes = FixMeshReadWrite();
            NormalizeScale();
            bool viewAligned = AutoAlignViewPosition(silent: true);
            bool lipSync = AutoSetupLipSync(silent: true);
            MarkSceneDirty();

            string details = $"Removed {scripts} missing script(s)\n" +
                             $"Cleaned {materials} material slot(s)\n" +
                             $"Fixed bounds on {bounds} skinned mesh(es)\n" +
                             $"Fixed {audio} audio source(s)\n" +
                             $"Enabled Read/Write on {meshes} mesh(es)\n" +
                             $"View position: {(viewAligned ? "aligned" : "skipped")}\n" +
                             $"Lip sync: {(lipSync ? "configured" : "skipped")}";

            EditorUtility.DisplayDialog("Auto-Fix Complete", "Master fixes have been applied.\n\n" + details, "OK");
        }

        private void CreateDummyController(Animator anim)
        {
            if (anim == null) return;

            string folder = "Assets/Vtool";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets"))
                    return;
                AssetDatabase.CreateFolder("Assets", "Vtool");
            }

            string path = folder + "/DummyController.controller";
            UnityEditor.Animations.AnimatorController controller;

            if (!System.IO.File.Exists(path))
                controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            else
                controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);

            Undo.RecordObject(anim, "Assign Dummy Controller");
            anim.runtimeAnimatorController = controller;
            MarkSceneDirty();
            Debug.Log("[Vtool] Created and assigned Dummy Animator Controller.");
        }

        private void UnpackPrefab()
        {
            if (PrefabUtility.IsPartOfAnyPrefab(targetAvatar))
            {
                Undo.RegisterFullObjectHierarchyUndo(targetAvatar, "Unpack Prefab");
                PrefabUtility.UnpackPrefabInstance(targetAvatar, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                MarkSceneDirty();
                Debug.Log("[Vtool] Prefab unpacked completely.");
            }
            else
            {
                EditorUtility.DisplayDialog("Not a Prefab", "The target avatar is not a prefab instance.", "OK");
            }
        }

        private int RemoveMissingScripts()
        {
            int count = 0;
            var allObjects = targetAvatar.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).ToArray();
            foreach (var go in allObjects)
            {
                if (go != null)
                    count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }

            if (count > 0) MarkSceneDirty();
            Debug.Log($"[Vtool] Removed {count} missing scripts.");
            return count;
        }

        private int CleanMissingMaterials()
        {
            var renderers = targetAvatar.GetComponentsInChildren<Renderer>(true);
            int cleanedSlots = 0;

            Undo.RecordObjects(renderers, "Clean Missing Materials");
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool needsUpdate = false;
                var newMats = new List<Material>();

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                        newMats.Add(mats[i]);
                    else
                    {
                        needsUpdate = true;
                        cleanedSlots++;
                    }
                }

                if (needsUpdate)
                    r.sharedMaterials = newMats.ToArray();
            }

            if (cleanedSlots > 0) MarkSceneDirty();
            Debug.Log($"[Vtool] Cleaned up {cleanedSlots} missing/null material slots.");
            return cleanedSlots;
        }

        private int FixMeshBounds()
        {
            var smrs = targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Undo.RecordObjects(smrs, "Fix Bounds");

            int fixedCount = 0;
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;

                var bounds = smr.sharedMesh.bounds;
                bounds.Expand(Mathf.Max(bounds.size.magnitude * 0.15f, 0.1f));
                smr.localBounds = bounds;
                fixedCount++;
            }

            if (fixedCount > 0) MarkSceneDirty();
            Debug.Log($"[Vtool] Fixed bounds for {fixedCount} SkinnedMeshRenderer(s).");
            return fixedCount;
        }

        private int FixAudioSources()
        {
            var audioSources = targetAvatar.GetComponentsInChildren<AudioSource>(true);
            Undo.RecordObjects(audioSources, "Fix Audio");

            int fixedCount = 0;
            foreach (var audio in audioSources)
            {
                if (audio == null) continue;
                bool changed = false;
                if (audio.spatialBlend < 1f)
                {
                    audio.spatialBlend = 1f;
                    changed = true;
                }
                if (audio.volume > 0.8f)
                {
                    audio.volume = 0.8f;
                    changed = true;
                }
                if (changed) fixedCount++;
            }

            if (fixedCount > 0) MarkSceneDirty();
            Debug.Log($"[Vtool] Fixed {fixedCount} AudioSource(s) (forced 3D and capped volume).");
            return fixedCount;
        }

        private int FixMeshReadWrite()
        {
            var meshes = new HashSet<Mesh>();
            foreach (var smr in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null && smr.sharedMesh != null) meshes.Add(smr.sharedMesh);
            foreach (var mf in targetAvatar.GetComponentsInChildren<MeshFilter>(true))
                if (mf != null && mf.sharedMesh != null) meshes.Add(mf.sharedMesh);

            int count = 0;
            foreach (var mesh in meshes)
            {
                if (mesh.isReadable) continue;

                string path = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(path)) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || importer.isReadable) continue;

                importer.isReadable = true;
                importer.SaveAndReimport();
                count++;
            }

            Debug.Log($"[Vtool] Enabled Read/Write on {count} mesh(es).");
            return count;
        }

        private void NormalizeScale()
        {
            if (targetAvatar.transform.localScale == Vector3.one)
            {
                Debug.Log("[Vtool] Root scale is already (1,1,1).");
                return;
            }

            Undo.RecordObject(targetAvatar.transform, "Normalize Scale");
            targetAvatar.transform.localScale = Vector3.one;
            MarkSceneDirty();
            Debug.Log("[Vtool] Normalized root scale to (1,1,1).");
        }

        private bool AutoAlignViewPosition(bool silent = false)
        {
            var animator = targetAvatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                if (!silent) Debug.LogWarning("[Vtool] Avatar is not Humanoid. Cannot auto-align View Position.");
                return false;
            }

            Transform leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            Transform rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);

            if (leftEye == null || rightEye == null)
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null)
                {
                    if (!silent) Debug.LogWarning("[Vtool] Eye/Head bones not found in Humanoid Rig.");
                    return false;
                }

                Vector3 headLocal = targetAvatar.transform.InverseTransformPoint(head.position);
                headLocal.y += 0.06f;
                headLocal.z += 0.08f;
                return ApplyViewPosition(headLocal, silent);
            }

            Vector3 worldCenter = (leftEye.position + rightEye.position) * 0.5f;
            Vector3 localPos = targetAvatar.transform.InverseTransformPoint(worldCenter);
            localPos.z += 0.015f;
            return ApplyViewPosition(localPos, silent);
        }

        private bool ApplyViewPosition(Vector3 localPos, bool silent)
        {
            var descriptorType = GetVRCDescriptorType();
            if (descriptorType == null) return false;

            var descriptor = targetAvatar.GetComponent(descriptorType);
            if (descriptor == null) return false;

            Undo.RecordObject(descriptor, "Align View Position");
            if (!TrySetMember(descriptor, descriptorType, "ViewPosition", localPos))
            {
                if (!silent) Debug.LogWarning("[Vtool] Could not set ViewPosition on VRCAvatarDescriptor.");
                return false;
            }

            EditorUtility.SetDirty(descriptor);
            MarkSceneDirty();
            if (!silent) Debug.Log("[Vtool] Successfully aligned View Position.");
            return true;
        }

        private bool AutoSetupLipSync(bool silent = false)
        {
            var descriptorType = GetVRCDescriptorType();
            if (descriptorType == null) return false;

            var descriptor = targetAvatar.GetComponent(descriptorType);
            if (descriptor == null) return false;

            SkinnedMeshRenderer bodyMesh = null;
            foreach (var smr in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;

                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    string shapeName = smr.sharedMesh.GetBlendShapeName(i).ToLowerInvariant();
                    if (shapeName.Contains("vrc.v_aa") || shapeName.Contains("vrc.v_sil"))
                    {
                        bodyMesh = smr;
                        break;
                    }
                }
                if (bodyMesh != null) break;
            }

            if (bodyMesh == null)
            {
                if (!silent) Debug.LogWarning("[Vtool] Could not find a mesh with standard 'vrc.v_*' viseme blendshapes.");
                return false;
            }

            var visemeNames = new string[StandardVisemeSuffixes.Length];
            int mapped = 0;
            for (int i = 0; i < StandardVisemeSuffixes.Length; i++)
                visemeNames[i] = FindVisemeBlendShapeName(bodyMesh.sharedMesh, StandardVisemeSuffixes[i], ref mapped);

            if (mapped == 0)
            {
                if (!silent) Debug.LogWarning("[Vtool] Found a candidate mesh but could not map any viseme blendshapes.");
                return false;
            }

            Undo.RecordObject(descriptor, "Setup Lip Sync");
            TrySetMember(descriptor, descriptorType, "VisemeSkinnedMesh", bodyMesh);
            TrySetMember(descriptor, descriptorType, "VisemeBlendShapes", visemeNames);
            TrySetEnumMember(descriptor, descriptorType, "lipSync", "VisemeBlendShape");

            EditorUtility.SetDirty(descriptor);
            MarkSceneDirty();
            if (!silent) Debug.Log($"[Vtool] Configured lip sync on '{bodyMesh.name}' with {mapped} viseme blendshape(s).");
            return true;
        }

        private static string FindVisemeBlendShapeName(Mesh mesh, string suffix, ref int mappedCount)
        {
            string[] prefixes = { "vrc.v_", "VRC.v_", "vrc.V_", "VRC.V_" };
            foreach (var prefix in prefixes)
            {
                string candidate = prefix + suffix;
                if (mesh.GetBlendShapeIndex(candidate) >= 0)
                {
                    mappedCount++;
                    return candidate;
                }
            }

            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string shapeName = mesh.GetBlendShapeName(i);
                if (shapeName.ToLowerInvariant().EndsWith(suffix))
                {
                    mappedCount++;
                    return shapeName;
                }
            }

            return string.Empty;
        }

        private void ClearBlueprintID()
        {
            if (!EditorUtility.DisplayDialog("Clear Blueprint ID",
                "This detaches the avatar from its current VRChat blueprint so it uploads as a new avatar. Continue?",
                "Clear ID", "Cancel"))
                return;

            var pipelineType = GetTypeSafe("VRC.Core.PipelineManager");
            if (pipelineType == null)
            {
                EditorUtility.DisplayDialog("Not Found", "PipelineManager was not found. Is the VRChat SDK installed?", "OK");
                return;
            }

            var pipeline = targetAvatar.GetComponent(pipelineType);
            if (pipeline == null)
            {
                EditorUtility.DisplayDialog("Not Found", "No PipelineManager component on this avatar.", "OK");
                return;
            }

            Undo.RecordObject(pipeline, "Clear Blueprint ID");
            if (TrySetMember(pipeline, pipelineType, "blueprintId", string.Empty))
            {
                EditorUtility.SetDirty(pipeline);
                MarkSceneDirty();
                Debug.Log("[Vtool] Cleared Blueprint ID.");
                EditorUtility.DisplayDialog("Blueprint Cleared", "Blueprint ID has been cleared. The next upload will create a new avatar.", "OK");
            }
        }

        private void CleanupEmptyGameObjects()
        {
            Undo.RegisterFullObjectHierarchyUndo(targetAvatar, "Cleanup Empty GameObjects");

            var protectedTransforms = new HashSet<Transform>();
            foreach (var smr in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                protectedTransforms.Add(smr.transform);
                if (smr.bones != null)
                {
                    foreach (var bone in smr.bones)
                        if (bone != null) protectedTransforms.Add(bone);
                }
                if (smr.rootBone != null) protectedTransforms.Add(smr.rootBone);
            }

            var anim = targetAvatar.GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (bone == HumanBodyBones.LastBone) continue;
                    var t = anim.GetBoneTransform(bone);
                    if (t != null) protectedTransforms.Add(t);
                }
            }

            var transformsToProcess = targetAvatar.GetComponentsInChildren<Transform>(true)
                .Where(t => t != null && t != targetAvatar.transform && !protectedTransforms.Contains(t))
                .ToList();

            int removed = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = transformsToProcess.Count - 1; i >= 0; i--)
                {
                    var t = transformsToProcess[i];
                    if (t == null)
                    {
                        transformsToProcess.RemoveAt(i);
                        continue;
                    }

                    if (t.childCount == 0 && t.gameObject.GetComponents<Component>().Length == 1)
                    {
                        Undo.DestroyObjectImmediate(t.gameObject);
                        removed++;
                        changed = true;
                        transformsToProcess.RemoveAt(i);
                    }
                }
            }

            MarkSceneDirty();
            Debug.Log($"[Vtool] Removed {removed} unused empty GameObject(s).");
            EditorUtility.DisplayDialog("Cleanup Complete", $"Removed {removed} unused empty GameObject(s).", "OK");
        }

        private void ConvertToQuest()
        {
            Shader mobileShader = FindQuestShader();
            if (mobileShader == null)
            {
                EditorUtility.DisplayDialog("Shader Missing",
                    "Could not find a Quest-compatible VRChat mobile shader. Is the VRChat SDK installed?",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Convert to Quest",
                "This changes avatar materials to use Quest-compatible shaders.\n\nDuplicate materials is recommended so PC versions are preserved.",
                "Convert", "Cancel"))
                return;

            bool duplicate = EditorUtility.DisplayDialog("Duplicate Materials?",
                "Create duplicated Quest material assets under Assets/Vtool/QuestMaterials before converting?",
                "Duplicate First", "Convert In Place");

            var renderers = targetAvatar.GetComponentsInChildren<Renderer>(true);
            int convertedCount = 0;
            var processedMaterials = new Dictionary<Material, Material>();

            Undo.RecordObjects(renderers, "Convert to Quest");
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool rendererChanged = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null || mat.shader == mobileShader) continue;

                    Material targetMat = mat;
                    if (duplicate)
                    {
                        if (!processedMaterials.TryGetValue(mat, out targetMat))
                        {
                            targetMat = DuplicateQuestMaterial(mat, mobileShader);
                            processedMaterials[mat] = targetMat;
                        }
                    }
                    else
                    {
                        Undo.RecordObject(mat, "Change Shader to Quest");
                        mat.shader = mobileShader;
                        EditorUtility.SetDirty(mat);
                        targetMat = mat;
                    }

                    mats[i] = targetMat;
                    rendererChanged = true;
                    convertedCount++;
                }

                if (rendererChanged)
                    r.sharedMaterials = mats;
            }

            AssetDatabase.SaveAssets();
            MarkSceneDirty();
            Debug.Log($"[Vtool] Converted {convertedCount} material slot(s) to Quest-compatible shaders.");
            EditorUtility.DisplayDialog("Quest Conversion",
                $"Converted {convertedCount} material slot(s) to '{mobileShader.name}'.",
                "OK");
        }

        private static Material DuplicateQuestMaterial(Material source, Shader mobileShader)
        {
            string folder = "Assets/Vtool/QuestMaterials";
            if (!AssetDatabase.IsValidFolder("Assets/Vtool"))
                AssetDatabase.CreateFolder("Assets", "Vtool");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Vtool", "QuestMaterials");

            string safeName = string.IsNullOrEmpty(source.name) ? "Material" : source.name.Replace("/", "_");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}_Quest.mat");

            var duplicate = new Material(source);
            duplicate.shader = mobileShader;
            AssetDatabase.CreateAsset(duplicate, path);
            return duplicate;
        }

        #endregion

        #region Helpers

        private static bool HasVRCDescriptor(GameObject go)
        {
            var descriptorType = GetVRCDescriptorType();
            return descriptorType != null && go.GetComponent(descriptorType) != null;
        }

        private static Shader FindQuestShader()
        {
            foreach (var shaderName in QuestShaderNames)
            {
                var shader = Shader.Find(shaderName);
                if (shader != null) return shader;
            }
            return null;
        }

        private static void MarkSceneDirty()
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

        private static System.Type GetVRCDescriptorType()
        {
            return GetTypeSafe("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
        }

        private static System.Type GetTypeSafe(string typeName)
        {
            var type = System.Type.GetType(typeName);
            if (type != null) return type;

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }

        private static bool TryGetMember(object target, System.Type type, string memberName, out object value)
        {
            value = null;
            if (target == null || type == null) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                value = field.GetValue(target);
                return true;
            }

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.CanRead)
            {
                value = property.GetValue(target);
                return true;
            }

            return false;
        }

        private static bool TrySetMember(object target, System.Type type, string memberName, object value)
        {
            if (target == null || type == null) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                field.SetValue(target, value);
                return true;
            }

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return true;
            }

            return false;
        }

        private static bool TrySetEnumMember(object target, System.Type type, string memberName, string enumName)
        {
            if (target == null || type == null) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(memberName, flags);
            if (field == null || !field.FieldType.IsEnum) return false;

            try
            {
                var enumValue = System.Enum.Parse(field.FieldType, enumName);
                field.SetValue(target, enumValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
