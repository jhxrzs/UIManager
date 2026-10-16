using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的键盘输入更新与唤醒。</summary>
    public partial class Test_UIBase
    {
        /// <summary>验证首次 Start 前隐藏失活的面板仍可由键盘快捷键唤醒。</summary>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void KeyboardSwitch_HiddenBeforeStart_ShouldStillWakePanel(bool isAsync)
        {
            // 创建“input”，隔离本用例的资源或观测数据。
            var input = new InputTestFixture();
            // 建立隔离的 Input System 测试环境，避免使用真实设备输入。
            input.Setup();
            // 初始化交互宿主（host）为空引用，作为后续回调的观测基准。
            UIBaseWithShowHideInteraction host = null;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 将“InputSystem.settings.updateMode”设为“InputSettings.UpdateMode.ProcessEventsManually”，建立当前测试条件。
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                // 保存“keyboard”的基准值“InputSystem.AddDevice<Keyboard>()”，供后续比较或恢复。
                var keyboard = InputSystem.AddDevice<Keyboard>();
                // 创建“key”，隔离本用例的资源或观测数据。
                var key = new KeyEntity { IsAsync = isAsync };
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                typeof(KeyEntity).GetField("keyNames", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(key, "Space");
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                typeof(KeyEntity).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(key, ShowHideAction.Switch);
                // 创建宿主物体（go）并配置测试所需的层级或表现组件。
                var go = CreateObject("EarlyHiddenKeyboardHost", CreateObject("EarlyHiddenKeyboardRoot").transform);
                // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
                go.SetActive(false);
                // 添加测试需要的组件，供后续生命周期与效果检查使用。
                host = go.AddComponent<UIBaseWithShowHideInteraction>();
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                typeof(UIBaseWithShowHideInteraction)
                    .GetField("keyInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(host, new List<KeyEntity> { key });
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

                // 激活只触发真实 Awake/OnEnable；本用例不让出帧，也不手动调用 Start。
                go.SetActive(true);
                // 确认交互宿主（host）的初始化完成标志为假。
                Assert.IsFalse(host.IsUIStateInitialized);
                // 提交交互宿主（host）的隐藏请求，检查子树联动与终态。
                host.Hide();
                // 确认宿主物体（go）的层级激活状态为假。
                Assert.IsFalse(go.activeInHierarchy);
                // 确认交互宿主（host）的组件启用状态为真。
                Assert.IsTrue(host.enabled);
                // 提交键盘状态变化，构造实际输入边沿。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                // 推进指定输入更新阶段，验证同帧按键只被消费一次。
                InputSystem.Update();

                // 确认“keyboard.spaceKey.wasPressedThisFrame”为真。
                Assert.IsTrue(keyboard.spaceKey.wasPressedThisFrame, "用例必须实际产生一次按键。");
                // 确认交互宿主（host）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, host.State, "首次 Start 前隐藏的面板仍须能由快捷键唤醒。");
                // 确认宿主物体（go）的层级激活状态为真。
                Assert.IsTrue(go.activeInHierarchy);
                // 确认显示次数（shows）等于1。
                Assert.AreEqual(1, shows);
            }
            finally
            {
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try { if (host) Object.DestroyImmediate(host.gameObject); }
                // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
                finally { input.TearDown(); }
            }
        }

        /// <summary>验证渲染前输入更新不会重复消费同一次按键并重复切换界面。</summary>
        /// <param name="updateType">本轮手动推进的输入更新阶段。</param>
        /// <param name="updateMode">Input System 当前采用的更新模式。</param>
        [TestCase(InputUpdateType.Dynamic, InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)]
        [TestCase(InputUpdateType.Fixed, InputSettings.UpdateMode.ProcessEventsInFixedUpdate)]
        [TestCase(InputUpdateType.Manual, InputSettings.UpdateMode.ProcessEventsManually)]
        public void KeyboardSwitch_BeforeRender_ShouldNotConsumeTheSamePressTwice(
            InputUpdateType updateType, InputSettings.UpdateMode updateMode)
        {
            // 隔离真实设备、事件队列和项目中的其他输入监听。
            var input = new InputTestFixture();
            // 建立隔离的 Input System 测试环境，避免使用真实设备输入。
            input.Setup();
            // 初始化交互宿主（host）为空引用，作为后续回调的观测基准。
            UIBaseWithShowHideInteraction host = null;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 将“InputSystem.settings.updateMode”设为“updateMode”，建立当前测试条件。
                InputSystem.settings.updateMode = updateMode;
                // Fixed 更新只接收其时间边界之前的事件；使用隔离时钟避免事件滞留。
                var runtime = typeof(InputTestFixture).GetProperty("runtime",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                runtime.GetType().GetProperty("currentTime").SetValue(runtime, 10d);
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                runtime.GetType().GetProperty("currentTimeForFixedUpdate").SetValue(runtime, 20d);
                // 注册支持指定更新阶段的测试键盘布局，覆盖渲染前输入回调。
                InputSystem.RegisterLayout(@"{
                    ""name"": ""UIManagerBeforeRenderKeyboard"",
                    ""extend"": ""Keyboard"", ""beforeRender"": ""Update""
                }");
                // 保存“keyboard”的基准值“(Keyboard)InputSystem.AddDevice("UIManagerBeforeRenderKeyboard")”，供后续比较或恢复。
                var keyboard = (Keyboard)InputSystem.AddDevice("UIManagerBeforeRenderKeyboard");
                // 创建“key”，隔离本用例的资源或观测数据。
                var key = new KeyEntity();
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                typeof(KeyEntity).GetField("keyNames", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(key, "Space");
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                typeof(KeyEntity).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(key, ShowHideAction.Switch);
                // 添加测试需要的组件，供后续生命周期与效果检查使用。
                host = CreateObject("KeyboardHost", CreateObject("KeyboardRoot").transform)
                    .AddComponent<UIBaseWithShowHideInteraction>();
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                typeof(UIBaseWithShowHideInteraction)
                    .GetField("keyInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(host, new List<KeyEntity> { key });
                // 保存目标对象（target）的基准值交互宿主（host），供后续比较或恢复。
                var target = host;
                // 为“host.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
                host.ShowActions += () => target.gameObject.SetActive(true);
                // 在“host.HideActions”中关闭物体，构造后续回调面对失活祖先的场景。
                host.HideActions += () => target.gameObject.SetActive(false);
                // 显式调用交互宿主的 Start，稳定验证初始化与解绑时序。
                StartLifecycleHost(host);
                // 确认“host.gameObject”的自身激活状态为假。
                Assert.IsFalse(host.gameObject.activeSelf);
                // 初始化显示次数（shows）为0，作为后续回调的观测基准。
                var shows = 0;
                // 初始化隐藏次数（hides）为0，作为后续回调的观测基准。
                var hides = 0;
                // 为“host.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
                host.AfterUIShowedEvent += () => shows++;
                // 为“host.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
                host.AfterUIHidedEvent += () => hides++;

                // 此重载是 Input System 的内部测试入口，显式指定阶段以覆盖真实 BeforeRender。
                var update = typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
                    null, new[] { typeof(InputUpdateType) }, null);
                // 确认“update”存在。
                Assert.IsNotNull(update);
                // 提交键盘状态变化，构造实际输入边沿。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), time: 1);
                // 推进指定的输入更新阶段，验证按键边沿不会重复消费。
                update.Invoke(null, new object[] { updateType });
                // 确认交互宿主（host）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, host.State);
                // 确认“host.gameObject”的自身激活状态为真。
                Assert.IsTrue(host.gameObject.activeSelf, "隐藏节点仍应响应显示快捷键。");
                // 确认“keyboard.spaceKey.wasPressedThisFrame”为真。
                Assert.IsTrue(keyboard.spaceKey.wasPressedThisFrame);
                // 保存“count”的基准值“InputState.updateCount”，供后续比较或恢复。
                var count = InputState.updateCount;
                // 初始化“beforeRenderObserved”为false，作为后续回调的观测基准。
                var beforeRenderObserved = false;
                // 累计“InputSystem.onAfterUpdate += () => beforeRenderObserved”的观测标志，保留此前已出现的时序异常。
                InputSystem.onAfterUpdate += () =>
                    beforeRenderObserved |= InputState.currentUpdateType == InputUpdateType.BeforeRender;
                // 推进指定的输入更新阶段，验证按键边沿不会重复消费。
                update.Invoke(null, new object[] { InputUpdateType.BeforeRender });
                // 确认“beforeRenderObserved”为真。
                Assert.IsTrue(beforeRenderObserved, "用例必须实际经过额外输入更新。");
                // 确认“InputState.updateCount”等于“count”。
                Assert.AreEqual(count, InputState.updateCount);
                // 确认“keyboard.spaceKey.wasPressedThisFrame”为真。
                Assert.IsTrue(keyboard.spaceKey.wasPressedThisFrame, "额外更新仍保留同一次按键标记。");
                // 确认交互宿主（host）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, host.State);
                // 确认显示次数（shows）等于1。
                Assert.AreEqual(1, shows);
                // 确认隐藏次数（hides）等于0。
                Assert.AreEqual(0, hides);

                // 不等待新的渲染帧；释放再按下是独立输入，必须再次切换。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), time: 2);
                // 推进指定的输入更新阶段，验证按键边沿不会重复消费。
                update.Invoke(null, new object[] { updateType });
                // 提交键盘状态变化，构造实际输入边沿。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), time: 3);
                // 推进指定的输入更新阶段，验证按键边沿不会重复消费。
                update.Invoke(null, new object[] { updateType });
                // 确认交互宿主（host）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, host.State);
                // 确认隐藏次数（hides）等于1。
                Assert.AreEqual(1, hides);
            }
            finally
            {
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try { if (host) Object.DestroyImmediate(host.gameObject); }
                // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
                finally { input.TearDown(); }
            }
        }
    }
}
