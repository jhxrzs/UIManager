using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>
    ///  UIBase 回归测试的补充部分，验证回调重入、祖先失活及销毁期间的生命周期。
    /// </summary>
    public partial class Test_UIBase
    {
        /// <summary>
        ///  创建带网格成员的组动效，并将面板置为显示状态以测试异步隐藏准备阶段。
        /// </summary>
        /// <param name="ui">创建并显示的宿主面板。</param>
        /// <param name="image">创建的图形成员，供用例注册标脏回调。</param>
        /// <param name="member">创建的网格动效组件，供用例检查恢复进度。</param>
        /// <param name="material">创建的独立材质，由调用方在用例结束时释放。</param>
        /// <returns>已初始化并显示的组控制器。</returns>
        private UIFlowGroupController CreateThirdAuditAsyncFlow(out UIBase ui, out Image image,
            out UIFlowMeshEffect member, out Material material)
        {
            // 创建暂未激活的动效宿主和面板，先完成参数配置。
            var go = CreateRuntimeEffectObject("ThirdAuditAsyncFlow", out ui);
            // 添加覆盖式画布，使成员进入真实 UI 网格生命周期。
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            // 添加负责驱动成员进度的组控制器。
            var controller = go.AddComponent<UIFlowGroupController>();
            // 设置非零隐藏时长，确保测试经过异步准备阶段。
            SetEffectField(controller, "hideDuration", 0.15f);
            // 为网格成员创建独立的矩形节点。
            var memberObject = new GameObject("AsyncMember", typeof(RectTransform));
            // 登记成员节点，确保用例结束后统一销毁。
            _createdObjects.Add(memberObject);
            // 将成员挂入组层级，并保持本地变换。
            memberObject.transform.SetParent(go.transform, false);
            // 添加图形成员，用真实标脏回调触发测试重入。
            image = memberObject.AddComponent<Image>();
            // 设置非零矩形尺寸，保证成员具有可观察的网格。
            image.rectTransform.sizeDelta = new Vector2(100f, 60f);
            // 创建本用例负责释放的独立 UI 材质。
            material = new Material(Shader.Find("UI/Default"));
            // 给图形成员设置自定义材质作为初始配置。
            image.material = material;
            // 添加网格动效，使成员参与组的进度驱动。
            member = memberObject.AddComponent<UIFlowMeshEffect>();
            // 完成配置后激活宿主，让 Unity 执行组件初始化。
            go.SetActive(true);
            // 将面板置为显示状态，为隐藏或恢复验证建立起点。
            ui.Show();
            // 返回已显示且包含网格成员的组控制器。
            return controller;
        }

        /// <summary>
        ///  验证准备隐藏时移除组动效后，面板仍等待其他尚未完成的异步行为。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdAuditAsyncFlow_DestroyDuringPreparation_ShouldWaitForOtherAction()
        {
            // 创建显示中的组及成员，保留进度和资源引用供后续检查。
            var controller = CreateThirdAuditAsyncFlow(out var ui, out var image, out var member, out var material);
            // 创建由测试手动完成的任务，用于观察其他行为是否被提前结束。
            var pending = new UniTaskCompletionSource();
            // 追加一个独立的挂起隐藏行为，控制器移除后仍应继续等待它。
            ui.HideAsyncActions += _ => pending.Task;
            // 从零开始记录隐藏完成通知次数。
            var after = 0;
            // 记录隐藏完成通知，检查失败或取消时是否误报成功。
            ui.AfterUIHidedEvent += () => after++;
            // 记录是否已经执行销毁，防止标脏回调重入时重复移除。
            var removed = false;
            // 准备在网格标脏的同步回调中移除被测对象。
            UnityAction remove = () =>
            {
                // 已经执行过销毁时跳过后续重入回调。
                if (removed) return;
                // 在实际销毁前设置标记，阻止销毁引起的嵌套回调重复执行。
                removed = true;
                // 在隐藏准备尚未返回时立即移除组控制器。
                Object.DestroyImmediate(controller);
            };
            // 把移除操作注册到真实的顶点标脏事件。
            image.RegisterDirtyVerticesCallback(remove);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 启动异步隐藏并捕获最终结果，观察回调销毁后的收尾行为。
                var operation = CaptureException(ui.HideAsync(CancellationToken.None));
                // 确认“removed”为真。
                Assert.IsTrue(removed);
                // 确认组控制器已经在准备阶段的回调中销毁。
                Assert.IsFalse(controller);
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted, "Removing Flow must not finish another pending UI action.");
                // 确认界面（ui）的界面状态等于隐藏过渡态。
                Assert.AreEqual(UIState.Hiding, ui.State);
                // 确认“after”等于0。
                Assert.AreEqual(0, after);
                // 确认“member”的流动进度为零。
                Assert.That(member.EffectAmount, Is.Zero);
                // 释放手动挂起的行为，使已启动的生命周期能够完成收尾。
                pending.TrySetResult();
                // 等待被测异步请求结束，再检查异常、通知次数和最终状态。
                yield return WaitForTask(operation);
                // 确认异步操作（operation）的异步结果为空。
                Assert.IsNull(operation.Result);
                // 确认界面（ui）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, ui.State);
                // 确认“after”等于1。
                Assert.AreEqual(1, after);
            }
            finally
            {
                // 释放手动挂起的行为，使已启动的生命周期能够完成收尾。
                pending.TrySetResult();
                // 图形仍存活时解除测试回调，避免后续资源清理再次触发它。
                if (image) image.UnregisterDirtyVerticesCallback(remove);
                // 释放本用例创建的运行时材质。
                Object.DestroyImmediate(material);
            }
        }

        /// <summary>
        ///  验证准备阶段同时移除动效和取消请求时，调用方仍收到取消且后续显隐可用。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdAuditAsyncFlow_DestroyAndCancelDuringPreparation_ShouldPreserveCallerCancellation()
        {
            // 创建显示中的组及成员，保留控制器、图形和材质供取消测试使用。
            var controller = CreateThirdAuditAsyncFlow(out var ui, out var image, out _, out var material);
            // 创建调用方取消源，并在用例结束时释放。
            using var caller = new CancellationTokenSource();
            // 记录是否已经执行销毁，防止标脏回调重入时重复移除。
            var removed = false;
            // 从零开始记录隐藏完成通知次数。
            var after = 0;
            // 记录隐藏完成通知，检查失败或取消时是否误报成功。
            ui.AfterUIHidedEvent += () => after++;
            // 准备在同一次标脏回调中移除控制器并取消调用方请求。
            UnityAction removeAndCancel = () =>
            {
                // 已经执行过销毁时跳过后续重入回调。
                if (removed) return;
                // 在实际销毁前设置标记，阻止销毁引起的嵌套回调重复执行。
                removed = true;
                // 在隐藏准备尚未返回时立即移除组控制器。
                Object.DestroyImmediate(controller);
                // 在控制器移除后主动取消请求，验证取消不会被内部清理吞掉。
                caller.Cancel();
            };
            // 将销毁与取消组合操作挂入真实顶点标脏事件。
            image.RegisterDirtyVerticesCallback(removeAndCancel);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 使用调用方令牌启动隐藏，并捕获最终的取消结果。
                var operation = CaptureException(ui.HideAsync(caller.Token));
                // 等待被测异步请求结束，再检查异常、通知次数和最终状态。
                yield return WaitForTask(operation);
                // 确认“removed”为真。
                Assert.IsTrue(removed);
                // 确认控制器已经销毁，取消结果来自真实销毁路径。
                Assert.IsFalse(controller);
                // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
                Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
                // 确认“after”等于0。
                Assert.AreEqual(0, after);
                // 确认界面（ui）的界面状态等于隐藏过渡态。
                Assert.AreEqual(UIState.Hiding, ui.State);
                // 将面板置为显示状态，为隐藏或恢复验证建立起点。
                ui.Show();
                // 确认界面（ui）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, ui.State, "Later requests must still be usable.");
            }
            finally
            {
                // 图形仍存活时解除销毁与取消回调。
                if (image) image.UnregisterDirtyVerticesCallback(removeAndCancel);
                // 释放本用例创建的运行时材质。
                Object.DestroyImmediate(material);
            }
        }

        /// <summary>
        ///  验证准备阶段销毁整个面板时安全取消，不再访问已销毁对象或发送完成通知。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdAuditAsyncFlow_DestroyPanelDuringPreparation_ShouldCancelWithoutStaleAccess()
        {
            // 创建显示中的面板和图形成员，准备在回调内销毁整个面板。
            CreateThirdAuditAsyncFlow(out var ui, out var image, out _, out var material);
            // 记录是否已经执行销毁，防止标脏回调重入时重复移除。
            var removed = false;
            // 从零开始记录隐藏完成通知次数。
            var after = 0;
            // 记录隐藏完成通知，检查失败或取消时是否误报成功。
            ui.AfterUIHidedEvent += () => after++;
            // 准备在网格标脏的同步回调中移除被测对象。
            UnityAction remove = () =>
            {
                // 已经执行过销毁时跳过后续重入回调。
                if (removed) return;
                // 在实际销毁前设置标记，阻止销毁引起的嵌套回调重复执行。
                removed = true;
                // 在隐藏准备回调内立即销毁整个面板及其成员。
                Object.DestroyImmediate(ui.gameObject);
            };
            // 把移除操作注册到真实的顶点标脏事件。
            image.RegisterDirtyVerticesCallback(remove);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 启动异步隐藏并捕获最终结果，观察回调销毁后的收尾行为。
                var operation = CaptureException(ui.HideAsync(CancellationToken.None));
                // 等待被测异步请求结束，再检查异常、通知次数和最终状态。
                yield return WaitForTask(operation);
                // 确认“removed”为真。
                Assert.IsTrue(removed);
                // 确认整个面板已经在准备阶段的回调中销毁。
                Assert.IsFalse(ui);
                // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
                Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
                // 确认“after”等于0。
                Assert.AreEqual(0, after);
            }
            finally
            {
                // 图形仍存活时解除测试回调，避免后续资源清理再次触发它。
                if (image) image.UnregisterDirtyVerticesCallback(remove);
                // 释放本用例创建的运行时材质。
                Object.DestroyImmediate(material);
            }
        }
    }
}
