using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;

namespace JH
{
    /// <summary>UI 生命周期的任务收尾规则：观察全部已启动行为后再传播失败。</summary>
    internal static class UIAsync
    {
        internal static UniTask WaitAll(UniTask first, UniTask second)
        {
            return WaitAll(new[] { first, second });
        }

        internal static async UniTask WaitAll(IReadOnlyList<UniTask> tasks)
        {
            // UniTask.WhenAll 在首个异常时结束；先捕获各任务结果以保证全部行为收尾。
            var observations = new UniTask<Exception>[tasks.Count];
            for (var i = 0; i < tasks.Count; i++) observations[i] = Observe(tasks[i]);
            var results = await UniTask.WhenAll(observations);

            List<Exception> exceptions = null;
            var hasFailure = false;
            foreach (var exception in results)
            {
                if (exception == null) continue;
                exceptions ??= new List<Exception>();
                exceptions.Add(exception);
                if (!(exception is OperationCanceledException)) hasFailure = true;
            }

            if (exceptions == null) return;
            if (hasFailure && exceptions.Count > 1) throw new AggregateException(exceptions);
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        private static async UniTask<Exception> Observe(UniTask task)
        {
            try
            {
                await task;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }
    }
}
