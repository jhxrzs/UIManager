using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的旋转圈数与过冲连续性。</summary>
    public partial class Test_UIBase
    {
        /// <summary>创建带多圈显隐旋转目标的 Rect 动效，输出其 UI 宿主。</summary>
        /// <param name="shownAngle">显示终态配置的旋转角度，保留符号和圈数。</param>
        /// <param name="hiddenAngle">隐藏终态配置的旋转角度，保留符号和圈数。</param>
        /// <param name="ui">当前测试所操作或输出的 UIBase 宿主。</param>
        /// <returns>配置了指定旋转端点的 Rect 动效。</returns>
        private RectEffectController CreateRotationEffect(float shownAngle, float hiddenAngle, out UIBase ui)
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out ui);
            // 保存“shown”的基准值“RectShownPose”，供后续比较或恢复。
            var shown = RectShownPose;
            // 保存“hidden”的基准值“RectHiddenPose”，供后续比较或恢复。
            var hidden = RectHiddenPose;
            // 将“shown.angle”设为“new Vector3(0f, 0f, shownAngle)”，建立当前测试条件。
            shown.angle = new Vector3(0f, 0f, shownAngle);
            // 将“hidden.angle”设为“new Vector3(0f, 0f, hiddenAngle)”，建立当前测试条件。
            hidden.angle = new Vector3(0f, 0f, hiddenAngle);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showTarget", shown);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideTarget", hidden);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showEase", Ease.Linear);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideEase", Ease.Linear);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 返回动效组件（effect），交给调用方继续配置或断言。
            return effect;
        }

        /// <summary>验证动效保留的有符号旋转角度和圈数符合预期。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <param name="expected">本轮需要满足的基准结果。</param>
        private static void AssertRotationAngle(RectEffectController effect, float expected)
        {
            // 确认“Quaternion.Angle(Quaternion.Euler(0f, 0f, expected), effect.transform.localRotation)”满足约束“Is.LessThan(0.05f)”。
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 0f, expected), effect.transform.localRotation),
                Is.LessThan(0.05f), "Rotation must follow the configured unwrapped angular path.");
        }

        /// <summary>验证负向过冲中反向播放时保留有符号旋转角度。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectRotation_NegativeOvershootReverse_ShouldKeepSignedAngle() =>
            VerifyRotationOvershootContinuation(0f, 90f, Ease.InBack, false);

        /// <summary>验证正向过冲中反向播放时保留超过一圈的角度。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectRotation_PositiveOvershootReverse_ShouldKeepAngleBeyondOneTurn() =>
            VerifyRotationOvershootContinuation(350f, 440f, Ease.OutBack, false);

        /// <summary>验证负向过冲取消后重试不丢失旋转方向。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectRotation_CanceledNegativeOvershootRetry_ShouldKeepSignedAngle() =>
            VerifyRotationOvershootContinuation(0f, 90f, Ease.InBack, true);

        /// <summary>验证正向过冲取消后重试不丢失额外旋转圈数。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectRotation_CanceledPositiveOvershootRetry_ShouldKeepAngleBeyondOneTurn() =>
            VerifyRotationOvershootContinuation(350f, 440f, Ease.OutBack, true);

        /// <summary>验证过冲后的反向或重试保留实际角度并沿配置方向继续。</summary>
        /// <param name="shownAngle">显示终态配置的旋转角度，保留符号和圈数。</param>
        /// <param name="hiddenAngle">隐藏终态配置的旋转角度，保留符号和圈数。</param>
        /// <param name="curve">本轮旋转过渡使用的缓动曲线。</param>
        /// <param name="retryHide">是否取消后重试隐藏；为 false 时改为反向显示。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyRotationOvershootContinuation(
            float shownAngle, float hiddenAngle, Ease curve, bool retryHide)
        {
            // 逐项检查或处理“var useGlobal in new[] { false, true }”，覆盖集合中的每个对象。
            foreach (var useGlobal in new[] { false, true })
            {
                // 创建动效组件（effect）并配置测试所需的层级或表现组件。
                var effect = CreateRotationEffect(shownAngle, hiddenAngle, out var ui);
                // 写入 Rect 动效的序列化测试配置。
                SetRectEffectField(effect, "hideEase", useGlobal ? curve : Ease.NotSet);
                // 写入 Rect 动效的序列化测试配置。
                SetRectEffectField(effect, "hideAngleEase", curve);
                // 创建“initialCancellation”，控制本次显隐的主动取消时机。
                using var initialCancellation = new CancellationTokenSource();
                // 启动并保存“initial”，随后检查等待、取消与完成结果。
                var initial = CaptureException(ui.HideAsync(initialCancellation.Token));
                // 保存播放进度（progress）的基准值“curve == Ease.InBack ? 0.25f : 0.7f”，供后续比较或恢复。
                var progress = curve == Ease.InBack ? 0.25f : 0.7f;
                // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                SampleRectProgress(effect, progress);
                // 保存“overshotAngle”的基准值“Mathf.LerpUnclamped(shownAngle, hiddenAngle, UIEasing.Evaluate(curve, progress))”，供后续比较或恢复。
                var overshotAngle = Mathf.LerpUnclamped(shownAngle, hiddenAngle, UIEasing.Evaluate(curve, progress));
                // 验证动效保留的有符号旋转角度和圈数符合预期。
                AssertRotationAngle(effect, overshotAngle);
                // 根据“retryHide”选择对应测试路径。
                if (retryHide)
                {
                    // 取消本轮操作，验证旧任务停止提交成功状态。
                    initialCancellation.Cancel();
                    // 等待“WaitForTask(initial)”，再检查操作完成后的状态。
                    yield return WaitForTask(initial);
                    // 确认“initial”的异步结果保留取消语义，不被报告为成功。
                    Assert.That(initial.Result, Is.InstanceOf<OperationCanceledException>());
                    // 写入 Rect 动效的序列化测试配置。
                    SetRectEffectField(effect, "hideEase", Ease.Linear);
                }
                // 创建“continuationCancellation”，控制本次显隐的主动取消时机。
                using var continuationCancellation = new CancellationTokenSource();
                // 启动并保存“continuation”，随后检查等待、取消与完成结果。
                var continuation = CaptureException(retryHide
                    ? ui.HideAsync(continuationCancellation.Token) : ui.ShowAsync(continuationCancellation.Token));
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try
                {
                    AssertRotationAngle(effect, overshotAngle); // 新过渡首帧必须保留实际朝向。
                    // 等待“WaitForTask(initial)”，再检查操作完成后的状态。
                    yield return WaitForTask(initial);
                    // 确认“initial”的异步结果保留取消语义，不被报告为成功。
                    Assert.That(initial.Result, Is.InstanceOf<OperationCanceledException>());
                    // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                    SampleRectProgress(effect, 0.5f);
                    // 验证动效保留的有符号旋转角度和圈数符合预期。
                    AssertRotationAngle(effect, Mathf.LerpUnclamped(
                        overshotAngle, retryHide ? hiddenAngle : shownAngle, 0.5f));
                    // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                    SampleRectProgress(effect, 1f);
                    // 验证动效保留的有符号旋转角度和圈数符合预期。
                    AssertRotationAngle(effect, retryHide ? hiddenAngle : shownAngle);
                }
                finally
                {
                    // 取消本轮操作，验证旧任务停止提交成功状态。
                    initialCancellation.Cancel();
                    // 取消本轮操作，验证旧任务停止提交成功状态。
                    continuationCancellation.Cancel();
                }
                // 等待“WaitForTask(continuation)”，再检查操作完成后的状态。
                yield return WaitForTask(continuation);
                // 确认“continuation”的异步结果保留取消语义，不被报告为成功。
                Assert.That(continuation.Result, Is.InstanceOf<OperationCanceledException>());
            }
        }

        /// <summary>验证多圈旋转中途反向仍使用配置的圈数与方向。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectRotation_MultiTurnMidflightReverse_ShouldKeepConfiguredWinding()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRotationEffect(0f, 720f, out var ui);
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            SampleRectProgress(effect, 0.75f); // Unity 会把 540 度读回为 180 度。
            // 验证动效保留的有符号旋转角度和圈数符合预期。
            AssertRotationAngle(effect, 540f);
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 启动并保存显示分支或操作（showing），随后检查等待、取消与完成结果。
            var showing = CaptureException(ui.ShowAsync(cancellation.Token));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
                yield return WaitForTask(hiding);
                // 确认隐藏分支或操作（hiding）的异步结果保留取消语义，不被报告为成功。
                Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
                // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                SampleRectProgress(effect, 0.5f);
                AssertRotationAngle(effect, 270f); // 保留配置的 540 到 0 度路径，不使用归一化后的 180 到 0 度。
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { cancellation.Cancel(); }
            // 等待“WaitForTask(showing)”，再检查操作完成后的状态。
            yield return WaitForTask(showing);
        }

        /// <summary>验证多圈旋转到稳定端点后再次播放不会被 Unity 角度归一化改变路径。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectRotation_MultiTurnStableEndpoint_ShouldKeepConfiguredWinding()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRotationEffect(0f, 720f, out var ui);
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 启动并保存显示分支或操作（showing），随后检查等待、取消与完成结果。
            var showing = CaptureException(ui.ShowAsync(cancellation.Token));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                SampleRectProgress(effect, 0.25f);
                AssertRotationAngle(effect, 540f); // 即使两端外观相同，也必须保留配置的两圈旋转。
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { cancellation.Cancel(); }
            // 等待“WaitForTask(showing)”，再检查操作完成后的状态。
            yield return WaitForTask(showing);
        }

        /// <summary>验证外部修改旋转后，新过渡以实际姿态为起点。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectRotation_ExternalRotationChange_ShouldUseActualPose()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRotationEffect(0f, 90f, out var ui);
            // 将“effect.transform.localRotation”设为“Quaternion.Euler(0f, 0f, 70f)”，建立当前测试条件。
            effect.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(cancellation.Token));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 验证动效保留的有符号旋转角度和圈数符合预期。
                AssertRotationAngle(effect, 70f);
                // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                SampleRectProgress(effect, 0.5f);
                // 验证动效保留的有符号旋转角度和圈数符合预期。
                AssertRotationAngle(effect, 80f);
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { cancellation.Cancel(); }
            // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
            yield return WaitForTask(hiding);
        }

        /// <summary>验证普通旋转目标继续按配置数值插值，不自动切换为最短路径。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectRotation_OrdinaryTargets_ShouldNotSilentlyBecomeShortestPath()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRotationEffect(350f, 10f, out var ui);
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(cancellation.Token));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                SampleRectProgress(effect, 0.5f);
                AssertRotationAngle(effect, 180f); // 配置的 350 到 10 度路径仍应旋转负 340 度。
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { cancellation.Cancel(); }
            // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
            yield return WaitForTask(hiding);
        }
    }
}
