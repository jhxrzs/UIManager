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
    /// <summary>验证 UIBase 的后添加动效的状态同步。</summary>
    public partial class Test_UIBase
    {
        /// <summary>验证后添加的动效同步已有隐藏终态，而不重播界面通知。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_LateAttachment_ShouldSynchronizeHiddenPresentation() =>
            VerifyLateAttachmentStableState(false);

        /// <summary>验证后添加的动效同步已有显示终态，而不重播界面通知。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_LateAttachment_ShouldSynchronizeShownPresentation() =>
            VerifyLateAttachmentStableState(true);

        /// <summary>验证各类后添加动效在 Start 时同步给定稳定状态且不改变 UI 生命周期。</summary>
        /// <param name="shown">宿主已经达到的稳定状态是否为显示。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyLateAttachmentStableState(bool shown)
        {
            // 逐项检查或处理“foreach (var type in new[] { typeof(AlphaEffectController), typeof(ColorEffectController),”，覆盖集合中的每个对象。
            foreach (var type in new[] { typeof(AlphaEffectController), typeof(ColorEffectController),
                         typeof(RectEffectController), typeof(AnimController) })
            {
                // 创建宿主物体（go）并配置测试所需的层级或表现组件。
                var go = CreateRuntimeEffectObject("Late" + type.Name, out var ui);
                // 添加并保存“graphic”，供后续生命周期与效果检查使用。
                var graphic = type == typeof(ColorEffectController) ? go.AddComponent<Image>() : null;
                // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
                go.SetActive(true);
                // 提交界面（ui）的显示请求，触发实际生命周期。
                ui.Show();
                // 根据“!shown”选择对应测试路径。
                if (!shown) ui.Hide();
                // 初始化状态变更次数（stateChanges）为0，作为后续回调的观测基准。
                var stateChanges = 0;
                // 初始化“afterCalls”为0，作为后续回调的观测基准。
                var afterCalls = 0;
                // 为“ui.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
                ui.OnStateChanged += (_, _) => stateChanges++;
                // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
                ui.AfterUIShowedEvent += () => afterCalls++;
                // 为“ui.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
                ui.AfterUIHidedEvent += () => afterCalls++;
                // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
                var effect = (EffectControllerBase)go.AddComponent(type);
                // 初始化“clip”为空引用，作为后续回调的观测基准。
                AnimationClip clip = null;
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try
                {
                    // 根据“effect is RectEffectController rect”选择对应测试路径。
                    if (effect is RectEffectController rect)
                    {
                        // 写入 Rect 动效的序列化测试配置。
                        SetRectEffectField(rect, "showTarget", RectShownPose);
                        // 写入 Rect 动效的序列化测试配置。
                        SetRectEffectField(rect, "hideTarget", RectHiddenPose);
                    }
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
                        animation.AddClip(clip, "LateClip");
                        // 将“animation.clip”设为“clip”，建立当前测试条件。
                        animation.clip = clip;
                        // 将“go.transform.localPosition”设为“new Vector3(7f, 0f, 0f)”，建立当前测试条件。
                        go.transform.localPosition = new Vector3(7f, 0f, 0f);
                    }
                    yield return null; // 等待真实 Start，在调用方配置和派生 Awake 完成后同步效果。
                    // 确认“(float)ReadEffectField(effect, "effectAmount")”符合“shown ? 0f : 1f”。
                    Assert.That((float)ReadEffectField(effect, "effectAmount"), Is.EqualTo(shown ? 0f : 1f));
                    // 根据“type == typeof(AlphaEffectController)”选择对应测试路径。
                    if (type == typeof(AlphaEffectController))
                        // 确认“go.GetComponent<CanvasGroup>()”的透明度符合“shown ? 1f : 0f”。
                        Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(shown ? 1f : 0f));
                    // 根据“graphic”选择对应测试路径。
                    if (graphic) Assert.That(graphic.color, Is.EqualTo(shown ? Color.white : Color.clear));
                    // 根据“effect is RectEffectController”选择对应测试路径。
                    if (effect is RectEffectController)
                        // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                        AssertRectPose((RectTransform)go.transform, shown ? RectShownPose : RectHiddenPose);
                    // 根据“type == typeof(AnimController)”选择对应测试路径。
                    if (type == typeof(AnimController))
                        // 确认“go.transform.localPosition.x”符合“shown ? 20f : 0f”，允许指定的浮点误差。
                        Assert.That(go.transform.localPosition.x, Is.EqualTo(shown ? 20f : 0f).Within(0.001f));
                    // 确认界面（ui）的界面状态等于“shown ? UIState.Showed : UIState.Hided”。
                    Assert.AreEqual(shown ? UIState.Showed : UIState.Hided, ui.State);
                    // 确认状态变更次数（stateChanges）等于0。
                    Assert.AreEqual(0, stateChanges, "Only the newly attached effect may be synchronized.");
                    // 确认“afterCalls”等于0。
                    Assert.AreEqual(0, afterCalls, "Attaching an effect must not replay UI notifications.");
                }
                finally
                {
                    // 根据动效组件（effect）选择对应测试路径。
                    if (effect) Object.DestroyImmediate(effect);
                    // 根据“clip”选择对应测试路径。
                    if (clip) Object.DestroyImmediate(clip);
                }
            }
        }

        /// <summary>验证过渡中添加的动效等待稳定状态，不把当前动画跳到端点。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_LateAttachment_DuringTransition_ShouldWaitForStableState()
        {
            // 逐项检查或处理“var showing in new[] { false, true }”，覆盖集合中的每个对象。
            foreach (var showing in new[] { false, true })
            {
                // 创建宿主物体（go）并配置测试所需的层级或表现组件。
                var go = CreateRuntimeEffectObject("AttachDuringTransition", out var ui);
                // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
                go.SetActive(true);
                // 提交界面（ui）的显示请求，触发实际生命周期。
                ui.Show();
                // 根据显示分支或操作（showing）选择对应测试路径。
                if (showing) ui.Hide();
                // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
                var pending = new UniTaskCompletionSource();
                // 为“if (showing) ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
                if (showing) ui.ShowAsyncActions += _ => pending.Task;
                // 为“else ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
                else ui.HideAsyncActions += _ => pending.Task;
                // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
                var operation = CaptureException(showing
                    ? ui.ShowAsync(CancellationToken.None) : ui.HideAsync(CancellationToken.None));
                // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
                var effect = go.AddComponent<AlphaEffectController>();
                // 获取效果所属组（group）的实际组件引用，供表现与生命周期断言使用。
                var group = go.GetComponent<CanvasGroup>();
                // 将效果所属组（group）的透明度设为0.42f，建立当前测试条件。
                group.alpha = 0.42f;
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try
                {
                    // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                    yield return null;
                    // 确认异步操作（operation）的完成标志为假。
                    Assert.IsFalse(operation.IsCompleted);
                    // 确认效果所属组（group）的透明度等于0.42f。
                    Assert.AreEqual(0.42f, group.alpha, "Late attachment must not snap an existing transition.");
                    // 完成手动控制的挂起任务，让已启动行为能够收口。
                    pending.TrySetResult();
                    // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
                    yield return WaitForTask(operation);
                    // 确认异步操作（operation）的异步结果为空。
                    Assert.IsNull(operation.Result);
                    // 确认效果所属组（group）的透明度等于“showing ? 1f : 0f”。
                    Assert.AreEqual(showing ? 1f : 0f, group.alpha);
                }
                // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
                finally { pending.TrySetResult(); }
            }
        }

        /// <summary>验证动效 Start 前已启动的正常过渡不会被首次状态同步覆盖。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_LateAttachment_NormalActionBeforeStart_ShouldKeepItsTransition()
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("ActionBeforeEffectStart", out var ui);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = go.AddComponent<AlphaEffectController>();
            // 写入动效测试字段，模拟编辑器中的序列化配置。
            SetEffectField(effect, "hideDuration", 60f);
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(ui.HideAsync(cancellation.Token));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted);
                // 确认“go.GetComponent<CanvasGroup>()”的透明度大于0.5f。
                Assert.Greater(go.GetComponent<CanvasGroup>().alpha, 0.5f,
                    "First Start must not replace the effect's already running transition with its endpoint.");
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { cancellation.Cancel(); }
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
        }

        /// <summary>验证动效早于 UIBase 初始化时等待正常初始行为，而不提前应用效果。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_StartBeforeUIInitialization_ShouldWaitForNormalInitialAction()
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("EffectBeforeUIStart", out var ui);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = go.AddComponent<LateAttachmentAwakeProbe>();
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            yield return null; // UI 组件仍禁用，尚未初始化序列化状态。
            // 确认界面（ui）的初始化完成标志为假。
            Assert.IsFalse(ui.IsUIStateInitialized);
            // 确认动效组件（effect）的显隐行为次数等于0。
            Assert.AreEqual(0, effect.ActionCount);
            // 将界面（ui）的组件启用状态设为true，建立当前测试条件。
            ui.enabled = true;
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
            // 确认动效组件（effect）的显隐行为次数等于1。
            Assert.AreEqual(1, effect.ActionCount, "UI initial Start must apply its action exactly once.");
            // 确认“effect.AppliedBeforeAwakeFinished”为假。
            Assert.IsFalse(effect.AppliedBeforeAwakeFinished);
        }

        /// <summary>验证后添加动效必须等待派生类 Awake 完成后才应用稳定状态。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_LateAttachment_ShouldWaitUntilDerivedAwakeFinished()
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("LateAwakeProbe", out var ui);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show(); ui.Hide();
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = go.AddComponent<LateAttachmentAwakeProbe>();
            // 确认动效组件（effect）的显隐行为次数等于0。
            Assert.AreEqual(0, effect.ActionCount);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 确认动效组件（effect）的显隐行为次数等于1。
            Assert.AreEqual(1, effect.ActionCount);
            // 确认“effect.AppliedBeforeAwakeFinished”为假。
            Assert.IsFalse(effect.AppliedBeforeAwakeFinished);
        }
    }

}
