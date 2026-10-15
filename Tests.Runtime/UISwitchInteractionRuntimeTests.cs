using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private Button CreateSwitchInteractionButton(UIBase target, bool isAsync)
        {
            var go = new GameObject("SwitchInteractionButton", typeof(RectTransform), typeof(Button));
            _createdObjects.Add(go);
            var button = go.GetComponent<Button>();
            var entity = new ButtonEntity { IsAsync = isAsync };
            typeof(ButtonEntity).GetField("button", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entity, button);
            typeof(ButtonEntity).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entity, ShowHideAction.Switch);
            entity.Init(target);
            if (isAsync)
            {
                target.ShowAsyncActions += _ => UniTask.CompletedTask;
                target.HideAsyncActions += _ => UniTask.CompletedTask;
            }
            return button;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_IdleTwoClicks_ShouldReturnToHidden(bool isAsync)
        {
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            var button = CreateSwitchInteractionButton(target, isAsync);
            button.onClick.Invoke();
            Assert.AreEqual(UIState.Showed, target.State, "First idle click must show");
            button.onClick.Invoke();
            Assert.AreEqual(UIState.Hided, target.State, "Second idle click must hide");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_QueuedShowThenSwitch_ShouldEndHidden(bool isAsync)
        {
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            var button = CreateSwitchInteractionButton(target, isAsync);
            var states = new List<UIState>();
            target.OnStateChanged += (_, current) => states.Add(current);
            source.BeforeUIShowEvent += () =>
            {
                target.Show();
                button.onClick.Invoke();
            };
            source.Show();
            Assert.AreEqual(UIState.Showed, source.State, "Source must finish normally");
            Assert.AreEqual(UIState.Hided, target.State, "Show then Switch must hide at execution time. Actual transitions: " + string.Join(" -> ", states));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_QueuedTwoSwitches_ShouldReturnToHidden(bool isAsync)
        {
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            var button = CreateSwitchInteractionButton(target, isAsync);
            var states = new List<UIState>();
            target.OnStateChanged += (_, current) => states.Add(current);
            source.BeforeUIShowEvent += () =>
            {
                button.onClick.Invoke();
                button.onClick.Invoke();
            };
            source.Show();
            Assert.AreEqual(UIState.Hided, target.State, "Two queued switches must restore original state. Actual transitions: " + string.Join(" -> ", states));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_QueuedHideThenSwitch_ShouldEndShown(bool isAsync)
        {
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            var button = CreateSwitchInteractionButton(target, isAsync);
            target.Show();
            var states = new List<UIState>();
            target.OnStateChanged += (_, current) => states.Add(current);
            source.BeforeUIShowEvent += () =>
            {
                target.Hide();
                button.onClick.Invoke();
            };
            source.Show();
            Assert.AreEqual(UIState.Showed, target.State, "Hide then Switch must show at execution time. Actual transitions: " + string.Join(" -> ", states));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_QueuedSwitchHideSwitch_ShouldPreserveSubmissionOrder(bool isAsync)
        {
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            var button = CreateSwitchInteractionButton(target, isAsync);
            var states = new List<UIState>();
            target.OnStateChanged += (_, current) => states.Add(current);
            source.BeforeUIShowEvent += () =>
            {
                button.onClick.Invoke();
                target.Hide();
                button.onClick.Invoke();
            };

            source.Show();

            // 判断方向后必须直接执行，不能再排到后续 Hide 请求之后。
            CollectionAssert.AreEqual(new[]
            {
                UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided,
                UIState.Showing, UIState.Showed
            }, states);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_CallbackLoop_ShouldRespectBehaviorExecutionLimit(bool isAsync)
        {
            using var limit = new LoopLimitScope(2);
            var target = CreateUI("SwitchLoop", CreateObject("SwitchLoopRoot").transform);
            var button = CreateSwitchInteractionButton(target, isAsync);
            var shows = 0;
            var hides = 0;
            target.AfterUIShowedEvent += () => { shows++; button.onClick.Invoke(); };
            target.AfterUIHidedEvent += () => { hides++; button.onClick.Invoke(); };
            LogAssert.Expect(LogType.Exception, new Regex("UIRequestLoopException: UI循环调度次数超限"));

            button.onClick.Invoke();

            Assert.AreEqual(2, shows);
            Assert.AreEqual(2, hides);
            Assert.AreEqual(UIState.Hided, target.State);
            var independent = CreateUI("AfterSwitchLoop", CreateObject("IndependentRoot").transform);
            independent.Show();
            Assert.AreEqual(UIState.Showed, independent.State, "循环被阻止后，调度器应能继续处理独立请求。");
        }

        [UnityTest]
        public IEnumerator SwitchInteraction_DuringPendingShow_ShouldCancelAndSwitchInOrder()
        {
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            var button = CreateSwitchInteractionButton(target, true);
            var pending = new UniTaskCompletionSource();
            var shows = 0;
            target.ShowAsyncActions += _ => ++shows == 1 ? pending.Task : UniTask.CompletedTask;
            var initialShow = CaptureException(target.ShowAsync(CancellationToken.None));
            Assert.AreEqual(UIState.Showing, target.State);
            source.BeforeUIShowEvent += () =>
            {
                button.onClick.Invoke();
                button.onClick.Invoke();
            };

            source.Show();

            // 第一次切换隐藏并取消旧显示，第二次依据最新状态重新显示。
            Assert.AreEqual(UIState.Showed, target.State);
            Assert.AreEqual(2, shows);
            pending.TrySetResult();
            yield return WaitForTask(initialShow);
            Assert.That(initialShow.Result, Is.InstanceOf<System.OperationCanceledException>());
            Assert.AreEqual(UIState.Showed, target.State, "旧显示恢复不得覆盖新一轮生命周期。");
        }
    }
}
