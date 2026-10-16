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
        [UnityTest]
        public IEnumerator Hide_PreviousShowCancellationDeactivatesAncestor_ShouldHideExistingDescendants() =>
            VerifyHideAfterCancellationDeactivatesAncestor(false);

        [UnityTest]
        public IEnumerator HideAsync_PreviousShowCancellationDeactivatesAncestor_ShouldHideExistingDescendants() =>
            VerifyHideAfterCancellationDeactivatesAncestor(true);

        private IEnumerator VerifyHideAfterCancellationDeactivatesAncestor(bool asynchronous)
        {
            var parent = CreateUI("CancelBeforeHideParent", CreateObject("CancelBeforeHideRoot").transform);
            var child = CreateUI("ExistingChild", parent.transform);
            var grandchild = CreateUI("ExistingGrandchild", child.transform);
            var pending = new UniTaskCompletionSource();
            var registration = default(CancellationTokenRegistration);
            var cancellationCalls = 0;
            var childHides = 0;
            var grandchildHides = 0;
            var oldAfterShows = 0;
            child.HideActions += () => childHides++;
            grandchild.HideActions += () => grandchildHides++;
            parent.AfterUIShowedEvent += () => oldAfterShows++;
            parent.ShowAsyncActions += token =>
            {
                registration = token.Register(() =>
                {
                    cancellationCalls++;
                    parent.gameObject.SetActive(false);
                });
                return pending.Task;
            };
            var showing = CaptureException(parent.ShowAsync(CancellationToken.None));
            try
            {
                child.Show();
                grandchild.Show();
                Assert.AreEqual(UIState.Showing, parent.State);
                Assert.AreEqual(UIState.Showed, child.State);
                Assert.AreEqual(UIState.Showed, grandchild.State);
                Assert.IsTrue(grandchild.gameObject.activeInHierarchy);

                if (asynchronous)
                {
                    var hiding = CaptureException(parent.HideAsync(CancellationToken.None));
                    yield return WaitForTask(hiding);
                    Assert.IsNull(hiding.Result);
                }
                else parent.Hide();

                Assert.AreEqual(1, cancellationCalls);
                Assert.IsFalse(parent.gameObject.activeInHierarchy);
                Assert.AreEqual(UIState.Hided, parent.State);
                Assert.AreEqual(UIState.Hided, child.State);
                Assert.AreEqual(UIState.Hided, grandchild.State);
                Assert.AreEqual(1, childHides);
                Assert.AreEqual(1, grandchildHides);
                Assert.IsFalse(showing.IsCompleted,
                    "被取消的旧显示仍须观察此前已启动、忽略令牌的行为。");
            }
            finally
            {
                registration.Dispose();
                pending.TrySetResult();
            }

            yield return WaitForTask(showing);
            Assert.That(showing.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(0, oldAfterShows);
            Assert.AreEqual(UIState.Hided, parent.State);
        }
    }
}
