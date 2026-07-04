using UnityEngine;
using UnityEditor;
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
        private GUIStyle boxStyle;
        private int tabIndex = 0;
        private readonly string[] tabs = { "Diagnostics", "Auto-Fixes", "Optimizations", "Quest/Android" };

        private void OnEnable()
        {
            AutoDetectAvatar();
        }

        private void AutoDetectAvatar()
        {
            if (targetAvatar != null) return;
            System.Type descriptorType = GetVRCDescriptorType();
            if (descriptorType != null)
            {
                var descriptors = FindObjectsOfType(descriptorType);
                if (descriptors != null && descriptors.Length > 0)
                {
                    targetAvatar = ((Component)descriptors[0]).gameObject;
                }
            }
        }

        [MenuItem("VRChat/Avatar Auto-Fixer Pro")]
        public static void ShowWindow()
        {
            var window = GetWindow<VRCAvatarAutoFixer>("VRC Auto-Fixer");
            window.minSize = new Vector2(420, 650);
            window.Show();
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
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(10, 10, 10, 10)
                };
            }
        }

        private void OnGUI()
        {
            InitStyles();

            GUILayout.Label("VRChat Avatar Auto-Fixer Pro", headerStyle);
            
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.BeginHorizontal();
            targetAvatar = (GameObject)EditorGUILayout.ObjectField("Avatar Root", targetAvatar, typeof(GameObject), true);
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
                    if (targetAvatar == null) Debug.LogWarning("[Auto-Fixer] No avatar with a VRCAvatarDescriptor was found in the active scene.");
                }
                return;
            }

            tabIndex = GUILayout.Toolbar(tabIndex, tabs);
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
            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Avatar Performance Diagnostics", EditorStyles.boldLabel);
            
            int polyCount = 0;
            int smrCount = 0;
            int materialCount = 0;

            var smrs = targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var mfs = targetAvatar.GetComponentsInChildren<MeshFilter>(true);
            var renderers = targetAvatar.GetComponentsInChildren<Renderer>(true);

            foreach (var smr in smrs)
            {
                smrCount++;
                if (smr.sharedMesh != null) polyCount += smr.sharedMesh.triangles.Length / 3;
            }
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh != null) polyCount += mf.sharedMesh.triangles.Length / 3;
            }
            foreach (var r in renderers)
            {
                materialCount += r.sharedMaterials.Length;
            }

            EditorGUILayout.LabelField("Polygons (Triangles):", polyCount.ToString("N0"));
            EditorGUILayout.LabelField("Skinned Meshes:", smrCount.ToString());
            EditorGUILayout.LabelField("Material Slots:", materialCount.ToString());

            EditorGUILayout.Space();
            if (polyCount > 70000) EditorGUILayout.HelpBox("Polygon count exceeds 70,000 (Very Poor on PC).", MessageType.Warning);
            else if (polyCount > 32000) EditorGUILayout.HelpBox("Polygon count is within Medium/Poor range.", MessageType.Info);
            else EditorGUILayout.HelpBox("Polygon count is Excellent/Good!", MessageType.Info);

            if (materialCount > 16) EditorGUILayout.HelpBox("High material count detected. Consider atlasing.", MessageType.Warning);

            // Animator Controller Check
            var anim = targetAvatar.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController == null)
            {
                EditorGUILayout.HelpBox("Animator has no controller assigned! This may cause a T-Pose in VRChat.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAutoFixesTab()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("1-Click Master Fix", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Runs all safe, essential fixes automatically.", MessageType.Info);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("RUN ALL MASTER FIXES", GUILayout.Height(40))) RunAllFixes();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Individual Fixes", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove Missing Scripts")) RemoveMissingScripts();
            if (GUILayout.Button("Clean Missing Materials")) CleanMissingMaterials();
            if (GUILayout.Button("Fix Skinned Mesh Bounds")) FixMeshBounds();
            if (GUILayout.Button("Fix Audio Sources (Spatial Blend)")) FixAudioSources();
            if (GUILayout.Button("Fix Mesh Read/Write")) FixMeshReadWrite();
            if (GUILayout.Button("Normalize Scale to (1,1,1)")) NormalizeScale();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("VRChat Specific Auto-Setup", EditorStyles.boldLabel);
            if (GUILayout.Button("Auto-Align View Position (Eyes)")) AutoAlignViewPosition();
            if (GUILayout.Button("Auto-Setup Lip Sync (Visemes)")) AutoSetupLipSync();
            if (GUILayout.Button("Clear Blueprint ID (Detach)")) ClearBlueprintID();
            EditorGUILayout.EndVertical();
        }

        private void DrawOptimizationsTab()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Prefab Utilities", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Unpacking a prefab completely disconnects it from the original file. This is often required before making deep structural changes.", MessageType.Info);
            if (GUILayout.Button("Unpack Prefab Completely", GUILayout.Height(30)))
            {
                UnpackPrefab();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Hierarchy Cleanup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Removes empty GameObjects that are NOT bones. Warning: Backup your avatar first if you use complex constraints targeting empty objects!", MessageType.Warning);
            
            if (GUILayout.Button("Remove Unused Empty GameObjects", GUILayout.Height(30)))
            {
                CleanupEmptyGameObjects();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawQuestTab()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Quest / Android Conversion", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("VRChat on Quest/Android strictly requires specific shaders. This tool will change all materials on this avatar to use 'VRChat/Mobile/Toon Lit'.", MessageType.Warning);
            
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            if (GUILayout.Button("Convert Materials to Quest Compatible", GUILayout.Height(40)))
            {
                ConvertToQuest();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Logic Implementation

        private void RunAllFixes()
        {
            Undo.RegisterFullObjectHierarchyUndo(targetAvatar, "Run All Auto-Fixes");
            RemoveMissingScripts();
            CleanMissingMaterials();
            FixMeshBounds();
            FixAudioSources();
            FixMeshReadWrite();
            NormalizeScale();
            AutoAlignViewPosition();
            EditorUtility.DisplayDialog("Auto-Fix Complete", "Master fixes have been successfully applied to your avatar.", "OK");
        }

        private void UnpackPrefab()
        {
            if (PrefabUtility.IsPartOfAnyPrefab(targetAvatar))
            {
                PrefabUtility.UnpackPrefabInstance(targetAvatar, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                Debug.Log("[Auto-Fixer] Prefab unpacked completely.");
            }
            else
            {
                Debug.Log("[Auto-Fixer] Target is not a prefab instance.");
            }
        }

        private void RemoveMissingScripts()
        {
            int count = 0;
            var allObjects = targetAvatar.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).ToArray();
            foreach (var go in allObjects) count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            Debug.Log($"[Auto-Fixer] Removed {count} missing scripts.");
        }

        private void CleanMissingMaterials()
        {
            var renderers = targetAvatar.GetComponentsInChildren<Renderer>(true);
            int cleanedSlots = 0;
            
            Undo.RecordObjects(renderers, "Clean Missing Materials");
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool needsUpdate = false;
                
                var newMats = new List<Material>();
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                    {
                        newMats.Add(mats[i]);
                    }
                    else
                    {
                        needsUpdate = true;
                        cleanedSlots++;
                    }
                }
                
                if (needsUpdate)
                {
                    r.sharedMaterials = newMats.ToArray();
                }
            }
            Debug.Log($"[Auto-Fixer] Cleaned up {cleanedSlots} missing/null material slots.");
        }

        private void FixMeshBounds()
        {
            var smrs = targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Undo.RecordObjects(smrs, "Fix Bounds");
            foreach (var smr in smrs) if (smr.sharedMesh != null) smr.localBounds = new Bounds(Vector3.zero, new Vector3(3f, 3f, 3f));
            Debug.Log($"[Auto-Fixer] Fixed bounds for {smrs.Length} SkinnedMeshRenderers.");
        }

        private void FixAudioSources()
        {
            var audioSources = targetAvatar.GetComponentsInChildren<AudioSource>(true);
            Undo.RecordObjects(audioSources, "Fix Audio");
            foreach (var audio in audioSources) audio.spatialBlend = 1f;
            Debug.Log($"[Auto-Fixer] Fixed {audioSources.Length} AudioSources.");
        }

        private void FixMeshReadWrite()
        {
            var meshes = new HashSet<Mesh>();
            foreach (var smr in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (smr.sharedMesh != null) meshes.Add(smr.sharedMesh);
            foreach (var mf in targetAvatar.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) meshes.Add(mf.sharedMesh);

            int count = 0;
            foreach (var mesh in meshes)
            {
                if (!mesh.isReadable)
                {
                    string path = AssetDatabase.GetAssetPath(mesh);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                        if (importer != null && !importer.isReadable)
                        {
                            importer.isReadable = true;
                            importer.SaveAndReimport();
                            count++;
                        }
                    }
                }
            }
            Debug.Log($"[Auto-Fixer] Enabled Read/Write on {count} meshes.");
        }

        private void NormalizeScale()
        {
            Undo.RecordObject(targetAvatar.transform, "Normalize Scale");
            targetAvatar.transform.localScale = Vector3.one;
            Debug.Log("[Auto-Fixer] Normalized root scale to (1,1,1).");
        }

        private void AutoAlignViewPosition()
        {
            var animator = targetAvatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[Auto-Fixer] Avatar is not Humanoid. Cannot auto-align View Position.");
                return;
            }

            Transform leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            Transform rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);

            if (leftEye == null || rightEye == null)
            {
                Debug.LogWarning("[Auto-Fixer] Eye bones not found in Humanoid Rig.");
                return;
            }

            Vector3 worldCenter = (leftEye.position + rightEye.position) / 2f;
            Vector3 localPos = targetAvatar.transform.InverseTransformPoint(worldCenter);
            
            // Push slightly forward to be "between" the eyes instead of inside the head
            localPos.z += 0.015f; 

            System.Type descriptorType = GetVRCDescriptorType();
            if (descriptorType != null)
            {
                var descriptor = targetAvatar.GetComponent(descriptorType);
                if (descriptor != null)
                {
                    Undo.RecordObject(descriptor, "Align View Position");
                    FieldInfo viewPosField = descriptorType.GetField("ViewPosition");
                    if (viewPosField != null)
                    {
                        viewPosField.SetValue(descriptor, localPos);
                        EditorUtility.SetDirty(descriptor);
                        Debug.Log("[Auto-Fixer] Successfully aligned View Position!");
                    }
                }
            }
        }

        private void AutoSetupLipSync()
        {
            System.Type descriptorType = GetVRCDescriptorType();
            if (descriptorType == null) return;
            
            var descriptor = targetAvatar.GetComponent(descriptorType);
            if (descriptor == null) return;

            SkinnedMeshRenderer bodyMesh = null;
            foreach (var smr in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                {
                    for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                    {
                        if (smr.sharedMesh.GetBlendShapeName(i).ToLower().Contains("vrc.v_aa"))
                        {
                            bodyMesh = smr;
                            break;
                        }
                    }
                }
                if (bodyMesh != null) break;
            }

            if (bodyMesh == null)
            {
                Debug.LogWarning("[Auto-Fixer] Could not find a mesh with standard 'vrc.v_aa' blendshapes.");
                return;
            }

            Undo.RecordObject(descriptor, "Setup Lip Sync");
            
            FieldInfo visemeMeshField = descriptorType.GetField("VisemeSkinnedMesh");
            if (visemeMeshField != null)
            {
                visemeMeshField.SetValue(descriptor, bodyMesh);
                EditorUtility.SetDirty(descriptor);
                Debug.Log("[Auto-Fixer] Assigned Viseme Skinned Mesh!");
            }
        }

        private void ClearBlueprintID()
        {
            System.Type pipelineType = GetTypeSafe("VRC.Core.PipelineManager");
            if (pipelineType != null)
            {
                var pipeline = targetAvatar.GetComponent(pipelineType);
                if (pipeline != null)
                {
                    Undo.RecordObject(pipeline, "Clear Blueprint ID");
                    var field = pipelineType.GetField("blueprintId");
                    if (field != null)
                    {
                        field.SetValue(pipeline, "");
                        EditorUtility.SetDirty(pipeline);
                        Debug.Log("[Auto-Fixer] Cleared Blueprint ID.");
                    }
                }
            }
        }

        private void CleanupEmptyGameObjects()
        {
            Undo.RegisterFullObjectHierarchyUndo(targetAvatar, "Cleanup Empty GameObjects");
            
            HashSet<Transform> protectedTransforms = new HashSet<Transform>();
            foreach (var smr in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                protectedTransforms.Add(smr.transform);
                foreach (var bone in smr.bones) if (bone != null) protectedTransforms.Add(bone);
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

            int removed = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                var allTransforms = targetAvatar.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (t == targetAvatar.transform) continue;
                    if (t == null) continue;
                    
                    if (t.childCount == 0 && t.GetComponents<Component>().Length == 1 && !protectedTransforms.Contains(t))
                    {
                        DestroyImmediate(t.gameObject);
                        removed++;
                        changed = true;
                    }
                }
            }
            Debug.Log($"[Auto-Fixer] Removed {removed} unused empty GameObjects.");
        }

        private void ConvertToQuest()
        {
            Shader mobileShader = Shader.Find("VRChat/Mobile/Toon Lit");
            if (mobileShader == null)
            {
                Debug.LogError("[Auto-Fixer] Could not find 'VRChat/Mobile/Toon Lit' shader. Is the VRChat SDK installed?");
                return;
            }

            var renderers = targetAvatar.GetComponentsInChildren<Renderer>(true);
            int materialCount = 0;
            
            Undo.RecordObjects(renderers, "Convert to Quest");
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != mobileShader)
                    {
                        Undo.RecordObject(mats[i], "Change Shader to Quest");
                        mats[i].shader = mobileShader;
                        EditorUtility.SetDirty(mats[i]);
                        changed = true;
                        materialCount++;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
            
            Debug.Log($"[Auto-Fixer] Converted {materialCount} materials to use Quest compatible shaders.");
            EditorUtility.DisplayDialog("Quest Conversion", $"Converted {materialCount} materials to 'VRChat/Mobile/Toon Lit'.\n\nPlease ensure you have duplicated your PC materials if you want to keep them separate!", "OK");
        }

        #endregion

        #region Helpers

        private static System.Type GetVRCDescriptorType()
        {
            return GetTypeSafe("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
        }

        private static System.Type GetTypeSafe(string typeName)
        {
            var type = System.Type.GetType(typeName);
            if (type != null) return type;
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        #endregion
    }
}
