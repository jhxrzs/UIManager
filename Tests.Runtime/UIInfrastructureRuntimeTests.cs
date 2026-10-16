using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>UniTask 迁移后的任务收尾与内部缓动行为。</summary>
    public class Test_UIInfrastructure
    {
        /// <summary>验证并行等待中的任务失败后仍观察其余未完成任务再报告错误。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator WaitAll_Failure_ShouldObservePendingTaskBeforeReturning()
        {
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 创建“failure”，隔离本用例的资源或观测数据。
            var failure = new InvalidOperationException("first handler failed");
            // 启动并观察内部异步操作，供后续断言其完成、异常或取消结果。
            var operation = CaptureError(UIAsync.WaitAll(UniTask.FromException(failure), pending.Task));

            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 确认异步操作（operation）的完成标志为假。
            Assert.IsFalse(operation.IsCompleted, "一个处理器失败后仍须等待已启动的其他处理器。");
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            pending.TrySetResult();
            // 等待“WaitFor(operation)”，再检查操作完成后的状态。
            yield return WaitFor(operation);
            // 确认异步操作（operation）的异步结果仍引用“failure”。
            Assert.AreSame(failure, operation.Result);
        }

        /// <summary>验证并行任务的多个失败都包含在最终异常中。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator WaitAll_MultipleFailures_ShouldReportEveryFailure()
        {
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 创建第一个预设异常，验证并行等待不会丢失已经发生的失败。
            var first = new InvalidOperationException("first failure");
            // 创建第二个预设异常，验证最终结果同时保留两个失败原因。
            var second = new ArgumentException("second failure");
            // 启动并观察内部异步操作，供后续断言其完成、异常或取消结果。
            var operation = CaptureError(UIAsync.WaitAll(UniTask.FromException(first), pending.Task));

            // 确认异步操作（operation）的完成标志为假。
            Assert.IsFalse(operation.IsCompleted);
            // 令挂起任务按预设错误失败，验证异常传播与其余任务观察。
            pending.TrySetException(second);
            // 等待“WaitFor(operation)”，再检查操作完成后的状态。
            yield return WaitFor(operation);
            // 确认异步操作（operation）的异步结果满足约束“Is.TypeOf<AggregateException>()”。
            Assert.That(operation.Result, Is.TypeOf<AggregateException>());
            // 确认“((AggregateException)operation.Result).InnerExceptions”包含相同元素于“new Exception[] { first, second }”。
            CollectionAssert.AreEquivalent(new Exception[] { first, second },
                ((AggregateException)operation.Result).InnerExceptions);
        }

        /// <summary>验证全部任务取消时保留取消异常语义，不包装成业务失败聚合。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator WaitAll_AllCanceled_ShouldPropagateCancellationWithoutAggregation()
        {
            // 创建取消令牌源，用于控制内部任务或数值过渡的取消。
            using var source = new CancellationTokenSource();
            // 在创建被测任务前先取消令牌，验证预取消路径。
            source.Cancel();
            // 启动并观察内部异步操作，供后续断言其完成、异常或取消结果。
            var operation = CaptureError(UIAsync.WaitAll(
                UniTask.FromCanceled(source.Token), UniTask.FromCanceled(source.Token)));
            // 等待“WaitFor(operation)”，再检查操作完成后的状态。
            yield return WaitFor(operation);
            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“((OperationCanceledException)operation.Result).CancellationToken”等于“source.Token”。
            Assert.AreEqual(source.Token, ((OperationCanceledException)operation.Result).CancellationToken);
        }

        /// <summary>验证预取消的零时长过渡不会写入目标值。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Transition_PreCanceledInstantDuration_ShouldNotWriteValue()
        {
            // 创建取消令牌源，用于控制内部任务或数值过渡的取消。
            using var source = new CancellationTokenSource();
            // 在创建被测任务前先取消令牌，验证预取消路径。
            source.Cancel();
            // 初始化“writes”为0，作为后续回调的观测基准。
            var writes = 0;
            // 启动并观察内部异步操作，供后续断言其完成、异常或取消结果。
            var operation = CaptureError(UITransition.RunAsync(0, 1, _ => writes++, 0,
                source.Token, Ease.Linear));
            // 等待“WaitFor(operation)”，再检查操作完成后的状态。
            yield return WaitFor(operation);
            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“writes”等于0。
            Assert.AreEqual(0, writes);
        }

        /// <summary>验证暂停游戏时间后过渡仍使用非缩放时间完成。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Transition_PausedGame_ShouldFinishUsingUnscaledTime()
        {
            // 保存“originalTimeScale”的基准值“Time.timeScale”，供后续比较或恢复。
            var originalTimeScale = Time.timeScale;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 将“Time.timeScale”设为0，建立当前测试条件。
                Time.timeScale = 0;
                // 保存“value”的基准值-1f，供后续比较或恢复。
                var value = -1f;
                // 保存“lastFrame”的基准值-1，供后续比较或恢复。
                var lastFrame = -1;
                // 初始化“duplicateWriteInFrame”为false，作为后续回调的观测基准。
                var duplicateWriteInFrame = false;
                // 启动并观察内部异步操作，供后续断言其完成、异常或取消结果。
                var operation = CaptureError(UITransition.RunAsync(0, 1, next =>
                {
                    // 累计“duplicateWriteInFrame”的观测标志，保留此前已出现的时序异常。
                    duplicateWriteInFrame |= lastFrame == Time.frameCount;
                    // 将“lastFrame”设为“Time.frameCount”，建立当前测试条件。
                    lastFrame = Time.frameCount;
                    // 将“value”设为“next”，建立当前测试条件。
                    value = next;
                }, 0.05f, CancellationToken.None, Ease.Linear));

                // 等待“WaitFor(operation)”，再检查操作完成后的状态。
                yield return WaitFor(operation);
                // 确认异步操作（operation）的异步结果为空。
                Assert.IsNull(operation.Result);
                // 确认“value”等于1f。
                Assert.AreEqual(1f, value);
                // 确认“duplicateWriteInFrame”为假。
                Assert.IsFalse(duplicateWriteInFrame, "每帧最多写入一次进度。");
            }
            finally
            {
                // 将“Time.timeScale”设为“originalTimeScale”，建立当前测试条件。
                Time.timeScale = originalTimeScale;
            }
        }

        /// <summary>验证属性写入回调取消过渡后，不再补写成功终值。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Transition_CanceledFromSetter_ShouldNotWriteTerminalValue()
        {
            // 创建取消令牌源，用于控制内部任务或数值过渡的取消。
            using var source = new CancellationTokenSource();
            // 初始化“writes”为0，作为后续回调的观测基准。
            var writes = 0;
            // 保存“value”的基准值-1f，供后续比较或恢复。
            var value = -1f;
            // 启动并观察内部异步操作，供后续断言其完成、异常或取消结果。
            var operation = CaptureError(UITransition.RunAsync(0, 1, next =>
            {
                // 将“value”设为“next”，建立当前测试条件。
                value = next;
                // 更新“writes”的计数，记录回调实际执行次数。
                writes++;
                // 在首次属性写入时取消过渡，验证之后不再补写终值。
                source.Cancel();
            }, 1f, source.Token, Ease.Linear));

            // 等待“WaitFor(operation)”，再检查操作完成后的状态。
            yield return WaitFor(operation);
            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“writes”等于1。
            Assert.AreEqual(1, writes);
            // 确认“value”等于0f。
            Assert.AreEqual(0f, value);
        }

        /// <summary>验证全部缓动曲线的采样结果有限且起止值正确。</summary>
        [Test]
        public void Easing_AllCurves_ShouldHaveFiniteValuesAndCorrectEndpoints()
        {
            // 逐项检查或处理“var ease in Enum.GetValues(typeof(Ease)).Cast<Ease>()”，覆盖集合中的每个对象。
            foreach (var ease in Enum.GetValues(typeof(Ease)).Cast<Ease>())
            {
                // 确认“UIEasing.Evaluate(ease, 0)”符合0f，允许指定的浮点误差。
                Assert.That(UIEasing.Evaluate(ease, 0), Is.EqualTo(0f).Within(0.00001f), ease.ToString());
                // 确认“UIEasing.Evaluate(ease, 1)”符合1f，允许指定的浮点误差。
                Assert.That(UIEasing.Evaluate(ease, 1), Is.EqualTo(1f).Within(0.00001f), ease.ToString());
                // 按既定范围逐项采样或处理，检查边界及重复执行行为。
                for (var sample = 0; sample <= 100; sample++)
                {
                    // 保存“value”的基准值“UIEasing.Evaluate(ease, sample / 100f)”，供后续比较或恢复。
                    var value = UIEasing.Evaluate(ease, sample / 100f);
                    // 确认“float.IsNaN(value) || float.IsInfinity(value)”为假。
                    Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value), ease.ToString());
                }
            }
        }

        /// <summary>等待异步操作并返回其异常，使协程可以直接断言失败类型。</summary>
        /// <param name="operation">需要等待、观察异常或触发的被测操作。</param>
        /// <returns>正常结束时返回 null、失败时返回原始异常的异步任务。</returns>
        private static async Task<Exception> CaptureError(UniTask operation)
        {
            // 捕获操作异常并返回给测试协程，便于精确断言失败类型。
            try
            {
                // 等待异步操作（operation）完成，保留其失败或取消结果。
                await operation;
                // 返回空引用，交给调用方继续配置或断言。
                return null;
            }
            catch (Exception exception)
            {
                // 返回捕获异常（exception），交给调用方继续配置或断言。
                return exception;
            }
        }

        /// <summary>等待测试操作完成并检查超时，避免未结束任务导致测试无限等待。</summary>
        /// <param name="operation">需要等待、观察异常或触发的被测操作。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private static IEnumerator WaitFor(Task operation)
        {
            // 保存等待截止时间（deadline）的基准值“Time.realtimeSinceStartup + 3f”，供后续比较或恢复。
            var deadline = Time.realtimeSinceStartup + 3f;
            // 在截止时间内持续观察任务状态，避免测试无限等待。
            while (!operation.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            // 确认异步操作（operation）的完成标志为真。
            Assert.IsTrue(operation.IsCompleted, "异步验证超时。");
            // 根据“operation.IsFaulted”选择对应测试路径。
            if (operation.IsFaulted) Assert.Fail(operation.Exception.ToString());
        }
    }
}
