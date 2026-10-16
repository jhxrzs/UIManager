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
        public void Preflight_DeeperAncestorBlocked_ShouldNotOpenEarlierRoot(bool strictRoot)
        {
            var root = CreateUI("UnopenedRoot", CreateObject("Container").transform);
            var middle = CreateUI("Middle", root.transform);
            var holder = CreateUI("MiddleHolder", root.transform);
            holder.MutexPriority = 10;
            SetUIBasePrivateField(holder, "state", UIState.Showed);
            SetUIBasePrivateField(holder, "_isUIStateInitialized", true);
            if (strictRoot) root.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            var child = CreateUI("Child", middle.transform);
            var calls = 0;
            root.BeforeUIShowEvent += () => calls++;
            root.ShowActions += () => calls++;
            middle.BeforeUIShowEvent += () => calls++;
            child.BeforeUIShowEvent += () => calls++;

            child.Show();

            Assert.AreEqual(0, calls);
            Assert.AreEqual(UIState.Hided, root.State);
            Assert.AreEqual(UIState.Hided, middle.State);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(UIState.Showed, holder.State);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Preflight_BlockedAncestor_ShouldPreserveChildSiblingAndAllNotifications(bool blockGrandparent)
        {
            var child = CreateBlockedAutoChain(blockGrandparent, out var blocked, out _, out var parent);
            var existing = CreateUI("ExistingChildSibling", parent.transform);
            // 显示兄弟后再恢复父级隐藏状态，构造合法入口能检查到的历史子状态。
            SetUIBasePrivateField(existing, "state", UIState.Showed);
            SetUIBasePrivateField(existing, "_isUIStateInitialized", true);
            var events = 0;
            blocked.OnStateChanged += (_, _) => events++;
            child.OnStateChanged += (_, _) => events++;
            child.BeforeUIShowEvent += () => events++;
            child.HideActions += () => events++;
            existing.HideActions += () => events++;

            child.Show();

            Assert.AreEqual(0, events);
            Assert.AreEqual(UIState.Showed, existing.State);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.IsFalse((bool)typeof(UIBase).GetField("_isUIStateInitialized",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(child));
        }

        [UnityTest]
        public IEnumerator Preflight_AsyncBlockedAncestor_ShouldPreserveExistingSiblingWithoutStartingAnimation()
        {
            var child = CreateBlockedAutoChain(true, out _, out _, out var parent);
            var sibling = CreateUI("ExistingSibling", parent.transform);
            SetUIBasePrivateField(sibling, "state", UIState.Showed);
            SetUIBasePrivateField(sibling, "_isUIStateInitialized", true);
            var calls = 0;
            child.BeforeUIShowEvent += () => calls++;
            sibling.HideAsyncActions += _ => { calls++; return UniTask.CompletedTask; };
            var result = CaptureException(child.ShowAsync(CancellationToken.None));
            yield return WaitForTask(result);
            Assert.IsNull(result.Result);
            Assert.AreEqual(0, calls);
            Assert.AreEqual(UIState.Showed, sibling.State);
        }

        [UnityTest]
        public IEnumerator Scheduling_AutoChainWithStrictIntermediate_ShouldStartAllAnimationsBeforeAnyCompletes()
        {
            var root = CreateUI("Root", CreateObject("Container").transform);
            var middle = CreateUI("StrictMiddle", root.transform);
            middle.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            var child = CreateUI("RequestedChild", middle.transform);
            var rootGate = new UniTaskCompletionSource();
            var middleGate = new UniTaskCompletionSource();
            var childGate = new UniTaskCompletionSource();
            var rootCalls = 0;
            var middleCalls = 0;
            var childCalls = 0;
            root.ShowAsyncActions += _ => { rootCalls++; return rootGate.Task; };
            middle.ShowAsyncActions += _ => { middleCalls++; return middleGate.Task; };
            child.ShowAsyncActions += _ => { childCalls++; return childGate.Task; };
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            try
            {
                Assert.AreEqual(1, rootCalls);
                Assert.AreEqual(1, middleCalls);
                Assert.AreEqual(1, childCalls);
                Assert.AreEqual(UIState.Showing, root.State);
                Assert.AreEqual(UIState.Showing, middle.State);
                Assert.AreEqual(UIState.Showing, child.State);
                Assert.IsFalse(operation.IsCompleted);
            }
            finally
            {
                rootGate.TrySetResult(); middleGate.TrySetResult(); childGate.TrySetResult();
            }
            yield return WaitForTask(operation);
            Assert.IsNull(operation.Result);
            Assert.AreEqual(UIState.Showed, root.State);
            Assert.AreEqual(UIState.Showed, middle.State);
            Assert.AreEqual(UIState.Showed, child.State);
        }

        [UnityTest]
        public IEnumerator Scheduling_PendingSiblingHide_ShouldNotDelayParentOrDefaultChildScheduling()
        {
            var container = CreateObject("Container");
            var old = CreateUI("Old", container.transform);
            old.Show();
            var panel = CreateUI("Panel", container.transform);
            var child = CreateUI("DefaultChild", panel.transform);
            child.ShowWithParent = true;
            var hideGate = new UniTaskCompletionSource();
            var showGate = new UniTaskCompletionSource();
            var childGate = new UniTaskCompletionSource();
            var childStarted = false;
            old.HideAsyncActions += _ => hideGate.Task;
            panel.ShowAsyncActions += _ => showGate.Task;
            child.ShowAsyncActions += _ => { childStarted = true; return childGate.Task; };
            var operation = CaptureException(panel.ShowAsync(CancellationToken.None));
            try
            {
                Assert.AreEqual(UIState.Hiding, old.State);
                Assert.AreEqual(UIState.Showing, panel.State);
                Assert.AreEqual(UIState.Showing, child.State);
                Assert.IsTrue(childStarted);
                Assert.IsFalse(operation.IsCompleted);
            }
            finally
            {
                hideGate.TrySetResult(); showGate.TrySetResult(); childGate.TrySetResult();
            }
            yield return WaitForTask(operation);
            Assert.IsNull(operation.Result);
            Assert.AreEqual(UIState.Hided, old.State);
            Assert.AreEqual(UIState.Showed, child.State);
        }

        [UnityTest]
        public IEnumerator Scheduling_DefaultChildrenInDifferentGroups_ShouldStartTogether()
        {
            var parent = CreateUI("Parent", CreateObject("Container").transform);
            var first = CreateUI("First", parent.transform);
            var second = CreateUI("Second", parent.transform);
            SetUIBasePrivateField(second, "mutexGroup", 1);
            first.ShowWithParent = second.ShowWithParent = true;
            var gate = new UniTaskCompletionSource();
            var secondStarted = false;
            first.ShowAsyncActions += _ => gate.Task;
            second.ShowAsyncActions += _ => { secondStarted = true; return UniTask.CompletedTask; };
            var operation = CaptureException(parent.ShowAsync(CancellationToken.None));
            try { Assert.IsTrue(secondStarted, "第一个子面板的动画不得延迟第二个子面板的调度。"); }
            finally { gate.TrySetResult(); }
            yield return WaitForTask(operation);
            Assert.IsNull(operation.Result);
        }

        [UnityTest]
        public IEnumerator Scheduling_NewDefaultChildDuringAnimation_ShouldNotBeDiscoveredOnCompletion()
        {
            var panel = CreateUI("Panel", CreateObject("Container").transform);
            var gate = new UniTaskCompletionSource();
            panel.ShowAsyncActions += _ => gate.Task;
            var operation = CaptureException(panel.ShowAsync(CancellationToken.None));
            var lateChild = CreateUI("LateChild", panel.transform);
            lateChild.ShowWithParent = true;
            gate.TrySetResult();
            yield return WaitForTask(operation);
            Assert.IsNull(operation.Result);
            Assert.AreEqual(UIState.Hided, lateChild.State, "动画完成阶段不再重新调度层级。");
        }

        [UnityTest]
        public IEnumerator Scheduling_ReparentDuringAnimation_ShouldNotHideNewParentsExistingPanel()
        {
            var first = CreateUI("FirstParent", CreateObject("FirstContainer").transform);
            var second = CreateUI("SecondParent", CreateObject("SecondContainer").transform);
            second.Show();
            var existing = CreateUI("Existing", second.transform);
            existing.Show();
            var child = CreateUI("Moving", first.transform);
            var gate = new UniTaskCompletionSource();
            child.ShowAsyncActions += _ => gate.Task;
            var hides = 0;
            existing.HideActions += () => hides++;
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            child.transform.SetParent(second.transform, false);
            gate.TrySetResult();
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(UIState.Showed, existing.State);
            Assert.AreEqual(0, hides);
        }

        [UnityTest]
        public IEnumerator Scheduling_AnimationFailure_ShouldNotUndoAlreadyOpenedAncestor()
        {
            var parent = CreateUI("Parent", CreateObject("Container").transform);
            var child = CreateUI("Child", parent.transform);
            var gate = new UniTaskCompletionSource();
            child.ShowAsyncActions += _ => gate.Task;
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            gate.TrySetException(new InvalidOperationException("presentation failed"));
            yield return WaitForTask(operation);
            Assert.That(operation.Result, Is.InstanceOf<InvalidOperationException>());
            Assert.AreEqual(UIState.Showed, parent.State);
            Assert.AreEqual(UIState.Showing, child.State, "表现失败不伪造成功，保留原有失败收尾语义。");
        }

        [UnityTest]
        public IEnumerator Scheduling_OverrideThrowsBeforeReturningTask_ShouldStillObserveStartedAncestor()
        {
            var parent = CreateUI("Parent", CreateObject("Container").transform);
            var childObject = CreateObject("ThrowingChild", parent.transform);
            var child = childObject.AddComponent<ThrowingShowUI>();
            child.enabled = false;
            var gate = new UniTaskCompletionSource();
            parent.ShowAsyncActions += _ => gate.Task;
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            try
            {
                yield return null;
                Assert.IsFalse(operation.IsCompleted, "覆写钩子同步抛错也不能丢掉已经启动的祖先动画任务。");
            }
            finally { gate.TrySetResult(); }
            yield return WaitForTask(operation);
            Assert.That(operation.Result, Is.InstanceOf<InvalidOperationException>());
            Assert.AreEqual("override failed", operation.Result.Message);
            Assert.AreEqual(UIState.Showed, parent.State);
            Assert.AreEqual(UIState.Showing, child.State);
        }

        [UnityTest]
        public IEnumerator Scheduling_OptionalBlockedChild_ShouldNotRejectParent()
        {
            var panel = CreateUI("Panel", CreateObject("Container").transform);
            var holder = CreateUI("Holder", panel.transform);
            panel.Show(); holder.MutexPriority = 10; holder.Show();
            var optional = CreateUI("Optional", panel.transform);
            optional.ShowWithParent = true;
            // 重新运行父级过渡，保留现有高优先级持有者。
            SetUIBasePrivateField(panel, "state", UIState.Hiding);
            var operation = CaptureException(panel.ShowAsync(CancellationToken.None));
            yield return WaitForTask(operation);
            Assert.IsNull(operation.Result);
            Assert.AreEqual(UIState.Showed, panel.State);
            Assert.AreEqual(UIState.Hided, optional.State);
            Assert.AreEqual(UIState.Showed, holder.State);
        }

        [UnityTest]
        public IEnumerator Scheduling_EqualPriorityDefaults_ShouldOnlyAnimateLastCandidate()
        {
            var parent = CreateUI("Parent", CreateObject("Container").transform);
            var first = CreateUI("First", parent.transform);
            var last = CreateUI("Last", parent.transform);
            first.ShowWithParent = last.ShowWithParent = true;
            var firstCalls = 0;
            first.ShowAsyncActions += _ => { firstCalls++; return UniTask.CompletedTask; };
            var operation = CaptureException(parent.ShowAsync(CancellationToken.None));
            yield return WaitForTask(operation);
            Assert.IsNull(operation.Result);
            Assert.AreEqual(0, firstCalls);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreEqual(UIState.Showed, last.State);
        }
    }

    public sealed class ThrowingShowUI : UIBase
    {
        protected override UniTask OnUIShowAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("override failed");
        }
    }
}
