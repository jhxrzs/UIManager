using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的自动父链准入与动态换父。</summary>
    public partial class Test_UIBase
    {
        /// <summary>建立被更高优先级界面阻挡的父链，返回阻挡位置及其当前持有者。</summary>
        /// <param name="blockAncestor">是否把阻挡点放在更高层祖先而非直接父界面。</param>
        /// <param name="blocked">输出被更高优先级界面阻挡的父链节点。</param>
        /// <param name="holder">输出阻挡被测请求的高优先级界面。</param>
        /// <param name="directParent">输出被测子界面的直接父界面。</param>
        /// <returns>具有指定祖先阻挡条件的目标子界面。</returns>
        private UIBase CreateBlockedAutoChain(bool blockAncestor, out UIBase blocked, out UIBase holder,
            out UIBase directParent)
        {
            // 创建根节点（root）并配置测试所需的层级或表现组件。
            var root = CreateObject("BlockedAutoRoot");
            // 将“blocked”设为“CreateUI("BlockedParent", root.transform)”，建立当前测试条件。
            blocked = CreateUI("BlockedParent", root.transform);
            // 将当前持有者（holder）设为“CreateUI("PriorityHolder", root.transform)”，建立当前测试条件。
            holder = CreateUI("PriorityHolder", root.transform);
            // 将当前持有者（holder）的互斥优先级设为3，建立当前测试条件。
            holder.MutexPriority = 3;
            // 提交当前持有者（holder）的显示请求，触发实际生命周期。
            holder.Show();
            // 将“directParent”设为“blockAncestor ? CreateUI("IntermediateParent", blocked.transform) : blocked”，建立当前测试条件。
            directParent = blockAncestor ? CreateUI("IntermediateParent", blocked.transform) : blocked;
            // 返回“CreateUI("AutoChild", directParent.transform)”，交给调用方继续配置或断言。
            return CreateUI("AutoChild", directParent.transform);
        }

        /// <summary>验证同步补父链被祖先优先级阻挡时，不启动子界面且不影响既有持有者。</summary>
        /// <param name="blockAncestor">是否把阻挡点放在更高层祖先而非直接父界面。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void AutoParent_SyncRejectedAncestor_ShouldStopChildAndLeaveHolderUntouched(bool blockAncestor)
        {
            // 保存子界面（child）的基准值“CreateBlockedAutoChain(blockAncestor, out var blocked, out var holder, out var parent)”，供后续比较或恢复。
            var child = CreateBlockedAutoChain(blockAncestor, out var blocked, out var holder, out var parent);
            // 初始化“parentShows”为0，作为后续回调的观测基准。
            var parentShows = 0;
            // 初始化“childShows”为0，作为后续回调的观测基准。
            var childShows = 0;
            // 初始化“childAfter”为0，作为后续回调的观测基准。
            var childAfter = 0;
            // 为“parent.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowActions += () => parentShows++;
            // 为“child.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowActions += () => childShows++;
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => childAfter++;

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认“blocked”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, blocked.State);
            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认当前持有者（holder）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, holder.State);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认“parentShows”等于0。
            Assert.AreEqual(0, parentShows);
            // 确认“childShows”等于0。
            Assert.AreEqual(0, childShows);
            // 确认“childAfter”等于0。
            Assert.AreEqual(0, childAfter);
            // 确认“child.GetComponent<UnityEngine.CanvasGroup>()”的射线拦截配置为假。
            Assert.IsFalse(child.GetComponent<UnityEngine.CanvasGroup>().blocksRaycasts);
            // 提交当前持有者（holder）的隐藏请求，检查子树联动与终态。
            holder.Hide();
            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();
            // 确认“blocked”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, blocked.State);
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State, "阻挡解除后的新请求必须能成功。");
            // 确认“childAfter”等于1。
            Assert.AreEqual(1, childAfter);
        }

        /// <summary>验证异步显示在直接父界面被阻挡时于生命周期开始前拒绝。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator AutoParent_AsyncRejectedParent_ShouldRejectBeforeStarting() =>
            VerifyAsyncAutoParentRejection(false);

        /// <summary>验证异步显示在更高层祖先被阻挡时于生命周期开始前拒绝。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator AutoParent_AsyncRejectedGrandparent_ShouldRejectBeforeStarting() =>
            VerifyAsyncAutoParentRejection(true);

        /// <summary>验证指定祖先位置的准入拒绝不会启动异步行为或改变当前持有者。</summary>
        /// <param name="blockAncestor">是否把阻挡点放在更高层祖先而非直接父界面。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyAsyncAutoParentRejection(bool blockAncestor)
        {
            // 保存子界面（child）的基准值“CreateBlockedAutoChain(blockAncestor, out var blocked, out var holder, out var parent)”，供后续比较或恢复。
            var child = CreateBlockedAutoChain(blockAncestor, out var blocked, out var holder, out var parent);
            // 初始化“childShows”为0，作为后续回调的观测基准。
            var childShows = 0;
            // 初始化“childAfter”为0，作为后续回调的观测基准。
            var childAfter = 0;
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => { childShows++; return UniTask.CompletedTask; };
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => childAfter++;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result, "预演拒绝按普通准入拒绝正常返回。");
            // 确认“blocked”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, blocked.State);
            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认当前持有者（holder）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, holder.State);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认“childShows”等于0。
            Assert.AreEqual(0, childShows);
            // 确认“childAfter”等于0。
            Assert.AreEqual(0, childAfter);
        }

        /// <summary>验证自动补父链按根到叶执行，使严格中间父界面在根就绪后准入。</summary>
        [Test]
        public void AutoParent_ShouldResolveRootBeforeStrictIntermediateParent()
        {
            // 创建根节点（root）并配置测试所需的层级或表现组件。
            var root = CreateUI("RootParent", CreateObject("RootContainer").transform);
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("StrictIntermediate", root.transform);
            // 将父界面（parent）的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            parent.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("AutoChild", parent.transform);
            // 创建“order”，隔离本用例的资源或观测数据。
            var order = new List<string>();
            // 为“root.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            root.ShowActions += () => order.Add("root");
            // 为“parent.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowActions += () => order.Add("parent");
            // 为“child.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowActions += () => order.Add("child");

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认“order”等于“new[] { "root", "parent", "child" }”。
            CollectionAssert.AreEqual(new[] { "root", "parent", "child" }, order);
            // 确认根节点（root）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, root.State);
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
        }

        /// <summary>验证同步补父链允许严格中间节点复用本轮已调度的 Showing 根界面。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator AutoParent_SyncStrictIntermediate_ShouldReuseAlreadyScheduledShowingRoot()
        {
            // 创建根节点（root）并配置测试所需的层级或表现组件。
            var root = CreateUI("PendingRoot", CreateObject("PendingRootContainer").transform);
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("StrictIntermediate", root.transform);
            // 将父界面（parent）的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            parent.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("SyncAutoChild", parent.transform);
            // 创建“rootPending”，使动画完成时机由测试显式控制。
            var rootPending = new UniTaskCompletionSource();
            // 初始化“rootAsyncShows”为0，作为后续回调的观测基准。
            var rootAsyncShows = 0;
            // 初始化“rootSyncShows”为0，作为后续回调的观测基准。
            var rootSyncShows = 0;
            // 保存“rootToken”的基准值不可取消的默认令牌，供后续比较或恢复。
            var rootToken = CancellationToken.None;
            // 创建“order”，隔离本用例的资源或观测数据。
            var order = new List<string>();
            // 初始化“childAfter”为0，作为后续回调的观测基准。
            var childAfter = 0;
            // 为“root.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            root.ShowAsyncActions += token =>
            {
                // 更新“rootAsyncShows”的计数，记录回调实际执行次数。
                rootAsyncShows++;
                // 将“rootToken”设为操作令牌（token），建立当前测试条件。
                rootToken = token;
                // 返回“rootPending.Task”，交给调用方继续配置或断言。
                return rootPending.Task;
            };
            // 为“root.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            root.ShowActions += () => rootSyncShows++;
            // 为“parent.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowActions += () => order.Add("parent");
            // 为“child.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowActions += () => order.Add("child");
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => childAfter++;

            // 真实请求已返回其同步调度阶段，只有根面板自身动画仍由门闩保持未完成。
            var rootOperation = CaptureException(root.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认根节点（root）的初始化完成标志为真。
                Assert.IsTrue(root.IsUIStateInitialized);
                // 确认根节点（root）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, root.State);
                // 确认“rootOperation”的完成标志为假。
                Assert.IsFalse(rootOperation.IsCompleted);
                // 确认“rootAsyncShows”等于1。
                Assert.AreEqual(1, rootAsyncShows);

                // 外部直接显示严格面板仍必须要求根面板已经 Showed。
                parent.Show();
                // 确认父界面（parent）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, parent.State);
                // 确认“order”为空。
                Assert.IsEmpty(order);

                // 自动补父链可以复用已调度的 Showing 根面板，并同步打开严格中间父和目标。
                child.Show();
                // 确认父界面（parent）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, parent.State);
                // 确认子界面（child）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, child.State);
                // 确认“order”等于“new[] { "parent", "child" }”。
                CollectionAssert.AreEqual(new[] { "parent", "child" }, order);
                // 确认“childAfter”等于1。
                Assert.AreEqual(1, childAfter);
                // 确认根节点（root）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, root.State);
                // 确认“rootOperation”的完成标志为假。
                Assert.IsFalse(rootOperation.IsCompleted, "补父链不能提前完成根面板的动画。");
                // 确认“rootAsyncShows”等于1。
                Assert.AreEqual(1, rootAsyncShows, "已完成关系调度的根面板不能重启动画。");
                // 确认“rootSyncShows”等于0。
                Assert.AreEqual(0, rootSyncShows, "同步补链不能用同步显示取代根面板的异步显示。");
                // 确认“rootToken”的取消标志为假。
                Assert.IsFalse(rootToken.IsCancellationRequested);

                // 本轮内部准入不能泄漏给后续的外部直接显示请求。
                parent.Hide();
                // 提交父界面（parent）的显示请求，触发实际生命周期。
                parent.Show();
                // 确认父界面（parent）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, parent.State);
                // 确认子界面（child）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, child.State);
                // 确认“order”等于“new[] { "parent", "child" }”。
                CollectionAssert.AreEqual(new[] { "parent", "child" }, order);
            }
            finally
            {
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                rootPending.TrySetResult();
            }

            // 等待“WaitForTask(rootOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(rootOperation);
            // 确认“rootOperation”的异步结果为空。
            Assert.IsNull(rootOperation.Result);
            // 确认根节点（root）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, root.State);
            // 确认“rootAsyncShows”等于1。
            Assert.AreEqual(1, rootAsyncShows);
            // 确认“rootSyncShows”等于0。
            Assert.AreEqual(0, rootSyncShows);
        }

        /// <summary>验证序列化 Showed 父界面必须完成实际初始化后才能显示子界面。</summary>
        [Test]
        public void AutoParent_SerializedShowedParent_ShouldRunRealInitializationBeforeChild()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("SerializedParent", CreateObject("RootContainer").transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parent, "state", UIState.Showed);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("AutoChild", parent.transform);
            // 创建“order”，隔离本用例的资源或观测数据。
            var order = new List<string>();
            // 为“parent.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowActions += () => order.Add("parent");
            // 为“child.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowActions += () => order.Add("child");

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认“order”等于“new[] { "parent", "child" }”。
            CollectionAssert.AreEqual(new[] { "parent", "child" }, order);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
            // 提交父界面（parent）的显示请求，触发实际生命周期。
            parent.Show();
            // 确认“order”的元素数量等于2。
            Assert.AreEqual(2, order.Count, "已真正初始化的父级不得重复派发显示行为。");
        }

        /// <summary>验证异步子界面完成前先补齐序列化 Showed 父界面的实际初始化。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator AutoParent_AsyncSerializedShowedParent_ShouldInitializeBeforeReportingChildSuccess()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("SerializedParent", CreateObject("RootContainer").transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parent, "state", UIState.Showed);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("AutoChild", parent.transform);
            // 初始化“parentShows”为0，作为后续回调的观测基准。
            var parentShows = 0;
            // 为“parent.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowAsyncActions += _ => { parentShows++; return UniTask.CompletedTask; };
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认“parentShows”等于1。
            Assert.AreEqual(1, parentShows);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
        }

        /// <summary>验证补父链前换父时使用新的 Transform 父链。</summary>
        [Test]
        public void AutoParent_ReparentedBeforeParentStage_ShouldResolveNewParent()
        {
            // 创建“oldParent”并配置测试所需的层级或表现组件。
            var oldParent = CreateUI("OldParent", CreateObject("OldRoot").transform);
            // 创建“newParent”并配置测试所需的层级或表现组件。
            var newParent = CreateUI("NewParent", CreateObject("NewRoot").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("AutoChild", oldParent.transform);
            // 为“child.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.BeforeUIShowEvent += () => child.transform.SetParent(newParent.transform, false);

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认“oldParent”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, oldParent.State);
            // 确认“newParent”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, newParent.State);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
        }

        /// <summary>验证补父链后换到未就绪父界面会隐藏子界面，但不回滚已打开的原父界面。</summary>
        [Test]
        public void AutoParent_ReparentedAfterParentStage_ShouldHideChildWithoutUndoingOpenedParent()
        {
            // 创建“oldParent”并配置测试所需的层级或表现组件。
            var oldParent = CreateUI("OldParent", CreateObject("OldRoot").transform);
            // 创建“newParent”并配置测试所需的层级或表现组件。
            var newParent = CreateUI("NewParent", CreateObject("NewRoot").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("AutoChild", oldParent.transform);
            // 初始化“after”为0，作为后续回调的观测基准。
            var after = 0;
            // 为“child.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowActions += () => child.transform.SetParent(newParent.transform, false);
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => after++;

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认“oldParent”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, oldParent.State, "成功打开的父级不执行事务回滚。");
            // 确认“newParent”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, newParent.State);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认“after”等于0。
            Assert.AreEqual(0, after);
        }

        /// <summary>验证动画等待期间换父后会在成功提交前重新检查父链准入。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator AutoParent_ReparentedWhileOwnAnimationPending_ShouldRevalidateBeforeSuccess()
        {
            // 创建“oldParent”并配置测试所需的层级或表现组件。
            var oldParent = CreateUI("OldParent", CreateObject("OldRoot").transform);
            // 创建“newParent”并配置测试所需的层级或表现组件。
            var newParent = CreateUI("NewParent", CreateObject("NewRoot").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("AutoChild", oldParent.transform);
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => pending.Task;
            // 初始化“after”为0，作为后续回调的观测基准。
            var after = 0;
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => after++;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 确认“oldParent”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, oldParent.State);
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            child.transform.SetParent(newParent.transform, false);
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            pending.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认“newParent”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, newParent.State);
            // 确认“after”等于0。
            Assert.AreEqual(0, after);
        }

        /// <summary>验证并行动画被抢占后仍等待所有已启动行为收口。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator AutoParent_PreemptedDuringParallelAnimations_ShouldObserveAllStartedActions()
        {
            // 创建根节点（root）并配置测试所需的层级或表现组件。
            var root = CreateObject("PreemptedRoot");
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("Parent", root.transform);
            // 创建当前持有者（holder）并配置测试所需的层级或表现组件。
            var holder = CreateUI("Holder", root.transform);
            // 将当前持有者（holder）的互斥优先级设为3，建立当前测试条件。
            holder.MutexPriority = 3;
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("Child", parent.transform);
            // 创建“parentPending”，使动画完成时机由测试显式控制。
            var parentPending = new UniTaskCompletionSource();
            // 创建“childPending”，使动画完成时机由测试显式控制。
            var childPending = new UniTaskCompletionSource();
            // 为“parent.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowAsyncActions += _ => parentPending.Task;
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => childPending.Task;
            // 初始化“after”为0，作为后续回调的观测基准。
            var after = 0;
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => after++;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认父界面（parent）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, parent.State);
                // 确认子界面（child）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, child.State);
                // 提交当前持有者（holder）的显示请求，触发实际生命周期。
                holder.Show();
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                childPending.TrySetResult();
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted, "即使已取消，也应观察仍未结束的父动画。");
            }
            finally
            {
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                parentPending.TrySetResult();
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                childPending.TrySetResult();
            }
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认当前持有者（holder）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, holder.State);
            // 确认“after”等于0。
            Assert.AreEqual(0, after);
        }

        /// <summary>验证自动补父链拒绝日志同时指出请求子界面和未就绪祖先。</summary>
        [Test]
        public void AutoParent_RejectionLog_ShouldIdentifyChildAndUnreadyAncestor()
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 保存子界面（child）的基准值“CreateBlockedAutoChain(false, out _, out _, out _)”，供后续比较或恢复。
            var child = CreateBlockedAutoChain(false, out _, out _, out _);

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认“log.Messages.Exists(message => message.Contains("AutoChild") && message.Contains("显示预演失败") && messa…”满足约束“Is.True”。
            Assert.That(log.Messages.Exists(message => message.Contains("AutoChild") &&
                message.Contains("显示预演失败") && message.Contains("BlockedParent") &&
                message.Contains("显示请求被拒绝")), Is.True);
        }

        /// <summary>验证随父显示允许子界面在父界面仍 Showing 时完成。</summary>
        [Test]
        public void AutoParent_Cascade_ShouldAllowChildCompletionWhileParentStillShowing()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("CascadeParent", CreateObject("Root").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("CascadeChild", parent.transform);
            // 将子界面（child）的随父显示配置设为true，建立当前测试条件。
            child.ShowWithParent = true;
            // 初始化“observed”为false，作为后续回调的观测基准。
            var observed = false;
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => observed = parent.State == UIState.Showing;

            // 提交父界面（parent）的显示请求，触发实际生命周期。
            parent.Show();

            // 确认“observed”为真。
            Assert.IsTrue(observed);
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
        }

        /// <summary>验证异步随父显示不会反向等待父界面的最终 Showed 状态。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator AutoParent_AsyncCascade_ShouldNotWaitForParentsFinalShowed()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("CascadeParent", CreateObject("Root").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("CascadeChild", parent.transform);
            // 将子界面（child）的随父显示配置设为true，建立当前测试条件。
            child.ShowWithParent = true;
            // 初始化“observed”为false，作为后续回调的观测基准。
            var observed = false;
            // 为“child.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.AfterUIShowedEvent += () => observed = parent.State == UIState.Showing;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(parent.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认“observed”为真。
            Assert.IsTrue(observed);
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
        }
    }
}
