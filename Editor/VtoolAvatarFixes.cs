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
            "VRChat/Mobile/Toon Standard",
            "VRChat/Mobile/Toon Lit",
            "VRChat/Mobile/Toon Lit Cutout",
            "VRChat/Mobile/Standard Lite",
            "VRChat/Mobile/Diffuse",
            "VRChat/Mobile/Bumped Diffuse",
            "VRChat/Mobile/Particles/Additive"
        };

        private static readonly string[] QuestMainTexNames =
        {
            "_MainTex", "_BaseMap", "_BaseColorMap", "_Diffuse", "_Albedo",
            "_ColorMap", "_MainColorTex", "_MainTex2D", "_Texture", "_Tex"
        };

        private static readonly string[] QuestColorNames =
        {
            "_Color", "_BaseColor", "_MainColor", "_TintColor", "_Color1", "_LitColor"
        };

        private static readonly string[] QuestEmissionTexNames =
        {
            "_EmissionMap", "_EmissionTex", "_EmissiveColorMap", "_Emission"
        };

        private static readonly string[] QuestEmissionColorNames =
        {
            "_EmissionColor", "_EmissiveColor", "_EmissionColour"
        };

        private static Material placeholderMaterial;

        public static FixSummary ApplyAllSafeFixes(GameObject avatar)
        {
            var s = new FixSummary();
            if (avatar == null) return s;

            Undo.RegisterFullObjectHierarchyUndo(avatar, "Vtool Fix All");

            s.MaterialSlots = FixMissingMaterials(avatar, allowPlaceholder: false);
            s.PipelineManager = EnsurePipelineManager(avatar);
            s.Bounds = FixMeshBounds(avatar);
            s.Audio = FixAudioSources(avatar, out s.AudioPlayOnAwake);
            s.ViewPosition = AlignViewPosition(avatar, onlyIfUnset: true);
            s.LipSync = SetupLipSync(avatar, onlyIfUnset: true);

            MarkDirty();
            return s;
        }

        #region Upload fixes

        public static int FixMissingMaterials(GameObject avatar, bool allowPlaceholder = false)
        {
            int fixedSlots = 0;
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            var headRoots = CollectHeadProtectionRoots(avatar);
            Undo.RecordObjects(renderers, "Fix Materials");

            foreach (var r in renderers)
            {
                if (r == null) continue;
                bool headProtected = IsUnderHeadProtection(r.transform, avatar, headRoots);
                // Never invent placeholder mats on head/face/hair — that can blank the face.
                bool canPlaceholder = allowPlaceholder && !headProtected;

                var mats = r.sharedMaterials;
                if (mats.Length == 0) continue;

                int subCount = GetSubMeshCount(r);
                var newMats = (Material[])mats.Clone();
                bool changed = false;

                for (int i = 0; i < newMats.Length; i++)
                {
                    if (newMats[i] != null) continue;
                    var fb = FindFallbackMaterial(newMats, i);
                    if (fb == null)
                    {
                        if (!canPlaceholder) continue;
                        fb = GetPlaceholderMaterial();
                        if (fb == null) continue;
                    }
                    newMats[i] = fb;
                    fixedSlots++;
                    changed = true;
                }

                if (subCount > 0 && newMats.Length < subCount)
                {
                    var expanded = new Material[subCount];
                    bool expandedChanged = false;
                    for (int i = 0; i < subCount; i++)
                    {
                        if (i < newMats.Length && newMats[i] != null)
                        {
                            expanded[i] = newMats[i];
                            continue;
                        }

                        var fb = FindFallbackMaterial(newMats, i);
                        if (fb == null && canPlaceholder)
                            fb = GetPlaceholderMaterial();
                        if (fb == null)
                        {
                            // Keep prior slot if any — never write a shorter/null head materials array.
                            if (i < newMats.Length)
                                expanded[i] = newMats[i];
                            continue;
                        }

                        expanded[i] = fb;
                        fixedSlots++;
                        expandedChanged = true;
                    }

                    if (expandedChanged)
                    {
                        newMats = expanded;
                        changed = true;
                    }
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
            // Writing mesh.bounds into localBounds can frustum-cull heads on bases like Manuka
            // (MANUKA_atama) when the mesh is not under the Head bone in the expected space.
            // Never touch head/face/hair SMRs — and never replace bounds on face blendshape meshes.
            var headRoots = CollectHeadProtectionRoots(avatar);
            var smrs = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Undo.RecordObjects(smrs, "Fix Bounds");
            int n = 0;
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                if (IsUnderHeadProtection(smr.transform, avatar, headRoots)) continue;
                if (LooksLikeFaceMesh(smr)) continue;

                // Expand the existing skinned localBounds — do not overwrite with mesh.bounds.
                var b = smr.localBounds;
                if (b.size.sqrMagnitude < 0.0001f)
                    b = smr.sharedMesh.bounds;
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

        public static bool AlignViewPosition(GameObject avatar, bool onlyIfUnset = false)
        {
            var anim = avatar.GetComponent<Animator>();
            if (anim == null || !anim.isHuman) return false;

            var descType = GetDescriptorType();
            var desc = descType != null ? avatar.GetComponent(descType) : null;
            if (desc == null) return false;

            if (onlyIfUnset &&
                TryGetMember(desc, descType, "ViewPosition", out var existing) &&
                existing is Vector3 current &&
                current.sqrMagnitude > 0.0001f)
                return false;

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

            Undo.RecordObject(desc, "View Position");
            if (!TrySetMember(desc, descType, "ViewPosition", local)) return false;
            EditorUtility.SetDirty(desc);
            return true;
        }

        public static bool SetupLipSync(GameObject avatar, bool onlyIfUnset = false)
        {
            var descType = GetDescriptorType();
            var desc = descType != null ? avatar.GetComponent(descType) : null;
            if (desc == null) return false;

            if (onlyIfUnset &&
                TryGetMember(desc, descType, "VisemeSkinnedMesh", out var existingMesh) &&
                existingMesh != null)
                return false;

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

        // Removes excess VRCPhysBone scripts only — never GameObjects, meshes, bones, or head/face/hair.
        public static int ReducePhysBoneComponents(GameObject avatar, int limit = 256)
        {
            if (avatar == null || limit < 0) return 0;

            var type = GetTypeSafe("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
            if (type == null) return 0;

            var headRoots = CollectHeadProtectionRoots(avatar);
            var components = avatar.GetComponentsInChildren(type, true)
                .Cast<Component>()
                .Where(c => c != null)
                .ToList();

            int excess = components.Count - limit;
            if (excess <= 0) return 0;

            // Never remove PhysBones on/under the head, or whose root targets the head/face/hair.
            var removable = components
                .Where(c => !IsPhysBoneHeadProtected(c, avatar, headRoots))
                .OrderByDescending(c => PhysBoneRemovalPriority(c))
                .ThenBy(c => HierarchyPath(c.transform), System.StringComparer.Ordinal)
                .ToList();

            int removed = 0;
            for (int i = 0; i < excess && i < removable.Count; i++)
            {
                var c = removable[i];
                if (c == null) continue;
                if (!TryDestroyPhysBoneOnly(c, avatar, headRoots)) continue;
                removed++;
            }

            return removed;
        }

        public static int RemoveMissingScripts(GameObject avatar)
        {
            if (avatar == null) return 0;
            var headRoots = CollectHeadProtectionRoots(avatar);
            int n = 0;
            foreach (var go in avatar.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject))
            {
                if (go == null) continue;
                if (IsUnderHeadProtection(go.transform, avatar, headRoots)) continue;
                // Never strip scripts from objects that also hold a mesh renderer (face/body geo).
                if (go.GetComponent<Renderer>() != null) continue;
                n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }
            return n;
        }

        // True if this transform is the head region (head/face/hair/eyes) or under it.
        public static bool IsUnderHeadProtection(Transform t, GameObject avatar)
        {
            return IsUnderHeadProtection(t, avatar, CollectHeadProtectionRoots(avatar));
        }

        private static bool IsUnderHeadProtection(Transform t, GameObject avatar, HashSet<Transform> headRoots)
        {
            if (t == null || avatar == null) return true; // fail-safe: protect unknowns

            if (headRoots != null)
            {
                foreach (var root in headRoots)
                {
                    if (root == null) continue;
                    if (t == root || t.IsChildOf(root)) return true;
                }
            }

            for (var cur = t; cur != null; cur = cur.parent)
            {
                if (IsHeadRelatedName(cur.name)) return true;
                if (cur == avatar.transform) break;
            }
            return false;
        }

        private static bool IsPhysBoneHeadProtected(Component physBone, GameObject avatar, HashSet<Transform> headRoots)
        {
            if (physBone == null) return true;
            if (IsUnderHeadProtection(physBone.transform, avatar, headRoots)) return true;

            // VRCPhysBone.rootTransform often points at hair/head while the script sits on Chest/Hips.
            if (TryGetMember(physBone, physBone.GetType(), "rootTransform", out var rootObj) && rootObj is Transform root)
            {
                if (IsUnderHeadProtection(root, avatar, headRoots)) return true;
            }

            return false;
        }

        private static HashSet<Transform> CollectHeadProtectionRoots(GameObject avatar)
        {
            var roots = new HashSet<Transform>();
            if (avatar == null) return roots;

            var anim = avatar.GetComponentInChildren<Animator>(true);
            if (anim != null && anim.isHuman)
            {
                AddBone(roots, anim, HumanBodyBones.Head);
                AddBone(roots, anim, HumanBodyBones.Neck);
                AddBone(roots, anim, HumanBodyBones.UpperChest);
                AddBone(roots, anim, HumanBodyBones.Jaw);
                AddBone(roots, anim, HumanBodyBones.LeftEye);
                AddBone(roots, anim, HumanBodyBones.RightEye);
            }

            // Named head/face/hair objects anywhere under the avatar
            foreach (var tr in avatar.GetComponentsInChildren<Transform>(true))
            {
                if (tr == null) continue;
                if (IsHeadRelatedName(tr.name))
                    roots.Add(tr);
            }

            // Face/head skinned meshes — protect the mesh object + its rootBone only
            // (do not add every skin bone; that would lock the whole body armature).
            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                bool nameHit = IsHeadRelatedName(smr.name) || IsHeadRelatedName(smr.gameObject.name) ||
                               (smr.sharedMesh != null && IsHeadRelatedName(smr.sharedMesh.name));
                bool faceBlend = LooksLikeFaceMesh(smr);
                if (!nameHit && !faceBlend) continue;

                roots.Add(smr.transform);
                if (smr.rootBone != null)
                    roots.Add(smr.rootBone);
            }

            return roots;
        }

        private static bool LooksLikeFaceMesh(SkinnedMeshRenderer smr)
        {
            if (smr == null || smr.sharedMesh == null) return false;
            var mesh = smr.sharedMesh;
            if (mesh.blendShapeCount < 8) return false;

            int hits = 0;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string n = mesh.GetBlendShapeName(i).ToLowerInvariant();
                if (n.Contains("v_") || n.Contains("viseme") || n.Contains("sil") ||
                    n.Contains("aa") || n.Contains("oh") || n.Contains("ch") ||
                    n.Contains("blink") || n.Contains("jaw") || n.Contains("mouth") ||
                    n.Contains("smile") || n.Contains("frown") || n.Contains("eye"))
                    hits++;
                if (hits >= 3) return true;
            }
            return mesh.blendShapeCount >= 20;
        }

        private static void AddBone(HashSet<Transform> set, Animator anim, HumanBodyBones bone)
        {
            var t = anim.GetBoneTransform(bone);
            if (t != null) set.Add(t);
        }

        private static bool IsHeadRelatedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();

            // Japanese kana/kanji common on Booth bases (Manuka uses MANUKA_atama = head)
            if (n.Contains("頭") || n.Contains("顔") || n.Contains("髪") || n.Contains("目") ||
                n.Contains("眉") || n.Contains("口") || n.Contains("耳") || n.Contains("首") ||
                n.Contains("瞳"))
                return true;

            return ContainsToken(n, "head") || ContainsToken(n, "face") || ContainsToken(n, "hair") ||
                   ContainsToken(n, "scalp") || ContainsToken(n, "skull") || ContainsToken(n, "neck") ||
                   ContainsToken(n, "jaw") || ContainsToken(n, "eye") || ContainsToken(n, "lash") ||
                   ContainsToken(n, "brow") || ContainsToken(n, "mouth") || ContainsToken(n, "teeth") ||
                   ContainsToken(n, "tooth") || ContainsToken(n, "tongue") || ContainsToken(n, "ear") ||
                   ContainsToken(n, "viseme") || ContainsToken(n, "ponytail") || ContainsToken(n, "braid") ||
                   ContainsToken(n, "bang") || ContainsToken(n, "fringe") || ContainsToken(n, "wig") ||
                   ContainsToken(n, "lipstick") || ContainsToken(n, "nose") || ContainsToken(n, "cheek") ||
                   ContainsToken(n, "chin") || ContainsToken(n, "forehead") || ContainsToken(n, "cranium") ||
                   ContainsToken(n, "horn") || ContainsToken(n, "antler") || ContainsToken(n, "pupil") ||
                   ContainsToken(n, "iris") || ContainsToken(n, "sclera") ||
                   // Romaji used by Manuka / Powari / many JP bases
                   ContainsToken(n, "atama") || ContainsToken(n, "kao") || ContainsToken(n, "kami") ||
                   ContainsToken(n, "hitomi") || ContainsToken(n, "mayu") || ContainsToken(n, "kuchi") ||
                   ContainsToken(n, "mimi") || ContainsToken(n, "kubi") || ContainsToken(n, "manuka") ||
                   ContainsToken(n, "powari");
        }

        private static bool ContainsToken(string name, string token)
        {
            int i = name.IndexOf(token, System.StringComparison.Ordinal);
            if (i < 0) return false;
            bool startOk = i == 0 || !char.IsLetterOrDigit(name[i - 1]);
            int end = i + token.Length;
            bool endOk = end >= name.Length || !char.IsLetterOrDigit(name[end]);
            // Allow glued names like MANUKA_atama / HeadGeo / HairBand
            if (token == "head" || token == "hair" || token == "face" || token == "eye" ||
                token == "wig" || token == "bang" || token == "braid" || token == "ponytail" ||
                token == "atama" || token == "kao" || token == "kami" || token == "manuka" ||
                token == "powari" || token == "hitomi")
                return true;
            return startOk && endOk;
        }

        // Only VRCPhysBone scripts — never GameObjects, Transform, Renderer, MeshFilter, Animator, Descriptor.
        private static bool TryDestroyPhysBoneOnly(Component c, GameObject avatar, HashSet<Transform> headRoots)
        {
            if (c == null) return false;
            if (c is Transform) return false;
            if (c is Renderer) return false;
            if (c is MeshFilter) return false;
            if (c is Animator) return false;
            if (c.GetType().Name.IndexOf("PhysBone", System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (IsPhysBoneHeadProtected(c, avatar, headRoots)) return false;

            Undo.DestroyObjectImmediate(c);
            return true;
        }

        private static int PhysBoneRemovalPriority(Component c)
        {
            int score = 0;
            if (!c.gameObject.activeInHierarchy) score += 10000;
            if (!c.gameObject.activeSelf) score += 1000;

            int depth = 0;
            for (var t = c.transform; t != null; t = t.parent) depth++;
            score += depth;
            return score;
        }

        private static string HierarchyPath(Transform t)
        {
            if (t == null) return string.Empty;
            var parts = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent)
                parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
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
                    if (mat == null || IsQuestMobileShader(mat.shader)) continue;

                    var shader = PickQuestShader(mat);
                    if (shader == null) continue;

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
                        ApplyQuestShaderKeepingColors(mat, shader);
                        EditorUtility.SetDirty(mat);
                        target = mat;
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

        private static Shader PickQuestShader(Material source)
        {
            bool cutout = LooksLikeCutout(source);
            if (cutout)
            {
                var cut = Shader.Find("VRChat/Mobile/Toon Lit Cutout");
                if (cut != null) return cut;
            }

            // Prefer Toon Standard when present (newer SDK) for better color / lighting fidelity.
            var toonStd = Shader.Find("VRChat/Mobile/Toon Standard");
            if (toonStd != null) return toonStd;

            var toonLit = Shader.Find("VRChat/Mobile/Toon Lit");
            if (toonLit != null) return toonLit;

            return FindQuestShader();
        }

        private static bool IsQuestMobileShader(Shader s)
        {
            if (s == null) return false;
            string n = s.name;
            return n.StartsWith("VRChat/Mobile/") || n.StartsWith("Hidden/VRCFallback/") || n.StartsWith("Mobile/");
        }

        private static bool LooksLikeCutout(Material m)
        {
            if (m == null) return false;
            if (m.HasProperty("_Cutoff") && m.GetFloat("_Cutoff") > 0.01f) return true;
            string sn = m.shader != null ? m.shader.name.ToLowerInvariant() : "";
            return sn.Contains("cutout") || sn.Contains("cut out") || sn.Contains("clip");
        }

        private static Material DuplicateQuestMaterial(Material source, Shader shader)
        {
            EnsureFolder("Assets/Vtool");
            if (!AssetDatabase.IsValidFolder("Assets/Vtool/QuestMaterials"))
                AssetDatabase.CreateFolder("Assets/Vtool", "QuestMaterials");
            string safe = string.IsNullOrEmpty(source.name) ? "Mat" : source.name.Replace("/", "_");
            string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/Vtool/QuestMaterials/{safe}_Quest.mat");

            // Build from the Quest shader, then copy appearance — avoids lost lilToon/Poiyomi props.
            var dup = new Material(shader) { name = source.name + "_Quest" };
            TransferQuestAppearance(source, dup);
            AssetDatabase.CreateAsset(dup, path);
            return dup;
        }

        private static void ApplyQuestShaderKeepingColors(Material mat, Shader shader)
        {
            Texture mainTex = FindTexture(mat, QuestMainTexNames) ?? mat.mainTexture;
            Vector2 scale = Vector2.one;
            Vector2 offset = Vector2.zero;
            string texProp = FindTexturePropertyName(mat, QuestMainTexNames);
            if (!string.IsNullOrEmpty(texProp))
            {
                scale = mat.GetTextureScale(texProp);
                offset = mat.GetTextureOffset(texProp);
            }

            Color color = ClampToLdr(FindColor(mat, QuestColorNames));
            Texture emissionTex = FindTexture(mat, QuestEmissionTexNames);
            Color emissionColor = ClampToLdr(FindColor(mat, QuestEmissionColorNames, Color.black));
            float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

            mat.shader = shader;
            ApplyQuestProperties(mat, mainTex, scale, offset, color, emissionTex, emissionColor, cutoff);
        }

        private static void TransferQuestAppearance(Material source, Material dest)
        {
            if (source == null || dest == null) return;

            Texture mainTex = FindTexture(source, QuestMainTexNames) ?? source.mainTexture;
            Vector2 scale = Vector2.one;
            Vector2 offset = Vector2.zero;
            string texProp = FindTexturePropertyName(source, QuestMainTexNames);
            if (!string.IsNullOrEmpty(texProp))
            {
                scale = source.GetTextureScale(texProp);
                offset = source.GetTextureOffset(texProp);
            }
            else if (mainTex != null)
            {
                scale = source.mainTextureScale;
                offset = source.mainTextureOffset;
            }

            Color color = ClampToLdr(FindColor(source, QuestColorNames));
            Texture emissionTex = FindTexture(source, QuestEmissionTexNames);
            Color emissionColor = ClampToLdr(FindColor(source, QuestEmissionColorNames, Color.black));
            float cutoff = source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f;

            ApplyQuestProperties(dest, mainTex, scale, offset, color, emissionTex, emissionColor, cutoff);
        }

        private static void ApplyQuestProperties(
            Material dest,
            Texture mainTex,
            Vector2 scale,
            Vector2 offset,
            Color color,
            Texture emissionTex,
            Color emissionColor,
            float cutoff)
        {
            if (dest.HasProperty("_MainTex"))
            {
                if (mainTex != null) dest.SetTexture("_MainTex", mainTex);
                dest.SetTextureScale("_MainTex", scale);
                dest.SetTextureOffset("_MainTex", offset);
            }
            else if (mainTex != null)
            {
                dest.mainTexture = mainTex;
                dest.mainTextureScale = scale;
                dest.mainTextureOffset = offset;
            }

            if (dest.HasProperty("_Color"))
                dest.SetColor("_Color", color);
            else
                dest.color = color;

            // Soften extreme tints that wash out or crush Quest lighting
            if (dest.HasProperty("_Color"))
            {
                var c = dest.GetColor("_Color");
                // Keep albedo mostly from the texture when tint is near-white
                if (c.r > 0.95f && c.g > 0.95f && c.b > 0.95f)
                    dest.SetColor("_Color", new Color(1f, 1f, 1f, c.a));
            }

            if (dest.HasProperty("_EmissionMap") && emissionTex != null)
                dest.SetTexture("_EmissionMap", emissionTex);
            if (dest.HasProperty("_EmissionColor") && emissionColor.maxColorComponent > 0.001f)
                dest.SetColor("_EmissionColor", emissionColor);

            if (dest.HasProperty("_Cutoff"))
                dest.SetFloat("_Cutoff", Mathf.Clamp01(cutoff));
        }

        private static Texture FindTexture(Material m, string[] names)
        {
            if (m == null) return null;
            foreach (var name in names)
            {
                if (!m.HasProperty(name)) continue;
                var t = m.GetTexture(name);
                if (t != null) return t;
            }
            return null;
        }

        private static string FindTexturePropertyName(Material m, string[] names)
        {
            if (m == null) return null;
            foreach (var name in names)
            {
                if (!m.HasProperty(name)) continue;
                if (m.GetTexture(name) != null) return name;
            }
            return null;
        }

        private static Color FindColor(Material m, string[] names) =>
            FindColor(m, names, Color.white);

        private static Color FindColor(Material m, string[] names, Color fallback)
        {
            if (m == null) return fallback;
            foreach (var name in names)
            {
                if (!m.HasProperty(name)) continue;
                return m.GetColor(name);
            }
            try { return m.color; }
            catch { return fallback; }
        }

        private static Color ClampToLdr(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (max > 1f)
            {
                c.r /= max;
                c.g /= max;
                c.b /= max;
            }
            c.a = Mathf.Clamp01(c.a);
            return c;
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
