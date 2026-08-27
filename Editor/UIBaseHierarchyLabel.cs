#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace JH
{
    /// <summary>
    /// 把UI的互斥组标记出来
    /// </summary>
    [InitializeOnLoad]
    public class UIBaseHierarchyLabel
    {
        /// <summary>
        /// GUIStyle
        /// </summary>
        private static GUIStyle _labelStyle;

        /// <summary>
        /// GUIStyle
        /// </summary>
        private static GUIStyle LabelStyle =>
            _labelStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.yellow },
                alignment = TextAnchor.MiddleRight
            };
        
        static UIBaseHierarchyLabel()
        {
            // 确保在编辑器初始化完成后再注册
            EditorApplication.delayCall += () =>
            {
#if UNITY_6000_4_OR_NEWER
                // Unity 6.4及更高版本改用EntityId回调，避免继续订阅已经废弃的InstanceID接口。
                EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyWindowItemOnGUI;
#else
                // Unity 6.4以前继续保留原有InstanceID兼容入口。
                EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
#endif
            };
        }

#if UNITY_6000_4_OR_NEWER
        /// <summary>
        ///  在Hierarchy可见项右侧绘制UI状态标签。
        /// </summary>
        /// <param name="entityId">当前Hierarchy项对应的实体标识。</param>
        /// <param name="selectionRect">当前Hierarchy项的绘制区域。</param>
        private static void OnHierarchyWindowItemOnGUI(EntityId entityId, Rect selectionRect)
        {
            // 新版编辑器通过EntityId还原Hierarchy中的GameObject。
            var gameObject = EditorUtility.EntityIdToObject(entityId) as GameObject;
            DrawHierarchyLabel(gameObject, selectionRect);
        }
#else
        /// <summary>
        ///  在Hierarchy可见项右侧绘制UI状态标签。
        /// </summary>
        /// <param name="instanceID">当前Hierarchy项对应的实例标识。</param>
        /// <param name="selectionRect">当前Hierarchy项的绘制区域。</param>
        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            // 旧版编辑器通过InstanceID还原Hierarchy中的GameObject。
            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            DrawHierarchyLabel(gameObject, selectionRect);
        }
#endif

        /// <summary>
        ///  为包含UIBase的Hierarchy节点绘制互斥组和显隐状态。
        /// </summary>
        /// <param name="gameObject">当前Hierarchy项对应的GameObject。</param>
        /// <param name="selectionRect">当前Hierarchy项的绘制区域。</param>
        private static void DrawHierarchyLabel(GameObject gameObject, Rect selectionRect)
        {
            if (gameObject != null)
            {
                var uiBase = gameObject.GetComponent<UIBase>();
                if (uiBase != null)
                {
                    // 获取 mutexGroup 的值
                    int mutexGroupValue = new SerializedObject(uiBase).FindProperty("mutexGroup").intValue;
                    string state = uiBase.State is UIState.Showed or UIState.Showing ? "S" : "H";

                    // 在游戏物体名称右侧绘制 Label
                    float labelWidth = 100;
                    Rect labelRect = new Rect(selectionRect);
                    labelRect.x = labelRect.xMax - labelWidth;
                    labelRect.width = labelWidth;
                    EditorGUI.LabelField(labelRect, mutexGroupValue + " " + state, LabelStyle);
                }
            }
        }
    }
}
#endif
