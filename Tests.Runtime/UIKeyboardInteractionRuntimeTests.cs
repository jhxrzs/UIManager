using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        [TestCase(InputUpdateType.Dynamic, InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)]
        [TestCase(InputUpdateType.Fixed, InputSettings.UpdateMode.ProcessEventsInFixedUpdate)]
        [TestCase(InputUpdateType.Manual, InputSettings.UpdateMode.ProcessEventsManually)]
        public void KeyboardSwitch_BeforeRender_ShouldNotConsumeTheSamePressTwice(
            InputUpdateType updateType, InputSettings.UpdateMode updateMode)
        {
            // 隔离真实设备、事件队列和项目中的其他输入监听。
            var input = new InputTestFixture();
            input.Setup();
            UIBaseWithShowHideInteraction host = null;
            try
            {
                InputSystem.settings.updateMode = updateMode;
                // Fixed 更新只接收其时间边界之前的事件；使用隔离时钟避免事件滞留。
                var runtime = typeof(InputTestFixture).GetProperty("runtime",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
                runtime.GetType().GetProperty("currentTime").SetValue(runtime, 10d);
                runtime.GetType().GetProperty("currentTimeForFixedUpdate").SetValue(runtime, 20d);
                InputSystem.RegisterLayout(@"{
                    ""name"": ""UIManagerBeforeRenderKeyboard"",
                    ""extend"": ""Keyboard"", ""beforeRender"": ""Update""
                }");
                var keyboard = (Keyboard)InputSystem.AddDevice("UIManagerBeforeRenderKeyboard");
                var key = new KeyEntity();
                typeof(KeyEntity).GetField("keyNames", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(key, "Space");
                typeof(KeyEntity).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(key, ShowHideAction.Switch);
                host = CreateObject("KeyboardHost", CreateObject("KeyboardRoot").transform)
                    .AddComponent<UIBaseWithShowHideInteraction>();
                typeof(UIBaseWithShowHideInteraction)
                    .GetField("keyInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(host, new List<KeyEntity> { key });
                var target = host;
                host.ShowActions += () => target.gameObject.SetActive(true);
                host.HideActions += () => target.gameObject.SetActive(false);
                StartLifecycleHost(host);
                Assert.IsFalse(host.gameObject.activeSelf);
                var shows = 0;
                var hides = 0;
                host.AfterUIShowedEvent += () => shows++;
                host.AfterUIHidedEvent += () => hides++;

                // 此重载是 Input System 的内部测试入口，显式指定阶段以覆盖真实 BeforeRender。
                var update = typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
                    null, new[] { typeof(InputUpdateType) }, null);
                Assert.IsNotNull(update);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), time: 1);
                update.Invoke(null, new object[] { updateType });
                Assert.AreEqual(UIState.Showed, host.State);
                Assert.IsTrue(host.gameObject.activeSelf, "隐藏节点仍应响应显示快捷键。");
                Assert.IsTrue(keyboard.spaceKey.wasPressedThisFrame);
                var count = InputState.updateCount;
                var beforeRenderObserved = false;
                InputSystem.onAfterUpdate += () =>
                    beforeRenderObserved |= InputState.currentUpdateType == InputUpdateType.BeforeRender;
                update.Invoke(null, new object[] { InputUpdateType.BeforeRender });
                Assert.IsTrue(beforeRenderObserved, "用例必须实际经过额外输入更新。");
                Assert.AreEqual(count, InputState.updateCount);
                Assert.IsTrue(keyboard.spaceKey.wasPressedThisFrame, "额外更新仍保留同一次按键标记。");
                Assert.AreEqual(UIState.Showed, host.State);
                Assert.AreEqual(1, shows);
                Assert.AreEqual(0, hides);

                // 不等待新的渲染帧；释放再按下是独立输入，必须再次切换。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), time: 2);
                update.Invoke(null, new object[] { updateType });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), time: 3);
                update.Invoke(null, new object[] { updateType });
                Assert.AreEqual(UIState.Hided, host.State);
                Assert.AreEqual(1, hides);
            }
            finally
            {
                try { if (host) Object.DestroyImmediate(host.gameObject); }
                finally { input.TearDown(); }
            }
        }
    }
}
