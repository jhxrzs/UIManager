using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的取消回调失活祖先后的递归隐藏。</summary>
    public partial class Test_UIBase
    {
        /// <summary>验证同步隐藏取消旧显示时，即使取消回调关闭祖先也不漏掉已有后代。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Hide_PreviousShowCancellationDeactivatesAncestor_ShouldHideExistingDescendants() =>
            VerifyHideAfterCancellationDeactivatesAncestor(false);

        /// <summary>验证异步隐藏取消旧显示时，即使取消回调关闭祖先也不漏掉已有后代。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator HideAsync_PreviousShowCancellationDeactivatesAncestor_ShouldHideExistingDescendants() =>
            VerifyHideAfterCancellationDeactivatesAncestor(true);

        /// <summary>验证取消旧显示引起的祖先失活不会丢失已取得资格的子树隐藏工作。</summary>
        /// <param name="asynchronous">是否通过异步显隐接口驱动被测流程。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyHideAfterCancellationDeactivatesAncestor(bool asynchronous)
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("CancelBeforeHideParent", CreateObject("CancelBeforeHideRoot").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("ExistingChild", parent.transform);
            // 创建孙级界面（grandchild）并配置测试所需的层级或表现组件。
            var grandchild = CreateUI("ExistingGrandchild", child.transform);
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 初始化取消回调注册（registration）为“default(CancellationTokenRegistration)”，作为后续回调的观测基准。
            var registration = default(CancellationTokenRegistration);
            // 初始化“cancellationCalls”为0，作为后续回调的观测基准。
            var cancellationCalls = 0;
            // 初始化“childHides”为0，作为后续回调的观测基准。
            var childHides = 0;
            // 初始化“grandchildHides”为0，作为后续回调的观测基准。
            var grandchildHides = 0;
            // 初始化“oldAfterShows”为0，作为后续回调的观测基准。
            var oldAfterShows = 0;
            // 为“child.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.HideActions += () => childHides++;
            // 为“grandchild.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            grandchild.HideActions += () => grandchildHides++;
            // 为“parent.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            parent.AfterUIShowedEvent += () => oldAfterShows++;
            // 为“parent.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowAsyncActions += token =>
            {
                // 注册旧操作的取消回调并保存注册句柄，构造取消期间改变环境的重入场景。
                registration = token.Register(() =>
                {
                    // 更新“cancellationCalls”的计数，记录回调实际执行次数。
                    cancellationCalls++;
                    // 切换“parent.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
                    parent.gameObject.SetActive(false);
                });
                // 返回“pending.Task”，交给调用方继续配置或断言。
                return pending.Task;
            };
            // 启动并保存显示分支或操作（showing），随后检查等待、取消与完成结果。
            var showing = CaptureException(parent.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 提交子界面（child）的显示请求，触发实际生命周期。
                child.Show();
                // 提交孙级界面（grandchild）的显示请求，触发实际生命周期。
                grandchild.Show();
                // 确认父界面（parent）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, parent.State);
                // 确认子界面（child）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, child.State);
                // 确认孙级界面（grandchild）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, grandchild.State);
                // 确认“grandchild.gameObject”的层级激活状态为真。
                Assert.IsTrue(grandchild.gameObject.activeInHierarchy);

                // 根据“asynchronous”选择对应测试路径。
                if (asynchronous)
                {
                    // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
                    var hiding = CaptureException(parent.HideAsync(CancellationToken.None));
                    // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
                    yield return WaitForTask(hiding);
                    // 确认隐藏分支或操作（hiding）的异步结果为空。
                    Assert.IsNull(hiding.Result);
                }
                // 覆盖条件不成立时的另一条配置或清理路径。
                else parent.Hide();

                // 确认“cancellationCalls”等于1。
                Assert.AreEqual(1, cancellationCalls);
                // 确认“parent.gameObject”的层级激活状态为假。
                Assert.IsFalse(parent.gameObject.activeInHierarchy);
                // 确认父界面（parent）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, parent.State);
                // 确认子界面（child）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, child.State);
                // 确认孙级界面（grandchild）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, grandchild.State);
                // 确认“childHides”等于1。
                Assert.AreEqual(1, childHides);
                // 确认“grandchildHides”等于1。
                Assert.AreEqual(1, grandchildHides);
                // 确认显示分支或操作（showing）的完成标志为假。
                Assert.IsFalse(showing.IsCompleted,
                    "被取消的旧显示仍须观察此前已启动、忽略令牌的行为。");
            }
            finally
            {
                // 释放取消回调注册（registration），恢复注册或全局配置。
                registration.Dispose();
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                pending.TrySetResult();
            }

            // 等待“WaitForTask(showing)”，再检查操作完成后的状态。
            yield return WaitForTask(showing);
            // 确认显示分支或操作（showing）的异步结果保留取消语义，不被报告为成功。
            Assert.That(showing.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“oldAfterShows”等于0。
            Assert.AreEqual(0, oldAfterShows);
            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
        }
    }
}
