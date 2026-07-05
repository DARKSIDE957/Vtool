using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace XVR.Tools
{
    public struct FixSummary
    {
        public int MissingScripts;
        public int MaterialSlots;
        public int Bounds;
        public int Audio;
        public int AudioPlayOnAwake;
        public int Mipmaps;
        public int OtherAvatarsDisabled;
        public bool PipelineManager;
        public bool ViewPosition;
        public bool LipSync;
        public int QuestMaterials;
        public int TexturesCapped;
    }

    public static class VtoolAvatarFixes
    {
        private static readonly string[] VisemeSuffixes =
        {
            "sil", "pp", "ff", "th", "dd", "kk", "ch", "ss", "nn", "rr", "aa", "e", "ih", "oh", "ou"
        };

        private static readonly string[] QuestShaderNames =
        {
            "VRChat/Mobile/Toon Lit",
            "VRChat/Mobile/Standard Lite",
            "VRChat/Mobile/Diffuse",
            "VRChat/Mobile/Bumped Diffuse",
            "VRChat/Mobile/Particles/Additive"
        };

        private static Material placeholderMaterial;

        public static FixSummary ApplyAllSafeFixes(GameObject avatar)
        {
            var s = new FixSummary();
            if (avatar == null) return s;

            Undo.RegisterFullObjectHierarchyUndo(avatar, "Vtool Fix All");

            s.MissingScripts = RemoveMissingScripts(avatar);
            s.MaterialSlots = FixMissingMaterials(avatar);
            s.PipelineManager = EnsurePipelineManager(avatar);
            s.Bounds = FixMeshBounds(avatar);
            s.Audio = FixAudioSources(avatar, out s.AudioPlayOnAwake);
            s.Mipmaps = EnableTextureMipmaps(avatar);
            s.OtherAvatarsDisabled = DisableOtherAvatars(avatar);
            s.ViewPosition = AlignViewPosition(avatar);
            s.LipSync = SetupLipSync(avatar);

            MarkDirty();
            return s;
        }

        #region Upload fixes

        public static int RemoveMissingScripts(GameObject avatar)
        {
            int n = 0;
            foreach (var go in avatar.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject))
                if (go != null) n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            return n;
        }

        public static int FixMissingMaterials(GameObject avatar)
        {
            int fixedSlots = 0;
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
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

        public static bool EnsurePipelineManager(GameObject avatar)
        {
            if (GetPipelineManager(avatar) != null) return false;

            var pipelineType = GetTypeSafe("VRC.Core.PipelineManager");
            if (pipelineType == null) return false;

            Undo.RegisterCompleteObjectUndo(avatar, "Add PipelineManager");
            avatar.AddComponent(pipelineType);
            return true;
        }

        public static int FixMeshBounds(GameObject avatar)
        {
            var smrs = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
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

        public static int FixAudioSources(GameObject avatar, out int playOnAwakeFixed)
        {
            playOnAwakeFixed = 0;
            var sources = avatar.GetComponentsInChildren<AudioSource>(true);
            Undo.RecordObjects(sources, "Fix Audio");
            int n = 0;
            foreach (var a in sources)
            {
                if (a == null) continue;
                bool c = false;
                if (a.spatialBlend < 1f) { a.spatialBlend = 1f; c = true; }
                if (a.volume > 0.8f) { a.volume = 0.8f; c = true; }
                if (a.playOnAwake) { a.playOnAwake = false; playOnAwakeFixed++; c = true; }
                if (c) n++;
            }
            return n;
        }

        public static int DisableOtherAvatars(GameObject avatar)
        {
            var type = GetDescriptorType();
            if (type == null) return 0;
            int n = 0;
            foreach (var o in FindObjects(type))
            {
                if (o == null) continue;
                var go = ((Component)o).gameObject;
                if (go == avatar || !go.activeInHierarchy) continue;
                Undo.RecordObject(go, "Disable Other Avatar");
                go.SetActive(false);
                n++;
            }
            return n;
        }

        public static bool AlignViewPosition(GameObject avatar)
        {
            var anim = avatar.GetComponent<Animator>();
            if (anim == null || !anim.isHuman) return false;

            var le = anim.GetBoneTransform(HumanBodyBones.LeftEye);
            var re = anim.GetBoneTransform(HumanBodyBones.RightEye);
            Vector3 local;

            if (le != null && re != null)
            {
                local = avatar.transform.InverseTransformPoint((le.position + re.position) * 0.5f);
                local.z += 0.015f;
            }
            else
            {
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head == null) return false;
                local = avatar.transform.InverseTransformPoint(head.position);
                local.y += 0.06f;
                local.z += 0.08f;
            }

            var descType = GetDescriptorType();
            var desc = descType != null ? avatar.GetComponent(descType) : null;
            if (desc == null) return false;

            Undo.RecordObject(desc, "View Position");
            if (!TrySetMember(desc, descType, "ViewPosition", local)) return false;
            EditorUtility.SetDirty(desc);
            return true;
        }

        public static bool SetupLipSync(GameObject avatar)
        {
            var descType = GetDescriptorType();
            var desc = descType != null ? avatar.GetComponent(descType) : null;
            if (desc == null) return false;

            SkinnedMeshRenderer face = null;
            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
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

        public static bool ClearBlueprintId(GameObject avatar)
        {
            var pipeline = GetPipelineManager(avatar);
            if (pipeline == null) return false;
            var type = pipeline.GetType();
            Undo.RecordObject(pipeline, "Clear Blueprint");
            if (!TrySetMember(pipeline, type, "blueprintId", string.Empty)) return false;
            EditorUtility.SetDirty(pipeline);
            return true;
        }

        public static void NormalizeRootScale(GameObject avatar)
        {
            if (avatar.transform.localScale == Vector3.one) return;
            Undo.RecordObject(avatar.transform, "Normalize Scale");
            avatar.transform.localScale = Vector3.one;
            MarkDirty();
        }

        #endregion

        #region Textures

        public static int CapTextureSizes(GameObject avatar, int maxSize)
        {
            int n = 0;
            foreach (var tex in CollectTextures(avatar))
            {
                var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
                if (imp == null || imp.maxTextureSize <= maxSize) continue;
                imp.maxTextureSize = maxSize;
                imp.SaveAndReimport();
                n++;
            }
            if (n > 0) AssetDatabase.SaveAssets();
            return n;
        }

        public static int RestoreTextureSizes(GameObject avatar)
        {
            int n = 0;
            foreach (var tex in CollectTextures(avatar))
            {
                var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
                if (imp == null) continue;
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                int target = Mathf.Clamp(Mathf.Max(w, h), 32, 8192);
                if (imp.maxTextureSize == target) continue;
                imp.maxTextureSize = target;
                imp.SaveAndReimport();
                n++;
            }
            if (n > 0) AssetDatabase.SaveAssets();
            return n;
        }

        public static int EnableTextureMipmaps(GameObject avatar)
        {
            int n = 0;
            foreach (var tex in CollectTextures(avatar))
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

        public static int ConvertToQuestShaders(GameObject avatar, bool duplicateMaterials)
        {
            var shader = FindQuestShader();
            if (shader == null) return 0;

            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            var processed = new Dictionary<Material, Material>();
            int n = 0;

            Undo.RecordObjects(renderers, "Quest Shaders");
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null || mat.shader == shader) continue;

                    Material target = mat;
                    if (duplicateMaterials)
                    {
                        if (!processed.TryGetValue(mat, out target))
                        {
                            target = DuplicateQuestMaterial(mat, shader);
                            processed[mat] = target;
                        }
                    }
                    else
                    {
                        Undo.RecordObject(mat, "Quest Shader");
                        mat.shader = shader;
                        EditorUtility.SetDirty(mat);
                    }
                    mats[i] = target;
                    changed = true;
                    n++;
                }
                if (changed) r.sharedMaterials = mats;
            }
            AssetDatabase.SaveAssets();
            return n;
        }

        public static HashSet<Texture> CollectTextures(GameObject avatar)
        {
            var set = new HashSet<Texture>();
            if (avatar == null) return set;
            foreach (var r in avatar.GetComponentsInChildren<Renderer>(true))
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

        #endregion

        #region Helpers

        public static Component GetPipelineManager(GameObject avatar)
        {
            var type = GetTypeSafe("VRC.Core.PipelineManager");
            return type != null ? avatar.GetComponent(type) : null;
        }

        public static System.Type GetDescriptorType() =>
            GetTypeSafe("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");

        public static System.Type GetTypeSafe(string name)
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

        public static Object[] FindObjects(System.Type type)
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType(type, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType(type);
#endif
        }

        public static bool TryGetMember(object obj, System.Type type, string name, out object value)
        {
            value = null;
            const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(name, f);
            if (field != null) { value = field.GetValue(obj); return true; }
            var prop = type.GetProperty(name, f);
            if (prop != null && prop.CanRead) { value = prop.GetValue(obj); return true; }
            return false;
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

        private static Shader FindQuestShader()
        {
            foreach (var n in QuestShaderNames)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return null;
        }

        private static Material DuplicateQuestMaterial(Material source, Shader shader)
        {
            EnsureFolder("Assets/Vtool");
            if (!AssetDatabase.IsValidFolder("Assets/Vtool/QuestMaterials"))
                AssetDatabase.CreateFolder("Assets/Vtool", "QuestMaterials");
            string safe = string.IsNullOrEmpty(source.name) ? "Mat" : source.name.Replace("/", "_");
            string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/Vtool/QuestMaterials/{safe}_Quest.mat");
            var dup = new Material(source) { shader = shader };
            AssetDatabase.CreateAsset(dup, path);
            return dup;
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

        private static Material GetPlaceholderMaterial()
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (!AssetDatabase.IsValidFolder("Assets")) return;
            AssetDatabase.CreateFolder("Assets", path.Replace("Assets/", ""));
        }

        public static void MarkDirty()
        {
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        #endregion
    }
}
