using UnityEditor;
using UnityEngine;

namespace XVR.Tools
{
    // Dedicated menu entries so the window stays discoverable under Vtool and Tools.
    public static class VtoolMenu
    {
        public const string WindowTitle = "Vtool";

        [MenuItem("Vtool/Avatar Auto-Fixer Pro", false, 0)]
        [MenuItem("Tools/Vtool/Avatar Auto-Fixer Pro", false, 0)]
        public static void OpenAutoFixer()
        {
            var w = EditorWindow.GetWindow<VRCAvatarAutoFixer>(WindowTitle);
            w.minSize = new Vector2(440, 640);
            w.Show();
            w.Focus();
        }

        [MenuItem("Vtool/Avatar Auto-Fixer Pro", true)]
        [MenuItem("Tools/Vtool/Avatar Auto-Fixer Pro", true)]
        public static bool OpenAutoFixerValidate() => true;
    }
}
