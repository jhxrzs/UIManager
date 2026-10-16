using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        [Test]
        public void Hide_ChildCallbackDeactivatesParent_ShouldFinishCapturedChildren()
        {
            var parent = CreateUI("DeactivateDuringHide", CreateObject("HideSnapshotRoot").transform);
            var first = CreateUI("FirstChild", parent.transform);
            var later = CreateUI("LaterUnmarkedChild", parent.transform);
            SetUIBasePrivateField(later, "mutexGroup", 1);
            var laterGroup = later.GetComponent<CanvasGroup>();
            var laterHideCalls = 0;
            parent.ShowActions += () => parent.gameObject.SetActive(true);
            later.ShowActions += () => laterGroup.alpha = 1f;
            later.HideActions += () => { laterHideCalls++; laterGroup.alpha = 0f; };
            parent.Show();
            first.Show();
            later.Show();
            first.transform.SetSiblingIndex(0);
            later.transform.SetSiblingIndex(1);
            Assert.IsTrue(first.gameObject.activeInHierarchy && later.gameObject.activeInHierarchy);
            Assert.AreEqual(UIState.Showed, later.State);
            Assert.IsFalse(later.ShowWithParent);
            first.HideActions += () => parent.gameObject.SetActive(false);

            parent.Hide();

            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.IsFalse(parent.gameObject.activeInHierarchy);
            parent.Show();
            Assert.AreEqual(UIState.Showed, parent.State);
            // 两个子UI在入口均已活跃并入选；父物体失活不能丢弃仍然直属它的后续隐藏工作。
            Assert.AreEqual(UIState.Hided, later.State,
                "An already selected child must finish hiding even when an earlier callback deactivates its parent.");
            Assert.AreEqual(1, laterHideCalls,
                "The remaining child's hide behavior must execute exactly once.");
            Assert.AreEqual(0f, laterGroup.alpha,
                "Reopening the parent must not reveal its unmarked child after a successful hide.");
        }

        [UnityTest]
        public IEnumerator HideAsync_ChildCallbackDeactivatesParent_ShouldFinishCapturedChildren()
        {
            var parent = CreateUI("DeactivateDuringAsyncHide", CreateObject("AsyncHideSnapshotRoot").transform);
            var first = CreateUI("FirstChild", parent.transform);
            var later = CreateUI("LaterUnmarkedChild", parent.transform);
            SetUIBasePrivateField(later, "mutexGroup", 1);
            var laterGroup = later.GetComponent<CanvasGroup>();
            var laterHideCalls = 0;
            parent.ShowActions += () => parent.gameObject.SetActive(true);
            later.ShowActions += () => laterGroup.alpha = 1f;
            later.HideActions += () => { laterHideCalls++; laterGroup.alpha = 0f; };
            parent.Show();
            first.Show();
            later.Show();
            first.transform.SetSiblingIndex(0);
            later.transform.SetSiblingIndex(1);
            Assert.IsTrue(first.gameObject.activeInHierarchy && later.gameObject.activeInHierarchy);
            Assert.AreEqual(UIState.Showed, later.State);
            Assert.IsFalse(later.ShowWithParent);
            first.HideActions += () => parent.gameObject.SetActive(false);
            var pending = new UniTaskCompletionSource();
            var parentAsyncHideCalls = 0;
            parent.HideAsyncActions += _ => { parentAsyncHideCalls++; return pending.Task; };

            var operation = CaptureException(parent.HideAsync(CancellationToken.None));
            try
            {
                Assert.AreEqual(1, parentAsyncHideCalls);
                Assert.AreEqual(UIState.Hiding, parent.State);
                Assert.IsFalse(operation.IsCompleted, "父面板应真实等待自身的异步隐藏行为。");
            }
            finally
            {
                pending.TrySetResult();
            }

            yield return WaitForTask(operation);
            Assert.IsNull(operation.Result);
            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.IsFalse(parent.gameObject.activeInHierarchy);
            parent.Show();
            Assert.AreEqual(UIState.Showed, parent.State);
            Assert.AreEqual(UIState.Hided, later.State,
                "An already selected child must finish hiding even when an earlier callback deactivates its parent.");
            Assert.AreEqual(1, laterHideCalls,
                "The remaining child's hide behavior must execute exactly once.");
            Assert.AreEqual(0f, laterGroup.alpha,
                "Reopening the parent must not reveal its unmarked child after a successful hide.");
        }

        [Test]
        public void Hide_ChildCallbackMovesLaterChildOut_ShouldPreserveMovedChild()
        {
            var parent = CreateUI("HideSource", CreateObject("HideSourceRoot").transform);
            var outside = CreateObject("OutsideHideSource");
            var first = CreateUI("FirstChild", parent.transform);
            var later = CreateUI("MovedChild", parent.transform);
            SetUIBasePrivateField(later, "mutexGroup", 1);
            parent.Show();
            first.Show();
            later.Show();
            first.transform.SetSiblingIndex(0);
            later.transform.SetSiblingIndex(1);
            var laterHides = 0;
            later.HideActions += () => laterHides++;
            first.HideActions += () => later.transform.SetParent(outside.transform, false);

            parent.Hide();

            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreSame(outside.transform, later.transform.parent);
            Assert.AreEqual(UIState.Showed, later.State,
                "A child that left the parent during a prior callback must not receive stale hide work.");
            Assert.AreEqual(0, laterHides);
        }

        [Test]
        public void Hide_ChildCallbackDestroysLaterChild_ShouldSkipDestroyedChild()
        {
            var parent = CreateUI("DestroyDuringHide", CreateObject("DestroyHideSnapshotRoot").transform);
            var first = CreateUI("FirstChild", parent.transform);
            var later = CreateUI("DestroyedChild", parent.transform);
            SetUIBasePrivateField(later, "mutexGroup", 1);
            parent.Show();
            first.Show();
            later.Show();
            first.transform.SetSiblingIndex(0);
            later.transform.SetSiblingIndex(1);
            var laterHides = 0;
            later.HideActions += () => laterHides++;
            first.HideActions += () => Object.DestroyImmediate(later.gameObject);

            parent.Hide();

            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.IsFalse(later, "前序子UI的回调必须实际销毁快照中的后续子UI。");
            Assert.AreEqual(0, laterHides, "已销毁的快照节点不能再接收隐藏行为。");
        }

        [Test]
        public void Hide_ChildAlreadyInactiveAtEntry_ShouldPreserveExcludedChild()
        {
            var parent = CreateUI("InactiveAtEntry", CreateObject("InactiveHideSnapshotRoot").transform);
            var first = CreateUI("ActiveChild", parent.transform);
            var later = CreateUI("AlreadyInactiveChild", parent.transform);
            SetUIBasePrivateField(later, "mutexGroup", 1);
            parent.Show();
            first.Show();
            later.Show();
            var firstHides = 0;
            var laterHides = 0;
            first.HideActions += () => firstHides++;
            later.HideActions += () => laterHides++;
            later.gameObject.SetActive(false);
            Assert.IsTrue(first.gameObject.activeInHierarchy);
            Assert.IsFalse(later.gameObject.activeInHierarchy);
            Assert.AreEqual(UIState.Showed, later.State);

            parent.Hide();

            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreEqual(1, firstHides);
            Assert.AreEqual(UIState.Showed, later.State,
                "入口已经失活且没有显示预约的子UI仍不进入隐藏快照。");
            Assert.AreEqual(0, laterHides);
            Assert.IsFalse(later.gameObject.activeSelf);
        }
    }
}
