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
        [UnityTest]
        public IEnumerator WaitAll_Failure_ShouldObservePendingTaskBeforeReturning()
        {
            var pending = new UniTaskCompletionSource();
            var failure = new InvalidOperationException("first handler failed");
            var operation = CaptureError(UIAsync.WaitAll(UniTask.FromException(failure), pending.Task));

            yield return null;
            Assert.IsFalse(operation.IsCompleted, "一个处理器失败后仍须等待已启动的其他处理器。");
            pending.TrySetResult();
            yield return WaitFor(operation);
            Assert.AreSame(failure, operation.Result);
        }

        [UnityTest]
        public IEnumerator WaitAll_MultipleFailures_ShouldReportEveryFailure()
        {
            var pending = new UniTaskCompletionSource();
            var first = new InvalidOperationException("first failure");
            var second = new ArgumentException("second failure");
            var operation = CaptureError(UIAsync.WaitAll(UniTask.FromException(first), pending.Task));

            Assert.IsFalse(operation.IsCompleted);
            pending.TrySetException(second);
            yield return WaitFor(operation);
            Assert.That(operation.Result, Is.TypeOf<AggregateException>());
            CollectionAssert.AreEquivalent(new Exception[] { first, second },
                ((AggregateException)operation.Result).InnerExceptions);
        }

        [UnityTest]
        public IEnumerator WaitAll_AllCanceled_ShouldPropagateCancellationWithoutAggregation()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            var operation = CaptureError(UIAsync.WaitAll(
                UniTask.FromCanceled(source.Token), UniTask.FromCanceled(source.Token)));
            yield return WaitFor(operation);
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(source.Token, ((OperationCanceledException)operation.Result).CancellationToken);
        }

        [UnityTest]
        public IEnumerator Transition_PreCanceledInstantDuration_ShouldNotWriteValue()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            var writes = 0;
            var operation = CaptureError(UITransition.RunAsync(0, 1, _ => writes++, 0,
                source.Token, Ease.Linear));
            yield return WaitFor(operation);
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(0, writes);
        }

        [UnityTest]
        public IEnumerator Transition_PausedGame_ShouldFinishUsingUnscaledTime()
        {
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                var value = -1f;
                var lastFrame = -1;
                var duplicateWriteInFrame = false;
                var operation = CaptureError(UITransition.RunAsync(0, 1, next =>
                {
                    duplicateWriteInFrame |= lastFrame == Time.frameCount;
                    lastFrame = Time.frameCount;
                    value = next;
                }, 0.05f, CancellationToken.None, Ease.Linear));

                yield return WaitFor(operation);
                Assert.IsNull(operation.Result);
                Assert.AreEqual(1f, value);
                Assert.IsFalse(duplicateWriteInFrame, "每帧最多写入一次进度。");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator Transition_CanceledFromSetter_ShouldNotWriteTerminalValue()
        {
            using var source = new CancellationTokenSource();
            var writes = 0;
            var value = -1f;
            var operation = CaptureError(UITransition.RunAsync(0, 1, next =>
            {
                value = next;
                writes++;
                source.Cancel();
            }, 1f, source.Token, Ease.Linear));

            yield return WaitFor(operation);
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(1, writes);
            Assert.AreEqual(0f, value);
        }

        [Test]
        public void Easing_AllCurves_ShouldHaveFiniteValuesAndCorrectEndpoints()
        {
            foreach (var ease in Enum.GetValues(typeof(Ease)).Cast<Ease>())
            {
                Assert.That(UIEasing.Evaluate(ease, 0), Is.EqualTo(0f).Within(0.00001f), ease.ToString());
                Assert.That(UIEasing.Evaluate(ease, 1), Is.EqualTo(1f).Within(0.00001f), ease.ToString());
                for (var sample = 0; sample <= 100; sample++)
                {
                    var value = UIEasing.Evaluate(ease, sample / 100f);
                    Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value), ease.ToString());
                }
            }
        }

        private static async Task<Exception> CaptureError(UniTask operation)
        {
            try
            {
                await operation;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static IEnumerator WaitFor(Task operation)
        {
            var deadline = Time.realtimeSinceStartup + 3f;
            while (!operation.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(operation.IsCompleted, "异步验证超时。");
            if (operation.IsFaulted) Assert.Fail(operation.Exception.ToString());
        }
    }
}
