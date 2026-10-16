using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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

        // 保留实际绑定的实体，首次 Start 前替换配置时也能清理旧监听。
        /// <summary>
        ///  实际完成初始化的按钮实体快照，配置替换后仍可清理旧监听。
        /// </summary>
        private ButtonEntity[] _boundButtonInteractions;
        /// <summary>
        ///  实际完成初始化的 Toggle 实体快照，配置替换后仍可清理旧监听。
        /// </summary>
        private ToggleEntity[] _boundToggleInteractions;

        /// <inheritdoc />
        protected override void Awake()
        {
            // 先完成 UIBase 的生命周期初始化。
            base.Awake();
            // 首次 Start 前也可能被父级联或公开 API 隐藏，必须提前保留外部唤醒入口。
            InitializeInteractions();
        }

        /// <inheritdoc />
        protected override void Start()
        {
            // 兼容 AddComponent 后、首次 Start 前完成的运行时配置，重复初始化不累积监听。
            InitializeInteractions();
            // 初始化通知可能销毁面板；其后不得重新绑定已经由 OnDestroy 清理的输入。
            base.Start();
        }

        /// <summary>
        ///  重新绑定当前交互配置，并订阅失活时仍可唤醒面板的输入回调。
        /// </summary>
        private void InitializeInteractions()
        {
            // 解除上一批实体及全局输入监听。
            UnbindInteractions();
            // 快照当前按钮配置，并保存实际初始化的实体。
            var buttons = _boundButtonInteractions = buttonInteraction.ToArray();
            // 快照当前 Toggle 配置，避免配置替换后丢失解绑对象。
            var toggles = _boundToggleInteractions = toggleInteraction.ToArray();

            // 初始化全部按钮显隐交互。
            foreach (var buttonEntity in buttons)
            {
                // 空元素不参与初始化，避免未完成的Inspector配置阻断其余交互。
                if (buttonEntity == null) continue;
                // 为当前按钮实体绑定本面板。
                buttonEntity.Init(this);
            }

            // 初始化全部Toggle显隐交互。
            foreach (var toggleEntity in toggles)
            {
                // 空元素不参与初始化。
                if (toggleEntity == null) continue;
                // 为当前 Toggle 实体绑定本面板。
                toggleEntity.Init(this);
            }

            // 初始化全部键盘显隐交互，并为旧序列化资源重新生成按键缓存。
            foreach (var keyEntity in keyInteraction)
            {
                // 空元素不参与初始化。
                if (keyEntity == null) continue;
                // 刷新当前键盘实体的运行时按键缓存。
                keyEntity.Init(this);
            }

            // 初始化回调已经销毁面板时不再订阅全局输入。
            if (!this) return;
            // 使用Input System全局更新回调，使UI节点失活后仍可接收用于重新显示的按键。
            InputSystem.onAfterUpdate += OnInputSystemAfterUpdate;
        }

        /// <summary>
        ///  Input System完成本轮输入更新后检查键盘显隐交互。
        /// </summary>
        private void OnInputSystemAfterUpdate()
        {
            // BeforeRender 不推进按键的更新计数，不能再次消费本轮 wasPressedThisFrame。
            // 按输入更新类型筛选，保留同一渲染帧内合法的 Fixed 和 Manual 更新。
            if (InputState.currentUpdateType is not (InputUpdateType.Dynamic or
                // 仅处理会推进按键状态的输入更新类型。
                InputUpdateType.Fixed or InputUpdateType.Manual)) return;

            // Unity对象已销毁或组件被开发者主动禁用时不再处理输入。
            if (!this || !enabled) return;

            // 当前没有可用键盘时不执行按键检查。
            var keyboard = Keyboard.current;
            // 没有键盘设备时结束本次输入检查。
            if (keyboard == null) return;

            // 每条键盘配置独立判断，因此不同配置可以响应不同按键。
            foreach (var keyEntity in keyInteraction)
            {
                // 空元素不参与本帧检查。
                if (keyEntity == null) continue;
                // 配置中的任意按键首次按下时执行该实体行为。
                keyEntity.ExecuteWhenPressed(this, keyboard);
            }
        }

        /// <summary>
        ///  Unity销毁当前UI时解除控件监听及全局输入回调。
        /// </summary>
        protected virtual void OnDestroy()
        {
            // 销毁时释放控件事件和静态输入事件的订阅。
            UnbindInteractions();
        }

        /// <summary>
        ///  解除实际绑定的交互实体和全局输入回调，并清空绑定快照。
        /// </summary>
        private void UnbindInteractions()
        {
            // 静态事件必须主动解除，避免Input System继续持有已经销毁的UI。
            InputSystem.onAfterUpdate -= OnInputSystemAfterUpdate;
            // 只有已经初始化过的按钮快照需要清理。
            if (_boundButtonInteractions != null)
                // 逐个解除实际绑定的按钮实体，允许空配置项。
                foreach (var buttonEntity in _boundButtonInteractions) buttonEntity?.Unbind();
            // 只有已经初始化过的 Toggle 快照需要清理。
            if (_boundToggleInteractions != null)
                // 逐个解除实际绑定的 Toggle 实体，允许空配置项。
                foreach (var toggleEntity in _boundToggleInteractions) toggleEntity?.Unbind();
            // 释放按钮快照，避免继续持有旧配置。
            _boundButtonInteractions = null;
            // 释放 Toggle 快照，使重复清理保持安全。
            _boundToggleInteractions = null;
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
                // 根据最新名称配置重新生成按键缓存与错误提示。
                keyEntity.RefreshKeys();
            }
        }
#endif
    }
}
