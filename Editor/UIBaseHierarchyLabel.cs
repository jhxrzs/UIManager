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
                EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
            };
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
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