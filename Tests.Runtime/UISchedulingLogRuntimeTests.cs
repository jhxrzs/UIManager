using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private sealed class SchedulingLogScope : IDisposable
        {
            private readonly bool _previous = UIOperationSettings.EnableSchedulingLogs;
            internal readonly List<string> Messages = new();
            internal readonly List<LogType> Types = new();

            internal SchedulingLogScope(bool enabled = true)
            {
                UIOperationSettings.EnableSchedulingLogs = enabled;
                Application.logMessageReceived += Capture;
            }

            private void Capture(string message, string stack, LogType type)
            {
                if (!message.StartsWith("[UIManager]", StringComparison.Ordinal)) return;
                Messages.Add(message);
                Types.Add(type);
            }

            public void Dispose()
            {
                Application.logMessageReceived -= Capture;
                UIOperationSettings.EnableSchedulingLogs = _previous;
            }
        }

        [Test]
        public void SchedulingLog_Disabled_ShouldNotLogRejectedRequests()
        {
            using var log = new SchedulingLogScope(false);
            var container = CreateObject("SilentPriorityRoot");
            var high = CreateUI("SilentHigh", container.transform);
            var low = CreateUI("SilentLow", container.transform);
            high.MutexPriority = 3;
            high.Show();
            low.Show();
            var strict = CreateUI("SilentStrict", CreateObject("SilentStrictRoot").transform);
            strict.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            strict.Show();

            Assert.AreEqual(0, log.Messages.Count);
            Assert.AreEqual(UIState.Hided, low.State);
            Assert.AreEqual(UIState.Hided, strict.State);
        }

        [Test]
        public void SchedulingLog_PriorityRejection_ShouldIdentifyHolderAndBothPrioritiesOnce()
        {
            using var log = new SchedulingLogScope();
            var container = CreateObject("LoggedPriorityRoot");
            var high = CreateUI("LoggedHigh", container.transform);
            var low = CreateUI("LoggedLow", container.transform);
            high.MutexPriority = 3;
            low.MutexPriority = 1;
            SetUIBasePrivateField(high, "mutexGroup", 7);
            SetUIBasePrivateField(low, "mutexGroup", 7);
            high.Show();
            var effects = 0;
            low.OnStateChanged += (_, _) => effects++;
            low.ShowActions += () => effects++;

            low.Show();

            AssertOneSchedulingLog(log, "LoggedPriorityRoot/LoggedLow", "显示请求被拒绝", "普通显示",
                "互斥组 7", "LoggedPriorityRoot/LoggedHigh", "目标优先级 1", "持有者优先级 3");
            Assert.AreEqual(0, effects);
            Assert.AreEqual(UIState.Hided, low.State);
            Assert.AreEqual(UIState.Showed, high.State);
        }

        [UnityTest]
        public IEnumerator SchedulingLog_AsyncRejection_ShouldLogOnceAndStillCompleteNormally()
        {
            using var log = new SchedulingLogScope();
            var container = CreateObject("AsyncLoggedPriorityRoot");
            var high = CreateUI("AsyncLoggedHigh", container.transform);
            var low = CreateUI("AsyncLoggedLow", container.transform);
            high.MutexPriority = 2;
            high.Show();

            var operation = CaptureException(low.ShowAsync(CancellationToken.None));
            yield return WaitForTask(operation);

            Assert.IsNull(operation.Result);
            AssertOneSchedulingLog(log, "AsyncLoggedLow", "AsyncLoggedHigh", "显示请求被拒绝");
            Assert.AreEqual(UIState.Hided, low.State);
            Assert.AreEqual(UIState.Showed, high.State);
        }

        [TestCase(UIState.Hided)]
        [TestCase(UIState.Showing)]
        [TestCase(UIState.Hiding)]
        public void SchedulingLog_UnreadyParent_ShouldReportItsActualState(UIState parentState)
        {
            using var log = new SchedulingLogScope();
            var parent = CreateUI("UnreadyLoggedParent", CreateObject("UnreadyLoggedRoot").transform);
            var child = CreateUI("StrictLoggedChild", parent.transform);
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            SetUIBasePrivateField(parent, "_isUIStateInitialized", true);
            SetUIBasePrivateField(parent, "state", parentState);

            child.Show();

            AssertOneSchedulingLog(log, "StrictLoggedChild", "严格父链", "UnreadyLoggedParent",
                "状态=" + parentState);
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(parentState, parent.State);
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void SchedulingLog_ShowedParentNotReady_ShouldExplainInitializationOrActivation(
            bool initialized, bool active)
        {
            using var log = new SchedulingLogScope();
            var parent = CreateUI("ReadinessLoggedParent", CreateObject("ReadinessLoggedRoot").transform);
            var child = CreateUI("ReadinessLoggedChild", parent.transform);
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            SetUIBasePrivateField(parent, "state", UIState.Showed);
            SetUIBasePrivateField(parent, "_isUIStateInitialized", initialized);
            parent.gameObject.SetActive(active);

            child.Show();

            AssertOneSchedulingLog(log, "ReadinessLoggedParent", "状态=Showed",
                "已初始化=" + initialized, "层级激活=" + active);
            Assert.AreEqual(UIState.Hided, child.State);
        }

        [Test]
        public void SchedulingLog_UnreadyAncestor_ShouldNameTheBlockingAncestor()
        {
            using var log = new SchedulingLogScope();
            var ancestor = CreateUI("BlockingLoggedAncestor", CreateObject("AncestorLoggedRoot").transform);
            var parent = CreateUI("ReadyLoggedParent", ancestor.transform);
            var child = CreateUI("AncestorLoggedChild", parent.transform);
            SetUIBasePrivateField(ancestor, "_isUIStateInitialized", true);
            SetUIBasePrivateField(parent, "_isUIStateInitialized", true);
            SetUIBasePrivateField(parent, "state", UIState.Showed);
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            child.Show();

            AssertOneSchedulingLog(log, "BlockingLoggedAncestor」尚未就绪", "状态=Hided", "AncestorLoggedChild");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SchedulingLog_MissingUIParent_ShouldExplainTheRequiredDirectParent(bool hasContainer)
        {
            using var log = new SchedulingLogScope();
            var container = hasContainer ? CreateObject("OrdinaryLoggedContainer").transform : null;
            var child = CreateUI("MissingParentLoggedChild", container);
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            child.Show();

            AssertOneSchedulingLog(log, "MissingParentLoggedChild", "严格父链", "未挂载 UIBase",
                hasContainer ? "OrdinaryLoggedContainer" : "<不存在>");
        }

        [Test]
        public void SchedulingLog_AdmissionLostDuringShow_ShouldDistinguishRevalidationAndLogOnce()
        {
            using var log = new SchedulingLogScope();
            var destination = CreateObject("LostAdmissionRoot");
            var high = CreateUI("MovedLoggedHigh", CreateObject("MovedHighOldRoot").transform);
            var low = CreateUI("LostAdmissionLow", destination.transform);
            high.MutexPriority = 3;
            high.Show();
            low.ShowActions += () => high.transform.SetParent(destination.transform, false);

            low.Show();

            AssertOneSchedulingLog(log, "LostAdmissionLow", "显示操作失去准入", "MovedLoggedHigh");
            Assert.AreEqual(UIState.Hided, low.State);
            Assert.AreEqual(UIState.Showed, high.State);
        }

        [Test]
        public void SchedulingLog_CascadeChildReparented_ShouldExplainSourceRelationshipLoss()
        {
            using var log = new SchedulingLogScope();
            var parent = CreateUI("LoggedCascadeSource", CreateObject("LoggedCascadeRoot").transform);
            var other = CreateUI("LoggedCascadeDestination", CreateObject("OtherCascadeRoot").transform);
            var child = CreateUI("LoggedCascadeChild", parent.transform);
            child.ShowWithParent = true;
            child.ShowActions += () => child.transform.SetParent(other.transform, false);

            parent.Show();

            AssertOneSchedulingLog(log, "LoggedCascadeChild", "随父显示", "父级联关系已失效",
                "LoggedCascadeSource", "LoggedCascadeDestination");
            Assert.AreEqual(UIState.Hided, child.State);
            Assert.AreEqual(UIState.Showed, parent.State);
        }

        [Test]
        public void SchedulingLog_SuccessNoOpAndAllowedPreemption_ShouldRemainSilent()
        {
            using var log = new SchedulingLogScope();
            var container = CreateObject("SuccessfulLoggedRoot");
            var low = CreateUI("SuccessfulLoggedLow", container.transform);
            var high = CreateUI("SuccessfulLoggedHigh", container.transform);
            high.MutexPriority = 2;

            low.Show();
            low.Show();
            high.Show();
            low.Hide();
            high.Hide();
            high.Hide();

            Assert.AreEqual(0, log.Messages.Count);
            Assert.AreEqual(UIState.Hided, low.State);
            Assert.AreEqual(UIState.Hided, high.State);
        }

        [UnityTest]
        public IEnumerator SchedulingLog_PreCanceledRequest_ShouldNotReportAnAdmissionRejection()
        {
            using var log = new SchedulingLogScope();
            var child = CreateUI("CanceledLoggedChild", CreateObject("CanceledLoggedRoot").transform);
            child.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var operation = CaptureException(child.ShowAsync(cancellation.Token));
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(0, log.Messages.Count);
        }

        private static void AssertOneSchedulingLog(SchedulingLogScope log, params string[] fragments)
        {
            Assert.AreEqual(1, log.Messages.Count, "一次失败判断应输出一条诊断。");
            Assert.AreEqual(LogType.Log, log.Types[0], "正常仲裁拒绝应使用普通 Log。");
            foreach (var fragment in fragments) StringAssert.Contains(fragment, log.Messages[0]);
        }
    }
}
