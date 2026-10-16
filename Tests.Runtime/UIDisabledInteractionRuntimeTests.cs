using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的禁用组件与失活物体的输入边界。</summary>
    public partial class Test_UIBase
    {
        /// <summary>按目标显隐方向触发按钮或 Toggle，统一交互测试的输入入口。</summary>
        /// <param name="control">要配置或触发的外部交互控件。</param>
        /// <param name="show">此次输入是否要求显示界面。</param>
        private static void TriggerLifecycleControl(InteractionControl control, bool show)
        {
            // 根据“control.Toggle”选择对应测试路径。
            if (control.Toggle) control.Toggle.onValueChanged.Invoke(show);
            // 覆盖条件不成立时的另一条配置或清理路径。
            else control.Trigger();
        }

        /// <summary>验证禁用交互组件后忽略显隐输入，重新启用后恢复绑定。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Interaction_DisabledOwner_ShouldIgnoreBothDirectionsAndResumeWhenEnabled(bool useToggle, bool isAsync)
        {
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateLifecycleControl(false, isAsync);
            // 创建切换控件（toggle）并配置测试所需的层级或表现组件。
            var toggle = CreateLifecycleControl(true, isAsync);
            // 创建交互宿主（host）并配置测试所需的层级或表现组件。
            var host = CreateLifecycleHost(button, toggle);
            // 显式调用交互宿主的 Start，稳定验证初始化与解绑时序。
            StartLifecycleHost(host);
            // 保存外部输入控件（control）的基准值“useToggle ? toggle : button”，供后续比较或恢复。
            var control = useToggle ? toggle : button;
            // 初始化“syncShows”为0，作为后续回调的观测基准。
            var syncShows = 0;
            // 初始化“asyncShows”为0，作为后续回调的观测基准。
            var asyncShows = 0;
            // 初始化“syncHides”为0，作为后续回调的观测基准。
            var syncHides = 0;
            // 初始化“asyncHides”为0，作为后续回调的观测基准。
            var asyncHides = 0;
            // 为“host.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            host.ShowActions += () => syncShows++;
            // 为“host.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            host.HideActions += () => syncHides++;
            // 为“host.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            host.ShowAsyncActions += _ => { asyncShows++; return UniTask.CompletedTask; };
            // 为“host.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            host.HideAsyncActions += _ => { asyncHides++; return UniTask.CompletedTask; };
            // 初始化显示完成通知次数（afterShows）为0，作为后续回调的观测基准。
            var afterShows = 0;
            // 初始化隐藏完成通知次数（afterHides）为0，作为后续回调的观测基准。
            var afterHides = 0;
            // 为“host.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            host.AfterUIShowedEvent += () => afterShows++;
            // 为“host.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            host.AfterUIHidedEvent += () => afterHides++;

            // 确认交互宿主（host）的组件启用状态为假。
            Assert.IsFalse(host.enabled);
            // 按目标显隐方向触发按钮或 Toggle，统一交互测试的输入入口。
            TriggerLifecycleControl(control, true);
            // 确认交互宿主（host）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, host.State);
            // 确认“syncShows + asyncShows”等于0。
            Assert.AreEqual(0, syncShows + asyncShows);
            // 确认显示完成通知次数（afterShows）等于0。
            Assert.AreEqual(0, afterShows);

            // 将交互宿主（host）的组件启用状态设为true，建立当前测试条件。
            host.enabled = true;
            // 按目标显隐方向触发按钮或 Toggle，统一交互测试的输入入口。
            TriggerLifecycleControl(control, true);
            // 确认交互宿主（host）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, host.State);
            // 确认“syncShows”等于“isAsync ? 0 : 1”。
            Assert.AreEqual(isAsync ? 0 : 1, syncShows);
            // 确认“asyncShows”等于“isAsync ? 1 : 0”。
            Assert.AreEqual(isAsync ? 1 : 0, asyncShows);
            // 确认显示完成通知次数（afterShows）等于1。
            Assert.AreEqual(1, afterShows, "重新启用应恢复原有绑定，且只响应一次。");

            // 将交互宿主（host）的组件启用状态设为false，建立当前测试条件。
            host.enabled = false;
            // 按目标显隐方向触发按钮或 Toggle，统一交互测试的输入入口。
            TriggerLifecycleControl(control, false);
            // 确认交互宿主（host）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, host.State);
            // 确认“syncHides + asyncHides”等于0。
            Assert.AreEqual(0, syncHides + asyncHides);
            // 确认隐藏完成通知次数（afterHides）等于0。
            Assert.AreEqual(0, afterHides);

            // 将交互宿主（host）的组件启用状态设为true，建立当前测试条件。
            host.enabled = true;
            // 按目标显隐方向触发按钮或 Toggle，统一交互测试的输入入口。
            TriggerLifecycleControl(control, false);
            // 确认交互宿主（host）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, host.State);
            // 确认“syncHides”等于“isAsync ? 0 : 1”。
            Assert.AreEqual(isAsync ? 0 : 1, syncHides);
            // 确认“asyncHides”等于“isAsync ? 1 : 0”。
            Assert.AreEqual(isAsync ? 1 : 0, asyncHides);
            // 确认隐藏完成通知次数（afterHides）等于1。
            Assert.AreEqual(1, afterHides);
        }

        /// <summary>验证组件仍启用但物体失活时，外部控件仍能唤醒界面。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Interaction_InactiveEnabledOwner_ShouldStillOpenFromExternalControl(bool useToggle, bool isAsync)
        {
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateLifecycleControl(false, isAsync);
            // 创建切换控件（toggle）并配置测试所需的层级或表现组件。
            var toggle = CreateLifecycleControl(true, isAsync);
            // 创建交互宿主（host）并配置测试所需的层级或表现组件。
            var host = CreateLifecycleHost(button, toggle);
            // 将交互宿主（host）的组件启用状态设为true，建立当前测试条件。
            host.enabled = true;
            // 为“host.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            host.ShowActions += () => host.gameObject.SetActive(true);
            // 在“host.HideActions”中关闭物体，构造后续回调面对失活祖先的场景。
            host.HideActions += () => host.gameObject.SetActive(false);
            // 显式调用交互宿主的 Start，稳定验证初始化与解绑时序。
            StartLifecycleHost(host);
            // 保存外部输入控件（control）的基准值“useToggle ? toggle : button”，供后续比较或恢复。
            var control = useToggle ? toggle : button;
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 初始化隐藏次数（hides）为0，作为后续回调的观测基准。
            var hides = 0;
            // 为“host.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            host.AfterUIShowedEvent += () => shows++;
            // 为“host.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            host.AfterUIHidedEvent += () => hides++;

            // 确认交互宿主（host）的组件启用状态为真。
            Assert.IsTrue(host.enabled);
            // 确认“host.gameObject”的层级激活状态为假。
            Assert.IsFalse(host.gameObject.activeInHierarchy);
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var round = 0; round < 2; round++)
            {
                // 按目标显隐方向触发按钮或 Toggle，统一交互测试的输入入口。
                TriggerLifecycleControl(control, true);
                // 确认交互宿主（host）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, host.State);
                // 确认“host.gameObject”的层级激活状态为真。
                Assert.IsTrue(host.gameObject.activeInHierarchy, "仅失活的面板应能由外部控件重新显示。");
                // 按目标显隐方向触发按钮或 Toggle，统一交互测试的输入入口。
                TriggerLifecycleControl(control, false);
                // 确认交互宿主（host）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, host.State);
                // 确认“host.gameObject”的层级激活状态为假。
                Assert.IsFalse(host.gameObject.activeInHierarchy);
                // 确认交互宿主（host）的组件启用状态为真。
                Assert.IsTrue(host.enabled, "GameObject 失活不能被当作组件主动禁用。");
            }
            // 确认显示次数（shows）等于2。
            Assert.AreEqual(2, shows);
            // 确认隐藏次数（hides）等于2。
            Assert.AreEqual(2, hides);
        }

        /// <summary>验证同次控件通知的较早监听禁用组件后，后续交互回调不再提交请求。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Interaction_DisabledByEarlierControlListener_ShouldIgnoreCurrentNotification(bool useToggle, bool isAsync)
        {
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateLifecycleControl(false, isAsync);
            // 创建切换控件（toggle）并配置测试所需的层级或表现组件。
            var toggle = CreateLifecycleControl(true, isAsync);
            // 创建交互宿主（host）并配置测试所需的层级或表现组件。
            var host = CreateLifecycleHost(button, toggle);
            // 将交互宿主（host）的组件启用状态设为true，建立当前测试条件。
            host.enabled = true;
            // 保存外部输入控件（control）的基准值“useToggle ? toggle : button”，供后续比较或恢复。
            var control = useToggle ? toggle : button;
            // 初始化外部业务监听次数（externalCalls）为0，作为后续回调的观测基准。
            var externalCalls = 0;
            // 追加独立业务监听并统计输入次数，验证框架解绑不会移除它。
            control.AddListener(() => { externalCalls++; host.enabled = false; });
            // 显式调用交互宿主的 Start，稳定验证初始化与解绑时序。
            StartLifecycleHost(host);
            // 初始化“actions”为0，作为后续回调的观测基准。
            var actions = 0;
            // 初始化显示完成通知次数（afterShows）为0，作为后续回调的观测基准。
            var afterShows = 0;
            // 为“host.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            host.ShowActions += () => actions++;
            // 为“host.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            host.ShowAsyncActions += _ => { actions++; return UniTask.CompletedTask; };
            // 为“host.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            host.AfterUIShowedEvent += () => afterShows++;

            // 按目标显隐方向触发按钮或 Toggle，统一交互测试的输入入口。
            TriggerLifecycleControl(control, true);

            // 确认外部业务监听次数（externalCalls）等于1。
            Assert.AreEqual(1, externalCalls, "不得影响控件上的其他业务监听。");
            // 确认交互宿主（host）的组件启用状态为假。
            Assert.IsFalse(host.enabled);
            // 确认交互宿主（host）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, host.State);
            // 确认“actions”等于0。
            Assert.AreEqual(0, actions, "实体执行入口必须复核同一轮通知中发生的禁用。");
            // 确认显示完成通知次数（afterShows）等于0。
            Assert.AreEqual(0, afterShows);
        }
    }
}
