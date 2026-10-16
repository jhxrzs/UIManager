using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private static void TriggerLifecycleControl(InteractionControl control, bool show)
        {
            if (control.Toggle) control.Toggle.onValueChanged.Invoke(show);
            else control.Trigger();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Interaction_DisabledOwner_ShouldIgnoreBothDirectionsAndResumeWhenEnabled(bool useToggle, bool isAsync)
        {
            var button = CreateLifecycleControl(false, isAsync);
            var toggle = CreateLifecycleControl(true, isAsync);
            var host = CreateLifecycleHost(button, toggle);
            StartLifecycleHost(host);
            var control = useToggle ? toggle : button;
            var syncShows = 0;
            var asyncShows = 0;
            var syncHides = 0;
            var asyncHides = 0;
            host.ShowActions += () => syncShows++;
            host.HideActions += () => syncHides++;
            host.ShowAsyncActions += _ => { asyncShows++; return UniTask.CompletedTask; };
            host.HideAsyncActions += _ => { asyncHides++; return UniTask.CompletedTask; };
            var afterShows = 0;
            var afterHides = 0;
            host.AfterUIShowedEvent += () => afterShows++;
            host.AfterUIHidedEvent += () => afterHides++;

            Assert.IsFalse(host.enabled);
            TriggerLifecycleControl(control, true);
            Assert.AreEqual(UIState.Hided, host.State);
            Assert.AreEqual(0, syncShows + asyncShows);
            Assert.AreEqual(0, afterShows);

            host.enabled = true;
            TriggerLifecycleControl(control, true);
            Assert.AreEqual(UIState.Showed, host.State);
            Assert.AreEqual(isAsync ? 0 : 1, syncShows);
            Assert.AreEqual(isAsync ? 1 : 0, asyncShows);
            Assert.AreEqual(1, afterShows, "重新启用应恢复原有绑定，且只响应一次。");

            host.enabled = false;
            TriggerLifecycleControl(control, false);
            Assert.AreEqual(UIState.Showed, host.State);
            Assert.AreEqual(0, syncHides + asyncHides);
            Assert.AreEqual(0, afterHides);

            host.enabled = true;
            TriggerLifecycleControl(control, false);
            Assert.AreEqual(UIState.Hided, host.State);
            Assert.AreEqual(isAsync ? 0 : 1, syncHides);
            Assert.AreEqual(isAsync ? 1 : 0, asyncHides);
            Assert.AreEqual(1, afterHides);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Interaction_InactiveEnabledOwner_ShouldStillOpenFromExternalControl(bool useToggle, bool isAsync)
        {
            var button = CreateLifecycleControl(false, isAsync);
            var toggle = CreateLifecycleControl(true, isAsync);
            var host = CreateLifecycleHost(button, toggle);
            host.enabled = true;
            host.ShowActions += () => host.gameObject.SetActive(true);
            host.HideActions += () => host.gameObject.SetActive(false);
            StartLifecycleHost(host);
            var control = useToggle ? toggle : button;
            var shows = 0;
            var hides = 0;
            host.AfterUIShowedEvent += () => shows++;
            host.AfterUIHidedEvent += () => hides++;

            Assert.IsTrue(host.enabled);
            Assert.IsFalse(host.gameObject.activeInHierarchy);
            for (var round = 0; round < 2; round++)
            {
                TriggerLifecycleControl(control, true);
                Assert.AreEqual(UIState.Showed, host.State);
                Assert.IsTrue(host.gameObject.activeInHierarchy, "仅失活的面板应能由外部控件重新显示。");
                TriggerLifecycleControl(control, false);
                Assert.AreEqual(UIState.Hided, host.State);
                Assert.IsFalse(host.gameObject.activeInHierarchy);
                Assert.IsTrue(host.enabled, "GameObject 失活不能被当作组件主动禁用。");
            }
            Assert.AreEqual(2, shows);
            Assert.AreEqual(2, hides);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Interaction_DisabledByEarlierControlListener_ShouldIgnoreCurrentNotification(bool useToggle, bool isAsync)
        {
            var button = CreateLifecycleControl(false, isAsync);
            var toggle = CreateLifecycleControl(true, isAsync);
            var host = CreateLifecycleHost(button, toggle);
            host.enabled = true;
            var control = useToggle ? toggle : button;
            var externalCalls = 0;
            // 在实体绑定前注册，真实 UnityEvent 按顺序先禁用宿主，再进入实体回调。
            control.AddListener(() => { externalCalls++; host.enabled = false; });
            StartLifecycleHost(host);
            var actions = 0;
            var afterShows = 0;
            host.ShowActions += () => actions++;
            host.ShowAsyncActions += _ => { actions++; return UniTask.CompletedTask; };
            host.AfterUIShowedEvent += () => afterShows++;

            TriggerLifecycleControl(control, true);

            Assert.AreEqual(1, externalCalls, "不得影响控件上的其他业务监听。");
            Assert.IsFalse(host.enabled);
            Assert.AreEqual(UIState.Hided, host.State);
            Assert.AreEqual(0, actions, "实体执行入口必须复核同一轮通知中发生的禁用。");
            Assert.AreEqual(0, afterShows);
        }
    }
}
