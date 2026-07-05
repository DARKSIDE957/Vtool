using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace XVR.Tools
{
    public enum IssueSeverity { Blocker, Warning, Info }

    public struct AvatarIssue
    {
        public IssueSeverity Severity;
        public string Message;
        public string FixHint;
    }

    public struct AvatarScanResult
    {
        public List<AvatarIssue> Issues;
        public int BlockerCount;
        public int WarningCount;

        // Performance
        public int PolyCount;
        public int SkinnedMeshCount;
        public int MaterialSlots;
        public int BoneCount;
        public float AvatarHeightMeters;

        // Textures
        public int TextureCount;
        public int Textures4K;
        public int TexturesOver2K;
        public int TexturesNoMipmaps;
        public float TextureMemoryMB;

        // Components
        public int MissingScripts;
        public int NullMaterialSlots;
        public int BrokenShaders;
        public int NegativeScales;
        public int NonUnitScales;
        public int LegacyDynamicBones;
        public int PhysBoneCount;
        public int BadAudioCount;
        public int AudioPlayOnAwake;
        public int ParticleCount;
        public int MissingMeshes;
        public int OtherAvatarsInScene;
        public int QuestBadShaders;

        // VRChat setup
        public bool HasDescriptor;
        public bool HasPipelineManager;
        public bool HasHumanoidAnimator;
        public bool HasChestBone;
        public bool HasViewPosition;
        public bool HasLipSync;
        public bool RootScaleIsOne;

        public string Summary
        {
            get
            {
                if (BlockerCount > 0)
                    return $"{BlockerCount} upload blocker(s) and {WarningCount} warning(s) — fix before uploading.";
                if (WarningCount > 0)
                    return $"No blockers, but {WarningCount} warning(s) to review.";
                return "All common checks passed. Run VRChat SDK Build & Test before uploading.";
            }
        }
    }

    public static class VtoolAvatarScan
    {
        private const int QuestPolyLimit = 20000;
        private const float MinHeight = 0.3f;
        private const float MaxHeight = 5f;

        private static readonly string[] MobileShaderPrefixes =
        {
            "VRChat/Mobile/", "Hidden/VRCFallback/", "Mobile/"
        };

        public static AvatarScanResult Scan(GameObject avatar)
        {
            var r = new AvatarScanResult { Issues = new List<AvatarIssue>() };
            if (avatar == null) return r;

            var countedMeshes = new HashSet<Mesh>();
            var textures = VtoolAvatarFixes.CollectTextures(avatar);
            bool boundsInit = false;
            Bounds bounds = default;

            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                r.SkinnedMeshCount++;
                if (smr.sharedMesh == null) r.MissingMeshes++;
                else if (countedMeshes.Add(smr.sharedMesh))
                    r.PolyCount += smr.sharedMesh.triangles.Length / 3;
            }

            foreach (var mf in avatar.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null) continue;
                if (mf.sharedMesh == null) r.MissingMeshes++;
                else if (countedMeshes.Add(mf.sharedMesh))
                    r.PolyCount += mf.sharedMesh.triangles.Length / 3;
            }

            foreach (var rend in avatar.GetComponentsInChildren<Renderer>(true))
            {
                if (rend == null) continue;
                if (!boundsInit) { bounds = rend.bounds; boundsInit = true; }
                else bounds.Encapsulate(rend.bounds);

                var mats = rend.sharedMaterials;
                r.MaterialSlots += mats.Length;
                foreach (var m in mats)
                {
                    if (m == null) { r.NullMaterialSlots++; continue; }
                    if (IsBrokenShader(m.shader)) r.BrokenShaders++;
                    else if (!IsQuestShader(m.shader)) r.QuestBadShaders++;
                }
            }

            r.AvatarHeightMeters = boundsInit ? bounds.size.y : 0f;
            r.BoneCount = CountBones(avatar);

            foreach (var t in avatar.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                r.MissingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                var s = t.localScale;
                if (s.x < 0 || s.y < 0 || s.z < 0) r.NegativeScales++;
                if (s != Vector3.one) r.NonUnitScales++;
            }

            r.RootScaleIsOne = avatar.transform.localScale == Vector3.one;
            r.LegacyDynamicBones = CountType(avatar, "DynamicBone");
            r.PhysBoneCount = CountType(avatar, "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
            r.ParticleCount = avatar.GetComponentsInChildren<ParticleSystem>(true).Length;
            r.OtherAvatarsInScene = CountOtherAvatars(avatar);

            foreach (var a in avatar.GetComponentsInChildren<AudioSource>(true))
            {
                if (a == null) continue;
                if (a.volume > 0.8f || a.spatialBlend < 1f) r.BadAudioCount++;
                if (a.playOnAwake) r.AudioPlayOnAwake++;
            }

            AnalyzeTextures(textures, ref r);

            var descType = VtoolAvatarFixes.GetDescriptorType();
            r.HasDescriptor = descType != null && avatar.GetComponent(descType) != null;
            r.HasPipelineManager = VtoolAvatarFixes.GetPipelineManager(avatar) != null;

            var anim = avatar.GetComponent<Animator>();
            r.HasHumanoidAnimator = anim != null && anim.isHuman;
            r.HasChestBone = anim != null && anim.isHuman && anim.GetBoneTransform(HumanBodyBones.Chest) != null;

            if (r.HasDescriptor && descType != null)
            {
                var desc = avatar.GetComponent(descType);
                if (VtoolAvatarFixes.TryGetMember(desc, descType, "ViewPosition", out var vp) && vp is Vector3 v && v.sqrMagnitude > 0.0001f)
                    r.HasViewPosition = true;
                if (VtoolAvatarFixes.TryGetMember(desc, descType, "VisemeSkinnedMesh", out var vm) && vm != null)
                    r.HasLipSync = true;
            }

            AddBlockers(ref r);
            AddWarnings(ref r);

            r.BlockerCount = r.Issues.Count(i => i.Severity == IssueSeverity.Blocker);
            r.WarningCount = r.Issues.Count(i => i.Severity == IssueSeverity.Warning);
            return r;
        }

        private static void AddBlockers(ref AvatarScanResult r)
        {
            if (!r.HasDescriptor)
                r.Issues.Add(Issue(IssueSeverity.Blocker, "Missing VRCAvatarDescriptor on avatar root", "Add from VRChat SDK menu"));
            if (!r.HasPipelineManager)
                r.Issues.Add(Issue(IssueSeverity.Blocker, "Missing PipelineManager on avatar root", "Use Fix All or add via SDK"));
            if (!r.HasHumanoidAnimator)
                r.Issues.Add(Issue(IssueSeverity.Blocker, "Missing humanoid Animator on avatar root", "Set rig to Humanoid in Import settings"));
            if (r.MissingScripts > 0)
                r.Issues.Add(Issue(IssueSeverity.Blocker, $"{r.MissingScripts} missing script reference(s)", "Fix All removes them"));
            if (r.NullMaterialSlots > 0)
                r.Issues.Add(Issue(IssueSeverity.Blocker, $"{r.NullMaterialSlots} null material slot(s)", "Fix All fills slots safely"));
            if (r.BrokenShaders > 0)
                r.Issues.Add(Issue(IssueSeverity.Blocker, $"{r.BrokenShaders} broken shader(s) (pink materials)", "Reassign shaders manually"));
            if (r.MissingMeshes > 0)
                r.Issues.Add(Issue(IssueSeverity.Blocker, $"{r.MissingMeshes} renderer(s) with missing mesh", "Reassign or remove broken renderers"));
            if (r.PolyCount > 200000)
                r.Issues.Add(Issue(IssueSeverity.Blocker, $"Extreme polygon count ({r.PolyCount:N0})", "Reduce in Blender or decimate"));
        }

        private static void AddWarnings(ref AvatarScanResult r)
        {
            if (!r.HasChestBone && r.HasHumanoidAnimator)
                r.Issues.Add(Issue(IssueSeverity.Warning, "Humanoid rig missing Chest bone mapping", "Map Chest in Rig configuration"));
            if (!r.HasViewPosition)
                r.Issues.Add(Issue(IssueSeverity.Warning, "View position not set on descriptor", "Fix All aligns to eyes"));
            if (!r.HasLipSync)
                r.Issues.Add(Issue(IssueSeverity.Warning, "Lip sync / visemes not configured", "Fix All sets up vrc.v_* blendshapes"));
            if (!r.RootScaleIsOne)
                r.Issues.Add(Issue(IssueSeverity.Warning, "Avatar root scale is not (1,1,1)", "Can cause IK issues — normalize if needed"));
            if (r.NegativeScales > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.NegativeScales} transform(s) with negative scale", "Can invert normals and break uploads"));
            if (r.NonUnitScales > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.NonUnitScales} transform(s) with non-unit scale", "May cause animation/IK issues"));
            if (r.PolyCount > 70000)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"High polygon count ({r.PolyCount:N0}) — Poor rank on PC", "Decimate or optimize mesh"));
            else if (r.PolyCount > QuestPolyLimit)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"Over Quest limit ({QuestPolyLimit:N0} tris)", "Required for Android/Quest uploads"));
            if (r.SkinnedMeshCount > 8)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.SkinnedMeshCount} skinned meshes (8+ hurts performance)", "Merge meshes if possible"));
            if (r.MaterialSlots > 16)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.MaterialSlots} material slots (16+ hurts performance)", "Atlas textures / merge materials"));
            if (r.Textures4K > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.Textures4K} texture(s) at 4K+", "Reduce to 2K in Textures tab"));
            if (r.TexturesOver2K > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.TexturesOver2K} texture(s) over 2K", "VRChat recommends 2K max"));
            if (r.TextureMemoryMB > 150)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"High texture memory (~{r.TextureMemoryMB:F0} MB)", "Can fail security checks"));
            if (r.TexturesNoMipmaps > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.TexturesNoMipmaps} texture(s) missing mipmaps", "Fix All enables mipmaps"));
            if (r.LegacyDynamicBones > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.LegacyDynamicBones} legacy Dynamic Bone(s)", "Migrate to PhysBones"));
            if (r.PhysBoneCount > 256)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.PhysBoneCount} PhysBones (256+ is Very Poor)", "Reduce PhysBone count"));
            if (r.BadAudioCount > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.BadAudioCount} audio source(s) need 3D spatialization", "Fix All corrects audio"));
            if (r.AudioPlayOnAwake > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.AudioPlayOnAwake} audio plays on awake", "Fix All disables playOnAwake"));
            if (r.ParticleCount > 16)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.ParticleCount} particle systems (16+ hurts performance)", "Reduce particle count"));
            if (r.OtherAvatarsInScene > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.OtherAvatarsInScene} other avatar(s) active in scene", "Fix All disables them"));
            if (r.QuestBadShaders > 0)
                r.Issues.Add(Issue(IssueSeverity.Warning, $"{r.QuestBadShaders} material(s) not Quest-compatible", "Use Quest conversion in Textures tab"));
            if (r.AvatarHeightMeters > MaxHeight || (r.AvatarHeightMeters > 0 && r.AvatarHeightMeters < MinHeight))
                r.Issues.Add(Issue(IssueSeverity.Warning, $"Unusual avatar height ({r.AvatarHeightMeters:F2}m)", "Check view position and scale"));
        }

        private static AvatarIssue Issue(IssueSeverity s, string msg, string hint) =>
            new AvatarIssue { Severity = s, Message = msg, FixHint = hint };

        private static void AnalyzeTextures(HashSet<Texture> textures, ref AvatarScanResult r)
        {
            r.TextureCount = textures.Count;
            long mem = 0;
            foreach (var tex in textures)
            {
                if (tex == null) continue;
                int dim = Mathf.Max(tex.width, tex.height);
                if (dim >= 4096) r.Textures4K++;
                if (dim > 2048) r.TexturesOver2K++;
                mem += (long)(dim * dim * 4 * 1.33f);

                var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
                if (imp != null && !imp.mipmapEnabled) r.TexturesNoMipmaps++;
            }
            r.TextureMemoryMB = mem / (1024f * 1024f);
        }

        private static int CountBones(GameObject avatar)
        {
            var bones = new HashSet<Transform>();
            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                if (smr.bones != null)
                    foreach (var b in smr.bones) if (b != null) bones.Add(b);
                if (smr.rootBone != null) bones.Add(smr.rootBone);
            }
            return bones.Count;
        }

        private static int CountType(GameObject avatar, string typeName)
        {
            var t = VtoolAvatarFixes.GetTypeSafe(typeName);
            return t == null ? 0 : avatar.GetComponentsInChildren(t, true).Length;
        }

        private static int CountOtherAvatars(GameObject self)
        {
            var type = VtoolAvatarFixes.GetDescriptorType();
            if (type == null) return 0;
            int n = 0;
            foreach (var o in VtoolAvatarFixes.FindObjects(type))
            {
                if (o == null) continue;
                var go = ((Component)o).gameObject;
                if (go != self && go.activeInHierarchy) n++;
            }
            return n;
        }

        private static bool IsBrokenShader(Shader s) =>
            s == null || s.name.Contains("InternalErrorShader") || s.name.Contains("Hidden/InternalError");

        private static bool IsQuestShader(Shader s)
        {
            if (s == null) return false;
            foreach (var p in MobileShaderPrefixes)
                if (s.name.StartsWith(p)) return true;
            return false;
        }
    }
}
