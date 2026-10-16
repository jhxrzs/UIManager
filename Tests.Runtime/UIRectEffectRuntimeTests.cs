using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的Rect 分项曲线、过冲与播放。</summary>
    public partial class Test_UIBase
    {
        /// <summary>Rect 动效的显示端点基准姿态。</summary>
        private static readonly EffectSerializedRectTransform RectShownPose = new()
        {
            anchorPos = Vector2.zero, angle = Vector3.zero, scale = Vector3.one,
            sizeDelta = new Vector2(100, 200), pivot = new Vector2(0.25f, 0.25f)
        };

        /// <summary>Rect 动效的隐藏端点基准姿态。</summary>
        private static readonly EffectSerializedRectTransform RectHiddenPose = new()
        {
            anchorPos = new Vector2(100, 200), angle = new Vector3(0, 0, 100),
            scale = new Vector3(3, 5, 1), sizeDelta = new Vector2(300, 600),
            pivot = new Vector2(0.75f, 0.75f)
        };

        /// <summary>创建配置了显隐目标姿态的 Rect 动效及其 UI 宿主。</summary>
        /// <param name="ui">当前测试所操作或输出的 UIBase 宿主。</param>
        /// <returns>已经配置显隐姿态的 Rect 动效组件。</returns>
        private RectEffectController CreateRectEffect(out UIBase ui)
        {
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject("RectEffect", typeof(RectTransform));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(go);
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            go.transform.SetParent(CreateObject("RectEffectContainer").transform, false);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(false);
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            ui = go.AddComponent<UIBase>();
            // 将界面（ui）的组件启用状态设为false，建立当前测试条件。
            ui.enabled = false;
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = go.AddComponent<RectEffectController>();
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showTarget", RectShownPose);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideTarget", RectHiddenPose);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "effectAmount", 1f);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showDuration", 10f);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideDuration", 10f);
            // 模拟预制体经过编辑器配置后再进入 Awake，不依赖运行时补查缓存。
            ValidateRectEffect(effect);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 返回动效组件（effect），交给调用方继续配置或断言。
            return effect;
        }

        /// <summary>写入 Rect 动效的序列化测试配置。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <param name="value">写入配置或用于断言的测试值。</param>
        private static void SetRectEffectField(RectEffectController effect, string name, object value)
        {
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var type = typeof(RectEffectController); type != null; type = type.BaseType)
            {
                // 取得“field”的反射入口，仅在测试内访问非公开配置。
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                // 根据“field == null”选择对应测试路径。
                if (field == null) continue;
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                field.SetValue(effect, value);
                // 结束当前辅助流程，避免继续处理已完成分支。
                return;
            }
            // 若未找到必要测试配置则立即失败，避免在无效前提下继续验证。
            Assert.Fail("Missing effect configuration: " + name);
        }

        /// <summary>调用 Rect 动效的编辑器验证入口，检查配置保留与预览行为。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        private static void ValidateRectEffect(RectEffectController effect) =>
            typeof(RectEffectController).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effect, null);

        /// <summary>按指定效果量采样 Rect 姿态，用于验证端点及过冲连续性。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <param name="amount">需要采样的动效效果量。</param>
        private static void SampleRectAmount(RectEffectController effect, float amount) =>
            typeof(EffectControllerBase).GetProperty("EffectAmount", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(effect, amount);

        /// <summary>按指定动画进度采样 Rect 姿态，用于比较缓动曲线。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <param name="progress">需要采样的归一化播放进度。</param>
        private static void SampleRectProgress(RectEffectController effect, float progress) =>
            typeof(RectEffectController).GetMethod("ApplyTransitionProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effect, new object[] { progress });

        /// <summary>读取 Rect 动效当前效果量，比较切换或重试前后的过渡起点。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <returns>当前 Rect 动效保存的效果量。</returns>
        private static float ReadRectAmount(RectEffectController effect) =>
            (float)typeof(EffectControllerBase).GetProperty("EffectAmount",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect);

        /// <summary>为五项 Rect 属性配置指定方向的独立缓动曲线。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <param name="showing">是否验证显示方向；为 false 时验证隐藏方向。</param>
        private static void ConfigureRectParts(RectEffectController effect, bool showing)
        {
            // 保存“prefix”的基准值“showing ? "show" : "hide"”，供后续比较或恢复。
            var prefix = showing ? "show" : "hide";
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, prefix + "AnchorPosEase", Ease.InQuad);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, prefix + "AngleEase", Ease.OutQuad);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, prefix + "ScaleEase", Ease.InCubic);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, prefix + "SizeDeltaEase", Ease.OutCubic);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, prefix + "PivotEase", Ease.Linear);
        }

        /// <summary>验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。</summary>
        /// <param name="rect">需要验证实际姿态的 RectTransform。</param>
        /// <param name="expected">本轮需要满足的基准结果。</param>
        private static void AssertRectPose(RectTransform rect, EffectSerializedRectTransform expected)
        {
            // 确认“Vector2.Distance(expected.anchorPos, rect.anchoredPosition)”满足约束“Is.LessThan(0.001f)”。
            Assert.That(Vector2.Distance(expected.anchorPos, rect.anchoredPosition), Is.LessThan(0.001f), "position");
            // 确认“Quaternion.Angle(Quaternion.Euler(expected.angle), rect.localRotation)”满足约束“Is.LessThan(0.01f)”。
            Assert.That(Quaternion.Angle(Quaternion.Euler(expected.angle), rect.localRotation), Is.LessThan(0.01f), "rotation");
            // 确认“Vector3.Distance(expected.scale, rect.localScale)”满足约束“Is.LessThan(0.001f)”。
            Assert.That(Vector3.Distance(expected.scale, rect.localScale), Is.LessThan(0.001f), "scale");
            // 确认“Vector2.Distance(expected.sizeDelta, rect.sizeDelta)”满足约束“Is.LessThan(0.001f)”。
            Assert.That(Vector2.Distance(expected.sizeDelta, rect.sizeDelta), Is.LessThan(0.001f), "size");
            // 确认“Vector2.Distance(expected.pivot, rect.pivot)”满足约束“Is.LessThan(0.001f)”。
            Assert.That(Vector2.Distance(expected.pivot, rect.pivot), Is.LessThan(0.001f), "pivot");
        }

        /// <summary>验证隐藏过渡分别使用五项属性的隐藏缓动曲线。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_Hide_ShouldUseAllFiveHideCurves() => VerifyRectParts(false);

        /// <summary>验证显示过渡按播放方向使用五项属性的显示缓动曲线。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_Show_ShouldUseAllFiveShowCurvesInPlaybackDirection() => VerifyRectParts(true);

        /// <summary>逐项验证指定方向的分项曲线采样结果。</summary>
        /// <param name="showing">是否验证显示方向；为 false 时验证隐藏方向。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyRectParts(bool showing)
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out var ui);
            // 为五项 Rect 属性配置指定方向的独立缓动曲线。
            ConfigureRectParts(effect, showing);
            // 根据显示分支或操作（showing）选择对应测试路径。
            if (showing) ui.Hide();
            // 覆盖条件不成立时的另一条配置或清理路径。
            else ui.Show();
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(showing ? ui.ShowAsync(cancellation.Token) : ui.HideAsync(cancellation.Token));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted);
                // 采样真实运行中的控制器，直接设置精确进度，避免按帧计时导致半程断言波动。
                SampleRectProgress(effect, 0.5f);
                // 创建预期结果（expected），隔离本用例的资源或观测数据。
                var expected = new EffectSerializedRectTransform
                {
                    anchorPos = showing ? new Vector2(75, 150) : new Vector2(25, 50),
                    angle = new Vector3(0, 0, showing ? 25 : 75),
                    scale = showing ? new Vector3(2.75f, 4.5f, 1) : new Vector3(1.25f, 1.5f, 1),
                    sizeDelta = showing ? new Vector2(125, 250) : new Vector2(275, 550),
                    pivot = new Vector2(0.5f, 0.5f)
                };
                // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                AssertRectPose((RectTransform)effect.transform, expected);
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { cancellation.Cancel(); }
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
        }

        /// <summary>验证全局隐藏曲线覆盖分项曲线且只应用一次缓动。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_GlobalHideCurve_ShouldOverridePartsAndApplyOnlyOnce() => VerifyRectGlobal(false);

        /// <summary>验证全局显示曲线覆盖分项曲线且只应用一次缓动。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_GlobalShowCurve_ShouldOverridePartsAndApplyOnlyOnce() => VerifyRectGlobal(true);

        /// <summary>验证指定方向的全局曲线统一影响全部属性且不重复缓动。</summary>
        /// <param name="showing">是否验证显示方向；为 false 时验证隐藏方向。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyRectGlobal(bool showing)
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out var ui);
            // 为五项 Rect 属性配置指定方向的独立缓动曲线。
            ConfigureRectParts(effect, showing);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, showing ? "showEase" : "hideEase", Ease.InQuad);
            // 调用 Rect 动效的编辑器验证入口，检查配置保留与预览行为。
            ValidateRectEffect(effect);
            // 根据显示分支或操作（showing）选择对应测试路径。
            if (showing) ui.Hide();
            // 覆盖条件不成立时的另一条配置或清理路径。
            else ui.Show();
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(showing ? ui.ShowAsync(cancellation.Token) : ui.HideAsync(cancellation.Token));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 从线性半程进入真实运行路径，全局 InQuad 映射为 0.25，分项不能再次求曲线。
                SampleRectProgress(effect, 0.5f);
                // 确认“((RectTransform)effect.transform).anchoredPosition.x”符合“showing ? 75f : 25f”，允许指定的浮点误差。
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x,
                    Is.EqualTo(showing ? 75f : 25f).Within(0.001f));
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { cancellation.Cancel(); }
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
        }

        /// <summary>验证隐藏中反向显示从当前姿态开始，并采用新方向的曲线。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_ReversingDuringHide_ShouldStartFromCurrentPoseAndKeepNewCurve()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out var ui);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideAnchorPosEase", Ease.InQuad);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showAnchorPosEase", Ease.OutQuad);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
            SampleRectProgress(effect, 0.5f);
            // 确认“((RectTransform)effect.transform).anchoredPosition.x”符合25f，允许指定的浮点误差。
            Assert.That(((RectTransform)effect.transform).anchoredPosition.x, Is.EqualTo(25f).Within(0.001f));
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 启动并保存显示分支或操作（showing），随后检查等待、取消与完成结果。
            var showing = CaptureException(ui.ShowAsync(cancellation.Token));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认“((RectTransform)effect.transform).anchoredPosition.x”符合25f，允许指定的浮点误差。
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x, Is.EqualTo(25f).Within(0.001f),
                    "反向播放的第一帧不能跳回另一条曲线上的位置。");
                // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
                yield return WaitForTask(hiding);
                // 确认隐藏分支或操作（hiding）的异步结果保留取消语义，不被报告为成功。
                Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
                // 旧动画 finally 已运行，新动画的半程仍应从 25 按 OutQuad 前进到 0，即 6.25。
                SampleRectProgress(effect, 0.5f);
                // 确认“((RectTransform)effect.transform).anchoredPosition.x”符合6.25f，允许指定的浮点误差。
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x, Is.EqualTo(6.25f).Within(0.001f));
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { cancellation.Cancel(); }
            // 等待“WaitForTask(showing)”，再检查操作完成后的状态。
            yield return WaitForTask(showing);
            // 确认显示分支或操作（showing）的异步结果保留取消语义，不被报告为成功。
            Assert.That(showing.Result, Is.InstanceOf<OperationCanceledException>());
        }

        /// <summary>验证同步显示取代隐藏过渡后立即应用精确显示端点。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_SyncShowReplacingHide_ShouldApplyExactEndpoint()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out var ui);
            // 为五项 Rect 属性配置指定方向的独立缓动曲线。
            ConfigureRectParts(effect, false);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
            SampleRectProgress(effect, 0.5f);

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();

            // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
            AssertRectPose((RectTransform)effect.transform, RectShownPose);
            // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
            yield return WaitForTask(hiding);
            // 确认隐藏分支或操作（hiding）的异步结果保留取消语义，不被报告为成功。
            Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
            // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
            AssertRectPose((RectTransform)effect.transform, RectShownPose);
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
            AssertRectPose((RectTransform)effect.transform, RectHiddenPose);
        }

        /// <summary>统一配置指定方向的缓动，可选择全局或各分项曲线。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <param name="showing">是否验证显示方向；为 false 时验证隐藏方向。</param>
        /// <param name="ease">需要配置的缓动曲线类型。</param>
        /// <param name="global">是否使用全局曲线覆盖分项曲线。</param>
        private static void ConfigureRectUniformEase(RectEffectController effect, bool showing, Ease ease, bool global)
        {
            // 保存“prefix”的基准值“showing ? "show" : "hide"”，供后续比较或恢复。
            var prefix = showing ? "show" : "hide";
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, prefix + "Ease", global ? ease : Ease.NotSet);
            // 逐项检查或处理“var part in new[] { "AnchorPos", "Angle", "Scale", "SizeDelta", "Pivot" }”，覆盖集合中的每个对象。
            foreach (var part in new[] { "AnchorPos", "Angle", "Scale", "SizeDelta", "Pivot" })
                // 写入 Rect 动效的序列化测试配置。
                SetRectEffectField(effect, prefix + part + "Ease", global ? Ease.Linear : ease);
        }

        /// <summary>按给定起终点和进度计算不截断的预期 Rect 姿态。</summary>
        /// <param name="from">动画插值的起始姿态。</param>
        /// <param name="to">动画插值的目标姿态。</param>
        /// <param name="progress">需要采样的归一化播放进度。</param>
        /// <returns>按给定起终点和进度插值的预期姿态。</returns>
        private static EffectSerializedRectTransform ExpectedRectPose(
            EffectSerializedRectTransform from, EffectSerializedRectTransform to, float progress) => new()
        {
            anchorPos = from.anchorPos + (to.anchorPos - from.anchorPos) * progress,
            angle = from.angle + (to.angle - from.angle) * progress,
            scale = from.scale + (to.scale - from.scale) * progress,
            sizeDelta = from.sizeDelta + (to.sizeDelta - from.sizeDelta) * progress,
            pivot = from.pivot + (to.pivot - from.pivot) * progress
        };

        /// <summary>验证隐藏方向的 Back 与 Elastic 缓动对全部属性保留过冲。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_HideBackAndElastic_ShouldPreserveOvershootForAllProperties() =>
            VerifyRectOvershoot(false);

        /// <summary>验证显示方向的 Back 与 Elastic 缓动对全部属性保留过冲。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_ShowBackAndElastic_ShouldPreserveOvershootForAllProperties() =>
            VerifyRectOvershoot(true);

        /// <summary>比较全局与分项曲线在过冲区间的实际姿态。</summary>
        /// <param name="showing">是否验证显示方向；为 false 时验证隐藏方向。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyRectOvershoot(bool showing)
        {
            // 创建“curves”，隔离本用例的资源或观测数据。
            var curves = new[] { Ease.InBack, Ease.OutBack, Ease.InElastic, Ease.OutElastic };
            // 已知曲线半程值独立于生产求值器，既检出截断，也检出重复应用曲线。
            var halfProgress = new[] { -0.0876975f, 1.0876975f, -0.015625f, 1.015625f };
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var i = 0; i < curves.Length; i++)
            {
                // 创建“global”并配置测试所需的层级或表现组件。
                var global = CreateRectEffect(out var globalUI);
                // 创建“parts”并配置测试所需的层级或表现组件。
                var parts = CreateRectEffect(out var partsUI);
                // 统一配置指定方向的缓动，可选择全局或各分项曲线。
                ConfigureRectUniformEase(global, showing, curves[i], true);
                // 统一配置指定方向的缓动，可选择全局或各分项曲线。
                ConfigureRectUniformEase(parts, showing, curves[i], false);
                // 根据显示分支或操作（showing）选择对应测试路径。
                if (showing) { globalUI.Hide(); partsUI.Hide(); }
                // 覆盖条件不成立时的另一条配置或清理路径。
                else { globalUI.Show(); partsUI.Show(); }

                // 创建“globalCancellation”，控制本次显隐的主动取消时机。
                using var globalCancellation = new CancellationTokenSource();
                // 创建“partsCancellation”，控制本次显隐的主动取消时机。
                using var partsCancellation = new CancellationTokenSource();
                // 启动并保存“globalOperation”，随后检查等待、取消与完成结果。
                var globalOperation = CaptureException(showing
                    ? globalUI.ShowAsync(globalCancellation.Token) : globalUI.HideAsync(globalCancellation.Token));
                // 启动并保存“partsOperation”，随后检查等待、取消与完成结果。
                var partsOperation = CaptureException(showing
                    ? partsUI.ShowAsync(partsCancellation.Token) : partsUI.HideAsync(partsCancellation.Token));
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try
                {
                    // 确认“globalOperation”的完成标志为假。
                    Assert.IsFalse(globalOperation.IsCompleted);
                    // 确认“partsOperation”的完成标志为假。
                    Assert.IsFalse(partsOperation.IsCompleted);
                    // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                    SampleRectProgress(global, 0.5f);
                    // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                    SampleRectProgress(parts, 0.5f);
                    // 保存预期结果（expected）的基准值“ExpectedRectPose(showing ? RectHiddenPose : RectShownPose, showing ? RectShownPose : RectHiddenPose…”，供后续比较或恢复。
                    var expected = ExpectedRectPose(showing ? RectHiddenPose : RectShownPose,
                        showing ? RectShownPose : RectHiddenPose, halfProgress[i]);
                    // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                    AssertRectPose((RectTransform)global.transform, expected);
                    // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                    AssertRectPose((RectTransform)parts.transform, expected);
                    // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                    AssertRectPose((RectTransform)global.transform,
                        EffectSerializedRectTransform.GetInstance((RectTransform)parts.transform));
                    // 确认“ReadRectAmount(global)”满足约束“Is.InRange(0f, 1f)”。
                    Assert.That(ReadRectAmount(global), Is.InRange(0f, 1f), "表现允许过冲，标准进度仍保持有效范围。");
                    // 确认“ReadRectAmount(parts)”满足约束“Is.InRange(0f, 1f)”。
                    Assert.That(ReadRectAmount(parts), Is.InRange(0f, 1f));
                }
                finally
                {
                    // 取消本轮操作，验证旧任务停止提交成功状态。
                    globalCancellation.Cancel();
                    // 取消本轮操作，验证旧任务停止提交成功状态。
                    partsCancellation.Cancel();
                }
                // 等待“WaitForTask(globalOperation)”，再检查操作完成后的状态。
                yield return WaitForTask(globalOperation);
                // 等待“WaitForTask(partsOperation)”，再检查操作完成后的状态。
                yield return WaitForTask(partsOperation);
                // 确认“globalOperation”的异步结果保留取消语义，不被报告为成功。
                Assert.That(globalOperation.Result, Is.InstanceOf<OperationCanceledException>());
                // 确认“partsOperation”的异步结果保留取消语义，不被报告为成功。
                Assert.That(partsOperation.Result, Is.InstanceOf<OperationCanceledException>());
            }
        }

        /// <summary>验证过冲期间反向播放时保留当前实际姿态并启动新过渡。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_ReversingDuringOvershoot_ShouldPreservePoseAndNewTransition()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out var ui);
            // 统一配置指定方向的缓动，可选择全局或各分项曲线。
            ConfigureRectUniformEase(effect, false, Ease.OutBack, true);
            // 统一配置指定方向的缓动，可选择全局或各分项曲线。
            ConfigureRectUniformEase(effect, true, Ease.OutElastic, true);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 创建“hideCancellation”，控制本次显隐的主动取消时机。
            using var hideCancellation = new CancellationTokenSource();
            // 创建“showCancellation”，控制本次显隐的主动取消时机。
            using var showCancellation = new CancellationTokenSource();
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(hideCancellation.Token));
            // 初始化显示分支或操作（showing）为空引用，作为后续回调的观测基准。
            System.Threading.Tasks.Task<Exception> showing = null;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                SampleRectProgress(effect, 0.5f);
                // 保存矩形变换（rect）的基准值“(RectTransform)effect.transform”，供后续比较或恢复。
                var rect = (RectTransform)effect.transform;
                // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                AssertRectPose(rect, ExpectedRectPose(RectShownPose, RectHiddenPose, 1.0876975f));
                // 保存“overshoot”的基准值“EffectSerializedRectTransform.GetInstance(rect)”，供后续比较或恢复。
                var overshoot = EffectSerializedRectTransform.GetInstance(rect);
                // 将显示分支或操作（showing）设为“CaptureException(ui.ShowAsync(showCancellation.Token))”，建立当前测试条件。
                showing = CaptureException(ui.ShowAsync(showCancellation.Token));
                // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                AssertRectPose(rect, overshoot);
                // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
                yield return WaitForTask(hiding);
                // 确认隐藏分支或操作（hiding）的异步结果保留取消语义，不被报告为成功。
                Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());

                // 旧动画取消收尾之后，新动画仍从捕获的越界姿态独立求值。
                SampleRectProgress(effect, 0.5f);
                // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                AssertRectPose(rect, ExpectedRectPose(overshoot, RectShownPose, 1.015625f));
                // 确认显示分支或操作（showing）的完成标志为假。
                Assert.IsFalse(showing.IsCompleted);
            }
            finally
            {
                // 取消本轮操作，验证旧任务停止提交成功状态。
                hideCancellation.Cancel();
                // 取消本轮操作，验证旧任务停止提交成功状态。
                showCancellation.Cancel();
            }
            // 等待“WaitForTask(showing)”，再检查操作完成后的状态。
            yield return WaitForTask(showing);
            // 确认显示分支或操作（showing）的异步结果保留取消语义，不被报告为成功。
            Assert.That(showing.Result, Is.InstanceOf<OperationCanceledException>());
        }

        /// <summary>验证过冲取消后重试从实际姿态开始，仍执行完整配置时长。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_CanceledOvershootRetry_ShouldRunFullDurationFromActualPose()
        {
            // 逐项检查或处理“var global in new[] { true, false }”，覆盖集合中的每个对象。
            foreach (var global in new[] { true, false })
            {
                // 创建动效组件（effect）并配置测试所需的层级或表现组件。
                var effect = CreateRectEffect(out var ui);
                // 统一配置指定方向的缓动，可选择全局或各分项曲线。
                ConfigureRectUniformEase(effect, false, Ease.OutBack, global);
                // 提交界面（ui）的显示请求，触发实际生命周期。
                ui.Show();
                // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
                using var cancellation = new CancellationTokenSource();
                // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
                var hiding = CaptureException(ui.HideAsync(cancellation.Token));
                // 声明跨分支使用的观测变量，保留后续创建或采样得到的结果。
                EffectSerializedRectTransform overshoot;
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try
                {
                    // 按指定动画进度采样 Rect 姿态，用于比较缓动曲线。
                    SampleRectProgress(effect, 0.5f);
                    // 将“overshoot”设为“EffectSerializedRectTransform.GetInstance((RectTransform)effect.transform)”，建立当前测试条件。
                    overshoot = EffectSerializedRectTransform.GetInstance((RectTransform)effect.transform);
                    // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                    AssertRectPose((RectTransform)effect.transform,
                        ExpectedRectPose(RectShownPose, RectHiddenPose, 1.0876975f));
                    // 根据“global”选择对应测试路径。
                    if (global) Assert.That(ReadRectAmount(effect), Is.EqualTo(1f));
                }
                // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
                finally { cancellation.Cancel(); }
                // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
                yield return WaitForTask(hiding);
                // 确认隐藏分支或操作（hiding）的异步结果保留取消语义，不被报告为成功。
                Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
                // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                AssertRectPose((RectTransform)effect.transform, overshoot);

                // 将“const float retryDuration”设为0.2f，建立当前测试条件。
                const float retryDuration = 0.2f;
                // 写入 Rect 动效的序列化测试配置。
                SetRectEffectField(effect, "hideDuration", retryDuration);
                // 创建“retryCancellation”，控制本次显隐的主动取消时机。
                using var retryCancellation = new CancellationTokenSource();
                // 保存“startedAt”的基准值“Time.unscaledTimeAsDouble”，供后续比较或恢复。
                var startedAt = Time.unscaledTimeAsDouble;
                // 启动并保存“retry”，随后检查等待、取消与完成结果。
                var retry = CaptureException(ui.HideAsync(retryCancellation.Token));
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try
                {
                    // 确认“retry”的完成标志为假。
                    Assert.IsFalse(retry.IsCompleted);
                    // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                    AssertRectPose((RectTransform)effect.transform, overshoot);
                    // 即使全局标准进度已等于隐藏终点，实际姿态仍需要完整的下一段过渡。
                    SampleRectProgress(effect, 0.5f);
                    // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                    AssertRectPose((RectTransform)effect.transform,
                        ExpectedRectPose(overshoot, RectHiddenPose, 1.0876975f));
                    // 确认“retry”的完成标志为假。
                    Assert.IsFalse(retry.IsCompleted);
                    // 等待“WaitForTask(retry)”，再检查操作完成后的状态。
                    yield return WaitForTask(retry);
                    // 确认“retry”的异步结果为空。
                    Assert.IsNull(retry.Result);
                    // 确认“Time.unscaledTimeAsDouble - startedAt”满足约束“Is.GreaterThanOrEqualTo(retryDuration - 0.00001)”。
                    Assert.That(Time.unscaledTimeAsDouble - startedAt,
                        Is.GreaterThanOrEqualTo(retryDuration - 0.00001), "重试必须按完整配置时长播放。");
                    // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
                    AssertRectPose((RectTransform)effect.transform, RectHiddenPose);
                    // 确认“ReadRectAmount(effect)”符合1f。
                    Assert.That(ReadRectAmount(effect), Is.EqualTo(1f));
                    // 确认界面（ui）的界面状态等于隐藏终态。
                    Assert.AreEqual(UIState.Hided, ui.State);
                }
                // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
                finally { retryCancellation.Cancel(); }
            }
        }

        /// <summary>验证编辑器预览使用线性插值并保留各分项曲线配置。</summary>
        [Test]
        public void RectEffect_OnValidate_ShouldPreservePartsAndPreviewLinearly()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out _);
            // 为五项 Rect 属性配置指定方向的独立缓动曲线。
            ConfigureRectParts(effect, true);
            // 为五项 Rect 属性配置指定方向的独立缓动曲线。
            ConfigureRectParts(effect, false);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showEase", Ease.OutCubic);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideEase", Ease.InCubic);
            // 按指定效果量采样 Rect 姿态，用于验证端点及过冲连续性。
            SampleRectAmount(effect, 0.5f);
            // 调用 Rect 动效的编辑器验证入口，检查配置保留与预览行为。
            ValidateRectEffect(effect);
            // 逐项检查或处理“var prefix in new[] { "show", "hide" }”，覆盖集合中的每个对象。
            foreach (var prefix in new[] { "show", "hide" })
            {
                // 创建“fields”，隔离本用例的资源或观测数据。
                var fields = new[] { "AnchorPosEase", "AngleEase", "ScaleEase", "SizeDeltaEase", "PivotEase" };
                // 创建预期结果（expected），隔离本用例的资源或观测数据。
                var expected = new[] { Ease.InQuad, Ease.OutQuad, Ease.InCubic, Ease.OutCubic, Ease.Linear };
                // 按既定范围逐项采样或处理，检查边界及重复执行行为。
                for (var i = 0; i < fields.Length; i++)
                    // 确认“typeof(RectEffectController) .GetField(prefix + fields[i], BindingFlags.Instance | BindingFlags.Non…”等于“expected[i]”。
                    Assert.AreEqual(expected[i], typeof(RectEffectController)
                        .GetField(prefix + fields[i], BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect));
            }
            // 确认“((RectTransform)effect.transform).anchoredPosition.x”符合50f，允许指定的浮点误差。
            Assert.That(((RectTransform)effect.transform).anchoredPosition.x, Is.EqualTo(50f).Within(0.001f));
        }

        /// <summary>验证零时长过渡同时应用精确端点与正确射线状态。</summary>
        [Test]
        public void RectEffect_ZeroDuration_ShouldApplyEndpointsAndRaycastState()
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out var ui);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showDuration", 0f);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideDuration", 0f);
            // 为五项 Rect 属性配置指定方向的独立缓动曲线。
            ConfigureRectParts(effect, true);
            // 为五项 Rect 属性配置指定方向的独立缓动曲线。
            ConfigureRectParts(effect, false);
            // 启动并保存显示分支或操作（showing），随后检查等待、取消与完成结果。
            var showing = CaptureException(ui.ShowAsync(CancellationToken.None));
            // 确认显示分支或操作（showing）的完成标志为真。
            Assert.IsTrue(showing.IsCompleted);
            // 确认显示分支或操作（showing）的异步结果为空。
            Assert.IsNull(showing.Result);
            // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
            AssertRectPose((RectTransform)effect.transform, RectShownPose);
            // 确认“effect.GetComponent<CanvasGroup>()”的射线拦截配置为真。
            Assert.IsTrue(effect.GetComponent<CanvasGroup>().blocksRaycasts);
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            // 确认隐藏分支或操作（hiding）的完成标志为真。
            Assert.IsTrue(hiding.IsCompleted);
            // 确认隐藏分支或操作（hiding）的异步结果为空。
            Assert.IsNull(hiding.Result);
            // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
            AssertRectPose((RectTransform)effect.transform, RectHiddenPose);
            // 确认“effect.GetComponent<CanvasGroup>()”的射线拦截配置为假。
            Assert.IsFalse(effect.GetComponent<CanvasGroup>().blocksRaycasts);
        }

        /// <summary>验证真实播放使用分项曲线并最终到达精确端点。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_PlaybackWithParts_ShouldMatchCurveAndReachEndpoints() => VerifyRectPlayback(false);

        /// <summary>验证真实播放使用全局曲线并最终到达精确端点。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator RectEffect_PlaybackWithGlobal_ShouldMatchCurveAndReachEndpoints() => VerifyRectPlayback(true);

        /// <summary>验证指定曲线配置下的实际动画中间采样与最终端点。</summary>
        /// <param name="useGlobal">是否使用全局曲线；为 false 时使用分项曲线。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyRectPlayback(bool useGlobal)
        {
            // 创建动效组件（effect）并配置测试所需的层级或表现组件。
            var effect = CreateRectEffect(out var ui);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showDuration", 0.1f);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideDuration", 0.1f);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showAnchorPosEase", Ease.OutQuad);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideAnchorPosEase", Ease.InQuad);
            // 根据“useGlobal”选择对应测试路径。
            if (useGlobal)
            {
                // 写入 Rect 动效的序列化测试配置。
                SetRectEffectField(effect, "showEase", Ease.OutQuad);
                // 写入 Rect 动效的序列化测试配置。
                SetRectEffectField(effect, "hideEase", Ease.InQuad);
            }
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            // 保存等待截止时间（deadline）的基准值“Time.realtimeSinceStartup + TestTimeout”，供后续比较或恢复。
            var deadline = Time.realtimeSinceStartup + TestTimeout;
            // 在截止时间内持续观察任务状态，避免测试无限等待。
            while (!hiding.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                // 保存效果量（amount）的基准值“ReadRectAmount(effect)”，供后续比较或恢复。
                var amount = ReadRectAmount(effect);
                // 确认“((RectTransform)effect.transform).anchoredPosition.x”符合“100f * (useGlobal ? amount : amount * amount)”，允许指定的浮点误差。
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x,
                    Is.EqualTo(100f * (useGlobal ? amount : amount * amount)).Within(0.001f));
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
            }
            // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
            yield return WaitForTask(hiding);
            // 确认隐藏分支或操作（hiding）的异步结果为空。
            Assert.IsNull(hiding.Result);
            // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
            AssertRectPose((RectTransform)effect.transform, RectHiddenPose);
            // 启动并保存显示分支或操作（showing），随后检查等待、取消与完成结果。
            var showing = CaptureException(ui.ShowAsync(CancellationToken.None));
            // 将等待截止时间（deadline）设为“Time.realtimeSinceStartup + TestTimeout”，建立当前测试条件。
            deadline = Time.realtimeSinceStartup + TestTimeout;
            // 在截止时间内持续观察任务状态，避免测试无限等待。
            while (!showing.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                // 保存效果量（amount）的基准值“ReadRectAmount(effect)”，供后续比较或恢复。
                var amount = ReadRectAmount(effect);
                // 确认“((RectTransform)effect.transform).anchoredPosition.x”符合“100f * (useGlobal ? amount : amount * amount)”，允许指定的浮点误差。
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x,
                    Is.EqualTo(100f * (useGlobal ? amount : amount * amount)).Within(0.001f));
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
            }
            // 等待“WaitForTask(showing)”，再检查操作完成后的状态。
            yield return WaitForTask(showing);
            // 确认显示分支或操作（showing）的异步结果为空。
            Assert.IsNull(showing.Result);
            // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
            AssertRectPose((RectTransform)effect.transform, RectShownPose);
        }
    }
}
