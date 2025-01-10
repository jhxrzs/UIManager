#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UIManager
{
    /// <summary>
    /// 把UI的互斥组标记出来
    /// </summary>
    [InitializeOnLoad]
    public class UIBaseHierarchyLabel
    {
        /// <summary>
        ///  UI的互斥组
        /// </summary>
        private static FieldInfo _mutexGroupType;

        /// <summary>
        ///  UI的互斥组
        /// </summary>
        private static FieldInfo MutexGroupFieldInfo => _mutexGroupType ??= typeof(UIBase).GetField("mutexGroup",
            BindingFlags.NonPublic | BindingFlags.Instance);

        static UIBaseHierarchyLabel()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
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
                    int mutexGroupValue = MutexGroupFieldInfo.GetValue(uiBase) is int value ? value : 0;

                    string state = uiBase.State is UIState.Showed or UIState.Showing ? "S" : "H";
                    // 设置标签样式
                    GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                    {
                        normal =
                        {
                            textColor = Color.yellow
                        },
                        alignment = TextAnchor.MiddleRight
                    };

                    // 在游戏物体名称右侧绘制 Label
                    float labelWidth = 100;
                    Rect labelRect = new Rect(selectionRect);
                    labelRect.x = labelRect.xMax - labelWidth;
                    labelRect.width = labelWidth;
                    EditorGUI.LabelField(labelRect, mutexGroupValue.ToString() + " " + state, labelStyle);
                }
            }
        }
    }
}
#endif