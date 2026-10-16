using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的同步关系调度与并行动画启动。</summary>
    public partial class Test_UIBase
    {
        /// <summary>验证深层祖先被阻挡时，预演不会先打开更高层根界面。</summary>
        /// <param name="strictRoot">根界面是否使用严格父链准入策略。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Preflight_DeeperAncestorBlocked_ShouldNotOpenEarlierRoot(bool strictRoot)
        {
            // 创建根节点（root）并配置测试所需的层级或表现组件。
            var root = CreateUI("UnopenedRoot", CreateObject("Container").transform);
            // 创建“middle”并配置测试所需的层级或表现组件。
            var middle = CreateUI("Middle", root.transform);
            // 创建当前持有者（holder）并配置测试所需的层级或表现组件。
            var holder = CreateUI("MiddleHolder", root.transform);
            // 将当前持有者（holder）的互斥优先级设为10，建立当前测试条件。
            holder.MutexPriority = 10;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(holder, "state", UIState.Showed);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(holder, "_isUIStateInitialized", true);
            // 根据“strictRoot”选择对应测试路径。
            if (strictRoot) root.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("Child", middle.transform);
            // 初始化回调次数（calls）为0，作为后续回调的观测基准。
            var calls = 0;
            // 为“root.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            root.BeforeUIShowEvent += () => calls++;
            // 为“root.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            root.ShowActions += () => calls++;
            // 为“middle.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            middle.BeforeUIShowEvent += () => calls++;
            // 为“child.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.BeforeUIShowEvent += () => calls++;

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认回调次数（calls）等于0。
            Assert.AreEqual(0, calls);
            // 确认根节点（root）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, root.State);
            // 确认“middle”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, middle.State);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认当前持有者（holder）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, holder.State);
        }

        /// <summary>验证祖先准入失败时保留子兄弟状态且不派发任何显隐通知。</summary>
        /// <param name="blockGrandparent">是否让祖父界面而非直接父界面失去准入。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Preflight_BlockedAncestor_ShouldPreserveChildSiblingAndAllNotifications(bool blockGrandparent)
        {
            // 保存子界面（child）的基准值“CreateBlockedAutoChain(blockGrandparent, out var blocked, out _, out var parent)”，供后续比较或恢复。
            var child = CreateBlockedAutoChain(blockGrandparent, out var blocked, out _, out var parent);
            // 创建“existing”并配置测试所需的层级或表现组件。
            var existing = CreateUI("ExistingChildSibling", parent.transform);
            // 显示兄弟后再恢复父级隐藏状态，构造合法入口能检查到的历史子状态。
            SetUIBasePrivateField(existing, "state", UIState.Showed);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(existing, "_isUIStateInitialized", true);
            // 初始化通知顺序记录（events）为0，作为后续回调的观测基准。
            var events = 0;
            // 为“blocked.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            blocked.OnStateChanged += (_, _) => events++;
            // 为“child.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            child.OnStateChanged += (_, _) => events++;
            // 为“child.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.BeforeUIShowEvent += () => events++;
            // 为“child.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.HideActions += () => events++;
            // 为“existing.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            existing.HideActions += () => events++;

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认通知顺序记录（events）等于0。
            Assert.AreEqual(0, events);
            // 确认“existing”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, existing.State);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认“(bool)typeof(UIBase).GetField("_isUIStateInitialized", System.Reflection.BindingFlags.Instance | Sy…”为假。
            Assert.IsFalse((bool)typeof(UIBase).GetField("_isUIStateInitialized",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(child));
        }

        /// <summary>验证异步预演失败时不隐藏既有兄弟，也不启动动画。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Preflight_AsyncBlockedAncestor_ShouldPreserveExistingSiblingWithoutStartingAnimation()
        {
            // 保存子界面（child）的基准值“CreateBlockedAutoChain(true, out _, out _, out var parent)”，供后续比较或恢复。
            var child = CreateBlockedAutoChain(true, out _, out _, out var parent);
            // 创建“sibling”并配置测试所需的层级或表现组件。
            var sibling = CreateUI("ExistingSibling", parent.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(sibling, "state", UIState.Showed);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(sibling, "_isUIStateInitialized", true);
            // 初始化回调次数（calls）为0，作为后续回调的观测基准。
            var calls = 0;
            // 为“child.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            child.BeforeUIShowEvent += () => calls++;
            // 为“sibling.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            sibling.HideAsyncActions += _ => { calls++; return UniTask.CompletedTask; };
            // 启动并保存执行结果（result），随后检查等待、取消与完成结果。
            var result = CaptureException(child.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(result)”，再检查操作完成后的状态。
            yield return WaitForTask(result);
            // 确认执行结果（result）的异步结果为空。
            Assert.IsNull(result.Result);
            // 确认回调次数（calls）等于0。
            Assert.AreEqual(0, calls);
            // 确认“sibling”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, sibling.State);
        }

        /// <summary>验证含严格中间节点的自动父链在同轮启动全部动画，无需逐层等待完成。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Scheduling_AutoChainWithStrictIntermediate_ShouldStartAllAnimationsBeforeAnyCompletes()
        {
            // 创建根节点（root）并配置测试所需的层级或表现组件。
            var root = CreateUI("Root", CreateObject("Container").transform);
            // 创建“middle”并配置测试所需的层级或表现组件。
            var middle = CreateUI("StrictMiddle", root.transform);
            // 将“middle”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            middle.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("RequestedChild", middle.transform);
            // 创建“rootGate”，使动画完成时机由测试显式控制。
            var rootGate = new UniTaskCompletionSource();
            // 创建“middleGate”，使动画完成时机由测试显式控制。
            var middleGate = new UniTaskCompletionSource();
            // 创建“childGate”，使动画完成时机由测试显式控制。
            var childGate = new UniTaskCompletionSource();
            // 初始化“rootCalls”为0，作为后续回调的观测基准。
            var rootCalls = 0;
            // 初始化“middleCalls”为0，作为后续回调的观测基准。
            var middleCalls = 0;
            // 初始化“childCalls”为0，作为后续回调的观测基准。
            var childCalls = 0;
            // 为“root.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            root.ShowAsyncActions += _ => { rootCalls++; return rootGate.Task; };
            // 为“middle.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            middle.ShowAsyncActions += _ => { middleCalls++; return middleGate.Task; };
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => { childCalls++; return childGate.Task; };
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认“rootCalls”等于1。
                Assert.AreEqual(1, rootCalls);
                // 确认“middleCalls”等于1。
                Assert.AreEqual(1, middleCalls);
                // 确认“childCalls”等于1。
                Assert.AreEqual(1, childCalls);
                // 确认根节点（root）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, root.State);
                // 确认“middle”的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, middle.State);
                // 确认子界面（child）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, child.State);
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted);
            }
            finally
            {
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                rootGate.TrySetResult(); middleGate.TrySetResult(); childGate.TrySetResult();
            }
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认根节点（root）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, root.State);
            // 确认“middle”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, middle.State);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
        }

        /// <summary>验证兄弟隐藏尚未结束时仍可调度父界面及默认子界面。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Scheduling_PendingSiblingHide_ShouldNotDelayParentOrDefaultChildScheduling()
        {
            // 创建层级容器（container）并配置测试所需的层级或表现组件。
            var container = CreateObject("Container");
            // 创建旧对象（old）并配置测试所需的层级或表现组件。
            var old = CreateUI("Old", container.transform);
            // 提交旧对象（old）的显示请求，触发实际生命周期。
            old.Show();
            // 创建面板（panel）并配置测试所需的层级或表现组件。
            var panel = CreateUI("Panel", container.transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("DefaultChild", panel.transform);
            // 将子界面（child）的随父显示配置设为true，建立当前测试条件。
            child.ShowWithParent = true;
            // 创建“hideGate”，使动画完成时机由测试显式控制。
            var hideGate = new UniTaskCompletionSource();
            // 创建“showGate”，使动画完成时机由测试显式控制。
            var showGate = new UniTaskCompletionSource();
            // 创建“childGate”，使动画完成时机由测试显式控制。
            var childGate = new UniTaskCompletionSource();
            // 初始化“childStarted”为false，作为后续回调的观测基准。
            var childStarted = false;
            // 为“old.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            old.HideAsyncActions += _ => hideGate.Task;
            // 为“panel.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            panel.ShowAsyncActions += _ => showGate.Task;
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => { childStarted = true; return childGate.Task; };
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(panel.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认旧对象（old）的界面状态等于隐藏过渡态。
                Assert.AreEqual(UIState.Hiding, old.State);
                // 确认面板（panel）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, panel.State);
                // 确认子界面（child）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, child.State);
                // 确认“childStarted”为真。
                Assert.IsTrue(childStarted);
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted);
            }
            finally
            {
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                hideGate.TrySetResult(); showGate.TrySetResult(); childGate.TrySetResult();
            }
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认旧对象（old）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, old.State);
            // 确认子界面（child）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, child.State);
        }

        /// <summary>验证不同互斥组的默认子界面在同轮启动动画。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Scheduling_DefaultChildrenInDifferentGroups_ShouldStartTogether()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("Parent", CreateObject("Container").transform);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("First", parent.transform);
            // 创建第二项（second）并配置测试所需的层级或表现组件。
            var second = CreateUI("Second", parent.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(second, "mutexGroup", 1);
            // 将首项（first）的随父显示配置设为“second.ShowWithParent = true”，建立当前测试条件。
            first.ShowWithParent = second.ShowWithParent = true;
            // 创建“gate”，使动画完成时机由测试显式控制。
            var gate = new UniTaskCompletionSource();
            // 初始化“secondStarted”为false，作为后续回调的观测基准。
            var secondStarted = false;
            // 为“first.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            first.ShowAsyncActions += _ => gate.Task;
            // 为“second.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            second.ShowAsyncActions += _ => { secondStarted = true; return UniTask.CompletedTask; };
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(parent.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try { Assert.IsTrue(secondStarted, "第一个子面板的动画不得延迟第二个子面板的调度。"); }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { gate.TrySetResult(); }
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
        }

        /// <summary>验证动画等待中新增的默认子界面不会在完成阶段重新被发现。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Scheduling_NewDefaultChildDuringAnimation_ShouldNotBeDiscoveredOnCompletion()
        {
            // 创建面板（panel）并配置测试所需的层级或表现组件。
            var panel = CreateUI("Panel", CreateObject("Container").transform);
            // 创建“gate”，使动画完成时机由测试显式控制。
            var gate = new UniTaskCompletionSource();
            // 为“panel.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            panel.ShowAsyncActions += _ => gate.Task;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(panel.ShowAsync(CancellationToken.None));
            // 创建“lateChild”并配置测试所需的层级或表现组件。
            var lateChild = CreateUI("LateChild", panel.transform);
            // 将“lateChild”的随父显示配置设为true，建立当前测试条件。
            lateChild.ShowWithParent = true;
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            gate.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认“lateChild”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lateChild.State, "动画完成阶段不再重新调度层级。");
        }

        /// <summary>验证动画中换父后的完成阶段不重新隐藏新父下的既有面板。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Scheduling_ReparentDuringAnimation_ShouldNotHideNewParentsExistingPanel()
        {
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("FirstParent", CreateObject("FirstContainer").transform);
            // 创建第二项（second）并配置测试所需的层级或表现组件。
            var second = CreateUI("SecondParent", CreateObject("SecondContainer").transform);
            // 提交第二项（second）的显示请求，触发实际生命周期。
            second.Show();
            // 创建“existing”并配置测试所需的层级或表现组件。
            var existing = CreateUI("Existing", second.transform);
            // 提交“existing”的显示请求，触发实际生命周期。
            existing.Show();
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("Moving", first.transform);
            // 创建“gate”，使动画完成时机由测试显式控制。
            var gate = new UniTaskCompletionSource();
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => gate.Task;
            // 初始化隐藏次数（hides）为0，作为后续回调的观测基准。
            var hides = 0;
            // 为“existing.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            existing.HideActions += () => hides++;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            child.transform.SetParent(second.transform, false);
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            gate.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认“existing”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, existing.State);
            // 确认隐藏次数（hides）等于0。
            Assert.AreEqual(0, hides);
        }

        /// <summary>验证后续动画失败不会回滚已经打开的祖先界面。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Scheduling_AnimationFailure_ShouldNotUndoAlreadyOpenedAncestor()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("Parent", CreateObject("Container").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("Child", parent.transform);
            // 创建“gate”，使动画完成时机由测试显式控制。
            var gate = new UniTaskCompletionSource();
            // 为“child.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowAsyncActions += _ => gate.Task;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 令挂起任务按预设错误失败，验证异常传播与其余任务观察。
            gate.TrySetException(new InvalidOperationException("presentation failed"));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果满足约束“Is.InstanceOf<InvalidOperationException>()”。
            Assert.That(operation.Result, Is.InstanceOf<InvalidOperationException>());
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
            // 确认子界面（child）的界面状态等于显示过渡态。
            Assert.AreEqual(UIState.Showing, child.State, "表现失败不伪造成功，保留原有失败收尾语义。");
        }

        /// <summary>验证派生异步行为返回任务前抛错时，仍观察已启动的祖先行为。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Scheduling_OverrideThrowsBeforeReturningTask_ShouldStillObserveStartedAncestor()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("Parent", CreateObject("Container").transform);
            // 创建“childObject”并配置测试所需的层级或表现组件。
            var childObject = CreateObject("ThrowingChild", parent.transform);
            // 添加并保存子界面（child），供后续生命周期与效果检查使用。
            var child = childObject.AddComponent<ThrowingShowUI>();
            // 将子界面（child）的组件启用状态设为false，建立当前测试条件。
            child.enabled = false;
            // 创建“gate”，使动画完成时机由测试显式控制。
            var gate = new UniTaskCompletionSource();
            // 为“parent.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowAsyncActions += _ => gate.Task;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted, "覆写钩子同步抛错也不能丢掉已经启动的祖先动画任务。");
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { gate.TrySetResult(); }
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果满足约束“Is.InstanceOf<InvalidOperationException>()”。
            Assert.That(operation.Result, Is.InstanceOf<InvalidOperationException>());
            // 确认“operation.Result.Message”等于“"override failed"”。
            Assert.AreEqual("override failed", operation.Result.Message);
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
            // 确认子界面（child）的界面状态等于显示过渡态。
            Assert.AreEqual(UIState.Showing, child.State);
        }

        /// <summary>验证可选随父显示子界面被阻挡不会拒绝父界面本身。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Scheduling_OptionalBlockedChild_ShouldNotRejectParent()
        {
            // 创建面板（panel）并配置测试所需的层级或表现组件。
            var panel = CreateUI("Panel", CreateObject("Container").transform);
            // 创建当前持有者（holder）并配置测试所需的层级或表现组件。
            var holder = CreateUI("Holder", panel.transform);
            // 显示父面板，并显示优先级为 10 的持有者。
            panel.Show(); holder.MutexPriority = 10; holder.Show();
            // 创建“optional”并配置测试所需的层级或表现组件。
            var optional = CreateUI("Optional", panel.transform);
            // 将“optional”的随父显示配置设为true，建立当前测试条件。
            optional.ShowWithParent = true;
            // 重新运行父级过渡，保留现有高优先级持有者。
            SetUIBasePrivateField(panel, "state", UIState.Hiding);
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(panel.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认面板（panel）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, panel.State);
            // 确认“optional”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, optional.State);
            // 确认当前持有者（holder）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, holder.State);
        }

        /// <summary>验证同组等优先级默认子界面只启动层级顺序最后一个候选。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Scheduling_EqualPriorityDefaults_ShouldOnlyAnimateLastCandidate()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("Parent", CreateObject("Container").transform);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("First", parent.transform);
            // 创建末尾界面（last）并配置测试所需的层级或表现组件。
            var last = CreateUI("Last", parent.transform);
            // 将首项（first）的随父显示配置设为“last.ShowWithParent = true”，建立当前测试条件。
            first.ShowWithParent = last.ShowWithParent = true;
            // 初始化“firstCalls”为0，作为后续回调的观测基准。
            var firstCalls = 0;
            // 为“first.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            first.ShowAsyncActions += _ => { firstCalls++; return UniTask.CompletedTask; };
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(parent.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认“firstCalls”等于0。
            Assert.AreEqual(0, firstCalls);
            // 确认首项（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认末尾界面（last）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, last.State);
        }
    }

}
