#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace JH
{
    /// <summary>
    ///  在Hierarchy中绘制UI互斥组、抢占优先级和显隐状态。
    /// </summary>
    [InitializeOnLoad]
    public class UIBaseHierarchyLabel
    {
        /// <summary>
        ///  Hierarchy右侧标签样式缓存。
        /// </summary>
        private static GUIStyle _labelStyle;

        /// <summary>
        ///  Hierarchy右侧标签样式。
        /// </summary>
        private static GUIStyle LabelStyle =>
            _labelStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.yellow },
                alignment = TextAnchor.MiddleRight
            };
        
        /// <summary>
        ///  延迟注册Hierarchy绘制回调，确保编辑器API已完成初始化。
        /// </summary>
        static UIBaseHierarchyLabel()
        {
            // 延迟一次调用后再订阅Hierarchy绘制事件。
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
            // 将还原的对象交给统一标签绘制入口。
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
            // 将还原的对象交给统一标签绘制入口。
            DrawHierarchyLabel(gameObject, selectionRect);
        }
#endif

        /// <summary>
        ///  为包含UIBase的Hierarchy节点绘制互斥组、抢占优先级和显隐状态。
        /// </summary>
        /// <param name="gameObject">当前Hierarchy项对应的GameObject。</param>
        /// <param name="selectionRect">当前Hierarchy项的绘制区域。</param>
        private static void DrawHierarchyLabel(GameObject gameObject, Rect selectionRect)
        {
            // 非GameObject项不存在UIBase配置，无需绘制。
            if (!gameObject) return;

            // 只处理直接挂载UIBase的Hierarchy节点。
            if (!gameObject.TryGetComponent<UIBase>(out var uiBase)) return;

            // 使用同一份SerializedObject读取私有序列化字段，保持与Inspector数据一致。
            var serializedUI = new SerializedObject(uiBase);
            // 读取该面板所属的互斥组编号。
            var mutexGroupValue = serializedUI.FindProperty("mutexGroup").intValue;
            // 读取该面板在互斥组内的抢占优先级。
            var mutexPriorityValue = serializedUI.FindProperty("mutexPriority").intValue;

            // Showing和Showed均按显示生命周期标记，其余状态按隐藏标记。
            var stateLabel = uiBase.State is UIState.Showed or UIState.Showing ? "S" : "H";
            // 组合组号、优先级和显隐缩写作为标签内容。
            var labelContent = $"{mutexGroupValue}/{mutexPriorityValue} {stateLabel}";

            // 在Hierarchy项右侧保留固定宽度绘制组号、优先级和状态。
            const float labelWidth = 110f;
            // 将标签区域对齐到当前层级项的右边缘。
            var labelRect = new Rect(selectionRect)
            {
                x = selectionRect.xMax - labelWidth,
                width = labelWidth
            };
            // 使用缓存样式绘制当前面板状态。
            EditorGUI.LabelField(labelRect, labelContent, LabelStyle);
        }
    }
}
#endif
