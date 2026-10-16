using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的显示完成时的准入复查。</summary>
    public partial class Test_UIBase
    {
        /// <summary>验证同步完成状态回调使父链失效后，不再派发显示成功通知。</summary>
        /// <param name="strict">是否使用要求连续父链已显示的严格策略。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Show_CompletionStateCallbackLosesParentAdmission_ShouldSkipSuccessNotification(bool strict)
        {
            // 创建“oldParent”并配置测试所需的层级或表现组件。
            var oldParent = CreateUI("CompletionOldParent", CreateObject("CompletionOldRoot").transform);
            // 创建“newParent”并配置测试所需的层级或表现组件。
            var newParent = CreateUI("CompletionHiddenParent", CreateObject("CompletionNewRoot").transform);
            // 提交“oldParent”的显示请求，触发实际生命周期。
            oldParent.Show();
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("CompletionChild", oldParent.transform);
            // 根据严格策略对象或标志（strict）选择对应测试路径。
            if (strict) child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 初始化“afterCount”为0，作为后续回调的观测基准。
            var afterCount = 0;
            // 初始化“newParentShows”为0，作为后续回调的观测基准。
            var newParentShows = 0;
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => afterCount++;
            // 为“newParent.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            newParent.ShowActions += () => newParentShows++;
            // 准备在 Showed 状态通知中换父的回调，验证完成前仍会复查父链准入。
            Action<UIState, UIState> moveOnShowed = (_, current) =>
            {
                // 根据“current == UIState.Showed”选择对应测试路径。
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };
            // 为“child.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            child.OnStateChanged += moveOnShowed;

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认“child.transform”的直属父节点等于“newParent.transform”。
            Assert.AreEqual(newParent.transform, child.transform.parent);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认“newParent”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, newParent.State);
            // 确认“newParentShows”等于0。
            Assert.AreEqual(0, newParentShows, "完成阶段不得重新补齐换父后的父链。");
            // 确认“afterCount”等于0。
            Assert.AreEqual(0, afterCount, "状态通知导致失去准入时，不得继续派发显示成功通知。");

            // 解除“child.OnStateChanged”的测试订阅，避免干扰后续用例。
            child.OnStateChanged -= moveOnShowed;
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            child.transform.SetParent(oldParent.transform, false);
            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
            // 确认“afterCount”等于1。
            Assert.AreEqual(1, afterCount, "恢复准入后的独立显示请求仍应成功一次。");
        }

        /// <summary>验证异步完成状态回调使严格父链失效后，不再报告显示成功。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_CompletionStateCallbackLosesStrictParentAdmission_ShouldSkipSuccessNotification() =>
            VerifyCompletionParentAdmissionAsync(true);

        /// <summary>验证异步完成状态回调使自动父链失效后，不再报告显示成功。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_CompletionStateCallbackLosesAutoParentAdmission_ShouldSkipSuccessNotification() =>
            VerifyCompletionParentAdmissionAsync(false);

        /// <summary>验证异步显示在完成状态通知后仍复核指定父链准入策略。</summary>
        /// <param name="strict">是否使用要求连续父链已显示的严格策略。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyCompletionParentAdmissionAsync(bool strict)
        {
            // 创建“oldParent”并配置测试所需的层级或表现组件。
            var oldParent = CreateUI("AsyncCompletionOldParent", CreateObject("AsyncCompletionOldRoot").transform);
            // 创建“newParent”并配置测试所需的层级或表现组件。
            var newParent = CreateUI("AsyncCompletionHiddenParent", CreateObject("AsyncCompletionNewRoot").transform);
            // 提交“oldParent”的显示请求，触发实际生命周期。
            oldParent.Show();
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("AsyncCompletionChild", oldParent.transform);
            // 根据严格策略对象或标志（strict）选择对应测试路径。
            if (strict) child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => pending.Task;
            // 初始化“afterCount”为0，作为后续回调的观测基准。
            var afterCount = 0;
            // 初始化“newParentShows”为0，作为后续回调的观测基准。
            var newParentShows = 0;
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => afterCount++;
            // 为“newParent.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            newParent.ShowActions += () => newParentShows++;
            // 为“child.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            child.OnStateChanged += (_, current) =>
            {
                // 根据“current == UIState.Showed”选择对应测试路径。
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };

            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            pending.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“child.transform”的直属父节点等于“newParent.transform”。
            Assert.AreEqual(newParent.transform, child.transform.parent);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认“newParent”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, newParent.State);
            // 确认“newParentShows”等于0。
            Assert.AreEqual(0, newParentShows, "异步完成阶段不得重新补齐换父后的父链。");
            // 确认“afterCount”等于0。
            Assert.AreEqual(0, afterCount, "本轮任务取消时，不得已向业务报告显示成功。");
        }

        /// <summary>验证异步完成状态回调新增同优先级兄弟时保留新请求，旧显示不报告成功。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_CompletionStateCallbackAddsEqualPrioritySibling_ShouldPreserveSiblingAndSkipSuccess()
        {
            // 创建“oldParent”并配置测试所需的层级或表现组件。
            var oldParent = CreateUI("AsyncMutexOldParent", CreateObject("AsyncMutexOldRoot").transform);
            // 创建“newParent”并配置测试所需的层级或表现组件。
            var newParent = CreateUI("AsyncMutexNewParent", CreateObject("AsyncMutexNewRoot").transform);
            // 提交“oldParent”的显示请求，触发实际生命周期。
            oldParent.Show();
            // 提交“newParent”的显示请求，触发实际生命周期。
            newParent.Show();
            // 创建“existing”并配置测试所需的层级或表现组件。
            var existing = CreateUI("AsyncMutexExisting", newParent.transform);
            // 提交“existing”的显示请求，触发实际生命周期。
            existing.Show();
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("AsyncMutexMoving", oldParent.transform);
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => pending.Task;
            // 初始化“existingHides”为0，作为后续回调的观测基准。
            var existingHides = 0;
            // 初始化“afterCount”为0，作为后续回调的观测基准。
            var afterCount = 0;
            // 为“existing.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            existing.HideActions += () => existingHides++;
            // 为“existing.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            existing.HideAsyncActions += _ => { existingHides++; return UniTask.CompletedTask; };
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => afterCount++;
            // 为“child.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            child.OnStateChanged += (_, current) =>
            {
                // 根据“current == UIState.Showed”选择对应测试路径。
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };

            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            pending.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认“existing”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, existing.State);
            // 确认“existingHides”等于0。
            Assert.AreEqual(0, existingHides, "异步完成阶段只停止失去互斥资格的自身，不隐藏新兄弟。");
            // 确认“afterCount”等于0。
            Assert.AreEqual(0, afterCount);
        }

        /// <summary>验证同步完成状态回调新增同优先级兄弟后先协调互斥再报告成功。</summary>
        [Test]
        public void Show_CompletionStateCallbackAddsEqualPrioritySibling_ShouldReconcileBeforeSuccess()
        {
            // 创建“oldParent”并配置测试所需的层级或表现组件。
            var oldParent = CreateUI("SyncMutexOldParent", CreateObject("SyncMutexOldRoot").transform);
            // 创建“newParent”并配置测试所需的层级或表现组件。
            var newParent = CreateUI("SyncMutexNewParent", CreateObject("SyncMutexNewRoot").transform);
            // 提交“oldParent”的显示请求，触发实际生命周期。
            oldParent.Show();
            // 提交“newParent”的显示请求，触发实际生命周期。
            newParent.Show();
            // 创建“existing”并配置测试所需的层级或表现组件。
            var existing = CreateUI("SyncMutexExisting", newParent.transform);
            // 提交“existing”的显示请求，触发实际生命周期。
            existing.Show();
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("SyncMutexMoving", oldParent.transform);
            // 初始化“existingHides”为0，作为后续回调的观测基准。
            var existingHides = 0;
            // 初始化“afterCount”为0，作为后续回调的观测基准。
            var afterCount = 0;
            // 保存“existingStateAtSuccess”的基准值显示终态，供后续比较或恢复。
            var existingStateAtSuccess = UIState.Showed;
            // 为“existing.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            existing.HideActions += () => existingHides++;
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => { afterCount++; existingStateAtSuccess = existing.State; };
            // 为“child.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            child.OnStateChanged += (_, current) =>
            {
                // 根据“current == UIState.Showed”选择对应测试路径。
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
            // 确认“existing”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, existing.State);
            // 确认“existingHides”等于1。
            Assert.AreEqual(1, existingHides);
            // 确认“afterCount”等于1。
            Assert.AreEqual(1, afterCount);
            // 确认“existingStateAtSuccess”等于隐藏终态。
            Assert.AreEqual(UIState.Hided, existingStateAtSuccess, "同步成功通知必须晚于本轮动态互斥协调。");
        }

        /// <summary>验证完成状态回调换到已就绪且无竞争的新父界面后仍可成功。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_CompletionStateCallbackMovesToReadyEmptyParent_ShouldStillSucceed()
        {
            // 创建“oldParent”并配置测试所需的层级或表现组件。
            var oldParent = CreateUI("ValidCompletionOldParent", CreateObject("ValidCompletionOldRoot").transform);
            // 创建“newParent”并配置测试所需的层级或表现组件。
            var newParent = CreateUI("ValidCompletionNewParent", CreateObject("ValidCompletionNewRoot").transform);
            // 提交“oldParent”的显示请求，触发实际生命周期。
            oldParent.Show();
            // 提交“newParent”的显示请求，触发实际生命周期。
            newParent.Show();
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("ValidCompletionChild", oldParent.transform);
            // 将子界面（child）的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => pending.Task;
            // 初始化“afterCount”为0，作为后续回调的观测基准。
            var afterCount = 0;
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => afterCount++;
            // 为“child.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            child.OnStateChanged += (_, current) =>
            {
                // 根据“current == UIState.Showed”选择对应测试路径。
                if (current == UIState.Showed) child.transform.SetParent(newParent.transform, false);
            };

            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            pending.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认“child.transform”的直属父节点等于“newParent.transform”。
            Assert.AreEqual(newParent.transform, child.transform.parent);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
            // 确认“afterCount”等于1。
            Assert.AreEqual(1, afterCount, "合法换父不能被一律取消或漏发成功通知。");
        }
    }
}
