using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的切换交互的排队顺序。</summary>
    public partial class Test_UIBase
    {
        /// <summary>创建绑定到目标界面的切换按钮，支持同步或异步交互实体。</summary>
        /// <param name="target">需要绑定或通过反射操作的目标对象。</param>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        /// <returns>绑定到指定目标的切换按钮。</returns>
        private Button CreateSwitchInteractionButton(UIBase target, bool isAsync)
        {
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject("SwitchInteractionButton", typeof(RectTransform), typeof(Button));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(go);
            // 获取按钮（button）的实际组件引用，供表现与生命周期断言使用。
            var button = go.GetComponent<Button>();
            // 创建“entity”，隔离本用例的资源或观测数据。
            var entity = new ButtonEntity { IsAsync = isAsync };
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            typeof(ButtonEntity).GetField("button", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entity, button);
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            typeof(ButtonEntity).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entity, ShowHideAction.Switch);
            // 初始化交互实体与目标绑定，覆盖首次绑定或重新绑定路径。
            entity.Init(target);
            // 根据“isAsync”选择对应测试路径。
            if (isAsync)
            {
                // 为“target.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
                target.ShowAsyncActions += _ => UniTask.CompletedTask;
                // 为“target.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
                target.HideAsyncActions += _ => UniTask.CompletedTask;
            }
            // 返回按钮（button），交给调用方继续配置或断言。
            return button;
        }

        /// <summary>验证空闲时连续两次切换按键依次显示和隐藏。</summary>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_IdleTwoClicks_ShouldReturnToHidden(bool isAsync)
        {
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateSwitchInteractionButton(target, isAsync);
            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            button.onClick.Invoke();
            // 确认目标对象（target）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, target.State, "First idle click must show");
            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            button.onClick.Invoke();
            // 确认目标对象（target）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, target.State, "Second idle click must hide");
        }

        /// <summary>验证排队的显示后切换按执行时状态转为隐藏。</summary>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_QueuedShowThenSwitch_ShouldEndHidden(bool isAsync)
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateSwitchInteractionButton(target, isAsync);
            // 创建“states”，隔离本用例的资源或观测数据。
            var states = new List<UIState>();
            // 为“target.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            target.OnStateChanged += (_, current) => states.Add(current);
            // 为“source.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.BeforeUIShowEvent += () =>
            {
                // 提交目标对象（target）的显示请求，触发实际生命周期。
                target.Show();
                // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
                button.onClick.Invoke();
            };
            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();
            // 确认原来源对象（source）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, source.State, "Source must finish normally");
            // 确认目标对象（target）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, target.State, "Show then Switch must hide at execution time. Actual transitions: " + string.Join(" -> ", states));
        }

        /// <summary>验证排队的两次切换分别在执行时判定方向，最终回到隐藏。</summary>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_QueuedTwoSwitches_ShouldReturnToHidden(bool isAsync)
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateSwitchInteractionButton(target, isAsync);
            // 创建“states”，隔离本用例的资源或观测数据。
            var states = new List<UIState>();
            // 为“target.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            target.OnStateChanged += (_, current) => states.Add(current);
            // 为“source.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.BeforeUIShowEvent += () =>
            {
                // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
                button.onClick.Invoke();
                // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
                button.onClick.Invoke();
            };
            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();
            // 确认目标对象（target）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, target.State, "Two queued switches must restore original state. Actual transitions: " + string.Join(" -> ", states));
        }

        /// <summary>验证排队的隐藏后切换最终显示界面。</summary>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_QueuedHideThenSwitch_ShouldEndShown(bool isAsync)
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateSwitchInteractionButton(target, isAsync);
            // 提交目标对象（target）的显示请求，触发实际生命周期。
            target.Show();
            // 创建“states”，隔离本用例的资源或观测数据。
            var states = new List<UIState>();
            // 为“target.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            target.OnStateChanged += (_, current) => states.Add(current);
            // 为“source.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.BeforeUIShowEvent += () =>
            {
                // 提交目标对象（target）的隐藏请求，检查子树联动与终态。
                target.Hide();
                // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
                button.onClick.Invoke();
            };
            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();
            // 确认目标对象（target）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, target.State, "Hide then Switch must show at execution time. Actual transitions: " + string.Join(" -> ", states));
        }

        /// <summary>验证切换、隐藏、再切换严格按提交顺序执行。</summary>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_QueuedSwitchHideSwitch_ShouldPreserveSubmissionOrder(bool isAsync)
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateSwitchInteractionButton(target, isAsync);
            // 创建“states”，隔离本用例的资源或观测数据。
            var states = new List<UIState>();
            // 为“target.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            target.OnStateChanged += (_, current) => states.Add(current);
            // 为“source.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.BeforeUIShowEvent += () =>
            {
                // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
                button.onClick.Invoke();
                // 提交目标对象（target）的隐藏请求，检查子树联动与终态。
                target.Hide();
                // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
                button.onClick.Invoke();
            };

            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();

            // 判断方向后必须直接执行，不能再排到后续 Hide 请求之后。
            CollectionAssert.AreEqual(new[]
            {
                UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided,
                UIState.Showing, UIState.Showed
            }, states);
        }

        /// <summary>验证交互回调形成的循环也遵守行为执行上限。</summary>
        /// <param name="isAsync">交互实体是否使用异步显隐接口。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void SwitchInteraction_CallbackLoop_ShouldRespectBehaviorExecutionLimit(bool isAsync)
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(2);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("SwitchLoop", CreateObject("SwitchLoopRoot").transform);
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateSwitchInteractionButton(target, isAsync);
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 初始化隐藏次数（hides）为0，作为后续回调的观测基准。
            var hides = 0;
            // 为“target.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            target.AfterUIShowedEvent += () => { shows++; button.onClick.Invoke(); };
            // 为“target.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            target.AfterUIHidedEvent += () => { hides++; button.onClick.Invoke(); };
            // 登记本步骤预期产生的异常日志，区分被测失败与意外错误。
            LogAssert.Expect(LogType.Exception, new Regex("UIRequestLoopException: UI循环调度次数超限"));

            // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
            button.onClick.Invoke();

            // 确认显示次数（shows）等于2。
            Assert.AreEqual(2, shows);
            // 确认隐藏次数（hides）等于2。
            Assert.AreEqual(2, hides);
            // 确认目标对象（target）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, target.State);
            // 创建“independent”并配置测试所需的层级或表现组件。
            var independent = CreateUI("AfterSwitchLoop", CreateObject("IndependentRoot").transform);
            // 提交“independent”的显示请求，触发实际生命周期。
            independent.Show();
            // 确认“independent”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, independent.State, "循环被阻止后，调度器应能继续处理独立请求。");
        }

        /// <summary>验证显示动画等待期间的切换按顺序取消旧显示并执行后续方向。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator SwitchInteraction_DuringPendingShow_ShouldCancelAndSwitchInOrder()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("Source", CreateObject("SourceContainer").transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("Target", CreateObject("TargetContainer").transform);
            // 创建按钮（button）并配置测试所需的层级或表现组件。
            var button = CreateSwitchInteractionButton(target, true);
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 为“target.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            target.ShowAsyncActions += _ => ++shows == 1 ? pending.Task : UniTask.CompletedTask;
            // 启动并保存“initialShow”，随后检查等待、取消与完成结果。
            var initialShow = CaptureException(target.ShowAsync(CancellationToken.None));
            // 确认目标对象（target）的界面状态等于显示过渡态。
            Assert.AreEqual(UIState.Showing, target.State);
            // 为“source.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.BeforeUIShowEvent += () =>
            {
                // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
                button.onClick.Invoke();
                // 从真实控件事件入口触发输入，验证当前实体绑定与方向选择。
                button.onClick.Invoke();
            };

            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();

            // 第一次切换隐藏并取消旧显示，第二次依据最新状态重新显示。
            Assert.AreEqual(UIState.Showed, target.State);
            // 确认显示次数（shows）等于2。
            Assert.AreEqual(2, shows);
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            pending.TrySetResult();
            // 等待“WaitForTask(initialShow)”，再检查操作完成后的状态。
            yield return WaitForTask(initialShow);
            // 确认“initialShow”的异步结果满足约束“Is.InstanceOf<System.OperationCanceledException>()”。
            Assert.That(initialShow.Result, Is.InstanceOf<System.OperationCanceledException>());
            // 确认目标对象（target）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, target.State, "旧显示恢复不得覆盖新一轮生命周期。");
        }
    }
}
