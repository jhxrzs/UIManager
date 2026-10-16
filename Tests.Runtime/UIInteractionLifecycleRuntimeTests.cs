using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的交互绑定、唤醒及解绑。</summary>
    public partial class Test_UIBase
    {



        /// <summary>创建按钮或 Toggle 及其交互实体，支持同步和异步显隐测试。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        /// <param name="entity">要复用的交互实体；为空时根据控件类型创建。</param>
        /// <returns>包含实体与真实控件引用的交互测试包装。</returns>
        private InteractionControl CreateLifecycleControl(bool useToggle, bool isAsync = false,
            ShowHideInteractionEntity entity = null)
        {
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject("ExternalControl", typeof(RectTransform),
                useToggle ? typeof(Toggle) : typeof(Button));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(go);
            // 创建外部输入控件（control），隔离本用例的资源或观测数据。
            var control = new InteractionControl
            {
                Entity = entity ?? (useToggle ? (ShowHideInteractionEntity)new ToggleEntity() : new ButtonEntity()),
                Button = go.GetComponent<Button>(),
                Toggle = go.GetComponent<Toggle>()
            };
            // 将“control.Entity.IsAsync”设为“isAsync”，建立当前测试条件。
            control.Entity.IsAsync = isAsync;
            // 更新实体实际绑定的控件，模拟序列化配置和运行时重新绑定。
            SetLifecycleControl(control.Entity, control);
            // 根据“!useToggle”选择对应测试路径。
            if (!useToggle)
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                typeof(ButtonEntity).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(control.Entity, ShowHideAction.Switch);
            // 返回外部输入控件（control），交给调用方继续配置或断言。
            return control;
        }

        /// <summary>更新实体实际绑定的控件，模拟序列化配置和运行时重新绑定。</summary>
        /// <param name="entity">要复用的交互实体；为空时根据控件类型创建。</param>
        /// <param name="control">要配置或触发的外部交互控件。</param>
        private static void SetLifecycleControl(ShowHideInteractionEntity entity, InteractionControl control)
        {
            // 保存组件类型（type）的基准值“entity is ButtonEntity ? typeof(ButtonEntity) : typeof(ToggleEntity)”，供后续比较或恢复。
            var type = entity is ButtonEntity ? typeof(ButtonEntity) : typeof(ToggleEntity);
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            type.GetField(entity is ButtonEntity ? "button" : "toggle",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(entity, entity is ButtonEntity ? (object)control.Button : control.Toggle);
        }

        /// <summary>创建禁用自动 Start 的交互宿主，并配置两类外部控件实体。</summary>
        /// <param name="button">为宿主提供输入的按钮交互配置。</param>
        /// <param name="toggle">为宿主提供输入的 Toggle 交互配置。</param>
        /// <returns>已配置按钮和 Toggle 的交互宿主。</returns>
        private UIBaseWithShowHideInteraction CreateLifecycleHost(InteractionControl button,
            InteractionControl toggle)
        {
            // 添加并保存交互宿主（host），供后续生命周期与效果检查使用。
            var host = CreateObject("InteractionHost", CreateObject("HostRoot").transform)
                .AddComponent<UIBaseWithShowHideInteraction>();
            // 将交互宿主（host）的组件启用状态设为false，建立当前测试条件。
            host.enabled = false;
            // 保存组件类型（type）的运行时类型，定位测试需要的字段或方法。
            var type = typeof(UIBaseWithShowHideInteraction);
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            type.GetField("buttonInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, new List<ButtonEntity> { (ButtonEntity)button.Entity });
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            type.GetField("toggleInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, new List<ToggleEntity> { (ToggleEntity)toggle.Entity });
            // 返回交互宿主（host），交给调用方继续配置或断言。
            return host;
        }

        /// <summary>显式调用交互宿主的 Start，稳定验证初始化与解绑时序。</summary>
        /// <param name="host">需要初始化或检查的交互宿主。</param>
        private static void StartLifecycleHost(UIBaseWithShowHideInteraction host)
        {
            // 显式调用真实 Start，使测试不依赖帧调度；销毁仍走 Unity 的真实 OnDestroy。
            typeof(UIBaseWithShowHideInteraction).GetMethod("Start",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, null);
        }

        /// <summary>验证首次 Start 前隐藏并失活的面板仍能由外部按钮同步唤醒。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Interaction_HiddenBeforeStart_Button_ShouldStillWakePanel() =>
            HiddenBeforeStart_ShouldStillWakePanel(false, false);

        /// <summary>验证首次 Start 前隐藏并失活的面板仍能由外部按钮异步唤醒。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Interaction_HiddenBeforeStart_AsyncButton_ShouldStillWakePanel() =>
            HiddenBeforeStart_ShouldStillWakePanel(false, true);

        /// <summary>验证首次 Start 前隐藏并失活的面板仍能由外部 Toggle 同步唤醒。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Interaction_HiddenBeforeStart_Toggle_ShouldStillWakePanel() =>
            HiddenBeforeStart_ShouldStillWakePanel(true, false);

        /// <summary>验证首次 Start 前隐藏并失活的面板仍能由外部 Toggle 异步唤醒。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Interaction_HiddenBeforeStart_AsyncToggle_ShouldStillWakePanel() =>
            HiddenBeforeStart_ShouldStillWakePanel(true, true);

        /// <summary>验证真实 Awake 后、首次 Start 前失活不会丢失控件绑定，唤醒后也不重复订阅。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator HiddenBeforeStart_ShouldStillWakePanel(bool useToggle, bool isAsync)
        {
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateLifecycleControl(false, isAsync);
            // 创建切换控件（toggle）并配置测试所需的层级或表现组件。
            var toggle = CreateLifecycleControl(true, isAsync);
            // 设置 Toggle 初始值但不触发通知，避免提前提交显隐请求。
            toggle.Toggle.SetIsOnWithoutNotify(false);
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateObject("HiddenBeforeStart", CreateObject("HiddenBeforeStartRoot").transform);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(false);
            // 添加并保存交互宿主（host），供后续生命周期与效果检查使用。
            var host = go.AddComponent<UIBaseWithShowHideInteraction>();
            // 模拟场景的序列化配置：所有字段和表现行为都在首次 Awake 之前就绪。
            var type = typeof(UIBaseWithShowHideInteraction);
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            type.GetField("buttonInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, new List<ButtonEntity> { (ButtonEntity)button.Entity });
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            type.GetField("toggleInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, new List<ToggleEntity> { (ToggleEntity)toggle.Entity });
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(host, "state", UIState.Showed);
            // 为“host.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            host.ShowActions += () => go.SetActive(true);
            // 在“host.HideActions”中关闭物体，构造后续回调面对失活祖先的场景。
            host.HideActions += () => go.SetActive(false);
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 为“host.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            host.AfterUIShowedEvent += () => shows++;

            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 确认交互宿主（host）的初始化完成标志为假。
            Assert.IsFalse(host.IsUIStateInitialized, "此时只经过真实 Awake/OnEnable，尚未执行 Start。");
            // 首次 Start 前的公开请求或父级联都可能隐藏面板；Showed 初态确保 Hide 不会短路。
            host.Hide();
            // 确认交互宿主（host）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, host.State);
            // 确认交互宿主（host）的组件启用状态为真。
            Assert.IsTrue(host.enabled);
            // 确认宿主物体（go）的层级激活状态为假。
            Assert.IsFalse(go.activeInHierarchy);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 根据“useToggle”选择对应测试路径。
            if (useToggle) toggle.Toggle.isOn = true;
            // 覆盖条件不成立时的另一条配置或清理路径。
            else button.Trigger();

            // 确认交互宿主（host）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, host.State, "Start 前失活不能丢失外部唤醒入口。");
            // 确认宿主物体（go）的层级激活状态为真。
            Assert.IsTrue(go.activeInHierarchy);
            // 确认显示次数（shows）等于1。
            Assert.AreEqual(1, shows, "每次输入只能执行一次显示。");

            // 唤醒后才让 Unity 自然执行首次 Start，随后验证绑定没有丢失或重复。
            yield return null;
            // 根据“useToggle”选择对应测试路径。
            if (useToggle) toggle.Toggle.isOn = false;
            // 覆盖条件不成立时的另一条配置或清理路径。
            else button.Trigger();
            // 确认交互宿主（host）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, host.State);
            // 根据“useToggle”选择对应测试路径。
            if (useToggle) toggle.Toggle.isOn = true;
            // 覆盖条件不成立时的另一条配置或清理路径。
            else button.Trigger();
            // 确认交互宿主（host）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, host.State);
            // 确认显示次数（shows）等于2。
            Assert.AreEqual(2, shows);
        }

        /// <summary>验证同一交互实体重复初始化后每次输入只执行一次显隐。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Interaction_Reinitialize_ShouldExecuteOnlyOnce(bool useToggle, bool isAsync)
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("Target", CreateObject("TargetRoot").transform);
            // 创建外部输入控件（control）并配置测试所需的层级或表现组件。
            var control = CreateLifecycleControl(useToggle, isAsync);
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 显示后再隐藏，让重复注册的第二个监听无法被 Showed 状态短路掩盖。
            ui.AfterUIShowedEvent += () => { shows++; ui.Hide(); };
            // 初始化交互实体与目标绑定，覆盖首次绑定或重新绑定路径。
            control.Entity.Init(ui);
            // 初始化交互实体与目标绑定，覆盖首次绑定或重新绑定路径。
            control.Entity.Init(ui);

            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            control.Trigger();

            // 确认显示次数（shows）等于1。
            Assert.AreEqual(1, shows);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
            // 解除交互实体的控件监听，验证清理与重复解绑行为。
            control.Entity.Unbind();
        }

        /// <summary>验证重复解绑安全且不移除控件的独立业务监听。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_Unbind_ShouldBeIdempotentAndPreserveOtherListeners(bool useToggle)
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("Target", CreateObject("TargetRoot").transform);
            // 创建外部输入控件（control）并配置测试所需的层级或表现组件。
            var control = CreateLifecycleControl(useToggle);
            // 初始化外部业务监听次数（externalCalls）为0，作为后续回调的观测基准。
            var externalCalls = 0;
            // 追加独立业务监听并统计输入次数，验证框架解绑不会移除它。
            control.AddListener(() => externalCalls++);
            // 初始化交互实体与目标绑定，覆盖首次绑定或重新绑定路径。
            control.Entity.Init(ui);

            // 解除交互实体的控件监听，验证清理与重复解绑行为。
            control.Entity.Unbind();
            // 解除交互实体的控件监听，验证清理与重复解绑行为。
            control.Entity.Unbind();
            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            control.Trigger();

            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
            // 确认外部业务监听次数（externalCalls）等于1。
            Assert.AreEqual(1, externalCalls);
        }

        /// <summary>验证重新绑定同时解除旧控件监听和旧目标关联。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_Rebind_ShouldDetachOldControlAndTarget(bool useToggle)
        {
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("First", CreateObject("FirstRoot").transform);
            // 创建第二项（second）并配置测试所需的层级或表现组件。
            var second = CreateUI("Second", CreateObject("SecondRoot").transform);
            // 创建“oldControl”并配置测试所需的层级或表现组件。
            var oldControl = CreateLifecycleControl(useToggle);
            // 创建“newControl”并配置测试所需的层级或表现组件。
            var newControl = CreateLifecycleControl(useToggle);
            // 初始化交互实体与目标绑定，覆盖首次绑定或重新绑定路径。
            oldControl.Entity.Init(first);
            // 更新实体实际绑定的控件，模拟序列化配置和运行时重新绑定。
            SetLifecycleControl(oldControl.Entity, newControl);
            // 初始化交互实体与目标绑定，覆盖首次绑定或重新绑定路径。
            oldControl.Entity.Init(second);

            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            oldControl.Trigger();
            // 确认首项（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认第二项（second）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, second.State);
            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            newControl.Trigger();
            // 确认首项（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认第二项（second）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, second.State);
            // 解除交互实体的控件监听，验证清理与重复解绑行为。
            oldControl.Entity.Unbind();
        }

        /// <summary>验证控件先被销毁后，实体仍能安全重复解绑。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_ControlDestroyedBeforeUnbind_ShouldAllowCleanup(bool useToggle)
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("Target", CreateObject("TargetRoot").transform);
            // 创建外部输入控件（control）并配置测试所需的层级或表现组件。
            var control = CreateLifecycleControl(useToggle);
            // 初始化交互实体与目标绑定，覆盖首次绑定或重新绑定路径。
            control.Entity.Init(ui);
            // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
            Object.DestroyImmediate(useToggle ? control.Toggle.gameObject : control.Button.gameObject);

            // 确认当前被测调用安全完成，不产生意外异常。
            Assert.DoesNotThrow(control.Entity.Unbind);
            // 确认当前被测调用安全完成，不产生意外异常。
            Assert.DoesNotThrow(control.Entity.Unbind);
        }

        /// <summary>验证宿主销毁时按钮和 Toggle 均解绑一次，独立业务监听继续生效。</summary>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_OwnerDestroy_ShouldUnbindBothControlsAndPreserveOtherListeners(bool isAsync)
        {
            // 创建“buttonEntity”，隔离本用例的资源或观测数据。
            var buttonEntity = new TrackedButtonEntity();
            // 创建“toggleEntity”，隔离本用例的资源或观测数据。
            var toggleEntity = new TrackedToggleEntity();
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateLifecycleControl(false, isAsync, buttonEntity);
            // 创建切换控件（toggle）并配置测试所需的层级或表现组件。
            var toggle = CreateLifecycleControl(true, isAsync, toggleEntity);
            // 初始化外部业务监听次数（externalCalls）为0，作为后续回调的观测基准。
            var externalCalls = 0;
            // 追加独立业务监听并统计输入次数，验证框架解绑不会移除它。
            button.AddListener(() => externalCalls++);
            // 追加独立业务监听并统计输入次数，验证框架解绑不会移除它。
            toggle.AddListener(() => externalCalls++);
            // 创建交互宿主（host）并配置测试所需的层级或表现组件。
            var host = CreateLifecycleHost(button, toggle);
            // 显式调用交互宿主的 Start，稳定验证初始化与解绑时序。
            StartLifecycleHost(host);
            // 保存“buttonUnbinds”的基准值“buttonEntity”的解绑次数，供后续比较或恢复。
            var buttonUnbinds = buttonEntity.UnbindCount;
            // 保存“toggleUnbinds”的基准值“toggleEntity”的解绑次数，供后续比较或恢复。
            var toggleUnbinds = toggleEntity.UnbindCount;

            // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
            Object.DestroyImmediate(host.gameObject);

            // 确认“buttonEntity”的解绑次数等于“buttonUnbinds + 1”。
            Assert.AreEqual(buttonUnbinds + 1, buttonEntity.UnbindCount);
            // 确认“toggleEntity”的解绑次数等于“toggleUnbinds + 1”。
            Assert.AreEqual(toggleUnbinds + 1, toggleEntity.UnbindCount);
            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            button.Trigger();
            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            toggle.Trigger();
            // 确认外部业务监听次数（externalCalls）等于2。
            Assert.AreEqual(2, externalCalls);
        }

        /// <summary>验证隐藏界面不会移除外部控件的重新显示入口。</summary>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_Hide_ShouldKeepExternalControlsAbleToShowPanel(bool isAsync)
        {
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateLifecycleControl(false, isAsync);
            // 创建切换控件（toggle）并配置测试所需的层级或表现组件。
            var toggle = CreateLifecycleControl(true, isAsync);
            // 创建交互宿主（host）并配置测试所需的层级或表现组件。
            var host = CreateLifecycleHost(button, toggle);
            host.enabled = true; // 本用例验证隐藏恢复，交互组件本身保持启用。
            // 显式调用交互宿主的 Start，稳定验证初始化与解绑时序。
            StartLifecycleHost(host);

            // 提交交互宿主（host）的显示请求，触发实际生命周期。
            host.Show();
            // 提交交互宿主（host）的隐藏请求，检查子树联动与终态。
            host.Hide();
            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            button.Trigger();
            // 确认交互宿主（host）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, host.State);
            // 提交交互宿主（host）的隐藏请求，检查子树联动与终态。
            host.Hide();
            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            toggle.Trigger();
            // 确认交互宿主（host）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, host.State);
        }

        /// <summary>验证控件较早监听销毁宿主后，旧通知快照中的交互回调安全退出。</summary>
        /// <param name="useToggle">是否使用 Toggle 输入；为 false 时使用按钮输入。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_DestroyOwnerDuringNotification_ShouldIgnoreStaleCallback(bool useToggle)
        {
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateLifecycleControl(false, true);
            // 创建切换控件（toggle）并配置测试所需的层级或表现组件。
            var toggle = CreateLifecycleControl(true, true);
            // 创建交互宿主（host）并配置测试所需的层级或表现组件。
            var host = CreateLifecycleHost(button, toggle);
            // 保存外部输入控件（control）的基准值“useToggle ? toggle : button”，供后续比较或恢复。
            var control = useToggle ? toggle : button;
            // 先注册会销毁宿主的控件监听，验证同轮旧实体回调能识别宿主已经失效。
            control.AddListener(() => Object.DestroyImmediate(host.gameObject));
            // 显式调用交互宿主的 Start，稳定验证初始化与解绑时序。
            StartLifecycleHost(host);

            // UnityEvent 已捕获本轮回调时，解绑不能保证阻止旧快照继续派发。
            control.Trigger();

            // 确认交互宿主（host）为假。
            Assert.IsFalse(host);
        }
    }
}
