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
        /// <param name="from">过渡开始时传入回调的数值。</param>
        /// <param name="to">过渡正常完成时传入回调的终值。</param>
        /// <param name="apply">在主线程应用当前插值结果的回调。</param>
        /// <param name="duration">过渡持续秒数；小于等于零时立即应用终值。</param>
        /// <param name="cancellationToken">取消本次过渡且不补写终值的令牌。</param>
        /// <param name="ease">把线性时间进度转换为插值权重的缓动曲线。</param>
        /// <returns>过渡完成、取消或回调失败时结束的异步任务。</returns>
        /// <exception cref="ArgumentNullException">应用回调为空时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">时长为非数值或无穷大时抛出。</exception>
        internal static async UniTask RunAsync(float from, float to, Action<float> apply, float duration,
            CancellationToken cancellationToken, Ease ease)
        {
            // 提前拒绝缺失回调和无法形成有限时间轴的时长。
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            // 非有限时长无法确定终点到达时间，必须明确拒绝。
            if (float.IsNaN(duration) || float.IsInfinity(duration))
                // 报告无效时长参数，避免进入永不完成的过渡。
                throw new ArgumentOutOfRangeException(nameof(duration));

            // 切换前后都检查取消，避免已取消的操作接触 Unity 对象。
            cancellationToken.ThrowIfCancellationRequested();
            // 确保后续读取时间与应用 Unity 对象状态发生在主线程。
            await UniTask.SwitchToMainThread(cancellationToken);
            // 切换线程等待期间可能取消，再次检查后才应用进度。
            cancellationToken.ThrowIfCancellationRequested();

            // 零或负时长直接应用终值，不额外等待一帧。
            if (duration <= 0)
            {
                // 无需等待的过渡精确写入目标值。
                apply(to);
                // 立即完成路径不再进入逐帧时间循环。
                return;
            }

            // 使用双精度非缩放时间记录起点，避免暂停及长时间运行影响进度。
            var startedAt = Time.unscaledTimeAsDouble;
            // 持续推进动画，直到经过时长达到目标或令牌取消。
            while (true)
            {
                // 每帧先检查取消，再按真实经过时间决定是否到达终点。
                cancellationToken.ThrowIfCancellationRequested();
                // 将非缩放时间差换算为本轮线性进度。
                var progress = (float)((Time.unscaledTimeAsDouble - startedAt) / duration);
                // 达到结束时间后退出循环，由统一收尾精确写入终值。
                if (progress >= 1f) break;

                // 不钳制缓动权重，使 Back 和 Elastic 等曲线保留预期过冲。
                apply(Mathf.LerpUnclamped(from, to, UIEasing.Evaluate(ease, progress)));
                // NextFrame 保证每帧最多推进一次；Yield 在部分调用时机可能于同帧恢复。
                await UniTask.NextFrame(cancellationToken: cancellationToken);
            }

            // 仅正常完成才精确写入终值；结束前到来的取消仍然生效。
            cancellationToken.ThrowIfCancellationRequested();
            // 正常完成时直接应用目标值，消除末帧浮点误差。
            apply(to);
        }
    }
}
