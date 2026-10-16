using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Show_CompletionStateCallbackLosesParentAdmission_ShouldSkipSuccessNotification(bool strict)
        {
            var oldParent = CreateUI("CompletionOldParent", CreateObject("CompletionOldRoot").transform);
            var newParent = CreateUI("CompletionHiddenParent", CreateObject("CompletionNewRoot").transform);
            oldParent.Show();
            var child = CreateUI("CompletionChild", oldParent.transform);
            if (strict) child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            var afterCount = 0;
            var newParentShows = 0;
            child.AfterUIShowedEvent += () => afterCount++;
            newParent.ShowActions += () => newParentShows++;
            Action<UIState, UIState> moveOnShowed = (_, current) =>
            {
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };
            child.OnStateChanged += moveOnShowed;

            child.Show();

            Assert.AreEqual(newParent.transform, child.transform.parent);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(UIState.Hided, newParent.State);
            Assert.AreEqual(0, newParentShows, "完成阶段不得重新补齐换父后的父链。");
            Assert.AreEqual(0, afterCount, "状态通知导致失去准入时，不得继续派发显示成功通知。");

            child.OnStateChanged -= moveOnShowed;
            child.transform.SetParent(oldParent.transform, false);
            child.Show();
            Assert.AreEqual(UIState.Showed, child.State);
            Assert.AreEqual(1, afterCount, "恢复准入后的独立显示请求仍应成功一次。");
        }

        [UnityTest]
        public IEnumerator ShowAsync_CompletionStateCallbackLosesStrictParentAdmission_ShouldSkipSuccessNotification() =>
            VerifyCompletionParentAdmissionAsync(true);

        [UnityTest]
        public IEnumerator ShowAsync_CompletionStateCallbackLosesAutoParentAdmission_ShouldSkipSuccessNotification() =>
            VerifyCompletionParentAdmissionAsync(false);

        private IEnumerator VerifyCompletionParentAdmissionAsync(bool strict)
        {
            var oldParent = CreateUI("AsyncCompletionOldParent", CreateObject("AsyncCompletionOldRoot").transform);
            var newParent = CreateUI("AsyncCompletionHiddenParent", CreateObject("AsyncCompletionNewRoot").transform);
            oldParent.Show();
            var child = CreateUI("AsyncCompletionChild", oldParent.transform);
            if (strict) child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            var pending = new UniTaskCompletionSource();
            child.ShowAsyncActions += _ => pending.Task;
            var afterCount = 0;
            var newParentShows = 0;
            child.AfterUIShowedEvent += () => afterCount++;
            newParent.ShowActions += () => newParentShows++;
            child.OnStateChanged += (_, current) =>
            {
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };

            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            pending.TrySetResult();
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(newParent.transform, child.transform.parent);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(UIState.Hided, newParent.State);
            Assert.AreEqual(0, newParentShows, "异步完成阶段不得重新补齐换父后的父链。");
            Assert.AreEqual(0, afterCount, "本轮任务取消时，不得已向业务报告显示成功。");
        }

        [UnityTest]
        public IEnumerator ShowAsync_CompletionStateCallbackAddsEqualPrioritySibling_ShouldPreserveSiblingAndSkipSuccess()
        {
            var oldParent = CreateUI("AsyncMutexOldParent", CreateObject("AsyncMutexOldRoot").transform);
            var newParent = CreateUI("AsyncMutexNewParent", CreateObject("AsyncMutexNewRoot").transform);
            oldParent.Show();
            newParent.Show();
            var existing = CreateUI("AsyncMutexExisting", newParent.transform);
            existing.Show();
            var child = CreateUI("AsyncMutexMoving", oldParent.transform);
            var pending = new UniTaskCompletionSource();
            child.ShowAsyncActions += _ => pending.Task;
            var existingHides = 0;
            var afterCount = 0;
            existing.HideActions += () => existingHides++;
            existing.HideAsyncActions += _ => { existingHides++; return UniTask.CompletedTask; };
            child.AfterUIShowedEvent += () => afterCount++;
            child.OnStateChanged += (_, current) =>
            {
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };

            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            pending.TrySetResult();
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(UIState.Showed, existing.State);
            Assert.AreEqual(0, existingHides, "异步完成阶段只停止失去互斥资格的自身，不隐藏新兄弟。");
            Assert.AreEqual(0, afterCount);
        }

        [Test]
        public void Show_CompletionStateCallbackAddsEqualPrioritySibling_ShouldReconcileBeforeSuccess()
        {
            var oldParent = CreateUI("SyncMutexOldParent", CreateObject("SyncMutexOldRoot").transform);
            var newParent = CreateUI("SyncMutexNewParent", CreateObject("SyncMutexNewRoot").transform);
            oldParent.Show();
            newParent.Show();
            var existing = CreateUI("SyncMutexExisting", newParent.transform);
            existing.Show();
            var child = CreateUI("SyncMutexMoving", oldParent.transform);
            var existingHides = 0;
            var afterCount = 0;
            var existingStateAtSuccess = UIState.Showed;
            existing.HideActions += () => existingHides++;
            child.AfterUIShowedEvent += () => { afterCount++; existingStateAtSuccess = existing.State; };
            child.OnStateChanged += (_, current) =>
            {
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };

            child.Show();

            Assert.AreEqual(UIState.Showed, child.State);
            Assert.AreEqual(UIState.Hided, existing.State);
            Assert.AreEqual(1, existingHides);
            Assert.AreEqual(1, afterCount);
            Assert.AreEqual(UIState.Hided, existingStateAtSuccess, "同步成功通知必须晚于本轮动态互斥协调。");
        }

        [UnityTest]
        public IEnumerator ShowAsync_CompletionStateCallbackMovesToReadyEmptyParent_ShouldStillSucceed()
        {
            var oldParent = CreateUI("ValidCompletionOldParent", CreateObject("ValidCompletionOldRoot").transform);
            var newParent = CreateUI("ValidCompletionNewParent", CreateObject("ValidCompletionNewRoot").transform);
            oldParent.Show();
            newParent.Show();
            var child = CreateUI("ValidCompletionChild", oldParent.transform);
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            var pending = new UniTaskCompletionSource();
            child.ShowAsyncActions += _ => pending.Task;
            var afterCount = 0;
            child.AfterUIShowedEvent += () => afterCount++;
            child.OnStateChanged += (_, current) =>
            {
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };

            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            pending.TrySetResult();
            yield return WaitForTask(operation);

            Assert.IsNull(operation.Result);
            Assert.AreEqual(newParent.transform, child.transform.parent);
            Assert.AreEqual(UIState.Showed, child.State);
            Assert.AreEqual(1, afterCount, "合法换父不能被一律取消或漏发成功通知。");
        }
    }
}
