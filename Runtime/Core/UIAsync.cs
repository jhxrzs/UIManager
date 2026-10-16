using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;

namespace JH
{
    /// <summary>UI 生命周期的任务收尾规则：观察全部已启动行为后再传播失败。</summary>
    internal static class UIAsync
    {
        /// <summary>观察全部已启动任务，待全部结束后按规则传播取消或异常。</summary>
        /// <param name="first">第一个需要观察完成的异步任务。</param>
        /// <param name="second">第二个需要观察完成的异步任务。</param>
        /// <returns>全部任务收尾后完成、取消或失败的任务。</returns>
        internal static UniTask WaitAll(UniTask first, UniTask second)
        {
            // 将两个任务交给统一的完整收尾逻辑。
            return WaitAll(new[] { first, second });
        }

        /// <summary>观察全部已启动任务，待全部结束后按规则传播取消或异常。</summary>
        /// <param name="tasks">本轮已经启动、必须全部观察结束的任务集合。</param>
        /// <returns>全部任务收尾后完成、取消或失败的任务。</returns>
        internal static async UniTask WaitAll(IReadOnlyList<UniTask> tasks)
        {
            // UniTask.WhenAll 在首个异常时结束；先捕获各任务结果以保证全部行为收尾。
            var observations = new UniTask<Exception>[tasks.Count];
            // 为每个任务创建只返回异常结果的观察任务。
            for (var i = 0; i < tasks.Count; i++) observations[i] = Observe(tasks[i]);
            // 等待所有观察任务完成，避免首个失败提前结束等待。
            var results = await UniTask.WhenAll(observations);

            // 仅在确实发生异常时创建异常收集列表。
            List<Exception> exceptions = null;
            // 区分真实失败与全部任务仅被取消的情况。
            var hasFailure = false;
            // 按原任务顺序检查每个观察结果。
            foreach (var exception in results)
            {
                // 成功任务没有需要传播的异常。
                if (exception == null) continue;
                // 首次遇到失败或取消时分配异常列表。
                exceptions ??= new List<Exception>();
                // 保存当前任务报告的原始异常。
                exceptions.Add(exception);
                // 记录是否存在取消以外的真实执行失败。
                if (!(exception is OperationCanceledException)) hasFailure = true;
            }

            // 全部任务成功时正常完成等待。
            if (exceptions == null) return;
            // 存在真实失败且有多个异常时完整保留各个错误。
            if (hasFailure && exceptions.Count > 1) throw new AggregateException(exceptions);
            // 单个失败或全部取消时按首个异常的原始堆栈传播。
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        /// <summary>将单个任务的异常转换为结果，避免提前中断其余行为的收尾。</summary>
        /// <param name="task">需要等待或观察的异步任务。</param>
        /// <returns>任务成功时为空，否则为该任务产生的异常。</returns>
        private static async UniTask<Exception> Observe(UniTask task)
        {
            // 将任务异常转为返回值，使外层能够等待所有任务。
            try
            {
                // 观察该任务直至成功、取消或失败。
                await task;
                // 成功任务以空异常表示。
                return null;
            }
            catch (Exception exception)
            {
                // 将任务失败或取消作为观察结果返回。
                return exception;
            }
        }
    }
}
