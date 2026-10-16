using System;
using NUnit.Framework;
using UnityEngine;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的面板组件唯一性。</summary>
    public partial class Test_UIBase
    {
        /// <summary>验证同一节点拒绝第二个 UIBase 或派生组件，并保留原有动效绑定。</summary>
        /// <param name="firstType">首先添加到面板上的组件类型。</param>
        /// <param name="secondType">尝试重复添加并应被拒绝的组件类型。</param>
        [TestCase(typeof(UIBase), typeof(UIBase))]
        [TestCase(typeof(UIBase), typeof(UIBaseWithShowHideInteraction))]
        [TestCase(typeof(UIBaseWithShowHideInteraction), typeof(UIBase))]
        [TestCase(typeof(UIBaseWithShowHideInteraction), typeof(UIBaseWithShowHideInteraction))]
        public void PanelComponent_AddingSecondBaseOrDerived_ShouldRejectAndKeepOriginalEffect(
            Type firstType, Type secondType)
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateObject("UniquePanel", CreateObject("UniquePanelContainer").transform);
            // 添加并保存原始基准（original），供后续生命周期与效果检查使用。
            var original = (UIBase)go.AddComponent(firstType);
            // 将原始基准（original）的组件启用状态设为false，建立当前测试条件。
            original.enabled = false;
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            go.AddComponent<AlphaEffectController>();
            // 提交原始基准（original）的显示请求，触发实际生命周期。
            original.Show();
            // 提交原始基准（original）的隐藏请求，检查子树联动与终态。
            original.Hide();
            // 确认“go.GetComponent<CanvasGroup>()”的透明度为零。
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.Zero);

            // 初始化“rejectionMessage”为空引用，作为后续回调的观测基准。
            string rejectionMessage = null;
            // 准备仅捕获重复组件拒绝消息的日志回调，核对 Unity 的唯一性约束。
            Application.LogCallback captureRejection = (message, _, _) =>
            {
                // 根据“message.StartsWith("Can't add '" + secondType.Name + "' to UniquePanel", StringComparison.Ordinal)”选择对应测试路径。
                if (message.StartsWith("Can't add '" + secondType.Name + "' to UniquePanel", StringComparison.Ordinal))
                    // 将“rejectionMessage”设为“message”，建立当前测试条件。
                    rejectionMessage = message;
            };
            // 声明跨分支使用的观测变量，保留后续创建或采样得到的结果。
            Component duplicate;
            // 为“Application.logMessageReceived”追加观测或重入回调，验证通知次数及执行顺序。
            Application.logMessageReceived += captureRejection;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try { duplicate = go.AddComponent(secondType); }
            // 无论重复添加是否抛错，都解除临时日志监听。
            finally { Application.logMessageReceived -= captureRejection; }

            // 确认“rejectionMessage”满足约束“Does.Contain("already added")”。
            Assert.That(rejectionMessage, Does.Contain("already added"), "Unity must explain the rejected configuration.");
            // 确认“duplicate”为空。
            Assert.IsNull(duplicate);
            // 确认“go.GetComponents<UIBase>()”等于“new[] { original }”。
            CollectionAssert.AreEqual(new[] { original }, go.GetComponents<UIBase>());
            // 提交原始基准（original）的显示请求，触发实际生命周期。
            original.Show();
            // 确认原始基准（original）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, original.State);
            // 确认“go.GetComponent<CanvasGroup>()”的透明度符合1f。
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f),
                "Rejecting the duplicate must preserve the original state-to-effect binding.");
        }
    }
}
