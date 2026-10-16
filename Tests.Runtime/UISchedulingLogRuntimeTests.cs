using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的调度拒绝日志。</summary>
    public partial class Test_UIBase
    {

        /// <summary>验证关闭调度日志后，准入拒绝不产生日志。</summary>
        [Test]
        public void SchedulingLog_Disabled_ShouldNotLogRejectedRequests()
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope(false);
            // 创建层级容器（container）并配置测试所需的层级或表现组件。
            var container = CreateObject("SilentPriorityRoot");
            // 创建高优先级界面（high）并配置测试所需的层级或表现组件。
            var high = CreateUI("SilentHigh", container.transform);
            // 创建低优先级界面（low）并配置测试所需的层级或表现组件。
            var low = CreateUI("SilentLow", container.transform);
            // 将高优先级界面（high）的互斥优先级设为3，建立当前测试条件。
            high.MutexPriority = 3;
            // 提交高优先级界面（high）的显示请求，触发实际生命周期。
            high.Show();
            // 提交低优先级界面（low）的显示请求，触发实际生命周期。
            low.Show();
            // 创建严格策略对象或标志（strict）并配置测试所需的层级或表现组件。
            var strict = CreateUI("SilentStrict", CreateObject("SilentStrictRoot").transform);
            // 将严格策略对象或标志（strict）的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            strict.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 提交严格策略对象或标志（strict）的显示请求，触发实际生命周期。
            strict.Show();

            // 确认“log.Messages”的元素数量等于0。
            Assert.AreEqual(0, log.Messages.Count);
            // 确认低优先级界面（low）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, low.State);
            // 确认严格策略对象或标志（strict）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, strict.State);
        }

        /// <summary>验证优先级拒绝日志只记录一次，并包含持有者与双方优先级。</summary>
        [Test]
        public void SchedulingLog_PriorityRejection_ShouldIdentifyHolderAndBothPrioritiesOnce()
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建层级容器（container）并配置测试所需的层级或表现组件。
            var container = CreateObject("LoggedPriorityRoot");
            // 创建高优先级界面（high）并配置测试所需的层级或表现组件。
            var high = CreateUI("LoggedHigh", container.transform);
            // 创建低优先级界面（low）并配置测试所需的层级或表现组件。
            var low = CreateUI("LoggedLow", container.transform);
            // 将高优先级界面（high）的互斥优先级设为3，建立当前测试条件。
            high.MutexPriority = 3;
            // 将低优先级界面（low）的互斥优先级设为1，建立当前测试条件。
            low.MutexPriority = 1;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(high, "mutexGroup", 7);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(low, "mutexGroup", 7);
            // 提交高优先级界面（high）的显示请求，触发实际生命周期。
            high.Show();
            // 初始化“effects”为0，作为后续回调的观测基准。
            var effects = 0;
            // 为“low.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            low.OnStateChanged += (_, _) => effects++;
            // 为“low.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            low.ShowActions += () => effects++;

            // 提交低优先级界面（low）的显示请求，触发实际生命周期。
            low.Show();

            // 验证只有一条调度日志，并完整包含本场景的拒绝原因与界面信息。
            AssertOneSchedulingLog(log, "LoggedPriorityRoot/LoggedLow", "显示请求被拒绝", "普通显示",
                "互斥组 7", "LoggedPriorityRoot/LoggedHigh", "目标优先级 1", "持有者优先级 3");
            // 确认“effects”等于0。
            Assert.AreEqual(0, effects);
            // 确认低优先级界面（low）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, low.State);
            // 确认高优先级界面（high）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, high.State);
        }

        /// <summary>验证异步准入拒绝只记录一次日志，任务仍正常返回。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator SchedulingLog_AsyncRejection_ShouldLogOnceAndStillCompleteNormally()
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建层级容器（container）并配置测试所需的层级或表现组件。
            var container = CreateObject("AsyncLoggedPriorityRoot");
            // 创建高优先级界面（high）并配置测试所需的层级或表现组件。
            var high = CreateUI("AsyncLoggedHigh", container.transform);
            // 创建低优先级界面（low）并配置测试所需的层级或表现组件。
            var low = CreateUI("AsyncLoggedLow", container.transform);
            // 将高优先级界面（high）的互斥优先级设为2，建立当前测试条件。
            high.MutexPriority = 2;
            // 提交高优先级界面（high）的显示请求，触发实际生命周期。
            high.Show();

            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(low.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 验证只有一条调度日志，并完整包含本场景的拒绝原因与界面信息。
            AssertOneSchedulingLog(log, "AsyncLoggedLow", "AsyncLoggedHigh", "显示请求被拒绝");
            // 确认低优先级界面（low）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, low.State);
            // 确认高优先级界面（high）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, high.State);
        }

        /// <summary>验证父链拒绝日志报告父界面的真实状态。</summary>
        /// <param name="parentState">父界面当前用于准入判断的状态。</param>
        [TestCase(UIState.Hided)]
        [TestCase(UIState.Showing)]
        [TestCase(UIState.Hiding)]
        public void SchedulingLog_UnreadyParent_ShouldReportItsActualState(UIState parentState)
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("UnreadyLoggedParent", CreateObject("UnreadyLoggedRoot").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("StrictLoggedChild", parent.transform);
            // 将子界面（child）的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parent, "_isUIStateInitialized", true);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parent, "state", parentState);

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 验证只有一条调度日志，并完整包含本场景的拒绝原因与界面信息。
            AssertOneSchedulingLog(log, "StrictLoggedChild", "严格父链", "UnreadyLoggedParent",
                "状态=" + parentState);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认父界面（parent）的界面状态等于“parentState”。
            Assert.AreEqual(parentState, parent.State);
        }

        /// <summary>验证 Showed 父界面尚未初始化或失活时日志说明具体原因。</summary>
        /// <param name="initialized">父界面是否已完成实际生命周期初始化。</param>
        /// <param name="active">父界面物体是否在层级中激活。</param>
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void SchedulingLog_ShowedParentNotReady_ShouldExplainInitializationOrActivation(
            bool initialized, bool active)
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("ReadinessLoggedParent", CreateObject("ReadinessLoggedRoot").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("ReadinessLoggedChild", parent.transform);
            // 将子界面（child）的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parent, "state", UIState.Showed);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parent, "_isUIStateInitialized", initialized);
            // 切换“parent.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            parent.gameObject.SetActive(active);

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 验证只有一条调度日志，并完整包含本场景的拒绝原因与界面信息。
            AssertOneSchedulingLog(log, "ReadinessLoggedParent", "状态=Showed",
                "已初始化=" + initialized, "层级激活=" + active);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
        }

        /// <summary>验证父链日志准确指出阻挡请求的高层祖先。</summary>
        [Test]
        public void SchedulingLog_UnreadyAncestor_ShouldNameTheBlockingAncestor()
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建祖先界面（ancestor）并配置测试所需的层级或表现组件。
            var ancestor = CreateUI("BlockingLoggedAncestor", CreateObject("AncestorLoggedRoot").transform);
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("ReadyLoggedParent", ancestor.transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("AncestorLoggedChild", parent.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(ancestor, "_isUIStateInitialized", true);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parent, "_isUIStateInitialized", true);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parent, "state", UIState.Showed);
            // 将子界面（child）的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 验证只有一条调度日志，并完整包含本场景的拒绝原因与界面信息。
            AssertOneSchedulingLog(log, "BlockingLoggedAncestor」尚未就绪", "状态=Hided", "AncestorLoggedChild");
        }

        /// <summary>验证缺少直接 UI 父节点时日志说明严格父链要求。</summary>
        /// <param name="hasContainer">是否提供没有 UIBase 的普通父容器。</param>
        [TestCase(true)]
        [TestCase(false)]
        public void SchedulingLog_MissingUIParent_ShouldExplainTheRequiredDirectParent(bool hasContainer)
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建层级容器（container）并配置测试所需的层级或表现组件。
            var container = hasContainer ? CreateObject("OrdinaryLoggedContainer").transform : null;
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("MissingParentLoggedChild", container);
            // 将子界面（child）的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 验证只有一条调度日志，并完整包含本场景的拒绝原因与界面信息。
            AssertOneSchedulingLog(log, "MissingParentLoggedChild", "严格父链", "未挂载 UIBase",
                hasContainer ? "OrdinaryLoggedContainer" : "<不存在>");
        }

        /// <summary>验证显示中失去准入的日志与初次拒绝区分，且仅记录一次。</summary>
        [Test]
        public void SchedulingLog_AdmissionLostDuringShow_ShouldDistinguishRevalidationAndLogOnce()
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建“destination”并配置测试所需的层级或表现组件。
            var destination = CreateObject("LostAdmissionRoot");
            // 创建高优先级界面（high）并配置测试所需的层级或表现组件。
            var high = CreateUI("MovedLoggedHigh", CreateObject("MovedHighOldRoot").transform);
            // 创建低优先级界面（low）并配置测试所需的层级或表现组件。
            var low = CreateUI("LostAdmissionLow", destination.transform);
            // 将高优先级界面（high）的互斥优先级设为3，建立当前测试条件。
            high.MutexPriority = 3;
            // 提交高优先级界面（high）的显示请求，触发实际生命周期。
            high.Show();
            // 为“low.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            low.ShowActions += () => high.transform.SetParent(destination.transform, false);

            // 提交低优先级界面（low）的显示请求，触发实际生命周期。
            low.Show();

            // 验证只有一条调度日志，并完整包含本场景的拒绝原因与界面信息。
            AssertOneSchedulingLog(log, "LostAdmissionLow", "显示操作失去准入", "MovedLoggedHigh");
            // 确认低优先级界面（low）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, low.State);
            // 确认高优先级界面（high）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, high.State);
        }

        /// <summary>验证级联子界面换父后日志说明其失去了原来源关系。</summary>
        [Test]
        public void SchedulingLog_CascadeChildReparented_ShouldExplainSourceRelationshipLoss()
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("LoggedCascadeSource", CreateObject("LoggedCascadeRoot").transform);
            // 创建“other”并配置测试所需的层级或表现组件。
            var other = CreateUI("LoggedCascadeDestination", CreateObject("OtherCascadeRoot").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("LoggedCascadeChild", parent.transform);
            // 将子界面（child）的随父显示配置设为true，建立当前测试条件。
            child.ShowWithParent = true;
            // 为“child.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            child.ShowActions += () => child.transform.SetParent(other.transform, false);

            // 提交父界面（parent）的显示请求，触发实际生命周期。
            parent.Show();

            // 验证只有一条调度日志，并完整包含本场景的拒绝原因与界面信息。
            AssertOneSchedulingLog(log, "LoggedCascadeChild", "随父显示", "父级联关系已失效",
                "LoggedCascadeSource", "LoggedCascadeDestination");
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
        }

        /// <summary>验证成功、状态短路及允许的抢占均不产出拒绝日志。</summary>
        [Test]
        public void SchedulingLog_SuccessNoOpAndAllowedPreemption_ShouldRemainSilent()
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建层级容器（container）并配置测试所需的层级或表现组件。
            var container = CreateObject("SuccessfulLoggedRoot");
            // 创建低优先级界面（low）并配置测试所需的层级或表现组件。
            var low = CreateUI("SuccessfulLoggedLow", container.transform);
            // 创建高优先级界面（high）并配置测试所需的层级或表现组件。
            var high = CreateUI("SuccessfulLoggedHigh", container.transform);
            // 将高优先级界面（high）的互斥优先级设为2，建立当前测试条件。
            high.MutexPriority = 2;

            // 提交低优先级界面（low）的显示请求，触发实际生命周期。
            low.Show();
            // 提交低优先级界面（low）的显示请求，触发实际生命周期。
            low.Show();
            // 提交高优先级界面（high）的显示请求，触发实际生命周期。
            high.Show();
            // 提交低优先级界面（low）的隐藏请求，检查子树联动与终态。
            low.Hide();
            // 提交高优先级界面（high）的隐藏请求，检查子树联动与终态。
            high.Hide();
            // 提交高优先级界面（high）的隐藏请求，检查子树联动与终态。
            high.Hide();

            // 确认“log.Messages”的元素数量等于0。
            Assert.AreEqual(0, log.Messages.Count);
            // 确认低优先级界面（low）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, low.State);
            // 确认高优先级界面（high）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, high.State);
        }

        /// <summary>验证预取消请求不会被错误记录为准入拒绝。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator SchedulingLog_PreCanceledRequest_ShouldNotReportAnAdmissionRejection()
        {
            // 创建捕获日志（log），隔离本用例的资源或观测数据。
            using var log = new SchedulingLogScope();
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("CanceledLoggedChild", CreateObject("CanceledLoggedRoot").transform);
            // 将子界面（child）的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 取消本轮操作，验证旧任务停止提交成功状态。
            cancellation.Cancel();

            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(child.ShowAsync(cancellation.Token));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“log.Messages”的元素数量等于0。
            Assert.AreEqual(0, log.Messages.Count);
        }

        /// <summary>验证仅产生一条预期等级的调度日志，且包含全部诊断片段。</summary>
        /// <param name="log">保存本轮调度日志的捕获作用域。</param>
        /// <param name="fragments">日志必须包含的诊断文本片段。</param>
        private static void AssertOneSchedulingLog(SchedulingLogScope log, params string[] fragments)
        {
            // 确认“log.Messages”的元素数量等于1。
            Assert.AreEqual(1, log.Messages.Count, "一次失败判断应输出一条诊断。");
            // 确认“log.Types[0]”等于“LogType.Log”。
            Assert.AreEqual(LogType.Log, log.Types[0], "正常仲裁拒绝应使用普通 Log。");
            // 逐项检查或处理“var fragment in fragments”，覆盖集合中的每个对象。
            foreach (var fragment in fragments) StringAssert.Contains(fragment, log.Messages[0]);
        }
    }
}
