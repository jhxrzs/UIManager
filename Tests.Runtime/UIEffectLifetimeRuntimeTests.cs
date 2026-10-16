using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的动效移除与异步任务生命周期。</summary>
    public partial class Test_UIBase
    {
        /// <summary>验证 Rect 显示动画中移除动效仅结束该动效，不提前完成其他行为。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_DestroyDuringRectShow_ShouldOnlyFinishRemovedEffect() =>
            VerifyRemovedEffect(typeof(RectEffectController), true);

        /// <summary>验证 Rect 隐藏动画中移除动效仅结束该动效，不提前完成其他行为。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_DestroyDuringRectHide_ShouldOnlyFinishRemovedEffect() =>
            VerifyRemovedEffect(typeof(RectEffectController), false);

        /// <summary>验证透明度显示动画中销毁动效后停止写入目标组件。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_DestroyDuringAlphaShow_ShouldStopWriting() =>
            VerifyRemovedEffect(typeof(AlphaEffectController), true);

        /// <summary>验证颜色隐藏动画中销毁动效后停止写入 Graphic。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_DestroyDuringColorHide_ShouldStopWriting() =>
            VerifyRemovedEffect(typeof(ColorEffectController), false);

        /// <summary>验证显示动画中移除动画控制器后停止对片段采样。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_DestroyDuringAnimationShow_ShouldStopSampling() =>
            VerifyRemovedEffect(typeof(AnimController), true);

        /// <summary>验证指定动效被移除后停止修改目标，同时界面继续等待其余行为。</summary>
        /// <param name="type">需要创建、查找字段或验证的组件类型。</param>
        /// <param name="showing">是否验证显示方向；为 false 时验证隐藏方向。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyRemovedEffect(Type type, bool showing)
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("EffectLifetime", out var ui);
            // 添加并保存“graphic”，供后续生命周期与效果检查使用。
            var graphic = type == typeof(ColorEffectController) ? go.AddComponent<Image>() : null;
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = (EffectControllerBase)go.AddComponent(type);
            // 写入动效测试字段，模拟编辑器中的序列化配置。
            SetEffectField(effect, "showDuration", 60f);
            // 写入动效测试字段，模拟编辑器中的序列化配置。
            SetEffectField(effect, "hideDuration", 60f);
            // 根据“effect is RectEffectController rectEffect”选择对应测试路径。
            if (effect is RectEffectController rectEffect)
            {
                // 写入 Rect 动效的序列化测试配置。
                SetRectEffectField(rectEffect, "showTarget", RectShownPose);
                // 写入 Rect 动效的序列化测试配置。
                SetRectEffectField(rectEffect, "hideTarget", RectHiddenPose);
            }
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 初始化“clip”为空引用，作为后续回调的观测基准。
            AnimationClip clip = null;
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 根据“type == typeof(AnimController)”选择对应测试路径。
                if (type == typeof(AnimController))
                {
                    // 将“clip”设为“new AnimationClip { legacy = true }”，建立当前测试条件。
                    clip = new AnimationClip { legacy = true };
                    // 配置动画片段的位置曲线，使采样结果能够由数值直接验证。
                    clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 20));
                    // 获取“animation”的实际组件引用，供表现与生命周期断言使用。
                    var animation = go.GetComponent<Animation>();
                    // 将测试动画片段注册到 Animation，供控制器按实际片段采样。
                    animation.AddClip(clip, "LifetimeClip");
                    // 将“animation.clip”设为“clip”，建立当前测试条件。
                    animation.clip = clip;
                }
                // 提交界面（ui）的显示请求，触发实际生命周期。
                ui.Show();
                // 根据显示分支或操作（showing）选择对应测试路径。
                if (showing) ui.Hide();
                // 为“if (showing) ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
                if (showing) ui.ShowAsyncActions += _ => pending.Task;
                // 为“else ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
                else ui.HideAsyncActions += _ => pending.Task;
                // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
                var operation = CaptureException(showing
                    ? ui.ShowAsync(CancellationToken.None) : ui.HideAsync(CancellationToken.None));
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 保存“pose”的基准值“EffectSerializedRectTransform.GetInstance((RectTransform)go.transform)”，供后续比较或恢复。
                var pose = EffectSerializedRectTransform.GetInstance((RectTransform)go.transform);
                // 获取“alpha”的实际组件引用，供表现与生命周期断言使用。
                var alpha = go.GetComponent<CanvasGroup>().alpha;
                // 保存“color”的基准值“graphic ? graphic.color : Color.white”，供后续比较或恢复。
                var color = graphic ? graphic.color : Color.white;
                // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
                Object.DestroyImmediate(effect);
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;

                // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                AssertRectPose((RectTransform)go.transform, pose);
                // 确认“go.GetComponent<CanvasGroup>()”的透明度等于“alpha”。
                Assert.AreEqual(alpha, go.GetComponent<CanvasGroup>().alpha);
                // 根据“graphic”选择对应测试路径。
                if (graphic) Assert.AreEqual(color, graphic.color);
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted, "移除一个动效不能提前完成其他仍在等待的行为。");
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                pending.TrySetResult();
                // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
                yield return WaitForTask(operation);
                // 确认异步操作（operation）的异步结果为空。
                Assert.IsNull(operation.Result, "单独移除动效不得让面板操作失败或取消。");
                // 确认界面（ui）的界面状态等于“showing ? UIState.Showed : UIState.Hided”。
                Assert.AreEqual(showing ? UIState.Showed : UIState.Hided, ui.State);
            }
            finally
            {
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                pending.TrySetResult();
                // 根据动效组件（effect）选择对应测试路径。
                if (effect) Object.DestroyImmediate(effect);
                // 根据“clip”选择对应测试路径。
                if (clip) Object.DestroyImmediate(clip);
            }
        }

        /// <summary>验证移除动效与调用方取消同时发生时保留界面取消语义。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_DestroyAndCallerCancel_ShouldStillCancelPanel()
        {
            // 逐项检查或处理“var cancelFirst in new[] { false, true }”，覆盖集合中的每个对象。
            foreach (var cancelFirst in new[] { false, true })
            {
                // 创建动效组件（effect）并配置测试所需的层级或表现组件。
                var effect = CreateRectEffect(out var ui);
                // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
                using var cancellation = new CancellationTokenSource();
                // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
                var operation = CaptureException(ui.ShowAsync(cancellation.Token));
                // 根据“cancelFirst”选择对应测试路径。
                if (cancelFirst) cancellation.Cancel();
                // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
                Object.DestroyImmediate(effect);
                // 根据“!cancelFirst”选择对应测试路径。
                if (!cancelFirst) cancellation.Cancel();
                // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
                yield return WaitForTask(operation);
                // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
                Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
                // 确认界面（ui）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, ui.State);
            }
        }

        /// <summary>验证销毁整个面板仍取消正在执行的异步请求。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_DestroyEntirePanel_ShouldStillCancelOperation()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out var ui);
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(ui.ShowAsync(CancellationToken.None));
            // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
            Object.DestroyImmediate(effect.gameObject);
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
        }

        /// <summary>验证委托快照中尚未调用的动效被销毁后安全跳过。</summary>
        /// <param name="asynchronous">是否通过异步显隐接口驱动被测流程。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Effect_DestroyedBeforeItsSnapshotCallback_ShouldBeSkipped(bool asynchronous)
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("SnapshotEffect", out var ui);
            // 初始化动效组件（effect）为空引用，作为后续回调的观测基准。
            EffectControllerBase effect = null;
            // 在“ui.ShowActions”中销毁目标，检验同轮后续回调是否安全退出。
            ui.ShowActions += () => Object.DestroyImmediate(effect);
            // 为“ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowAsyncActions += _ =>
            {
                // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
                Object.DestroyImmediate(effect);
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            effect = go.AddComponent<RectEffectController>();
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 根据“asynchronous”选择对应测试路径。
            if (asynchronous)
            {
                // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
                var operation = CaptureException(ui.ShowAsync(CancellationToken.None));
                // 确认异步操作（operation）的完成标志为真。
                Assert.IsTrue(operation.IsCompleted);
                // 确认异步操作（operation）的异步结果为空。
                Assert.IsNull(operation.Result);
            }
            // 覆盖条件不成立时的另一条配置或清理路径。
            else ui.Show();
            // 确认界面（ui）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, ui.State);
        }
    }
}
