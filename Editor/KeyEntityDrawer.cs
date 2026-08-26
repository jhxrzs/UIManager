#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace JH
{
    /// <summary>
    ///  键盘显隐交互实体的Inspector绘制器。
    /// </summary>
    [CustomPropertyDrawer(typeof(KeyEntity))]
    public class KeyEntityDrawer : PropertyDrawer
    {
        /// <summary>
        ///  Inspector字段之间的垂直间距。
        /// </summary>
        private const float FieldSpacing = 2f;

        /// <summary>
        ///  无效按键提示占用的行数。
        /// </summary>
        private const float ErrorLineCount = 2f;

        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 折叠状态只需要显示标题行。
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

            // 展开后固定显示标题、行为、异步选项和按键输入框。
            var height = EditorGUIUtility.singleLineHeight * 4f + FieldSpacing * 3f;
            var invalidKeyNamesProperty = property.FindPropertyRelative("invalidKeyNames");

            // 存在无效名称时额外预留错误提示区域。
            if (invalidKeyNamesProperty != null &&
                !string.IsNullOrWhiteSpace(invalidKeyNamesProperty.stringValue))
            {
                height += EditorGUIUtility.singleLineHeight * ErrorLineCount + FieldSpacing;
            }

            return height;
        }

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 将整个实体纳入Prefab覆盖和多对象编辑的属性范围。
            EditorGUI.BeginProperty(position, label, property);

            // 第一行绘制可折叠标题。
            var lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);

            // 折叠状态无需绘制子字段。
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            // 获取实体中需要直接编辑的序列化字段。
            var actionProperty = property.FindPropertyRelative("action");
            var isAsyncProperty = property.FindPropertyRelative("<IsAsync>k__BackingField");
            var keyNamesProperty = property.FindPropertyRelative("keyNames");
            var invalidKeyNamesProperty = property.FindPropertyRelative("invalidKeyNames");

            // 子字段相对列表元素标题缩进一级。
            EditorGUI.indentLevel++;

            // 第二行绘制显隐行为。
            lineRect.y += EditorGUIUtility.singleLineHeight + FieldSpacing;
            if (actionProperty != null)
                EditorGUI.PropertyField(lineRect, actionProperty, new GUIContent("显隐行为"));

            // 第三行绘制同步或异步选项。
            lineRect.y += EditorGUIUtility.singleLineHeight + FieldSpacing;
            if (isAsyncProperty != null)
                EditorGUI.PropertyField(lineRect, isAsyncProperty, new GUIContent("是否异步"));

            // 第四行只绘制字符串输入框，不暴露数量庞大的Key枚举列表。
            lineRect.y += EditorGUIUtility.singleLineHeight + FieldSpacing;
            if (keyNamesProperty != null)
            {
                var keyNamesLabel = new GUIContent("触发按键",
                    "多个按键使用空格分隔，例如：A B C LeftCtrl；空格键请输入Space");
                EditorGUI.PropertyField(lineRect, keyNamesProperty, keyNamesLabel);
            }

            // 拼写错误直接显示在对应配置下方，避免无效按键被静默忽略。
            if (invalidKeyNamesProperty != null &&
                !string.IsNullOrWhiteSpace(invalidKeyNamesProperty.stringValue))
            {
                lineRect.y += EditorGUIUtility.singleLineHeight + FieldSpacing;
                lineRect.height = EditorGUIUtility.singleLineHeight * ErrorLineCount;
                EditorGUI.HelpBox(lineRect,
                    "无法识别以下按键：" + invalidKeyNamesProperty.stringValue,
                    MessageType.Error);
            }

            // 恢复Inspector缩进，避免影响后续属性。
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }
}
#endif
