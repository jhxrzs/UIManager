using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace JH
{
    /// <summary>仅供 UI 效果控制器使用的进度过渡，不公开通用 Tween 接口。</summary>
    internal static class UITransition
    {
        /// <summary>使用真实时间推进 UI 进度，游戏暂停时仍可完成显隐。</summary>
        internal static async UniTask RunAsync(float from, float to, Action<float> apply, float duration,
            CancellationToken cancellationToken, Ease ease)
        {
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            if (float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration));

            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.SwitchToMainThread(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (duration <= 0)
            {
                apply(to);
                return;
            }

            var startedAt = Time.unscaledTimeAsDouble;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var progress = (float)((Time.unscaledTimeAsDouble - startedAt) / duration);
                if (progress >= 1f) break;

                apply(Mathf.LerpUnclamped(from, to, UIEasing.Evaluate(ease, progress)));
                // NextFrame 保证每帧最多推进一次；Yield 在部分调用时机可能于同帧恢复。
                await UniTask.NextFrame(cancellationToken: cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            apply(to);
        }
    }
}
