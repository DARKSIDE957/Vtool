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
        public string Code;
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
                    return VtoolLocalization.TF("summary.blockers", BlockerCount, WarningCount);
                if (WarningCount > 0)
                    return VtoolLocalization.TF("summary.warnings", WarningCount);
                return VtoolLocalization.T("summary.ok");
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
                r.Issues.Add(Issue(IssueSeverity.Blocker, "E_NO_DESCRIPTOR", "issue.no_descriptor", "hint.no_descriptor"));
            if (!r.HasPipelineManager)
                r.Issues.Add(Issue(IssueSeverity.Blocker, "E_NO_PIPELINE", "issue.no_pipeline", "hint.no_pipeline"));
            if (!r.HasHumanoidAnimator)
                r.Issues.Add(Issue(IssueSeverity.Blocker, "E_NO_HUMANOID", "issue.no_humanoid", "hint.no_humanoid"));
            if (r.MissingScripts > 0)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_MISSING_SCRIPTS", "issue.missing_scripts", "hint.missing_scripts", r.MissingScripts));
            if (r.NullMaterialSlots > 0)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_NULL_MATS", "issue.null_mats", "hint.null_mats", r.NullMaterialSlots));
            if (r.BrokenShaders > 0)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_BROKEN_SHADERS", "issue.broken_shaders", "hint.broken_shaders", r.BrokenShaders));
            if (r.MissingMeshes > 0)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_MISSING_MESHES", "issue.missing_meshes", "hint.missing_meshes", r.MissingMeshes));
            if (r.PolyCount > 200000)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_EXTREME_POLY", "issue.extreme_poly", "hint.extreme_poly", r.PolyCount.ToString("N0")));
            if (r.PhysBoneCount > 256)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_PHYSBONE_LIMIT", "issue.physbone_limit", "hint.physbone_limit", r.PhysBoneCount));
        }

        private static void AddWarnings(ref AvatarScanResult r)
        {
            if (!r.HasChestBone && r.HasHumanoidAnimator)
                r.Issues.Add(Issue(IssueSeverity.Warning, "W_NO_CHEST", "issue.no_chest", "hint.no_chest"));
            if (!r.HasViewPosition)
                r.Issues.Add(Issue(IssueSeverity.Warning, "W_NO_VIEW", "issue.no_view", "hint.fix_if_empty"));
            if (!r.HasLipSync)
                r.Issues.Add(Issue(IssueSeverity.Warning, "W_NO_LIPSYNC", "issue.no_lipsync", "hint.fix_if_empty"));
            if (!r.RootScaleIsOne)
                r.Issues.Add(Issue(IssueSeverity.Warning, "W_ROOT_SCALE", "issue.root_scale", "hint.root_scale"));
            if (r.NegativeScales > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_NEG_SCALE", "issue.neg_scale", "hint.neg_scale", r.NegativeScales));
            if (r.NonUnitScales > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_NONUNIT_SCALE", "issue.nonunit_scale", "hint.nonunit_scale", r.NonUnitScales));
            if (r.PolyCount > 70000)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_HIGH_POLY", "issue.high_poly", "hint.high_poly", r.PolyCount.ToString("N0")));
            else if (r.PolyCount > QuestPolyLimit)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_QUEST_POLY", "issue.quest_poly", "hint.quest_poly", QuestPolyLimit.ToString("N0")));
            if (r.SkinnedMeshCount > 8)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_SKINNED_MANY", "issue.skinned_many", "hint.skinned_many", r.SkinnedMeshCount));
            if (r.MaterialSlots > 16)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_MATS_MANY", "issue.mats_many", "hint.mats_many", r.MaterialSlots));
            if (r.Textures4K > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_TEX_4K", "issue.tex_4k", "hint.tex_4k", r.Textures4K));
            if (r.TexturesOver2K > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_TEX_2K", "issue.tex_2k", "hint.tex_2k", r.TexturesOver2K));
            if (r.TextureMemoryMB > 150)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_TEX_MEM", "issue.tex_mem", "hint.tex_mem", r.TextureMemoryMB.ToString("F0")));
            if (r.TexturesNoMipmaps > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_NO_MIP", "issue.no_mip", "hint.use_tex_tab", r.TexturesNoMipmaps));
            if (r.LegacyDynamicBones > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_DYNBONE", "issue.dynbone", "hint.dynbone", r.LegacyDynamicBones));
            if (r.PhysBoneCount > 32 && r.PhysBoneCount <= 256)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PB_POOR", "issue.pb_poor", "hint.pb_poor", r.PhysBoneCount));
            if (r.BadAudioCount > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_BAD_AUDIO", "issue.bad_audio", "hint.fix_all_audio", r.BadAudioCount));
            if (r.AudioPlayOnAwake > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PLAY_AWAKE", "issue.play_awake", "hint.play_awake", r.AudioPlayOnAwake));
            if (r.ParticleCount > 16)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PARTICLES", "issue.particles", "hint.particles", r.ParticleCount));
            if (r.OtherAvatarsInScene > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_OTHER_AVATARS", "issue.other_avatars", "hint.other_avatars", r.OtherAvatarsInScene));
            if (r.QuestBadShaders > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_QUEST_MATS", "issue.quest_mats", "hint.quest_mats", r.QuestBadShaders));
            if (r.AvatarHeightMeters > MaxHeight || (r.AvatarHeightMeters > 0 && r.AvatarHeightMeters < MinHeight))
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_HEIGHT", "issue.height", "hint.height", r.AvatarHeightMeters.ToString("F2")));
        }

        private static AvatarIssue Issue(IssueSeverity s, string code, string msgKey, string hintKey) =>
            new AvatarIssue
            {
                Severity = s,
                Code = code,
                Message = VtoolLocalization.T(msgKey),
                FixHint = VtoolLocalization.T(hintKey)
            };

        private static AvatarIssue IssueF(IssueSeverity s, string code, string msgKey, string hintKey, params object[] args) =>
            new AvatarIssue
            {
                Severity = s,
                Code = code,
                Message = VtoolLocalization.TF(msgKey, args),
                FixHint = VtoolLocalization.T(hintKey)
            };

        public static string BuildDiagnosticReport(GameObject avatar, AvatarScanResult scan, string packageVersion)
        {
            var sb = new System.Text.StringBuilder(2048);
            sb.AppendLine("=== Vtool Diagnostic Report ===");
            sb.AppendLine("Vtool: " + (packageVersion ?? "?"));
            sb.AppendLine("Unity: " + Application.unityVersion);
            sb.AppendLine("Time: " + System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            sb.AppendLine("Avatar: " + (avatar != null ? avatar.name : "(none)"));
            sb.AppendLine();

            if (scan.Issues == null || scan.Issues.Count == 0)
            {
                sb.AppendLine("Codes: (none — scan looks OK)");
            }
            else
            {
                sb.Append("Codes: ");
                bool first = true;
                foreach (var i in scan.Issues)
                {
                    if (string.IsNullOrEmpty(i.Code)) continue;
                    if (!first) sb.Append(", ");
                    sb.Append(i.Code);
                    first = false;
                }
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("--- Issues ---");
                foreach (var i in scan.Issues)
                {
                    string sev = i.Severity == IssueSeverity.Blocker ? "BLOCKER" : i.Severity == IssueSeverity.Warning ? "WARNING" : "INFO";
                    sb.Append('[').Append(sev).Append("] ").Append(i.Code ?? "?").AppendLine();
                    if (!string.IsNullOrEmpty(i.Message))
                        sb.Append("  ").AppendLine(i.Message);
                    if (!string.IsNullOrEmpty(i.FixHint))
                        sb.Append("  Fix: ").AppendLine(i.FixHint);
                }
            }

            sb.AppendLine();
            sb.AppendLine("--- Stats ---");
            sb.AppendLine($"Blockers: {scan.BlockerCount} | Warnings: {scan.WarningCount}");
            sb.AppendLine($"Polys: {scan.PolyCount} | Skinned: {scan.SkinnedMeshCount} | Mats: {scan.MaterialSlots} | Bones: {scan.BoneCount}");
            sb.AppendLine($"Height: {scan.AvatarHeightMeters:F2}m | PhysBones: {scan.PhysBoneCount} | Particles: {scan.ParticleCount}");
            sb.AppendLine($"Textures: {scan.TextureCount} | 4K: {scan.Textures4K} | >2K: {scan.TexturesOver2K} | ~{scan.TextureMemoryMB:F0}MB | NoMip: {scan.TexturesNoMipmaps}");
            sb.AppendLine($"Descriptor: {scan.HasDescriptor} | Pipeline: {scan.HasPipelineManager} | Humanoid: {scan.HasHumanoidAnimator}");
            sb.AppendLine($"Chest: {scan.HasChestBone} | View: {scan.HasViewPosition} | LipSync: {scan.HasLipSync}");
            sb.AppendLine($"MissingScripts: {scan.MissingScripts} | NullMats: {scan.NullMaterialSlots} | BrokenShaders: {scan.BrokenShaders}");
            sb.AppendLine($"QuestBadShaders: {scan.QuestBadShaders} | OtherAvatars: {scan.OtherAvatarsInScene}");
            sb.AppendLine("=== End ===");
            return sb.ToString();
        }

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
