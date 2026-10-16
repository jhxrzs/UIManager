using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private UIBase CreateRaycastPanel(bool blocksWhenVisible, bool interactable, out CanvasGroup group,
            bool activate = true)
        {
            var go = new GameObject("RaycastPanel", typeof(RectTransform), typeof(CanvasGroup));
            _createdObjects.Add(go);
            go.SetActive(false);
            group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = blocksWhenVisible;
            group.interactable = interactable;
            var ui = go.AddComponent<UIBase>();
            ui.enabled = false;
            if (activate) go.SetActive(true);
            return ui;
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void Raycast_SyncCycles_ShouldRestoreVisibleConfigurationWithoutChangingInteractable(
            bool blocksWhenVisible, bool interactable)
        {
            var ui = CreateRaycastPanel(blocksWhenVisible, interactable, out var group);
            Assert.IsFalse(group.blocksRaycasts, "隐藏初态在 Awake 后应立即放行射线。");
            Assert.AreEqual(blocksWhenVisible, ui.BlocksRaycastsWhenVisible);
            for (var i = 0; i < 3; i++)
            {
                ui.Show();
                Assert.AreEqual(blocksWhenVisible, group.blocksRaycasts);
                Assert.AreEqual(interactable, group.interactable);
                ui.Hide();
                Assert.IsFalse(group.blocksRaycasts);
                Assert.AreEqual(interactable, group.interactable);
            }
        }

        [Test]
        public void Raycast_StateSubscribers_ShouldObserveAppliedRaycastState()
        {
            var ui = CreateRaycastPanel(true, true, out var group);
            var observations = new List<string>();
            ui.OnStateChanged += (_, current) => observations.Add(current + ":" + group.blocksRaycasts);

            ui.Show();
            ui.Hide();

            CollectionAssert.AreEqual(new[] { "Showing:True", "Showed:True", "Hiding:True", "Hided:False" }, observations);
        }

        [UnityTest]
        public IEnumerator Raycast_ParallelHideActions_ShouldReleaseOnlyAfterAllActionsComplete()
        {
            var ui = CreateRaycastPanel(true, false, out var group);
            ui.Show();
            var first = new UniTaskCompletionSource();
            var second = new UniTaskCompletionSource();
            ui.HideAsyncActions += _ => first.Task;
            ui.HideAsyncActions += _ => second.Task;
            var operation = CaptureException(ui.HideAsync(CancellationToken.None));
            Assert.AreEqual(UIState.Hiding, ui.State);
            Assert.IsTrue(group.blocksRaycasts);

            first.TrySetResult();
            yield return null;

            Assert.IsFalse(operation.IsCompleted);
            Assert.IsTrue(group.blocksRaycasts, "单个隐藏行为完成不能提前放行射线。");
            Assert.IsFalse(group.interactable);
            second.TrySetResult();
            yield return WaitForTask(operation);
            Assert.IsNull(operation.Result);
            Assert.AreEqual(UIState.Hided, ui.State);
            Assert.IsFalse(group.blocksRaycasts);
            Assert.IsFalse(group.interactable);
        }

        [UnityTest]
        public IEnumerator Raycast_CanceledHide_ShouldKeepBlockingUntilSuccessfulHide()
        {
            var ui = CreateRaycastPanel(true, true, out var group);
            ui.Show();
            ui.HideAsyncActions += WaitUntilCanceled;
            using var cancellation = new CancellationTokenSource();
            var hiding = CaptureException(ui.HideAsync(cancellation.Token));
            cancellation.Cancel();
            yield return WaitForTask(hiding);

            Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(UIState.Hiding, ui.State);
            Assert.IsTrue(group.blocksRaycasts, "取消不是隐藏完成。");
            Assert.IsTrue(group.interactable);
            ui.Hide();
            Assert.IsFalse(group.blocksRaycasts);
        }

        [UnityTest]
        public IEnumerator Raycast_FailedHide_ShouldKeepBlockingUntilSuccessfulHide()
        {
            var ui = CreateRaycastPanel(true, true, out var group);
            ui.Show();
            var failure = new InvalidOperationException("hide effect failed");
            ui.HideAsyncActions += _ => UniTask.FromException(failure);
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            yield return WaitForTask(hiding);

            Assert.AreSame(failure, hiding.Result);
            Assert.AreEqual(UIState.Hiding, ui.State);
            Assert.IsTrue(group.blocksRaycasts);
            Assert.IsTrue(group.interactable);
            ui.Hide();
            Assert.IsFalse(group.blocksRaycasts);
        }

        [UnityTest]
        public IEnumerator Raycast_PreCanceledRequests_ShouldNotChangeRaycastsOrInteractable()
        {
            var ui = CreateRaycastPanel(true, false, out var group);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var showing = CaptureException(ui.ShowAsync(cancellation.Token));
            yield return WaitForTask(showing);
            Assert.That(showing.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.IsFalse(group.blocksRaycasts);
            ui.Show();
            var hiding = CaptureException(ui.HideAsync(cancellation.Token));
            yield return WaitForTask(hiding);
            Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.IsTrue(group.blocksRaycasts);
            Assert.IsFalse(group.interactable);
        }

        [UnityTest]
        public IEnumerator Raycast_CompoundEffects_RectFirst_ShouldKeepBlockingDuringTransitions() =>
            VerifyCompoundRaycastEffects(true);

        [UnityTest]
        public IEnumerator Raycast_CompoundEffects_AlphaFirst_ShouldKeepBlockingDuringTransitions() =>
            VerifyCompoundRaycastEffects(false);

        private IEnumerator VerifyCompoundRaycastEffects(bool rectFirst)
        {
            var rect = CreateRectEffect(out var ui);
            SetRectEffectField(rect, "showDuration", 0.05f);
            SetRectEffectField(rect, "hideDuration", 0.05f);
            ui.gameObject.SetActive(false);
            var alpha = ui.gameObject.AddComponent<AlphaEffectController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(EffectControllerBase).GetField("showDuration", flags).SetValue(alpha, 0f);
            typeof(EffectControllerBase).GetField("hideDuration", flags).SetValue(alpha, 0f);
            typeof(EffectControllerBase).GetMethod("OnValidate", flags).Invoke(alpha, null);
            ui.gameObject.SetActive(true);
            var rectShow = (Func<CancellationToken, UniTask>)Delegate.CreateDelegate(typeof(Func<CancellationToken, UniTask>),
                rect, typeof(RectEffectController).GetMethod("OnUIShowAsync", flags));
            var rectHide = (Func<CancellationToken, UniTask>)Delegate.CreateDelegate(typeof(Func<CancellationToken, UniTask>),
                rect, typeof(RectEffectController).GetMethod("OnUIHideAsync", flags));
            var alphaShow = (Func<CancellationToken, UniTask>)Delegate.CreateDelegate(typeof(Func<CancellationToken, UniTask>),
                alpha, typeof(EffectControllerBase).GetMethod("OnUIShowAsync", flags));
            var alphaHide = (Func<CancellationToken, UniTask>)Delegate.CreateDelegate(typeof(Func<CancellationToken, UniTask>),
                alpha, typeof(EffectControllerBase).GetMethod("OnUIHideAsync", flags));
            ui.ShowAsyncActions -= rectShow;
            ui.ShowAsyncActions -= alphaShow;
            ui.HideAsyncActions -= rectHide;
            ui.HideAsyncActions -= alphaHide;
            ui.ShowAsyncActions += rectFirst ? rectShow : alphaShow;
            ui.ShowAsyncActions += rectFirst ? alphaShow : rectShow;
            ui.HideAsyncActions += rectFirst ? rectHide : alphaHide;
            ui.HideAsyncActions += rectFirst ? alphaHide : rectHide;
            var group = ui.GetComponent<CanvasGroup>();
            group.interactable = false;
            ui.Show();

            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            Assert.AreEqual(UIState.Hiding, ui.State);
            Assert.IsFalse(hiding.IsCompleted);
            Assert.IsTrue(group.blocksRaycasts, "瞬间完成的透明度效果不能抢先关闭位移动画期间的射线阻挡。");
            Assert.IsFalse(group.interactable);
            yield return WaitForTask(hiding);
            Assert.IsNull(hiding.Result);
            Assert.IsFalse(group.blocksRaycasts);
            var showing = CaptureException(ui.ShowAsync(CancellationToken.None));
            Assert.AreEqual(UIState.Showing, ui.State);
            Assert.IsTrue(group.blocksRaycasts, "允许动画中交互，不等待所有动画完成才恢复射线配置。");
            Assert.IsFalse(group.interactable);
            yield return WaitForTask(showing);
            Assert.IsNull(showing.Result);
            Assert.IsTrue(group.blocksRaycasts);
            Assert.IsFalse(group.interactable);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Raycast_Lifecycle_ShouldNotChangeChildButtonInteractability(bool interactable)
        {
            var ui = CreateRaycastPanel(true, interactable, out var group);
            var child = new GameObject("Button", typeof(RectTransform), typeof(Button));
            _createdObjects.Add(child);
            child.transform.SetParent(ui.transform, false);
            var button = child.GetComponent<Button>();
            foreach (var show in new[] { true, false, true })
            {
                if (show) ui.Show();
                else ui.Hide();
                Assert.IsTrue(button.interactable, "不改写按钮自身设置。");
                Assert.AreEqual(interactable, group.interactable);
                Assert.AreEqual(interactable, button.IsInteractable());
            }
        }

        [UnityTest]
        public IEnumerator Raycast_RuntimeVisibleConfiguration_ShouldNotEnableHiddenPanelOrChangeHiding()
        {
            var ui = CreateRaycastPanel(true, true, out var group);
            ui.BlocksRaycastsWhenVisible = false;
            Assert.IsFalse(group.blocksRaycasts);
            ui.Show();
            Assert.IsFalse(group.blocksRaycasts);
            ui.BlocksRaycastsWhenVisible = true;
            Assert.IsTrue(group.blocksRaycasts);
            var pending = new UniTaskCompletionSource();
            ui.HideAsyncActions += _ => pending.Task;
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            ui.BlocksRaycastsWhenVisible = false;
            Assert.IsTrue(group.blocksRaycasts, "Hiding 保持已有值，直到全部隐藏行为完成。");
            pending.TrySetResult();
            yield return WaitForTask(hiding);
            ui.Show();
            Assert.IsFalse(group.blocksRaycasts);
            ui.Hide();
            ui.BlocksRaycastsWhenVisible = true;
            Assert.IsFalse(group.blocksRaycasts, "修改显示配置不能让隐藏面板挡住射线。");
            ui.Show();
            Assert.IsTrue(group.blocksRaycasts);
        }

        [Test]
        public void Raycast_ParentHideAndShowWithParent_ShouldApplyEachPanelsState()
        {
            var parent = CreateRaycastPanel(true, true, out var parentGroup);
            var child = CreateRaycastPanel(true, true, out var childGroup);
            child.transform.SetParent(parent.transform, false);
            child.ShowWithParent = true;
            parent.Show();
            Assert.IsTrue(parentGroup.blocksRaycasts);
            Assert.IsTrue(childGroup.blocksRaycasts);
            parent.Hide();
            Assert.IsFalse(parentGroup.blocksRaycasts);
            Assert.IsFalse(childGroup.blocksRaycasts);
            parent.Show();
            Assert.IsTrue(parentGroup.blocksRaycasts);
            Assert.IsTrue(childGroup.blocksRaycasts);
        }

        [Test]
        public void Raycast_ShowBeforeAwake_ShouldCaptureConfigurationOnlyOnce()
        {
            var ui = CreateRaycastPanel(false, true, out var group, false);
            ui.Show();
            Assert.AreEqual(UIState.Showed, ui.State);
            Assert.IsFalse(group.blocksRaycasts);
            ui.gameObject.SetActive(true);
            Assert.IsFalse(ui.BlocksRaycastsWhenVisible);
            ui.Hide();
            ui.Show();
            Assert.IsFalse(group.blocksRaycasts);
        }

        [UnityTest]
        public IEnumerator Raycast_GraphicRaycaster_ShouldPassThroughHiddenPanelAndRestoreOnShow()
        {
            var canvasObject = new GameObject("RaycastCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _createdObjects.Add(canvasObject);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var eventObject = new GameObject("RaycastEvents", typeof(EventSystem));
            _createdObjects.Add(eventObject);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            _createdObjects.Add(background);
            background.transform.SetParent(canvasObject.transform, false);
            ((RectTransform)background.transform).sizeDelta = new Vector2(200, 200);
            var ui = CreateRaycastPanel(true, true, out _);
            ui.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)ui.transform;
            rect.sizeDelta = new Vector2(200, 200);
            ui.gameObject.AddComponent<Image>();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(eventObject.GetComponent<EventSystem>())
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            var raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            ui.Show();
            raycaster.Raycast(pointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreSame(ui.gameObject, hits[0].gameObject);
            ui.Hide();
            hits.Clear();
            raycaster.Raycast(pointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreSame(background, hits[0].gameObject);
            ui.Show();
            hits.Clear();
            raycaster.Raycast(pointer, hits);
            Assert.AreSame(ui.gameObject, hits[0].gameObject);
        }
    }
}
