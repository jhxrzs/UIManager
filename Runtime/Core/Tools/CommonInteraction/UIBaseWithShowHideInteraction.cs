using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JH
{
    /// <summary>
    ///  支持按钮、Toggle和键盘输入的UI显隐交互。
    /// </summary>
    public class UIBaseWithShowHideInteraction : UIBase
    {
        /// <summary>
        ///  按钮的显隐交互。
        /// </summary>
        [SerializeField] private List<ButtonEntity> buttonInteraction = new();
        
        /// <summary>
        ///  Toggle的显隐交互。
        /// </summary>
        [SerializeField] private List<ToggleEntity> toggleInteraction = new();

        /// <summary>
        ///  键盘按键的显隐交互。
        /// </summary>
        [SerializeField] private List<KeyEntity> keyInteraction = new();

        /// <inheritdoc />
        protected override void Start()
        {
            // 先执行UI基类的状态初始化。
            base.Start();

            // 初始化全部按钮显隐交互。
            foreach (var buttonEntity in buttonInteraction)
            {
                // 空元素不参与初始化，避免未完成的Inspector配置阻断其余交互。
                if (buttonEntity == null) continue;
                buttonEntity.Init(this);
            }

            // 初始化全部Toggle显隐交互。
            foreach (var toggleEntity in toggleInteraction)
            {
                // 空元素不参与初始化。
                if (toggleEntity == null) continue;
                toggleEntity.Init(this);
            }

            // 初始化全部键盘显隐交互，并为旧序列化资源重新生成按键缓存。
            foreach (var keyEntity in keyInteraction)
            {
                // 空元素不参与初始化。
                if (keyEntity == null) continue;
                keyEntity.Init(this);
            }

            // 使用Input System全局更新回调，使UI节点失活后仍可接收用于重新显示的按键。
            InputSystem.onAfterUpdate -= OnInputSystemAfterUpdate;
            InputSystem.onAfterUpdate += OnInputSystemAfterUpdate;
        }

        /// <summary>
        ///  Input System完成本轮输入更新后检查键盘显隐交互。
        /// </summary>
        private void OnInputSystemAfterUpdate()
        {
            // Unity对象已销毁或组件被开发者主动禁用时不再处理输入。
            if (!this || !enabled) return;

            // 当前没有可用键盘时不执行按键检查。
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 每条键盘配置独立判断，因此不同配置可以响应不同按键。
            foreach (var keyEntity in keyInteraction)
            {
                // 空元素不参与本帧检查。
                if (keyEntity == null) continue;
                keyEntity.ExecuteWhenPressed(this, keyboard);
            }
        }

        /// <summary>
        ///  Unity销毁当前UI时解除全局输入回调。
        /// </summary>
        protected virtual void OnDestroy()
        {
            // 静态事件必须主动解除，避免Input System继续持有已经销毁的UI。
            InputSystem.onAfterUpdate -= OnInputSystemAfterUpdate;
        }

#if UNITY_EDITOR
        /// <summary>
        ///  Inspector配置改变时重新解析按键名称。
        /// </summary>
        private void OnValidate()
        {
            // 嵌套的可序列化实体不会收到Unity的OnValidate，因此由宿主统一调度解析。
            foreach (var keyEntity in keyInteraction)
            {
                // 空元素不参与解析。
                if (keyEntity == null) continue;
                keyEntity.RefreshKeys();
            }
        }
#endif
    }
}
