using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private UIBase CreateBlockedAutoChain(bool blockAncestor, out UIBase blocked, out UIBase holder,
            out UIBase directParent)
        {
            var root = CreateObject("BlockedAutoRoot");
            blocked = CreateUI("BlockedParent", root.transform);
            holder = CreateUI("PriorityHolder", root.transform);
            holder.MutexPriority = 3;
            holder.Show();
            directParent = blockAncestor ? CreateUI("IntermediateParent", blocked.transform) : blocked;
            return CreateUI("AutoChild", directParent.transform);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AutoParent_SyncRejectedAncestor_ShouldStopChildAndLeaveHolderUntouched(bool blockAncestor)
        {
            var child = CreateBlockedAutoChain(blockAncestor, out var blocked, out var holder, out var parent);
            var parentShows = 0;
            var childShows = 0;
            var childAfter = 0;
            parent.ShowActions += () => parentShows++;
            child.ShowActions += () => childShows++;
            child.AfterUIShowedEvent += () => childAfter++;

            child.Show();

            Assert.AreEqual(UIState.Hided, blocked.State);
            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Showed, holder.State);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(0, parentShows);
            Assert.AreEqual(0, childShows);
            Assert.AreEqual(0, childAfter);
            Assert.IsFalse(child.GetComponent<UnityEngine.CanvasGroup>().blocksRaycasts);
            holder.Hide();
            child.Show();
            Assert.AreEqual(UIState.Showed, blocked.State);
            Assert.AreEqual(UIState.Showed, parent.State);
            Assert.AreEqual(UIState.Showed, child.State, "阻挡解除后的新请求必须能成功。");
            Assert.AreEqual(1, childAfter);
        }

        [UnityTest]
        public IEnumerator AutoParent_AsyncRejectedParent_ShouldRejectBeforeStarting() =>
            VerifyAsyncAutoParentRejection(false);

        [UnityTest]
        public IEnumerator AutoParent_AsyncRejectedGrandparent_ShouldRejectBeforeStarting() =>
            VerifyAsyncAutoParentRejection(true);

        private IEnumerator VerifyAsyncAutoParentRejection(bool blockAncestor)
        {
            var child = CreateBlockedAutoChain(blockAncestor, out var blocked, out var holder, out var parent);
            var childShows = 0;
            var childAfter = 0;
            child.ShowAsyncActions += _ => { childShows++; return UniTask.CompletedTask; };
            child.AfterUIShowedEvent += () => childAfter++;
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            yield return WaitForTask(operation);

            Assert.IsNull(operation.Result, "预演拒绝按普通准入拒绝正常返回。");
            Assert.AreEqual(UIState.Hided, blocked.State);
            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Showed, holder.State);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(0, childShows);
            Assert.AreEqual(0, childAfter);
        }

        [Test]
        public void AutoParent_ShouldResolveRootBeforeStrictIntermediateParent()
        {
            var root = CreateUI("RootParent", CreateObject("RootContainer").transform);
            var parent = CreateUI("StrictIntermediate", root.transform);
            parent.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            var child = CreateUI("AutoChild", parent.transform);
            var order = new List<string>();
            root.ShowActions += () => order.Add("root");
            parent.ShowActions += () => order.Add("parent");
            child.ShowActions += () => order.Add("child");

            child.Show();

            CollectionAssert.AreEqual(new[] { "root", "parent", "child" }, order);
            Assert.AreEqual(UIState.Showed, root.State);
            Assert.AreEqual(UIState.Showed, parent.State);
            Assert.AreEqual(UIState.Showed, child.State);
        }

        [UnityTest]
        public IEnumerator AutoParent_SyncStrictIntermediate_ShouldReuseAlreadyScheduledShowingRoot()
        {
            var root = CreateUI("PendingRoot", CreateObject("PendingRootContainer").transform);
            var parent = CreateUI("StrictIntermediate", root.transform);
            parent.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            var child = CreateUI("SyncAutoChild", parent.transform);
            var rootPending = new UniTaskCompletionSource();
            var rootAsyncShows = 0;
            var rootSyncShows = 0;
            var rootToken = CancellationToken.None;
            var order = new List<string>();
            var childAfter = 0;
            root.ShowAsyncActions += token =>
            {
                rootAsyncShows++;
                rootToken = token;
                return rootPending.Task;
            };
            root.ShowActions += () => rootSyncShows++;
            parent.ShowActions += () => order.Add("parent");
            child.ShowActions += () => order.Add("child");
            child.AfterUIShowedEvent += () => childAfter++;

            // 真实请求已返回其同步调度阶段，只有根面板自身动画仍由门闩保持未完成。
            var rootOperation = CaptureException(root.ShowAsync(CancellationToken.None));
            try
            {
                Assert.IsTrue(root.IsUIStateInitialized);
                Assert.AreEqual(UIState.Showing, root.State);
                Assert.IsFalse(rootOperation.IsCompleted);
                Assert.AreEqual(1, rootAsyncShows);

                // 外部直接显示严格面板仍必须要求根面板已经 Showed。
                parent.Show();
                Assert.AreEqual(UIState.Hided, parent.State);
                Assert.IsEmpty(order);

                // 自动补父链可以复用已调度的 Showing 根面板，并同步打开严格中间父和目标。
                child.Show();
                Assert.AreEqual(UIState.Showed, parent.State);
                Assert.AreEqual(UIState.Showed, child.State);
                CollectionAssert.AreEqual(new[] { "parent", "child" }, order);
                Assert.AreEqual(1, childAfter);
                Assert.AreEqual(UIState.Showing, root.State);
                Assert.IsFalse(rootOperation.IsCompleted, "补父链不能提前完成根面板的动画。");
                Assert.AreEqual(1, rootAsyncShows, "已完成关系调度的根面板不能重启动画。");
                Assert.AreEqual(0, rootSyncShows, "同步补链不能用同步显示取代根面板的异步显示。");
                Assert.IsFalse(rootToken.IsCancellationRequested);

                // 本轮内部准入不能泄漏给后续的外部直接显示请求。
                parent.Hide();
                parent.Show();
                Assert.AreEqual(UIState.Hided, parent.State);
                Assert.AreEqual(UIState.Hided, child.State);
                CollectionAssert.AreEqual(new[] { "parent", "child" }, order);
            }
            finally
            {
                rootPending.TrySetResult();
            }

            yield return WaitForTask(rootOperation);
            Assert.IsNull(rootOperation.Result);
            Assert.AreEqual(UIState.Showed, root.State);
            Assert.AreEqual(1, rootAsyncShows);
            Assert.AreEqual(0, rootSyncShows);
        }

        [Test]
        public void AutoParent_SerializedShowedParent_ShouldRunRealInitializationBeforeChild()
        {
            var parent = CreateUI("SerializedParent", CreateObject("RootContainer").transform);
            SetUIBasePrivateField(parent, "state", UIState.Showed);
            var child = CreateUI("AutoChild", parent.transform);
            var order = new List<string>();
            parent.ShowActions += () => order.Add("parent");
            child.ShowActions += () => order.Add("child");

            child.Show();

            CollectionAssert.AreEqual(new[] { "parent", "child" }, order);
            Assert.AreEqual(UIState.Showed, child.State);
            parent.Show();
            Assert.AreEqual(2, order.Count, "已真正初始化的父级不得重复派发显示行为。");
        }

        [UnityTest]
        public IEnumerator AutoParent_AsyncSerializedShowedParent_ShouldInitializeBeforeReportingChildSuccess()
        {
            var parent = CreateUI("SerializedParent", CreateObject("RootContainer").transform);
            SetUIBasePrivateField(parent, "state", UIState.Showed);
            var child = CreateUI("AutoChild", parent.transform);
            var parentShows = 0;
            parent.ShowAsyncActions += _ => { parentShows++; return UniTask.CompletedTask; };
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            yield return WaitForTask(operation);

            Assert.IsNull(operation.Result);
            Assert.AreEqual(1, parentShows);
            Assert.AreEqual(UIState.Showed, child.State);
        }

        [Test]
        public void AutoParent_ReparentedBeforeParentStage_ShouldResolveNewParent()
        {
            var oldParent = CreateUI("OldParent", CreateObject("OldRoot").transform);
            var newParent = CreateUI("NewParent", CreateObject("NewRoot").transform);
            var child = CreateUI("AutoChild", oldParent.transform);
            child.BeforeUIShowEvent += () => child.transform.SetParent(newParent.transform, false);

            child.Show();

            Assert.AreEqual(UIState.Hided, oldParent.State);
            Assert.AreEqual(UIState.Showed, newParent.State);
            Assert.AreEqual(UIState.Showed, child.State);
        }

        [Test]
        public void AutoParent_ReparentedAfterParentStage_ShouldHideChildWithoutUndoingOpenedParent()
        {
            var oldParent = CreateUI("OldParent", CreateObject("OldRoot").transform);
            var newParent = CreateUI("NewParent", CreateObject("NewRoot").transform);
            var child = CreateUI("AutoChild", oldParent.transform);
            var after = 0;
            child.ShowActions += () => child.transform.SetParent(newParent.transform, false);
            child.AfterUIShowedEvent += () => after++;

            child.Show();

            Assert.AreEqual(UIState.Showed, oldParent.State, "成功打开的父级不执行事务回滚。");
            Assert.AreEqual(UIState.Hided, newParent.State);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(0, after);
        }

        [UnityTest]
        public IEnumerator AutoParent_ReparentedWhileOwnAnimationPending_ShouldRevalidateBeforeSuccess()
        {
            var oldParent = CreateUI("OldParent", CreateObject("OldRoot").transform);
            var newParent = CreateUI("NewParent", CreateObject("NewRoot").transform);
            var child = CreateUI("AutoChild", oldParent.transform);
            var pending = new UniTaskCompletionSource();
            child.ShowAsyncActions += _ => pending.Task;
            var after = 0;
            child.AfterUIShowedEvent += () => after++;
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            Assert.AreEqual(UIState.Showed, oldParent.State);
            child.transform.SetParent(newParent.transform, false);
            pending.TrySetResult();
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(UIState.Hided, newParent.State);
            Assert.AreEqual(0, after);
        }

        [UnityTest]
        public IEnumerator AutoParent_PreemptedDuringParallelAnimations_ShouldObserveAllStartedActions()
        {
            var root = CreateObject("PreemptedRoot");
            var parent = CreateUI("Parent", root.transform);
            var holder = CreateUI("Holder", root.transform);
            holder.MutexPriority = 3;
            var child = CreateUI("Child", parent.transform);
            var parentPending = new UniTaskCompletionSource();
            var childPending = new UniTaskCompletionSource();
            parent.ShowAsyncActions += _ => parentPending.Task;
            child.ShowAsyncActions += _ => childPending.Task;
            var after = 0;
            child.AfterUIShowedEvent += () => after++;
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            try
            {
                Assert.AreEqual(UIState.Showing, parent.State);
                Assert.AreEqual(UIState.Showing, child.State);
                holder.Show();
                childPending.TrySetResult();
                yield return null;
                Assert.IsFalse(operation.IsCompleted, "即使已取消，也应观察仍未结束的父动画。");
            }
            finally
            {
                parentPending.TrySetResult();
                childPending.TrySetResult();
            }
            yield return WaitForTask(operation);
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(UIState.Showed, holder.State);
            Assert.AreEqual(0, after);
        }

        [Test]
        public void AutoParent_RejectionLog_ShouldIdentifyChildAndUnreadyAncestor()
        {
            using var log = new SchedulingLogScope();
            var child = CreateBlockedAutoChain(false, out _, out _, out _);

            child.Show();

            Assert.That(log.Messages.Exists(message => message.Contains("AutoChild") &&
                message.Contains("显示预演失败") && message.Contains("BlockedParent") &&
                message.Contains("显示请求被拒绝")), Is.True);
        }

        [Test]
        public void AutoParent_Cascade_ShouldAllowChildCompletionWhileParentStillShowing()
        {
            var parent = CreateUI("CascadeParent", CreateObject("Root").transform);
            var child = CreateUI("CascadeChild", parent.transform);
            child.ShowWithParent = true;
            var observed = false;
            child.AfterUIShowedEvent += () => observed = parent.State == UIState.Showing;

            parent.Show();

            Assert.IsTrue(observed);
            Assert.AreEqual(UIState.Showed, parent.State);
            Assert.AreEqual(UIState.Showed, child.State);
        }

        [UnityTest]
        public IEnumerator AutoParent_AsyncCascade_ShouldNotWaitForParentsFinalShowed()
        {
            var parent = CreateUI("CascadeParent", CreateObject("Root").transform);
            var child = CreateUI("CascadeChild", parent.transform);
            child.ShowWithParent = true;
            var observed = false;
            child.AfterUIShowedEvent += () => observed = parent.State == UIState.Showing;
            var operation = CaptureException(parent.ShowAsync(CancellationToken.None));
            yield return WaitForTask(operation);

            Assert.IsNull(operation.Result);
            Assert.IsTrue(observed);
            Assert.AreEqual(UIState.Showed, parent.State);
            Assert.AreEqual(UIState.Showed, child.State);
        }
    }
}
