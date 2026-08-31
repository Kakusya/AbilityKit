using UnityEditor;

namespace AbilityKit.Game.Editor.Gizmos
{
    /// <summary>
    /// 用 EditorPrefs 持久化 MOBA Gizmo 设置，避免 Domain Reload 后丢失。
    /// 仅做最小数据序列化（int mask），不做版本号管理——后续若需要兼容旧值，
    /// 再扩展为版本化结构即可。
    /// </summary>
    internal static class MobaGizmoSettingsPersistence
    {
        private const string EnabledMaskKey = "AbilityKit.Game.Editor.Gizmos.EnabledMask.v1";

        public static int LoadEnabledMask()
        {
            return EditorPrefs.GetInt(EnabledMaskKey, 0);
        }

        public static void SaveEnabledMask(int mask)
        {
            EditorPrefs.SetInt(EnabledMaskKey, mask);
        }

        public static void EnsureDefaults(int defaultMask)
        {
            if (!EditorPrefs.HasKey(EnabledMaskKey))
            {
                EditorPrefs.SetInt(EnabledMaskKey, defaultMask);
            }
        }

        public static void Clear()
        {
            EditorPrefs.DeleteKey(EnabledMaskKey);
        }
    }
}