using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace XVR.Tools
{
    public static class VtoolAvatarRollback
    {
        private const string ContainerName = "__VtoolRollback";

        private struct TextureState
        {
            public string Path;
            public int MaxSize;
            public bool Mipmaps;
        }

        private class Session
        {
            public GameObject Backup;
            public List<TextureState> Textures = new List<TextureState>();
        }

        private static readonly Dictionary<int, Session> Sessions = new Dictionary<int, Session>();

        public static bool HasRollback(GameObject avatar) =>
            avatar != null &&
            Sessions.TryGetValue(avatar.GetInstanceID(), out var session) &&
            session.Backup != null;

        public static void EnsureCapture(GameObject avatar)
        {
            if (avatar == null || HasRollback(avatar)) return;
            Capture(avatar);
        }

        public static void Capture(GameObject avatar)
        {
            if (avatar == null) return;

            Clear(avatar);

            var container = GetContainer();
            var backup = Object.Instantiate(avatar, container.transform);
            backup.name = avatar.name + "_Rollback";
            backup.SetActive(false);
            backup.hideFlags = HideFlags.HideInHierarchy;

            Sessions[avatar.GetInstanceID()] = new Session { Backup = backup };
        }

        public static void RecordTextures(GameObject avatar, IEnumerable<Texture> textures)
        {
            if (avatar == null || textures == null) return;

            EnsureCapture(avatar);
            if (!Sessions.TryGetValue(avatar.GetInstanceID(), out var session)) return;

            foreach (var tex in textures)
            {
                if (tex == null) continue;
                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path)) continue;

                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;

                bool alreadySaved = false;
                for (int i = 0; i < session.Textures.Count; i++)
                {
                    if (session.Textures[i].Path != path) continue;
                    alreadySaved = true;
                    break;
                }

                if (alreadySaved) continue;

                session.Textures.Add(new TextureState
                {
                    Path = path,
                    MaxSize = imp.maxTextureSize,
                    Mipmaps = imp.mipmapEnabled
                });
            }
        }

        public static GameObject Restore(GameObject avatar)
        {
            if (avatar == null || !HasRollback(avatar)) return avatar;

            var session = Sessions[avatar.GetInstanceID()];
            var backup = session.Backup;
            if (backup == null) return avatar;

            int sourceId = avatar.GetInstanceID();

            var parent = avatar.transform.parent;
            int sibling = avatar.transform.GetSiblingIndex();
            string name = avatar.name;
            bool active = avatar.activeSelf;
            int layer = avatar.layer;
            string tag = avatar.tag;

            var restored = Object.Instantiate(backup);
            restored.name = name;
            restored.tag = tag;
            restored.layer = layer;
            restored.transform.SetParent(parent, false);
            restored.transform.SetSiblingIndex(sibling);
            restored.SetActive(active);

            Undo.RegisterCreatedObjectUndo(restored, "Vtool Rollback");
            Undo.DestroyObjectImmediate(avatar);

            RestoreTextures(session.Textures);
            ClearByInstanceId(sourceId);

            VtoolAvatarFixes.MarkDirty();
            Selection.activeGameObject = restored;
            return restored;
        }

        public static void Clear(GameObject avatar)
        {
            if (avatar == null) return;
            ClearByInstanceId(avatar.GetInstanceID());
        }

        private static void ClearByInstanceId(int instanceId)
        {
            if (!Sessions.TryGetValue(instanceId, out var session)) return;

            if (session.Backup != null)
                Object.DestroyImmediate(session.Backup);

            Sessions.Remove(instanceId);
        }

        private static void RestoreTextures(List<TextureState> states)
        {
            if (states == null || states.Count == 0) return;

            int restored = 0;
            foreach (var state in states)
            {
                var imp = AssetImporter.GetAtPath(state.Path) as TextureImporter;
                if (imp == null) continue;

                bool changed = false;
                if (imp.maxTextureSize != state.MaxSize)
                {
                    imp.maxTextureSize = state.MaxSize;
                    changed = true;
                }
                if (imp.mipmapEnabled != state.Mipmaps)
                {
                    imp.mipmapEnabled = state.Mipmaps;
                    changed = true;
                }

                if (!changed) continue;
                imp.SaveAndReimport();
                restored++;
            }

            if (restored > 0)
                AssetDatabase.SaveAssets();
        }

        private static Transform GetContainer()
        {
            var existing = GameObject.Find(ContainerName);
            if (existing != null) return existing.transform;

            var container = new GameObject(ContainerName);
            container.hideFlags = HideFlags.HideInHierarchy;
            Undo.RegisterCreatedObjectUndo(container, "Vtool Rollback");
            return container.transform;
        }
    }
}
