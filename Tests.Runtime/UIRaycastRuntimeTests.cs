using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的显隐生命周期中的射线拦截。</summary>
    public partial class Test_UIBase
    {
        /// <summary>创建可配置可见射线拦截与交互属性的界面，输出 CanvasGroup 供断言。</summary>
        /// <param name="blocksWhenVisible">界面可见时是否拦截射线。</param>
        /// <param name="interactable">测试开始时需要保留的可交互配置。</param>
        /// <param name="group">输出用于断言射线拦截与交互配置的 CanvasGroup。</param>
        /// <param name="activate">是否立即激活测试宿主以触发 Unity 生命周期。</param>
        /// <returns>配置完成的射线测试面板。</returns>
        private UIBase CreateRaycastPanel(bool blocksWhenVisible, bool interactable, out CanvasGroup group,
            bool activate = true)
        {
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject("RaycastPanel", typeof(RectTransform), typeof(CanvasGroup));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(go);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(false);
            // 将效果所属组（group）设为“go.GetComponent<CanvasGroup>()”，建立当前测试条件。
            group = go.GetComponent<CanvasGroup>();
            // 将效果所属组（group）的射线拦截配置设为“blocksWhenVisible”，建立当前测试条件。
            group.blocksRaycasts = blocksWhenVisible;
            // 将效果所属组（group）的可交互配置设为“interactable”，建立当前测试条件。
            group.interactable = interactable;
            // 添加并保存界面（ui），供后续生命周期与效果检查使用。
            var ui = go.AddComponent<UIBase>();
            // 将界面（ui）的组件启用状态设为false，建立当前测试条件。
            ui.enabled = false;
            // 根据“activate”选择对应测试路径。
            if (activate) go.SetActive(true);
            // 返回界面（ui），交给调用方继续配置或断言。
            return ui;
        }

        /// <summary>验证同步显隐循环恢复配置的可见射线状态，并保留 interactable。</summary>
        /// <param name="blocksWhenVisible">界面可见时是否拦截射线。</param>
        /// <param name="interactable">测试开始时需要保留的可交互配置。</param>
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void Raycast_SyncCycles_ShouldRestoreVisibleConfigurationWithoutChangingInteractable(
            bool blocksWhenVisible, bool interactable)
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(blocksWhenVisible, interactable, out var group);
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts, "隐藏初态在 Awake 后应立即放行射线。");
            // 确认“ui.BlocksRaycastsWhenVisible”等于“blocksWhenVisible”。
            Assert.AreEqual(blocksWhenVisible, ui.BlocksRaycastsWhenVisible);
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var i = 0; i < 3; i++)
            {
                // 提交界面（ui）的显示请求，触发实际生命周期。
                ui.Show();
                // 确认效果所属组（group）的射线拦截配置等于“blocksWhenVisible”。
                Assert.AreEqual(blocksWhenVisible, group.blocksRaycasts);
                // 确认效果所属组（group）的可交互配置等于“interactable”。
                Assert.AreEqual(interactable, group.interactable);
                // 提交界面（ui）的隐藏请求，检查子树联动与终态。
                ui.Hide();
                // 确认效果所属组（group）的射线拦截配置为假。
                Assert.IsFalse(group.blocksRaycasts);
                // 确认效果所属组（group）的可交互配置等于“interactable”。
                Assert.AreEqual(interactable, group.interactable);
            }
        }

        /// <summary>验证状态订阅者读取到已应用的射线拦截状态。</summary>
        [Test]
        public void Raycast_StateSubscribers_ShouldObserveAppliedRaycastState()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(true, true, out var group);
            // 创建“observations”，隔离本用例的资源或观测数据。
            var observations = new List<string>();
            // 为“ui.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            ui.OnStateChanged += (_, current) => observations.Add(current + ":" + group.blocksRaycasts);

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();

            // 确认“observations”等于“new[] { "Showing:True", "Showed:True", "Hiding:True", "Hided:False" }”。
            CollectionAssert.AreEqual(new[] { "Showing:True", "Showed:True", "Hiding:True", "Hided:False" }, observations);
        }

        /// <summary>验证并行隐藏行为全部完成后才释放射线拦截。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Raycast_ParallelHideActions_ShouldReleaseOnlyAfterAllActionsComplete()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(true, false, out var group);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 创建第一个隐藏行为完成源，单独控制它的完成时机。
            var first = new UniTaskCompletionSource();
            // 创建第二个隐藏行为完成源，验证界面必须等待两个行为。
            var second = new UniTaskCompletionSource();
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += _ => first.Task;
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += _ => second.Task;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(ui.HideAsync(CancellationToken.None));
            // 确认界面（ui）的界面状态等于隐藏过渡态。
            Assert.AreEqual(UIState.Hiding, ui.State);
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts);

            // 完成手动控制的挂起任务，让已启动行为能够收口。
            first.TrySetResult();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 确认异步操作（operation）的完成标志为假。
            Assert.IsFalse(operation.IsCompleted);
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts, "单个隐藏行为完成不能提前放行射线。");
            // 确认效果所属组（group）的可交互配置为假。
            Assert.IsFalse(group.interactable);
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            second.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
            // 确认效果所属组（group）的可交互配置为假。
            Assert.IsFalse(group.interactable);
        }

        /// <summary>验证隐藏被取消后继续拦截射线，直到后续隐藏成功。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Raycast_CanceledHide_ShouldKeepBlockingUntilSuccessfulHide()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(true, true, out var group);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += WaitUntilCanceled;
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(cancellation.Token));
            // 取消本轮操作，验证旧任务停止提交成功状态。
            cancellation.Cancel();
            // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
            yield return WaitForTask(hiding);

            // 确认隐藏分支或操作（hiding）的异步结果保留取消语义，不被报告为成功。
            Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认界面（ui）的界面状态等于隐藏过渡态。
            Assert.AreEqual(UIState.Hiding, ui.State);
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts, "取消不是隐藏完成。");
            // 确认效果所属组（group）的可交互配置为真。
            Assert.IsTrue(group.interactable);
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
        }

        /// <summary>验证隐藏失败后继续拦截射线，直到后续隐藏成功。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Raycast_FailedHide_ShouldKeepBlockingUntilSuccessfulHide()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(true, true, out var group);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 创建“failure”，隔离本用例的资源或观测数据。
            var failure = new InvalidOperationException("hide effect failed");
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += _ => UniTask.FromException(failure);
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
            yield return WaitForTask(hiding);

            // 确认隐藏分支或操作（hiding）的异步结果仍引用“failure”。
            Assert.AreSame(failure, hiding.Result);
            // 确认界面（ui）的界面状态等于隐藏过渡态。
            Assert.AreEqual(UIState.Hiding, ui.State);
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts);
            // 确认效果所属组（group）的可交互配置为真。
            Assert.IsTrue(group.interactable);
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
        }

        /// <summary>验证预取消请求不会改变射线拦截或交互属性。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Raycast_PreCanceledRequests_ShouldNotChangeRaycastsOrInteractable()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(true, false, out var group);
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 取消本轮操作，验证旧任务停止提交成功状态。
            cancellation.Cancel();
            // 启动并保存显示分支或操作（showing），随后检查等待、取消与完成结果。
            var showing = CaptureException(ui.ShowAsync(cancellation.Token));
            // 等待“WaitForTask(showing)”，再检查操作完成后的状态。
            yield return WaitForTask(showing);
            // 确认显示分支或操作（showing）的异步结果保留取消语义，不被报告为成功。
            Assert.That(showing.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(cancellation.Token));
            // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
            yield return WaitForTask(hiding);
            // 确认隐藏分支或操作（hiding）的异步结果保留取消语义，不被报告为成功。
            Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts);
            // 确认效果所属组（group）的可交互配置为假。
            Assert.IsFalse(group.interactable);
        }

        /// <summary>验证 Rect 先注册的复合动效在过渡期间持续拦截射线。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Raycast_CompoundEffects_RectFirst_ShouldKeepBlockingDuringTransitions() =>
            VerifyCompoundRaycastEffects(true);

        /// <summary>验证透明度先注册的复合动效在过渡期间持续拦截射线。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Raycast_CompoundEffects_AlphaFirst_ShouldKeepBlockingDuringTransitions() =>
            VerifyCompoundRaycastEffects(false);

        /// <summary>验证不同动效注册顺序下，射线状态均由界面生命周期统一管理。</summary>
        /// <param name="rectFirst">是否先添加 Rect 动效，再添加透明度动效。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyCompoundRaycastEffects(bool rectFirst)
        {
            // 创建矩形变换（rect）并配置测试所需的层级或表现组件。
            var rect = CreateRectEffect(out var ui);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(rect, "showDuration", 0.05f);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(rect, "hideDuration", 0.05f);
            // 切换“ui.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            ui.gameObject.SetActive(false);
            // 添加并保存“alpha”，供后续生命周期与效果检查使用。
            var alpha = ui.gameObject.AddComponent<AlphaEffectController>();
            // 保存“flags”的基准值“BindingFlags.Instance | BindingFlags.NonPublic”，供后续比较或恢复。
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            typeof(EffectControllerBase).GetField("showDuration", flags).SetValue(alpha, 0f);
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            typeof(EffectControllerBase).GetField("hideDuration", flags).SetValue(alpha, 0f);
            // 显式调用编辑器验证入口，检查配置缓存与预览同步。
            typeof(EffectControllerBase).GetMethod("OnValidate", flags).Invoke(alpha, null);
            // 切换“ui.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            ui.gameObject.SetActive(true);
            // 取得“rectShow”的反射入口，仅在测试内访问非公开配置。
            var rectShow = (Func<CancellationToken, UniTask>)Delegate.CreateDelegate(typeof(Func<CancellationToken, UniTask>),
                rect, typeof(RectEffectController).GetMethod("OnUIShowAsync", flags));
            // 取得“rectHide”的反射入口，仅在测试内访问非公开配置。
            var rectHide = (Func<CancellationToken, UniTask>)Delegate.CreateDelegate(typeof(Func<CancellationToken, UniTask>),
                rect, typeof(RectEffectController).GetMethod("OnUIHideAsync", flags));
            // 取得“alphaShow”的反射入口，仅在测试内访问非公开配置。
            var alphaShow = (Func<CancellationToken, UniTask>)Delegate.CreateDelegate(typeof(Func<CancellationToken, UniTask>),
                alpha, typeof(EffectControllerBase).GetMethod("OnUIShowAsync", flags));
            // 取得“alphaHide”的反射入口，仅在测试内访问非公开配置。
            var alphaHide = (Func<CancellationToken, UniTask>)Delegate.CreateDelegate(typeof(Func<CancellationToken, UniTask>),
                alpha, typeof(EffectControllerBase).GetMethod("OnUIHideAsync", flags));
            // 解除“ui.ShowAsyncActions”的测试订阅，避免干扰后续用例。
            ui.ShowAsyncActions -= rectShow;
            // 解除“ui.ShowAsyncActions”的测试订阅，避免干扰后续用例。
            ui.ShowAsyncActions -= alphaShow;
            // 解除“ui.HideAsyncActions”的测试订阅，避免干扰后续用例。
            ui.HideAsyncActions -= rectHide;
            // 解除“ui.HideAsyncActions”的测试订阅，避免干扰后续用例。
            ui.HideAsyncActions -= alphaHide;
            // 为“ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowAsyncActions += rectFirst ? rectShow : alphaShow;
            // 为“ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowAsyncActions += rectFirst ? alphaShow : rectShow;
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += rectFirst ? rectHide : alphaHide;
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += rectFirst ? alphaHide : rectHide;
            // 获取效果所属组（group）的实际组件引用，供表现与生命周期断言使用。
            var group = ui.GetComponent<CanvasGroup>();
            // 将效果所属组（group）的可交互配置设为false，建立当前测试条件。
            group.interactable = false;
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();

            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            // 确认界面（ui）的界面状态等于隐藏过渡态。
            Assert.AreEqual(UIState.Hiding, ui.State);
            // 确认隐藏分支或操作（hiding）的完成标志为假。
            Assert.IsFalse(hiding.IsCompleted);
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts, "瞬间完成的透明度效果不能抢先关闭位移动画期间的射线阻挡。");
            // 确认效果所属组（group）的可交互配置为假。
            Assert.IsFalse(group.interactable);
            // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
            yield return WaitForTask(hiding);
            // 确认隐藏分支或操作（hiding）的异步结果为空。
            Assert.IsNull(hiding.Result);
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
            // 启动并保存显示分支或操作（showing），随后检查等待、取消与完成结果。
            var showing = CaptureException(ui.ShowAsync(CancellationToken.None));
            // 确认界面（ui）的界面状态等于显示过渡态。
            Assert.AreEqual(UIState.Showing, ui.State);
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts, "允许动画中交互，不等待所有动画完成才恢复射线配置。");
            // 确认效果所属组（group）的可交互配置为假。
            Assert.IsFalse(group.interactable);
            // 等待“WaitForTask(showing)”，再检查操作完成后的状态。
            yield return WaitForTask(showing);
            // 确认显示分支或操作（showing）的异步结果为空。
            Assert.IsNull(showing.Result);
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts);
            // 确认效果所属组（group）的可交互配置为假。
            Assert.IsFalse(group.interactable);
        }

        /// <summary>验证父界面显隐不会修改子按钮原有可交互配置。</summary>
        /// <param name="interactable">测试开始时需要保留的可交互配置。</param>
        [TestCase(true)]
        [TestCase(false)]
        public void Raycast_Lifecycle_ShouldNotChangeChildButtonInteractability(bool interactable)
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(true, interactable, out var group);
            // 创建子界面（child），隔离本用例的资源或观测数据。
            var child = new GameObject("Button", typeof(RectTransform), typeof(Button));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(child);
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            child.transform.SetParent(ui.transform, false);
            // 获取按钮（button）的实际组件引用，供表现与生命周期断言使用。
            var button = child.GetComponent<Button>();
            // 逐项检查或处理“var show in new[] { true, false, true }”，覆盖集合中的每个对象。
            foreach (var show in new[] { true, false, true })
            {
                // 根据“show”选择对应测试路径。
                if (show) ui.Show();
                // 覆盖条件不成立时的另一条配置或清理路径。
                else ui.Hide();
                // 确认按钮（button）的可交互配置为真。
                Assert.IsTrue(button.interactable, "不改写按钮自身设置。");
                // 确认效果所属组（group）的可交互配置等于“interactable”。
                Assert.AreEqual(interactable, group.interactable);
                // 确认“button.IsInteractable()”等于“interactable”。
                Assert.AreEqual(interactable, button.IsInteractable());
            }
        }

        /// <summary>验证运行时修改可见射线配置不会开启隐藏面板，也不提前改变隐藏过渡。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Raycast_RuntimeVisibleConfiguration_ShouldNotEnableHiddenPanelOrChangeHiding()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(true, true, out var group);
            // 将“ui.BlocksRaycastsWhenVisible”设为false，建立当前测试条件。
            ui.BlocksRaycastsWhenVisible = false;
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
            // 将“ui.BlocksRaycastsWhenVisible”设为true，建立当前测试条件。
            ui.BlocksRaycastsWhenVisible = true;
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts);
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += _ => pending.Task;
            // 启动并保存隐藏分支或操作（hiding），随后检查等待、取消与完成结果。
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            // 将“ui.BlocksRaycastsWhenVisible”设为false，建立当前测试条件。
            ui.BlocksRaycastsWhenVisible = false;
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts, "Hiding 保持已有值，直到全部隐藏行为完成。");
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            pending.TrySetResult();
            // 等待“WaitForTask(hiding)”，再检查操作完成后的状态。
            yield return WaitForTask(hiding);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 将“ui.BlocksRaycastsWhenVisible”设为true，建立当前测试条件。
            ui.BlocksRaycastsWhenVisible = true;
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts, "修改显示配置不能让隐藏面板挡住射线。");
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 确认效果所属组（group）的射线拦截配置为真。
            Assert.IsTrue(group.blocksRaycasts);
        }

        /// <summary>验证父隐藏及随父显示分别应用各级面板自己的射线配置。</summary>
        [Test]
        public void Raycast_ParentHideAndShowWithParent_ShouldApplyEachPanelsState()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateRaycastPanel(true, true, out var parentGroup);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateRaycastPanel(true, true, out var childGroup);
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            child.transform.SetParent(parent.transform, false);
            // 将子界面（child）的随父显示配置设为true，建立当前测试条件。
            child.ShowWithParent = true;
            // 提交父界面（parent）的显示请求，触发实际生命周期。
            parent.Show();
            // 确认“parentGroup”的射线拦截配置为真。
            Assert.IsTrue(parentGroup.blocksRaycasts);
            // 确认“childGroup”的射线拦截配置为真。
            Assert.IsTrue(childGroup.blocksRaycasts);
            // 提交父界面（parent）的隐藏请求，检查子树联动与终态。
            parent.Hide();
            // 确认“parentGroup”的射线拦截配置为假。
            Assert.IsFalse(parentGroup.blocksRaycasts);
            // 确认“childGroup”的射线拦截配置为假。
            Assert.IsFalse(childGroup.blocksRaycasts);
            // 提交父界面（parent）的显示请求，触发实际生命周期。
            parent.Show();
            // 确认“parentGroup”的射线拦截配置为真。
            Assert.IsTrue(parentGroup.blocksRaycasts);
            // 确认“childGroup”的射线拦截配置为真。
            Assert.IsTrue(childGroup.blocksRaycasts);
        }

        /// <summary>验证首次 Awake 前调用显示也只捕获一次原始可见射线配置。</summary>
        [Test]
        public void Raycast_ShowBeforeAwake_ShouldCaptureConfigurationOnlyOnce()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(false, true, out var group, false);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 确认界面（ui）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, ui.State);
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
            // 切换“ui.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            ui.gameObject.SetActive(true);
            // 确认“ui.BlocksRaycastsWhenVisible”为假。
            Assert.IsFalse(ui.BlocksRaycastsWhenVisible);
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 确认效果所属组（group）的射线拦截配置为假。
            Assert.IsFalse(group.blocksRaycasts);
        }

        /// <summary>验证真实 GraphicRaycaster 在隐藏时穿透面板，重新显示后恢复命中。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Raycast_GraphicRaycaster_ShouldPassThroughHiddenPanelAndRestoreOnShow()
        {
            // 创建“canvasObject”，隔离本用例的资源或观测数据。
            var canvasObject = new GameObject("RaycastCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(canvasObject);
            // 将“canvasObject.GetComponent<Canvas>().renderMode”设为“RenderMode.ScreenSpaceOverlay”，建立当前测试条件。
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            // 创建“eventObject”，隔离本用例的资源或观测数据。
            var eventObject = new GameObject("RaycastEvents", typeof(EventSystem));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(eventObject);
            // 创建“background”，隔离本用例的资源或观测数据。
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(background);
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            background.transform.SetParent(canvasObject.transform, false);
            // 将“((RectTransform)background.transform).sizeDelta”设为“new Vector2(200, 200)”，建立当前测试条件。
            ((RectTransform)background.transform).sizeDelta = new Vector2(200, 200);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateRaycastPanel(true, true, out _);
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            ui.transform.SetParent(canvasObject.transform, false);
            // 保存矩形变换（rect）的基准值“(RectTransform)ui.transform”，供后续比较或恢复。
            var rect = (RectTransform)ui.transform;
            // 将“rect.sizeDelta”设为“new Vector2(200, 200)”，建立当前测试条件。
            rect.sizeDelta = new Vector2(200, 200);
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            ui.gameObject.AddComponent<Image>();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 强制完成 Canvas 布局与网格更新，保证射线命中查询使用实际几何。
            Canvas.ForceUpdateCanvases();
            // 获取“pointer”的实际组件引用，供表现与生命周期断言使用。
            var pointer = new PointerEventData(eventObject.GetComponent<EventSystem>())
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            // 创建“hits”，隔离本用例的资源或观测数据。
            var hits = new List<RaycastResult>();
            // 获取“raycaster”的实际组件引用，供表现与生命周期断言使用。
            var raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 执行真实 GraphicRaycaster 命中查询，观察隐藏面板是否放行射线。
            raycaster.Raycast(pointer, hits);
            // 确认“hits”具有实际结果。
            Assert.IsNotEmpty(hits);
            // 确认“hits[0].gameObject”仍引用“ui.gameObject”。
            Assert.AreSame(ui.gameObject, hits[0].gameObject);
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 清空“hits”，防止上一步数据影响后续观察。
            hits.Clear();
            // 执行真实 GraphicRaycaster 命中查询，观察隐藏面板是否放行射线。
            raycaster.Raycast(pointer, hits);
            // 确认“hits”具有实际结果。
            Assert.IsNotEmpty(hits);
            // 确认“hits[0].gameObject”仍引用“background”。
            Assert.AreSame(background, hits[0].gameObject);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 清空“hits”，防止上一步数据影响后续观察。
            hits.Clear();
            // 执行真实 GraphicRaycaster 命中查询，观察隐藏面板是否放行射线。
            raycaster.Raycast(pointer, hits);
            // 确认“hits[0].gameObject”仍引用“ui.gameObject”。
            Assert.AreSame(ui.gameObject, hits[0].gameObject);
        }
    }
}
