using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>
    ///  UIBase 回归测试的补充部分，验证回调重入、祖先失活及销毁期间的生命周期。
    /// </summary>
    public partial class Test_UIBase
    {
        /// <summary>
        ///  创建父面板和两个可同时显示的有序子分支，用于验证隐藏期间的回调重入。
        /// </summary>
        /// <param name="parent">创建的父面板。</param>
        /// <param name="first">按层级顺序首先处理的子面板。</param>
        /// <param name="later">在首个子面板之后处理的子面板。</param>
        private void CreateThirdAuditHideBranches(out UIBase parent, out UIBase first, out UIBase later)
        {
            // 在独立容器下创建父面板，隔离其他用例的层级关系。
            parent = CreateUI("ThirdAuditParent", CreateObject("ThirdAuditContainer").transform);
            // 创建首先参与隐藏的子面板。
            first = CreateUI("FirstBranch", parent.transform);
            // 创建在前序回调之后处理的子面板。
            later = CreateUI("LaterBranch", parent.transform);
            // 把后续面板放入另一互斥组，使两个子分支可以同时显示。
            SetUIBasePrivateField(later, "mutexGroup", 1);
            // 显示父面板，建立或恢复祖先的可用状态。
            parent.Show();
            // 显示前序子面板，使其隐藏行为会被父级联调用。
            first.Show();
            // 显示后续子面板，检查前序回调是否影响其后代状态。
            later.Show();
            // 固定前序面板为第一个子节点，确保其回调先发生。
            first.transform.SetSiblingIndex(0);
            // 固定后续面板为第二个子节点，确保它接受重入后的状态检查。
            later.transform.SetSiblingIndex(1);
        }

        /// <summary>
        ///  验证前序子面板停用祖先后，同步隐藏仍递归处理原本应隐藏的孙级面板。
        /// </summary>
        [Test]
        public void ThirdAudit_Core_HideDeactivatedAncestor_ShouldHideExistingGrandchild()
        {
            // 创建两个同时显示且顺序固定的子分支，准备触发隐藏回调重入。
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            // 在后续分支下创建同步隐藏需要处理的孙级面板。
            var grandchild = CreateUI("ExistingGrandchild", later.transform);
            // 取得孙级面板的 CanvasGroup，检查状态之外的实际透明度。
            var group = grandchild.GetComponent<CanvasGroup>();
            // 从零开始记录孙级面板隐藏行为的执行次数。
            var grandchildHides = 0;
            // 让孙级面板的显示行为恢复不透明，便于观察实际表现。
            grandchild.ShowActions += () => group.alpha = 1f;
            // 记录孙级隐藏次数，并把真实透明度设为零。
            grandchild.HideActions += () => { grandchildHides++; group.alpha = 0f; };
            // 预先显示孙级面板，使其属于本次隐藏应处理的后代。
            grandchild.Show();
            // 为父面板配置重新激活行为，便于隐藏后检查残留状态。
            parent.ShowActions += () => parent.gameObject.SetActive(true);
            // 在首个子面板隐藏时停用祖先，制造后续分支失活的边界。
            first.HideActions += () => parent.gameObject.SetActive(false);
            // 确认“grandchild.gameObject”的层级激活状态为真。
            Assert.IsTrue(grandchild.gameObject.activeInHierarchy);
            // 确认孙级界面（grandchild）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, grandchild.State);
            // 确认孙级界面（grandchild）的随父显示配置为假。
            Assert.IsFalse(grandchild.ShowWithParent);

            // 从父面板提交同步隐藏，触发对子树的完整递归处理。
            parent.Hide();

            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认前序界面（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认后续界面（later）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, later.State,
                "The already captured direct child must still be hidden by the existing fix.");
            // 记录实际状态与表现，便于失败时定位回调时序。
            TestContext.Out.WriteLine(
                $"DIAGNOSTIC deeper sync hide: grandchildState={grandchild.State}; " +
                $"grandchildHides={grandchildHides}; alpha={group.alpha}; " +
                $"activeSelf={grandchild.gameObject.activeSelf}; activeInHierarchy={grandchild.gameObject.activeInHierarchy}");

            // 显示父面板，建立或恢复祖先的可用状态。
            parent.Show();
            // 显示后续子面板，检查前序回调是否影响其后代状态。
            later.Show();
            // 孙级面板在请求前已激活，且始终没有改变父节点。
            // 前序回调停用祖先不能打断对该面板的递归隐藏。
            TestContext.Out.WriteLine(
                $"DIAGNOSTIC deeper sync reopen: grandchildState={grandchild.State}; " +
                $"grandchildHides={grandchildHides}; alpha={group.alpha}; " +
                $"activeInHierarchy={grandchild.gameObject.activeInHierarchy}; showWithParent={grandchild.ShowWithParent}");
            // 确认孙级界面（grandchild）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, grandchild.State);
            // 确认“grandchildHides”等于1。
            Assert.AreEqual(1, grandchildHides);
            // 确认效果所属组（group）的透明度等于0f。
            Assert.AreEqual(0f, group.alpha);
        }

        /// <summary>
        ///  验证祖先失活不打断异步隐藏对子孙面板的处理，并且仍等待父级异步行为。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdAudit_Core_HideAsyncDeactivatedAncestor_ShouldHideExistingGrandchild()
        {
            // 创建两个同时显示且顺序固定的子分支，准备触发隐藏回调重入。
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            // 在后续分支下创建异步隐藏需要处理的孙级面板。
            var grandchild = CreateUI("ExistingAsyncGrandchild", later.transform);
            // 取得孙级面板的 CanvasGroup，检查状态之外的实际透明度。
            var group = grandchild.GetComponent<CanvasGroup>();
            // 从零开始记录孙级面板隐藏行为的执行次数。
            var grandchildHides = 0;
            // 让孙级面板的显示行为恢复不透明，便于观察实际表现。
            grandchild.ShowActions += () => group.alpha = 1f;
            // 记录孙级隐藏次数，并把真实透明度设为零。
            grandchild.HideActions += () => { grandchildHides++; group.alpha = 0f; };
            // 预先显示孙级面板，使其属于本次隐藏应处理的后代。
            grandchild.Show();
            // 为父面板配置重新激活行为，便于隐藏后检查残留状态。
            parent.ShowActions += () => parent.gameObject.SetActive(true);
            // 在首个子面板隐藏时停用祖先，制造后续分支失活的边界。
            first.HideActions += () => parent.gameObject.SetActive(false);
            // 创建由测试手动完成的任务，用于观察其他行为是否被提前结束。
            var pending = new UniTaskCompletionSource();
            // 从零开始记录父面板异步隐藏行为的启动次数。
            var parentAsyncCalls = 0;
            // 记录父级异步行为的启动，并用手动任务阻止其立即完成。
            parent.HideAsyncActions += _ => { parentAsyncCalls++; return pending.Task; };
            // 确认“grandchild.gameObject”的层级激活状态为真。
            Assert.IsTrue(grandchild.gameObject.activeInHierarchy);
            // 确认孙级界面（grandchild）的随父显示配置为假。
            Assert.IsFalse(grandchild.ShowWithParent);

            // 启动父级异步隐藏，观察子树处理和父级等待之间的时序。
            var operation = CaptureException(parent.HideAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认“parentAsyncCalls”等于1。
                Assert.AreEqual(1, parentAsyncCalls);
                // 确认父界面（parent）的界面状态等于隐藏过渡态。
                Assert.AreEqual(UIState.Hiding, parent.State);
                // 确认后续界面（later）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, later.State);
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted);
                // 记录实际状态与表现，便于失败时定位回调时序。
                TestContext.Out.WriteLine(
                    $"DIAGNOSTIC deeper async scheduled: grandchildState={grandchild.State}; " +
                    $"grandchildHides={grandchildHides}; alpha={group.alpha}");
            }
            finally
            {
                // 释放手动挂起的行为，使已启动的生命周期能够完成收尾。
                pending.TrySetResult();
            }

            // 等待被测异步请求结束，再检查异常、通知次数和最终状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认前序界面（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认后续界面（later）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, later.State);
            // 显示父面板，建立或恢复祖先的可用状态。
            parent.Show();
            // 显示后续子面板，检查前序回调是否影响其后代状态。
            later.Show();
            // 记录实际状态与表现，便于失败时定位回调时序。
            TestContext.Out.WriteLine(
                $"DIAGNOSTIC deeper async reopen: grandchildState={grandchild.State}; " +
                $"grandchildHides={grandchildHides}; alpha={group.alpha}; " +
                $"activeInHierarchy={grandchild.gameObject.activeInHierarchy}; showWithParent={grandchild.ShowWithParent}");
            // 确认孙级界面（grandchild）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, grandchild.State);
            // 确认“grandchildHides”等于1。
            Assert.AreEqual(1, grandchildHides);
            // 确认效果所属组（group）的透明度等于0f。
            Assert.AreEqual(0f, group.alpha);
        }

        /// <summary>
        ///  验证子面板在隐藏前通知中停用自身后，已选中的后代仍完成一次隐藏。
        /// </summary>
        [Test]
        public void ThirdAudit_Core_ChildDeactivatesItselfInBeforeHide_ShouldFinishItsCapturedDescendants()
        {
            // 创建独立的父面板，验证子节点在通知中停用自身的情况。
            var parent = CreateUI("SelfDeactivateParent", CreateObject("SelfDeactivateContainer").transform);
            // 创建将在隐藏前通知中停用自身的子面板。
            var child = CreateUI("SelfDeactivatingChild", parent.transform);
            // 创建隐藏请求开始时应被选中的孙级面板。
            var grandchild = CreateUI("CapturedGrandchild", child.transform);
            // 取得孙级面板的 CanvasGroup，检查状态之外的实际透明度。
            var group = grandchild.GetComponent<CanvasGroup>();
            // 从零开始记录孙级面板隐藏行为的执行次数。
            var grandchildHides = 0;
            // 重新显示子面板时恢复节点激活，以便检查孙级是否遗漏隐藏。
            child.ShowActions += () => child.gameObject.SetActive(true);
            // 让孙级面板的显示行为恢复不透明，便于观察实际表现。
            grandchild.ShowActions += () => group.alpha = 1f;
            // 记录孙级隐藏次数，并把真实透明度设为零。
            grandchild.HideActions += () => { grandchildHides++; group.alpha = 0f; };
            // 预先显示孙级面板，使其属于本次隐藏应处理的后代。
            grandchild.Show();
            // 在子面板隐藏前通知中停用自身，测试已选中后代的继续处理。
            child.BeforeUIHideEvent += () => child.gameObject.SetActive(false);

            // 从父面板提交同步隐藏，触发对子树的完整递归处理。
            parent.Hide();

            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认孙级界面（grandchild）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, grandchild.State);
            // 确认“grandchildHides”等于1。
            Assert.AreEqual(1, grandchildHides);
            // 确认“child.gameObject”的自身激活状态为假。
            Assert.IsFalse(child.gameObject.activeSelf);
            // 显示父面板，建立或恢复祖先的可用状态。
            parent.Show();
            // 重新激活子面板，检查孙级的隐藏状态是否保持。
            child.Show();
            // 确认“grandchild.gameObject”的层级激活状态为真。
            Assert.IsTrue(grandchild.gameObject.activeInHierarchy);
            // 确认孙级界面（grandchild）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, grandchild.State);
            // 确认效果所属组（group）的透明度等于0f。
            Assert.AreEqual(0f, group.alpha);
            // 确认“grandchildHides”等于1。
            Assert.AreEqual(1, grandchildHides);
        }

        /// <summary>
        ///  验证已选中的子面板在回调中移出又移回原父节点时，只隐藏一次。
        /// </summary>
        [Test]
        public void ThirdAudit_Core_HideCallbackMovesCapturedChildOutAndBack_ShouldHideCurrentChildOnce()
        {
            // 创建两个同时显示且顺序固定的子分支，准备触发隐藏回调重入。
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            // 创建临时外部父节点，供回调中移出并移回子面板。
            var outside = CreateObject("TemporaryOutsideParent");
            // 从零开始记录后续子面板的隐藏次数。
            var laterHides = 0;
            // 记录后续子面板实际执行隐藏的次数。
            later.HideActions += () => laterHides++;
            // 在首个子面板隐藏回调中移动后续子面板，构造层级先移出再移回的情况。
            first.HideActions += () =>
            {
                // 在前序回调中把后续子面板暂时移出原父节点。
                later.transform.SetParent(outside.transform, false);
                // 同一回调内把子面板移回，验证访问时按当前归属处理。
                later.transform.SetParent(parent.transform, false);
            };

            // 从父面板提交同步隐藏，触发对子树的完整递归处理。
            parent.Hide();

            // 确认“later.transform”的直属父节点仍引用“parent.transform”。
            Assert.AreSame(parent.transform, later.transform.parent);
            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认前序界面（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认后续界面（later）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, later.State,
                "A selected child that is again directly owned when visited must complete its hide.");
            // 确认“laterHides”等于1。
            Assert.AreEqual(1, laterHides);
        }

        /// <summary>
        ///  验证首个子面板取消请求后，停止后续子面板和父级行为，也不发送成功通知。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdAudit_Core_HideAsyncCanceledByFirstChild_ShouldStopRemainingCapturedWork()
        {
            // 创建两个同时显示且顺序固定的子分支，准备触发隐藏回调重入。
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            // 创建本次隐藏请求的调用方取消源，并在结束时释放。
            using var cancellation = new CancellationTokenSource();
            // 从零开始记录后续子面板的隐藏次数。
            var laterHides = 0;
            // 记录取消之后父级异步隐藏行为是否被错误启动。
            var parentAsyncHides = 0;
            // 记录取消之后父级是否错误发送隐藏完成通知。
            var parentAfterHides = 0;
            // 记录后续子面板实际执行隐藏的次数。
            later.HideActions += () => laterHides++;
            // 追加立即完成的父级行为，用计数检查取消后是否仍启动它。
            parent.HideAsyncActions += _ => { parentAsyncHides++; return UniTask.CompletedTask; };
            // 记录父级隐藏完成通知，验证取消请求不会误报成功。
            parent.AfterUIHidedEvent += () => parentAfterHides++;
            // 在首个子面板的隐藏回调中停用祖先并取消请求。
            first.HideActions += () =>
            {
                // 先停用祖先，模拟前序子面板隐藏引起的层级失活。
                parent.gameObject.SetActive(false);
                // 随后取消调用方请求，要求停止剩余子树和父级工作。
                cancellation.Cancel();
            };

            // 带调用方令牌启动父级隐藏，并保存最终结果供取消断言使用。
            var operation = CaptureException(parent.HideAsync(cancellation.Token));
            // 等待被测异步请求结束，再检查异常、通知次数和最终状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认前序界面（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认后续界面（later）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, later.State,
                "Caller cancellation must stop later work even though the child remains in the snapshot.");
            // 确认“laterHides”等于0。
            Assert.AreEqual(0, laterHides);
            // 确认“parentAsyncHides”等于0。
            Assert.AreEqual(0, parentAsyncHides);
            // 确认“parentAfterHides”等于0。
            Assert.AreEqual(0, parentAfterHides);
            // 确认父界面（parent）的界面状态不同于隐藏终态。
            Assert.AreNotEqual(UIState.Hided, parent.State,
                "A canceled parent hide must not report a successful completion state.");
        }

        /// <summary>
        ///  验证祖先失活后的隐藏仍取消孙级显示预留，并等待已启动但忽略令牌的行为收尾。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdAudit_Core_HideDeactivatedAncestor_ShouldCancelGrandchildShowReservation()
        {
            // 创建两个同时显示且顺序固定的子分支，准备触发隐藏回调重入。
            CreateThirdAuditHideBranches(out var parent, out var first, out var later);
            // 创建将停留在显示过程中的孙级面板。
            var grandchild = CreateUI("PendingGrandchild", later.transform);
            // 创建由测试手动完成的任务，用于观察其他行为是否被提前结束。
            var pending = new UniTaskCompletionSource();
            // 预留显示令牌观测值，回调启动后替换成实际收到的令牌。
            var showToken = CancellationToken.None;
            // 记录孙级面板显示完成通知次数。
            var afterShows = 0;
            // 记录孙级面板实际执行隐藏的次数。
            var hides = 0;
            // 捕获显示令牌，同时用忽略取消的挂起任务模拟已经启动的业务行为。
            grandchild.ShowAsyncActions += token => { showToken = token; return pending.Task; };
            // 记录显示完成通知，确保被隐藏替换的显示流程不会补发成功通知。
            grandchild.AfterUIShowedEvent += () => afterShows++;
            // 记录对孙级显示预留进行收尾时的实际隐藏次数。
            grandchild.HideActions += () => hides++;
            // 启动孙级异步显示并保持挂起，随后由父级隐藏取消该显示预留。
            var operation = CaptureException(grandchild.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认孙级界面（grandchild）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, grandchild.State);
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted);
                // 在首个子面板隐藏时停用祖先，制造后续分支失活的边界。
                first.HideActions += () => parent.gameObject.SetActive(false);

                // 从父面板提交同步隐藏，触发对子树的完整递归处理。
                parent.Hide();

                // 确认父界面（parent）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, parent.State);
                // 确认后续界面（later）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, later.State);
                // 确认孙级界面（grandchild）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, grandchild.State,
                    "Even under an inactive ancestor, the existing show reservation must be canceled and hidden.");
                // 确认“showToken”的取消标志为真。
                Assert.IsTrue(showToken.IsCancellationRequested);
                // 确认隐藏次数（hides）等于1。
                Assert.AreEqual(1, hides);
                // 确认显示完成通知次数（afterShows）等于0。
                Assert.AreEqual(0, afterShows);
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted,
                    "The canceled operation must still observe the already started token-ignoring behavior.");
            }
            finally
            {
                // 释放手动挂起的行为，使已启动的生命周期能够完成收尾。
                pending.TrySetResult();
            }

            // 等待被测异步请求结束，再检查异常、通知次数和最终状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认孙级界面（grandchild）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, grandchild.State);
            // 确认隐藏次数（hides）等于1。
            Assert.AreEqual(1, hides);
            // 确认显示完成通知次数（afterShows）等于0。
            Assert.AreEqual(0, afterShows);
        }
    }
}
