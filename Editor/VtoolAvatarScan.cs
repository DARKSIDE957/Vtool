using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XVR.Tools
{
    public enum IssueSeverity { Blocker, Warning, Info }

    public struct AvatarIssue
    {
        public IssueSeverity Severity;
        public string Code;
        public string Message;
        public string FixHint;
        public string MsgKey;
        public string HintKey;
        public object[] FormatArgs;
        public List<string> RelatedObjects;
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
        public int PhysBoneColliderCount;
        public int PhysBoneAffectedTransforms;
        public int PhysBonesOverTransformLimit;
        public int BadAudioCount;
        public int AudioPlayOnAwake;
        public int ParticleCount;
        public int MissingMeshes;
        public int OtherAvatarsInScene;
        public int QuestBadShaders;
        public int NullAvatarAnimatorCount;

        // Detail lists (capped) for support report
        public List<string> NullMatDetails;
        public List<string> BrokenShaderDetails;
        public List<string> MissingMeshDetails;
        public List<string> MissingScriptDetails;
        public List<string> BadAudioDetails;
        public List<string> PhysBoneOverLimitDetails;
        public List<string> NullAvatarAnimatorDetails;

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
        private const int DetailCap = 15;

        private static readonly string[] MobileShaderPrefixes =
        {
            "VRChat/Mobile/", "Hidden/VRCFallback/", "Mobile/"
        };

        private static readonly string[] ReportPackageIds =
        {
            "com.vrchat.avatars",
            "nadena.dev.ndmf",
            "nadena.dev.modular-avatar",
            "com.anatawa12.avatar-optimizer",
            "com.vrcfury.vrcfury"
        };

        public static AvatarScanResult Scan(GameObject avatar)
        {
            var r = new AvatarScanResult
            {
                Issues = new List<AvatarIssue>(),
                NullMatDetails = new List<string>(),
                BrokenShaderDetails = new List<string>(),
                MissingMeshDetails = new List<string>(),
                MissingScriptDetails = new List<string>(),
                BadAudioDetails = new List<string>(),
                PhysBoneOverLimitDetails = new List<string>(),
                NullAvatarAnimatorDetails = new List<string>()
            };
            if (avatar == null) return r;

            var countedMeshes = new HashSet<Mesh>();
            var textures = VtoolAvatarFixes.CollectTextures(avatar);
            bool boundsInit = false;
            Bounds bounds = default;

            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                r.SkinnedMeshCount++;
                if (smr.sharedMesh == null)
                {
                    r.MissingMeshes++;
                    AddDetail(r.MissingMeshDetails, VtoolAvatarFixes.HierarchyPath(smr.transform) + " (SkinnedMeshRenderer)");
                }
                else if (countedMeshes.Add(smr.sharedMesh))
                    r.PolyCount += smr.sharedMesh.triangles.Length / 3;
            }

            foreach (var mf in avatar.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null) continue;
                if (mf.sharedMesh == null)
                {
                    r.MissingMeshes++;
                    AddDetail(r.MissingMeshDetails, VtoolAvatarFixes.HierarchyPath(mf.transform) + " (MeshFilter)");
                }
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
                string path = VtoolAvatarFixes.HierarchyPath(rend.transform);
                string typeName = rend.GetType().Name;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null)
                    {
                        r.NullMaterialSlots++;
                        AddDetail(r.NullMatDetails, path + " (" + typeName + ") slot " + i);
                        continue;
                    }
                    if (IsBrokenShader(m.shader))
                    {
                        r.BrokenShaders++;
                        string shaderName = m.shader != null ? m.shader.name : "(null)";
                        AddDetail(r.BrokenShaderDetails, path + " mat=" + m.name + " shader=" + shaderName);
                    }
                    else if (!IsQuestShader(m.shader))
                        r.QuestBadShaders++;
                }
            }

            r.AvatarHeightMeters = boundsInit ? bounds.size.y : 0f;
            r.BoneCount = CountBones(avatar);

            foreach (var t in avatar.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missing > 0)
                {
                    r.MissingScripts += missing;
                    AddDetail(r.MissingScriptDetails, VtoolAvatarFixes.HierarchyPath(t) + " x" + missing);
                }
                var s = t.localScale;
                if (s.x < 0 || s.y < 0 || s.z < 0) r.NegativeScales++;
                if (s != Vector3.one) r.NonUnitScales++;
            }

            r.RootScaleIsOne = avatar.transform.localScale == Vector3.one;
            r.LegacyDynamicBones = CountType(avatar, "DynamicBone");
            AnalyzePhysBones(avatar, ref r);
            r.ParticleCount = avatar.GetComponentsInChildren<ParticleSystem>(true).Length;
            r.OtherAvatarsInScene = CountOtherAvatars(avatar);

            foreach (var a in avatar.GetComponentsInChildren<AudioSource>(true))
            {
                if (a == null) continue;
                if (a.volume > 0.8f || a.spatialBlend < 1f)
                {
                    r.BadAudioCount++;
                    AddDetail(r.BadAudioDetails, VtoolAvatarFixes.HierarchyPath(a.transform) +
                        " vol=" + a.volume.ToString("F2") + " spatial=" + a.spatialBlend.ToString("F2"));
                }
                if (a.playOnAwake) r.AudioPlayOnAwake++;
            }

            AnalyzeNullAvatarAnimators(avatar, ref r);
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
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_MISSING_SCRIPTS", "issue.missing_scripts", "hint.missing_scripts",
                    r.MissingScriptDetails, r.MissingScripts));
            if (r.NullMaterialSlots > 0)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_NULL_MATS", "issue.null_mats", "hint.null_mats",
                    r.NullMatDetails, r.NullMaterialSlots));
            if (r.BrokenShaders > 0)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_BROKEN_SHADERS", "issue.broken_shaders", "hint.broken_shaders",
                    r.BrokenShaderDetails, r.BrokenShaders));
            if (r.MissingMeshes > 0)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_MISSING_MESHES", "issue.missing_meshes", "hint.missing_meshes",
                    r.MissingMeshDetails, r.MissingMeshes));
            if (r.PolyCount > 200000)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_EXTREME_POLY", "issue.extreme_poly", "hint.extreme_poly",
                    null, r.PolyCount.ToString("N0")));
            if (r.PhysBoneCount > 256)
                r.Issues.Add(IssueF(IssueSeverity.Blocker, "E_PHYSBONE_LIMIT", "issue.physbone_limit", "hint.physbone_limit",
                    null, r.PhysBoneCount));
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
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_NEG_SCALE", "issue.neg_scale", "hint.neg_scale", null, r.NegativeScales));
            if (r.NonUnitScales > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_NONUNIT_SCALE", "issue.nonunit_scale", "hint.nonunit_scale", null, r.NonUnitScales));
            if (r.PolyCount > 70000)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_HIGH_POLY", "issue.high_poly", "hint.high_poly", null, r.PolyCount.ToString("N0")));
            else if (r.PolyCount > QuestPolyLimit)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_QUEST_POLY", "issue.quest_poly", "hint.quest_poly", null, QuestPolyLimit.ToString("N0")));
            if (r.SkinnedMeshCount > 8)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_SKINNED_MANY", "issue.skinned_many", "hint.skinned_many", null, r.SkinnedMeshCount));
            if (r.MaterialSlots > 16)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_MATS_MANY", "issue.mats_many", "hint.mats_many", null, r.MaterialSlots));
            if (r.Textures4K > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_TEX_4K", "issue.tex_4k", "hint.tex_4k", null, r.Textures4K));
            if (r.TexturesOver2K > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_TEX_2K", "issue.tex_2k", "hint.tex_2k", null, r.TexturesOver2K));
            if (r.TextureMemoryMB > 150)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_TEX_MEM", "issue.tex_mem", "hint.tex_mem", null, r.TextureMemoryMB.ToString("F0")));
            if (r.TexturesNoMipmaps > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_NO_MIP", "issue.no_mip", "hint.use_tex_tab", null, r.TexturesNoMipmaps));
            if (r.LegacyDynamicBones > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_DYNBONE", "issue.dynbone", "hint.dynbone", null, r.LegacyDynamicBones));
            if (r.PhysBoneCount > 32 && r.PhysBoneCount <= 256)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PB_POOR", "issue.pb_poor", "hint.pb_poor", null, r.PhysBoneCount));
            if (r.PhysBoneCount > 8)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PB_QUEST", "issue.pb_quest", "hint.pb_quest", null, r.PhysBoneCount));
            if (r.PhysBoneAffectedTransforms > 64)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PB_QUEST_XF", "issue.pb_quest_xf", "hint.pb_quest_xf", null, r.PhysBoneAffectedTransforms));
            if (r.PhysBonesOverTransformLimit > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PB_XF_LIMIT", "issue.pb_xf_limit", "hint.pb_xf_limit",
                    r.PhysBoneOverLimitDetails, r.PhysBonesOverTransformLimit));
            if (r.PhysBoneColliderCount > 16)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PB_COLLIDERS", "issue.pb_colliders", "hint.pb_colliders", null, r.PhysBoneColliderCount));
            if (r.BadAudioCount > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_BAD_AUDIO", "issue.bad_audio", "hint.fix_all_audio",
                    r.BadAudioDetails, r.BadAudioCount));
            if (r.AudioPlayOnAwake > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PLAY_AWAKE", "issue.play_awake", "hint.play_awake", null, r.AudioPlayOnAwake));
            if (r.ParticleCount > 16)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_PARTICLES", "issue.particles", "hint.particles", null, r.ParticleCount));
            if (r.OtherAvatarsInScene > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_OTHER_AVATARS", "issue.other_avatars", "hint.other_avatars", null, r.OtherAvatarsInScene));
            if (r.QuestBadShaders > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_QUEST_MATS", "issue.quest_mats", "hint.quest_mats", null, r.QuestBadShaders));
            if (r.AvatarHeightMeters > MaxHeight || (r.AvatarHeightMeters > 0 && r.AvatarHeightMeters < MinHeight))
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_HEIGHT", "issue.height", "hint.height", null, r.AvatarHeightMeters.ToString("F2")));
            if (r.NullAvatarAnimatorCount > 0)
                r.Issues.Add(IssueF(IssueSeverity.Warning, "W_NULL_AVATAR_ANIMATOR", "issue.null_avatar_animator", "hint.null_avatar_animator",
                    r.NullAvatarAnimatorDetails, r.NullAvatarAnimatorCount));
        }

        private static AvatarIssue Issue(IssueSeverity s, string code, string msgKey, string hintKey) =>
            new AvatarIssue
            {
                Severity = s,
                Code = code,
                MsgKey = msgKey,
                HintKey = hintKey,
                Message = VtoolLocalization.T(msgKey),
                FixHint = VtoolLocalization.T(hintKey)
            };

        private static AvatarIssue IssueF(IssueSeverity s, string code, string msgKey, string hintKey,
            List<string> related, params object[] args) =>
            new AvatarIssue
            {
                Severity = s,
                Code = code,
                MsgKey = msgKey,
                HintKey = hintKey,
                FormatArgs = args,
                RelatedObjects = related != null && related.Count > 0 ? new List<string>(related) : null,
                Message = VtoolLocalization.TF(msgKey, args),
                FixHint = VtoolLocalization.T(hintKey)
            };

        public static string BuildDiagnosticReport(GameObject avatar, AvatarScanResult scan, string packageVersion,
            bool hasRollback = false)
        {
            var sb = new StringBuilder(8192);
            sb.AppendLine("=== Vtool Diagnostic Report ===");
            sb.AppendLine("ReportVersion: 2");
            sb.AppendLine("Vtool: " + (packageVersion ?? "?"));
            sb.AppendLine("Unity: " + Application.unityVersion);
            sb.AppendLine("Platform: " + Application.platform);
            sb.AppendLine("ActiveBuildTarget: " + EditorUserBuildSettings.activeBuildTarget);
            sb.AppendLine("OS: " + SystemInfo.operatingSystem);
            sb.AppendLine("UI Language: " + VtoolLocalization.Language);
            sb.AppendLine("Time: " + System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            sb.AppendLine();

            AppendEnvironment(sb);
            AppendAvatarSection(sb, avatar, scan, hasRollback);
            AppendCodesAndIssues(sb, scan);
            if (avatar != null)
            {
                AppendMeshMaterials(sb, avatar);
                AppendHeadSnapshot(sb, avatar);
            }
            AppendNdmfNote(sb);
            AppendStats(sb, scan);
            AppendUserNotes(sb);
            sb.AppendLine("=== End ===");
            return sb.ToString();
        }

        private static void AppendEnvironment(StringBuilder sb)
        {
            sb.AppendLine("--- Environment ---");
            Dictionary<string, string> versions = null;
            try
            {
                var packages = PackageInfo.GetAllRegisteredPackages();
                if (packages != null)
                {
                    versions = new Dictionary<string, string>();
                    foreach (var p in packages)
                    {
                        if (p == null || string.IsNullOrEmpty(p.name)) continue;
                        versions[p.name] = p.version ?? "?";
                    }
                }
            }
            catch
            {
                versions = null;
            }

            foreach (var id in ReportPackageIds)
            {
                if (versions != null && versions.TryGetValue(id, out var ver))
                    sb.AppendLine(id + ": " + ver);
                else
                    sb.AppendLine(id + ": not installed");
            }
            sb.AppendLine();
        }

        private static void AppendAvatarSection(StringBuilder sb, GameObject avatar, AvatarScanResult scan, bool hasRollback)
        {
            sb.AppendLine("--- Avatar ---");
            if (avatar == null)
            {
                sb.AppendLine("Name: (none)");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("Name: " + avatar.name);
            sb.AppendLine("Scene: " + (avatar.scene.IsValid() ? avatar.scene.name : "(none)"));
            sb.AppendLine("Hierarchy path: " + VtoolAvatarFixes.HierarchyPath(avatar.transform));

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(avatar);
            if (string.IsNullOrEmpty(prefabPath))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(avatar);
                if (src != null)
                    prefabPath = AssetDatabase.GetAssetPath(src);
            }
            sb.AppendLine("Prefab: " + (string.IsNullOrEmpty(prefabPath) ? "scene instance" : prefabPath));

            string blueprint = VtoolAvatarFixes.GetBlueprintId(avatar);
            sb.AppendLine("Blueprint ID: " + (string.IsNullOrEmpty(blueprint) ? "(unset)" : blueprint));

            var scale = avatar.transform.localScale;
            sb.AppendLine("Root scale: (" + scale.x.ToString("G") + ", " + scale.y.ToString("G") + ", " + scale.z.ToString("G") + ")");
            sb.AppendLine("Animator: " + (scan.HasHumanoidAnimator ? "Humanoid" : "missing/non-humanoid") +
                          " | Chest: " + YesNo(scan.HasChestBone));
            sb.AppendLine("Descriptor: " + YesNo(scan.HasDescriptor) +
                          " | Pipeline: " + YesNo(scan.HasPipelineManager) +
                          " | View: " + YesNo(scan.HasViewPosition) +
                          " | LipSync: " + YesNo(scan.HasLipSync));
            sb.AppendLine("Rollback available: " + YesNo(hasRollback));
            sb.AppendLine();
        }

        private static void AppendCodesAndIssues(StringBuilder sb, AvatarScanResult scan)
        {
            if (scan.Issues == null || scan.Issues.Count == 0)
            {
                sb.AppendLine("--- Codes ---");
                sb.AppendLine("(none — scan looks OK)");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("--- Codes ---");
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

            sb.AppendLine("--- Issues (detailed) ---");
            foreach (var i in scan.Issues)
            {
                string sev = i.Severity == IssueSeverity.Blocker ? "BLOCKER" :
                    i.Severity == IssueSeverity.Warning ? "WARNING" : "INFO";
                sb.Append('[').Append(sev).Append("] ").Append(i.Code ?? "?").AppendLine();

                string msg = ResolveEnMessage(i);
                string hint = ResolveEnHint(i);
                if (!string.IsNullOrEmpty(msg))
                    sb.Append("  ").AppendLine(msg);
                if (!string.IsNullOrEmpty(hint))
                    sb.Append("  Fix: ").AppendLine(hint);

                if (i.RelatedObjects != null && i.RelatedObjects.Count > 0)
                {
                    sb.AppendLine("  Objects:");
                    foreach (var obj in i.RelatedObjects)
                        sb.Append("    - ").AppendLine(obj);
                }
            }
            sb.AppendLine();
        }

        private static string ResolveEnMessage(AvatarIssue i)
        {
            if (string.IsNullOrEmpty(i.MsgKey)) return i.Message ?? string.Empty;
            if (i.FormatArgs != null && i.FormatArgs.Length > 0)
                return VtoolLocalization.TFEn(i.MsgKey, i.FormatArgs);
            return VtoolLocalization.TEn(i.MsgKey);
        }

        private static string ResolveEnHint(AvatarIssue i)
        {
            if (string.IsNullOrEmpty(i.HintKey)) return i.FixHint ?? string.Empty;
            return VtoolLocalization.TEn(i.HintKey);
        }

        private static void AppendMeshMaterials(StringBuilder sb, GameObject avatar)
        {
            sb.AppendLine("--- Mesh / materials ---");

            var meshes = new List<(string path, int tris, string rootBone, int slots, int nullSlots)>();
            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                int tris = smr.sharedMesh != null ? smr.sharedMesh.triangles.Length / 3 : 0;
                int nullSlots = 0;
                var mats = smr.sharedMaterials;
                if (mats != null)
                    foreach (var m in mats)
                        if (m == null) nullSlots++;
                meshes.Add((
                    VtoolAvatarFixes.HierarchyPath(smr.transform),
                    tris,
                    smr.rootBone != null ? smr.rootBone.name : "(null)",
                    mats != null ? mats.Length : 0,
                    nullSlots));
            }
            meshes.Sort((a, b) => b.tris.CompareTo(a.tris));

            sb.AppendLine("Skinned meshes (by tris, top 20):");
            int shown = 0;
            foreach (var m in meshes)
            {
                if (shown >= 20) break;
                sb.AppendLine("  " + m.path + ": " + m.tris.ToString("N0") + " tris | rootBone=" + m.rootBone +
                              " | materials=" + m.slots + " | missingSlots=" + m.nullSlots);
                shown++;
            }
            if (meshes.Count == 0)
                sb.AppendLine("  (none)");

            var shaderCounts = new Dictionary<string, int>();
            var broken = new List<string>();
            foreach (var rend in avatar.GetComponentsInChildren<Renderer>(true))
            {
                if (rend == null) continue;
                var mats = rend.sharedMaterials;
                if (mats == null) continue;
                string path = VtoolAvatarFixes.HierarchyPath(rend.transform);
                foreach (var mat in mats)
                {
                    if (mat == null) continue;
                    if (IsBrokenShader(mat.shader))
                    {
                        if (broken.Count < DetailCap)
                            broken.Add(path + " mat=" + mat.name + " shader=" +
                                       (mat.shader != null ? mat.shader.name : "(null)"));
                        continue;
                    }
                    if (mat.shader == null) continue;
                    string sn = mat.shader.name;
                    if (IsQuestShader(mat.shader)) continue;
                    if (!shaderCounts.ContainsKey(sn)) shaderCounts[sn] = 0;
                    shaderCounts[sn]++;
                }
            }

            sb.AppendLine("Broken/pink shaders:");
            if (broken.Count == 0) sb.AppendLine("  (none)");
            else foreach (var b in broken) sb.AppendLine("  " + b);

            sb.AppendLine("Non-Quest shaders (top 15 by count):");
            var topShaders = shaderCounts.OrderByDescending(kv => kv.Value).Take(15).ToList();
            if (topShaders.Count == 0) sb.AppendLine("  (none)");
            else foreach (var kv in topShaders)
                sb.AppendLine("  " + kv.Value + "x " + kv.Key);
            sb.AppendLine();
        }

        private static void AppendHeadSnapshot(StringBuilder sb, GameObject avatar)
        {
            sb.AppendLine("--- Head / face snapshot ---");
            var names = VtoolAvatarFixes.CollectHeadRelatedRendererNames(avatar, 30);
            if (names == null || names.Count == 0)
                sb.AppendLine("(none detected)");
            else
                foreach (var n in names)
                    sb.AppendLine("  " + n);
            sb.AppendLine();
        }

        private static void AppendNdmfNote(StringBuilder sb)
        {
            sb.AppendLine("--- NDMF / Play Mode ---");
            bool ndmf = IsPackageInstalled("nadena.dev.ndmf");
            sb.AppendLine("NDMF installed: " + YesNo(ndmf));
            if (ndmf)
            {
                sb.AppendLine(
                    "If Play Mode or upload aborts with BuildFrameworkOptimizeHook, paste the FIRST Console exception ABOVE that line (Avatar Optimizer / Modular Avatar / VRCFury). The VRChat callback line alone is not the root cause.");
            }
            sb.AppendLine();
        }

        private static void AppendStats(StringBuilder sb, AvatarScanResult scan)
        {
            sb.AppendLine("--- Stats ---");
            sb.AppendLine("Blockers: " + scan.BlockerCount + " | Warnings: " + scan.WarningCount);
            sb.AppendLine("Polys: " + scan.PolyCount + " | Skinned: " + scan.SkinnedMeshCount +
                          " | Mats: " + scan.MaterialSlots + " | Bones: " + scan.BoneCount);
            sb.AppendLine("Height: " + scan.AvatarHeightMeters.ToString("F2") + "m | PhysBones: " + scan.PhysBoneCount +
                          " (xf:" + scan.PhysBoneAffectedTransforms + ", colliders:" + scan.PhysBoneColliderCount +
                          ", over256xf:" + scan.PhysBonesOverTransformLimit + ") | Particles: " + scan.ParticleCount);
            sb.AppendLine("Textures: " + scan.TextureCount + " | 4K: " + scan.Textures4K + " | >2K: " + scan.TexturesOver2K +
                          " | ~" + scan.TextureMemoryMB.ToString("F0") + "MB | NoMip: " + scan.TexturesNoMipmaps);
            sb.AppendLine("Descriptor: " + scan.HasDescriptor + " | Pipeline: " + scan.HasPipelineManager +
                          " | Humanoid: " + scan.HasHumanoidAnimator);
            sb.AppendLine("Chest: " + scan.HasChestBone + " | View: " + scan.HasViewPosition + " | LipSync: " + scan.HasLipSync);
            sb.AppendLine("MissingScripts: " + scan.MissingScripts + " | NullMats: " + scan.NullMaterialSlots +
                          " | BrokenShaders: " + scan.BrokenShaders + " | MissingMeshes: " + scan.MissingMeshes);
            sb.AppendLine("DynamicBones: " + scan.LegacyDynamicBones + " | BadAudio: " + scan.BadAudioCount +
                          " | PlayOnAwake: " + scan.AudioPlayOnAwake);
            sb.AppendLine("QuestBadShaders: " + scan.QuestBadShaders + " | OtherAvatars: " + scan.OtherAvatarsInScene +
                          " | NullAvatarAnimators: " + scan.NullAvatarAnimatorCount);
            sb.AppendLine();
        }

        private static void AppendUserNotes(StringBuilder sb)
        {
            sb.AppendLine("--- User notes (fill before sending) ---");
            sb.AppendLine("What I clicked: [ ] Check  [ ] Fix All  [ ] Individual: _______________");
            sb.AppendLine("What broke: ");
            sb.AppendLine("Console error (paste below):");
            sb.AppendLine();
        }

        private static bool IsPackageInstalled(string packageId)
        {
            try
            {
                var packages = PackageInfo.GetAllRegisteredPackages();
                if (packages == null) return false;
                foreach (var p in packages)
                    if (p != null && p.name == packageId) return true;
            }
            catch { /* ignore */ }
            return false;
        }

        private static string YesNo(bool v) => v ? "Yes" : "No";

        private static void AddDetail(List<string> list, string line)
        {
            if (list == null || list.Count >= DetailCap || string.IsNullOrEmpty(line)) return;
            list.Add(line);
        }

        private static void AnalyzeNullAvatarAnimators(GameObject avatar, ref AvatarScanResult r)
        {
            var rootAnim = avatar.GetComponent<Animator>();
            foreach (var anim in avatar.GetComponentsInChildren<Animator>(true))
            {
                if (anim == null) continue;
                // Root humanoid is expected; flag any Animator with no Avatar assigned.
                // Skip only if this is the root humanoid with a valid avatar.
                if (anim == rootAnim && anim.isHuman && anim.avatar != null) continue;
                if (anim.avatar != null) continue;

                r.NullAvatarAnimatorCount++;
                AddDetail(r.NullAvatarAnimatorDetails,
                    VtoolAvatarFixes.HierarchyPath(anim.transform) +
                    (anim == rootAnim ? " (root)" : " (child)") +
                    " enabled=" + anim.enabled);
            }
        }

        private static void AnalyzePhysBones(GameObject avatar, ref AvatarScanResult r)
        {
            var pbType = VtoolAvatarFixes.GetTypeSafe("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
            var colliderType = VtoolAvatarFixes.GetTypeSafe("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider");

            if (colliderType != null)
                r.PhysBoneColliderCount = avatar.GetComponentsInChildren(colliderType, true).Length;

            if (pbType == null)
            {
                r.PhysBoneCount = 0;
                return;
            }

            var components = avatar.GetComponentsInChildren(pbType, true);
            r.PhysBoneCount = components.Length;
            int totalTransforms = 0;
            int overLimit = 0;

            foreach (var obj in components)
            {
                var c = obj as Component;
                if (c == null) continue;

                Transform root = c.transform;
                if (VtoolAvatarFixes.TryGetMember(c, pbType, "rootTransform", out var rootObj) && rootObj is Transform rt && rt != null)
                    root = rt;

                int count = CountHierarchyTransforms(root);
                totalTransforms += count;
                if (count > 256)
                {
                    overLimit++;
                    AddDetail(r.PhysBoneOverLimitDetails,
                        VtoolAvatarFixes.HierarchyPath(c.transform) + " transforms=" + count +
                        " root=" + (root != null ? root.name : "(null)"));
                }
            }

            r.PhysBoneAffectedTransforms = totalTransforms;
            r.PhysBonesOverTransformLimit = overLimit;
        }

        private static int CountHierarchyTransforms(Transform root)
        {
            if (root == null) return 0;
            int n = 1;
            for (int i = 0; i < root.childCount; i++)
                n += CountHierarchyTransforms(root.GetChild(i));
            return n;
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
