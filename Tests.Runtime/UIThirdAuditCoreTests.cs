using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private void CreateThirdAuditHideBranches(out UIBase parent, out UIBase first, out UIBase later)
        {
            parent = CreateUI("ThirdAuditParent", CreateObject("ThirdAuditContainer").transform);
            first = CreateUI("FirstBranch", parent.transform);
            later = CreateUI("LaterBranch", parent.transform);
            SetUIBasePrivateField(later, "mutexGroup", 1);
            parent.Show();
            first.Show();
            later.Show();
            first.transform.SetSiblingIndex(0);
            later.transform.SetSiblingIndex(1);
        }

        [Test]
        public void ThirdAudit_Core_HideDeactivatedAncestor_ShouldHideExistingGrandchild()
        {
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            var grandchild = CreateUI("ExistingGrandchild", later.transform);
            var group = grandchild.GetComponent<CanvasGroup>();
            var grandchildHides = 0;
            grandchild.ShowActions += () => group.alpha = 1f;
            grandchild.HideActions += () => { grandchildHides++; group.alpha = 0f; };
            grandchild.Show();
            parent.ShowActions += () => parent.gameObject.SetActive(true);
            first.HideActions += () => parent.gameObject.SetActive(false);
            Assert.IsTrue(grandchild.gameObject.activeInHierarchy);
            Assert.AreEqual(UIState.Showed, grandchild.State);
            Assert.IsFalse(grandchild.ShowWithParent);

            parent.Hide();

            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreEqual(UIState.Hided, later.State,
                "The already captured direct child must still be hidden by the existing fix.");
            TestContext.Out.WriteLine(
                $"DIAGNOSTIC deeper sync hide: grandchildState={grandchild.State}; " +
                $"grandchildHides={grandchildHides}; alpha={group.alpha}; " +
                $"activeSelf={grandchild.gameObject.activeSelf}; activeInHierarchy={grandchild.gameObject.activeInHierarchy}");

            parent.Show();
            later.Show();
            // The grandchild was already active before the public request and never changed
            // parent. Earlier ancestor deactivation must not interrupt recursive hiding.
            TestContext.Out.WriteLine(
                $"DIAGNOSTIC deeper sync reopen: grandchildState={grandchild.State}; " +
                $"grandchildHides={grandchildHides}; alpha={group.alpha}; " +
                $"activeInHierarchy={grandchild.gameObject.activeInHierarchy}; showWithParent={grandchild.ShowWithParent}");
            Assert.AreEqual(UIState.Hided, grandchild.State);
            Assert.AreEqual(1, grandchildHides);
            Assert.AreEqual(0f, group.alpha);
        }

        [UnityTest]
        public IEnumerator ThirdAudit_Core_HideAsyncDeactivatedAncestor_ShouldHideExistingGrandchild()
        {
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            var grandchild = CreateUI("ExistingAsyncGrandchild", later.transform);
            var group = grandchild.GetComponent<CanvasGroup>();
            var grandchildHides = 0;
            grandchild.ShowActions += () => group.alpha = 1f;
            grandchild.HideActions += () => { grandchildHides++; group.alpha = 0f; };
            grandchild.Show();
            parent.ShowActions += () => parent.gameObject.SetActive(true);
            first.HideActions += () => parent.gameObject.SetActive(false);
            var pending = new UniTaskCompletionSource();
            var parentAsyncCalls = 0;
            parent.HideAsyncActions += _ => { parentAsyncCalls++; return pending.Task; };
            Assert.IsTrue(grandchild.gameObject.activeInHierarchy);
            Assert.IsFalse(grandchild.ShowWithParent);

            var operation = CaptureException(parent.HideAsync(CancellationToken.None));
            try
            {
                Assert.AreEqual(1, parentAsyncCalls);
                Assert.AreEqual(UIState.Hiding, parent.State);
                Assert.AreEqual(UIState.Hided, later.State);
                Assert.IsFalse(operation.IsCompleted);
                TestContext.Out.WriteLine(
                    $"DIAGNOSTIC deeper async scheduled: grandchildState={grandchild.State}; " +
                    $"grandchildHides={grandchildHides}; alpha={group.alpha}");
            }
            finally
            {
                pending.TrySetResult();
            }

            yield return WaitForTask(operation);
            Assert.IsNull(operation.Result);
            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreEqual(UIState.Hided, later.State);
            parent.Show();
            later.Show();
            TestContext.Out.WriteLine(
                $"DIAGNOSTIC deeper async reopen: grandchildState={grandchild.State}; " +
                $"grandchildHides={grandchildHides}; alpha={group.alpha}; " +
                $"activeInHierarchy={grandchild.gameObject.activeInHierarchy}; showWithParent={grandchild.ShowWithParent}");
            Assert.AreEqual(UIState.Hided, grandchild.State);
            Assert.AreEqual(1, grandchildHides);
            Assert.AreEqual(0f, group.alpha);
        }

        [Test]
        public void ThirdAudit_Core_ChildDeactivatesItselfInBeforeHide_ShouldFinishItsCapturedDescendants()
        {
            var parent = CreateUI("SelfDeactivateParent", CreateObject("SelfDeactivateContainer").transform);
            var child = CreateUI("SelfDeactivatingChild", parent.transform);
            var grandchild = CreateUI("CapturedGrandchild", child.transform);
            var group = grandchild.GetComponent<CanvasGroup>();
            var grandchildHides = 0;
            child.ShowActions += () => child.gameObject.SetActive(true);
            grandchild.ShowActions += () => group.alpha = 1f;
            grandchild.HideActions += () => { grandchildHides++; group.alpha = 0f; };
            grandchild.Show();
            child.BeforeUIHideEvent += () => child.gameObject.SetActive(false);

            parent.Hide();

            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(UIState.Hided, grandchild.State);
            Assert.AreEqual(1, grandchildHides);
            Assert.IsFalse(child.gameObject.activeSelf);
            parent.Show();
            child.Show();
            Assert.IsTrue(grandchild.gameObject.activeInHierarchy);
            Assert.AreEqual(UIState.Hided, grandchild.State);
            Assert.AreEqual(0f, group.alpha);
            Assert.AreEqual(1, grandchildHides);
        }

        [Test]
        public void ThirdAudit_Core_HideCallbackMovesCapturedChildOutAndBack_ShouldHideCurrentChildOnce()
        {
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            var outside = CreateObject("TemporaryOutsideParent");
            var laterHides = 0;
            later.HideActions += () => laterHides++;
            first.HideActions += () =>
            {
                later.transform.SetParent(outside.transform, false);
                later.transform.SetParent(parent.transform, false);
            };

            parent.Hide();

            Assert.AreSame(parent.transform, later.transform.parent);
            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreEqual(UIState.Hided, later.State,
                "A selected child that is again directly owned when visited must complete its hide.");
            Assert.AreEqual(1, laterHides);
        }

        [UnityTest]
        public IEnumerator ThirdAudit_Core_HideAsyncCanceledByFirstChild_ShouldStopRemainingCapturedWork()
        {
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            using var cancellation = new CancellationTokenSource();
            var laterHides = 0;
            var parentAsyncHides = 0;
            var parentAfterHides = 0;
            later.HideActions += () => laterHides++;
            parent.HideAsyncActions += _ => { parentAsyncHides++; return UniTask.CompletedTask; };
            parent.AfterUIHidedEvent += () => parentAfterHides++;
            first.HideActions += () =>
            {
                parent.gameObject.SetActive(false);
                cancellation.Cancel();
            };

            var operation = CaptureException(parent.HideAsync(cancellation.Token));
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreEqual(UIState.Showed, later.State,
                "Caller cancellation must stop later work even though the child remains in the snapshot.");
            Assert.AreEqual(0, laterHides);
            Assert.AreEqual(0, parentAsyncHides);
            Assert.AreEqual(0, parentAfterHides);
            Assert.AreNotEqual(UIState.Hided, parent.State,
                "A canceled parent hide must not report a successful completion state.");
        }

        [UnityTest]
        public IEnumerator ThirdAudit_Core_HideDeactivatedAncestor_ShouldCancelGrandchildShowReservation()
        {
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            var grandchild = CreateUI("PendingGrandchild", later.transform);
            var pending = new UniTaskCompletionSource();
            var showToken = CancellationToken.None;
            var afterShows = 0;
            var hides = 0;
            grandchild.ShowAsyncActions += token => { showToken = token; return pending.Task; };
            grandchild.AfterUIShowedEvent += () => afterShows++;
            grandchild.HideActions += () => hides++;
            var operation = CaptureException(grandchild.ShowAsync(CancellationToken.None));
            try
            {
                Assert.AreEqual(UIState.Showing, grandchild.State);
                Assert.IsFalse(operation.IsCompleted);
                first.HideActions += () => parent.gameObject.SetActive(false);

                parent.Hide();

                Assert.AreEqual(UIState.Hided, parent.State);
                Assert.AreEqual(UIState.Hided, later.State);
                Assert.AreEqual(UIState.Hided, grandchild.State,
                    "Even under an inactive ancestor, the existing show reservation must be canceled and hidden.");
                Assert.IsTrue(showToken.IsCancellationRequested);
                Assert.AreEqual(1, hides);
                Assert.AreEqual(0, afterShows);
                Assert.IsFalse(operation.IsCompleted,
                    "The canceled operation must still observe the already started token-ignoring behavior.");
            }
            finally
            {
                pending.TrySetResult();
            }

            yield return WaitForTask(operation);
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(UIState.Hided, grandchild.State);
            Assert.AreEqual(1, hides);
            Assert.AreEqual(0, afterShows);
        }
    }
}
