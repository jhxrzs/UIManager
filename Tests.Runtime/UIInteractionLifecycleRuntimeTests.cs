using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private sealed class TrackedButtonEntity : ButtonEntity
        {
            internal int UnbindCount;
            public override void Unbind() { UnbindCount++; base.Unbind(); }
        }

        private sealed class TrackedToggleEntity : ToggleEntity
        {
            internal int UnbindCount;
            public override void Unbind() { UnbindCount++; base.Unbind(); }
        }

        private sealed class InteractionControl
        {
            internal ShowHideInteractionEntity Entity;
            internal Button Button;
            internal Toggle Toggle;
            internal void Trigger()
            {
                if (Button) Button.onClick.Invoke();
                else Toggle.onValueChanged.Invoke(true);
            }
            internal void AddListener(Action listener)
            {
                if (Button) Button.onClick.AddListener(() => listener());
                else Toggle.onValueChanged.AddListener(_ => listener());
            }
        }

        private InteractionControl CreateLifecycleControl(bool useToggle, bool isAsync = false,
            ShowHideInteractionEntity entity = null)
        {
            var go = new GameObject("ExternalControl", typeof(RectTransform),
                useToggle ? typeof(Toggle) : typeof(Button));
            _createdObjects.Add(go);
            var control = new InteractionControl
            {
                Entity = entity ?? (useToggle ? (ShowHideInteractionEntity)new ToggleEntity() : new ButtonEntity()),
                Button = go.GetComponent<Button>(),
                Toggle = go.GetComponent<Toggle>()
            };
            control.Entity.IsAsync = isAsync;
            SetLifecycleControl(control.Entity, control);
            if (!useToggle)
                typeof(ButtonEntity).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(control.Entity, ShowHideAction.Switch);
            return control;
        }

        private static void SetLifecycleControl(ShowHideInteractionEntity entity, InteractionControl control)
        {
            var type = entity is ButtonEntity ? typeof(ButtonEntity) : typeof(ToggleEntity);
            type.GetField(entity is ButtonEntity ? "button" : "toggle",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(entity, entity is ButtonEntity ? (object)control.Button : control.Toggle);
        }

        private UIBaseWithShowHideInteraction CreateLifecycleHost(InteractionControl button,
            InteractionControl toggle)
        {
            var host = CreateObject("InteractionHost", CreateObject("HostRoot").transform)
                .AddComponent<UIBaseWithShowHideInteraction>();
            host.enabled = false;
            var type = typeof(UIBaseWithShowHideInteraction);
            type.GetField("buttonInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, new List<ButtonEntity> { (ButtonEntity)button.Entity });
            type.GetField("toggleInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, new List<ToggleEntity> { (ToggleEntity)toggle.Entity });
            return host;
        }

        private static void StartLifecycleHost(UIBaseWithShowHideInteraction host)
        {
            // 显式调用真实 Start，使测试不依赖帧调度；销毁仍走 Unity 的真实 OnDestroy。
            typeof(UIBaseWithShowHideInteraction).GetMethod("Start",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, null);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Interaction_Reinitialize_ShouldExecuteOnlyOnce(bool useToggle, bool isAsync)
        {
            var ui = CreateUI("Target", CreateObject("TargetRoot").transform);
            var control = CreateLifecycleControl(useToggle, isAsync);
            var shows = 0;
            // 显示后再隐藏，让重复注册的第二个监听无法被 Showed 状态短路掩盖。
            ui.AfterUIShowedEvent += () => { shows++; ui.Hide(); };
            control.Entity.Init(ui);
            control.Entity.Init(ui);

            control.Trigger();

            Assert.AreEqual(1, shows);
            Assert.AreEqual(UIState.Hided, ui.State);
            control.Entity.Unbind();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_Unbind_ShouldBeIdempotentAndPreserveOtherListeners(bool useToggle)
        {
            var ui = CreateUI("Target", CreateObject("TargetRoot").transform);
            var control = CreateLifecycleControl(useToggle);
            var externalCalls = 0;
            control.AddListener(() => externalCalls++);
            control.Entity.Init(ui);

            control.Entity.Unbind();
            control.Entity.Unbind();
            control.Trigger();

            Assert.AreEqual(UIState.Hided, ui.State);
            Assert.AreEqual(1, externalCalls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_Rebind_ShouldDetachOldControlAndTarget(bool useToggle)
        {
            var first = CreateUI("First", CreateObject("FirstRoot").transform);
            var second = CreateUI("Second", CreateObject("SecondRoot").transform);
            var oldControl = CreateLifecycleControl(useToggle);
            var newControl = CreateLifecycleControl(useToggle);
            oldControl.Entity.Init(first);
            SetLifecycleControl(oldControl.Entity, newControl);
            oldControl.Entity.Init(second);

            oldControl.Trigger();
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreEqual(UIState.Hided, second.State);
            newControl.Trigger();
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreEqual(UIState.Showed, second.State);
            oldControl.Entity.Unbind();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_ControlDestroyedBeforeUnbind_ShouldAllowCleanup(bool useToggle)
        {
            var ui = CreateUI("Target", CreateObject("TargetRoot").transform);
            var control = CreateLifecycleControl(useToggle);
            control.Entity.Init(ui);
            Object.DestroyImmediate(useToggle ? control.Toggle.gameObject : control.Button.gameObject);

            Assert.DoesNotThrow(control.Entity.Unbind);
            Assert.DoesNotThrow(control.Entity.Unbind);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_OwnerDestroy_ShouldUnbindBothControlsAndPreserveOtherListeners(bool isAsync)
        {
            var buttonEntity = new TrackedButtonEntity();
            var toggleEntity = new TrackedToggleEntity();
            var button = CreateLifecycleControl(false, isAsync, buttonEntity);
            var toggle = CreateLifecycleControl(true, isAsync, toggleEntity);
            var externalCalls = 0;
            button.AddListener(() => externalCalls++);
            toggle.AddListener(() => externalCalls++);
            var host = CreateLifecycleHost(button, toggle);
            StartLifecycleHost(host);
            var buttonUnbinds = buttonEntity.UnbindCount;
            var toggleUnbinds = toggleEntity.UnbindCount;

            Object.DestroyImmediate(host.gameObject);

            Assert.AreEqual(buttonUnbinds + 1, buttonEntity.UnbindCount);
            Assert.AreEqual(toggleUnbinds + 1, toggleEntity.UnbindCount);
            button.Trigger();
            toggle.Trigger();
            Assert.AreEqual(2, externalCalls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_Hide_ShouldKeepExternalControlsAbleToShowPanel(bool isAsync)
        {
            var button = CreateLifecycleControl(false, isAsync);
            var toggle = CreateLifecycleControl(true, isAsync);
            var host = CreateLifecycleHost(button, toggle);
            StartLifecycleHost(host);

            host.Show();
            host.Hide();
            button.Trigger();
            Assert.AreEqual(UIState.Showed, host.State);
            host.Hide();
            toggle.Trigger();
            Assert.AreEqual(UIState.Showed, host.State);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Interaction_DestroyOwnerDuringNotification_ShouldIgnoreStaleCallback(bool useToggle)
        {
            var button = CreateLifecycleControl(false, true);
            var toggle = CreateLifecycleControl(true, true);
            var host = CreateLifecycleHost(button, toggle);
            var control = useToggle ? toggle : button;
            control.AddListener(() => Object.DestroyImmediate(host.gameObject));
            StartLifecycleHost(host);

            // UnityEvent 已捕获本轮回调时，解绑不能保证阻止旧快照继续派发。
            control.Trigger();

            Assert.IsFalse(host);
        }
    }
}
