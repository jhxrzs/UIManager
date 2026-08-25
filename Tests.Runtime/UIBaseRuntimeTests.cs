using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace
namespace JH.Test_UIManager
{
    /// <summary>
    ///  UIBase异步生命周期、取消传播和动态层级关系的PlayMode回归测试。
    /// </summary>
    public class Test_UIBase
    {
        /// <summary>
        ///  单个异步用例允许等待的最长实时秒数。
        /// </summary>
        private const float TestTimeout = 3f;

        /// <summary>
        ///  当前用例创建的全部GameObject，供测试结束时统一清理。
        /// </summary>
        private readonly List<GameObject> _createdObjects = new();

        /// <summary>
        ///  每个测试结束后销毁临时层级，避免对象进入后续用例的兄弟关系查询。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            // 反向销毁可优先清理子节点，并避免父节点销毁后再次处理已销毁对象。
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                // Unity对象已经随父节点销毁时会表现为false，不再重复销毁。
                var createdObject = _createdObjects[i];
                if (createdObject) Object.DestroyImmediate(createdObject);
            }

            // 清空托管引用，保证下个用例从全新对象集合开始。
            _createdObjects.Clear();
        }

        /// <summary>
        ///  验证异步显示和隐藏在全部处理器正常完成时分别提交一次After生命周期。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAndHideAsync_CompletedActions_ShouldReachFinalStatesAndInvokeAfterOnce()
        {
            // 放入非UI容器，避免测试场景中的其他根UI参与兄弟关系。
            var container = CreateObject("NormalAsyncRoot");
            var ui = CreateUI("NormalAsyncUI", container.transform);
            var showActionCount = 0;
            var hideActionCount = 0;
            var afterShowCount = 0;
            var afterHideCount = 0;

            // 使用立即完成任务覆盖异步显示处理器的正常组合路径。
            ui.ShowAsyncActions += _ =>
            {
                showActionCount++;
                return JTask.CompletedTask;
            };

            // 使用立即完成任务覆盖异步隐藏处理器的正常组合路径。
            ui.HideAsyncActions += _ =>
            {
                hideActionCount++;
                return JTask.CompletedTask;
            };

            // 分别统计两个完整生命周期的After通知次数。
            ui.AfterUIShowedEvent += () => afterShowCount++;
            ui.AfterUIHidedEvent += () => afterHideCount++;

            // 等待显示流程，任何未捕获异常都会由结果断言报告。
            Task<Exception> showOperation = CaptureException(ui.ShowAsync(CancellationToken.None));
            yield return WaitForTask(showOperation);

            // 显示应无异常完成，并且只运行一次处理器与After回调。
            Assert.IsNull(showOperation.Result, $"异步显示不应产生异常：{showOperation.Result}");
            Assert.AreEqual(UIState.Showed, ui.State, "异步显示完成后状态应为Showed");
            Assert.AreEqual(1, showActionCount, "显示处理器应且只应执行一次");
            Assert.AreEqual(1, afterShowCount, "显示After事件应且只应执行一次");

            // 在同一UI上继续等待隐藏流程，覆盖隐藏侧的无令牌WhenAll调用。
            Task<Exception> hideOperation = CaptureException(ui.HideAsync(CancellationToken.None));
            yield return WaitForTask(hideOperation);

            // 隐藏应无异常完成，并且不应重复显示侧生命周期。
            Assert.IsNull(hideOperation.Result, $"异步隐藏不应产生异常：{hideOperation.Result}");
            Assert.AreEqual(UIState.Hided, ui.State, "异步隐藏完成后状态应为Hided");
            Assert.AreEqual(1, hideActionCount, "隐藏处理器应且只应执行一次");
            Assert.AreEqual(1, afterHideCount, "隐藏After事件应且只应执行一次");
            Assert.AreEqual(1, afterShowCount, "隐藏流程不应重复触发显示After事件");
        }

        /// <summary>
        ///  验证预取消的显示和隐藏不会改变状态、派发事件或启动用户处理器。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAndHideAsync_PreCanceled_ShouldHaveNoSideEffects()
        {
            // 两个对象分别放入非UI容器，保证状态准备过程互不触发兄弟互斥。
            var showContainer = CreateObject("PreCanceledShowRoot");
            var hideContainer = CreateObject("PreCanceledHideRoot");

            // 显示测试对象保持默认Hided状态。
            var showUI = CreateUI("PreCanceledShowUI", showContainer.transform);

            // 隐藏测试对象先同步进入Showed，再开始记录待验证副作用。
            var hideUI = CreateUI("PreCanceledHideUI", hideContainer.transform);
            hideUI.Show();

            // 分别记录显示侧和隐藏侧所有公开可观察副作用。
            var showBeforeCount = 0;
            var showAfterCount = 0;
            var showActionCount = 0;
            var showStateChangedCount = 0;
            var hideBeforeCount = 0;
            var hideAfterCount = 0;
            var hideActionCount = 0;
            var hideStateChangedCount = 0;

            // 预取消时以下显示委托均不得被调用。
            showUI.BeforeUIShowEvent += () => showBeforeCount++;
            showUI.AfterUIShowedEvent += () => showAfterCount++;
            showUI.OnStateChanged += (_, _) => showStateChangedCount++;
            showUI.ShowAsyncActions += _ =>
            {
                showActionCount++;
                return JTask.CompletedTask;
            };

            // 预取消时以下隐藏委托均不得被调用。
            hideUI.BeforeUIHideEvent += () => hideBeforeCount++;
            hideUI.AfterUIHidedEvent += () => hideAfterCount++;
            hideUI.OnStateChanged += (_, _) => hideStateChangedCount++;
            hideUI.HideAsyncActions += _ =>
            {
                hideActionCount++;
                return JTask.CompletedTask;
            };

            // 两个令牌都在调用API之前取消，验证入口处的取消检查。
            using var showTokenSource = new CancellationTokenSource();
            using var hideTokenSource = new CancellationTokenSource();
            showTokenSource.Cancel();
            hideTokenSource.Cancel();

            // 同时观察显示和隐藏结果，避免依赖同步完成细节。
            Task<Exception> showOperation = CaptureException(showUI.ShowAsync(showTokenSource.Token));
            Task<Exception> hideOperation = CaptureException(hideUI.HideAsync(hideTokenSource.Token));
            yield return WaitForTask(Task.WhenAll(showOperation, hideOperation));

            // 两个API都必须通过标准取消异常结束。
            Assert.That(showOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "预取消显示应传播OperationCanceledException");
            Assert.That(hideOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "预取消隐藏应传播OperationCanceledException");

            // 显示对象必须维持调用前状态，且没有任何生命周期副作用。
            Assert.AreEqual(UIState.Hided, showUI.State, "预取消显示不应改变原始状态");
            Assert.AreEqual(0, showBeforeCount, "预取消显示不应触发Before事件");
            Assert.AreEqual(0, showAfterCount, "预取消显示不应触发After事件");
            Assert.AreEqual(0, showActionCount, "预取消显示不应启动用户处理器");
            Assert.AreEqual(0, showStateChangedCount, "预取消显示不应触发状态变化");

            // 隐藏对象也必须维持调用前状态，且没有任何生命周期副作用。
            Assert.AreEqual(UIState.Showed, hideUI.State, "预取消隐藏不应改变原始状态");
            Assert.AreEqual(0, hideBeforeCount, "预取消隐藏不应触发Before事件");
            Assert.AreEqual(0, hideAfterCount, "预取消隐藏不应触发After事件");
            Assert.AreEqual(0, hideActionCount, "预取消隐藏不应启动用户处理器");
            Assert.AreEqual(0, hideStateChangedCount, "预取消隐藏不应触发状态变化");
        }

        /// <summary>
        ///  验证忽略令牌的处理器不会被WhenAll提前遗弃，结束后仍应传播取消且不提交After状态。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_HandlerIgnoresToken_ShouldWaitThenThrowAndSkipAfter()
        {
            // 使用非UI容器隔离关系，并用外部开关控制故意不观察取消令牌的任务。
            var container = CreateObject("IgnoreTokenRoot");
            var ui = CreateUI("IgnoreTokenUI", container.transform);
            var releaseHandler = false;
            var handlerStarted = false;
            var afterShowCount = 0;

            // 该处理器只等待releaseHandler，刻意忽略UI传入的令牌。
            ui.ShowAsyncActions += _ =>
            {
                handlerStarted = true;
                return new JTask(() => releaseHandler);
            };
            ui.AfterUIShowedEvent += () => afterShowCount++;

            // 启动显示后等待一帧，确保处理器已经进入组合任务。
            using var tokenSource = new CancellationTokenSource();
            Task<Exception> showOperation = CaptureException(ui.ShowAsync(tokenSource.Token));
            yield return null;

            // 取消操作，但暂时不允许不合作的处理器完成。
            tokenSource.Cancel();
            yield return null;
            var completedBeforeHandlerReleased = showOperation.IsCompleted;

            // 无论中间断言结果如何都先释放处理器，避免给全局调度器留下永久等待项。
            releaseHandler = true;
            yield return WaitForTask(showOperation);

            // WhenAll只组合任务，不能因为外部令牌取消就提前结束等待。
            Assert.IsTrue(handlerStarted, "显示处理器应在取消前启动");
            Assert.IsFalse(completedBeforeHandlerReleased, "处理器未结束时组合任务不得提前完成");

            // 处理器结束后，UI生命周期检查令牌并阻止完成状态和After事件。
            Assert.That(showOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "忽略令牌的处理器结束后仍应传播操作取消");
            Assert.AreEqual(0, afterShowCount, "已取消显示不得触发After事件");
            Assert.AreNotEqual(UIState.Showed, ui.State, "已取消显示不得提交Showed状态");
        }

        /// <summary>
        ///  验证内部父UI的独立取消会穿过关系组合传播到最外层子UI。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_ParentActionCanceled_ShouldPropagateThroughChildComposition()
        {
            // 非UI容器隔离测试层级，避免Scene根节点参与兄弟查询。
            var container = CreateObject("NestedCancellationRoot");
            var parentUI = CreateUI("NestedCancellationParent", container.transform);
            var childUI = CreateUI("NestedCancellationChild", parentUI.transform);
            var parentAfterCount = 0;
            var childAfterCount = 0;

            // 父UI使用独立令牌取消，子UI调用方令牌保持None。
            using var parentActionTokenSource = new CancellationTokenSource();
            parentUI.ShowAsyncActions += _ => WaitUntilCanceled(parentActionTokenSource.Token);
            childUI.ShowAsyncActions += _ => JTask.CompletedTask;
            parentUI.AfterUIShowedEvent += () => parentAfterCount++;
            childUI.AfterUIShowedEvent += () => childAfterCount++;

            // 启动子UI会递归启动直接父UI的异步显示。
            Task<Exception> childOperation = CaptureException(childUI.ShowAsync(CancellationToken.None));
            yield return null;

            // 仅取消父UI内部处理器，验证取消不能被父UI公开入口吞掉。
            parentActionTokenSource.Cancel();
            yield return WaitForTask(childOperation);

            // 内部取消必须使整条组合链失败，任何层级都不能提交After。
            Assert.That(childOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "父UI内部取消必须传播到子UI调用方");
            Assert.AreEqual(0, parentAfterCount, "取消后的父UI不得触发After事件");
            Assert.AreEqual(0, childAfterCount, "父UI取消后的子UI不得触发After事件");
        }

        /// <summary>
        ///  验证三层UI按根到直接父节点顺序显示，每一层生命周期均执行一次。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_ThreeLevelHierarchy_ShouldInvokeEachLevelOnce()
        {
            // 构建容器、祖父UI、父UI、子UI四级Transform结构。
            var container = CreateObject("ThreeLevelRoot");
            var grandParentUI = CreateUI("GrandParentUI", container.transform);
            var parentUI = CreateUI("ParentUI", grandParentUI.transform);
            var childUI = CreateUI("ChildUI", parentUI.transform);
            var panels = new[] { grandParentUI, parentUI, childUI };
            var beforeCounts = new int[panels.Length];
            var actionCounts = new int[panels.Length];
            var afterCounts = new int[panels.Length];
            var releaseGrandParent = false;

            // 为每一层注册独立计数器，避免仅凭最终状态遗漏重复调用。
            for (var i = 0; i < panels.Length; i++)
            {
                var index = i;
                panels[index].BeforeUIShowEvent += () => beforeCounts[index]++;
                panels[index].ShowAsyncActions += _ =>
                {
                    actionCounts[index]++;

                    // 根祖先保持挂起，用于证明父节点不会与祖先并发重复启动。
                    return index == 0
                        ? new JTask(() => releaseGrandParent)
                        : JTask.CompletedTask;
                };
                panels[index].AfterUIShowedEvent += () => afterCounts[index]++;
            }

            // 只请求最深层子UI，完整祖先快照应先启动根祖先。
            Task<Exception> showOperation = CaptureException(childUI.ShowAsync(CancellationToken.None));
            yield return null;

            // 先记录挂起期间的执行次数，再释放闸门，避免失败断言遗留永久等待项。
            var grandParentCountBeforeRelease = actionCounts[0];
            var parentCountBeforeRelease = actionCounts[1];
            releaseGrandParent = true;

            // 根祖先必须只启动一次，直接父UI必须等祖先完成后才能开始。
            Assert.AreEqual(1, grandParentCountBeforeRelease,
                "根祖先挂起期间只能启动一次显示处理器");
            Assert.AreEqual(0, parentCountBeforeRelease,
                "直接父UI不得在根祖先完成前并发启动");

            // 解除根祖先闸门后再等待完整父链收口。
            yield return WaitForTask(showOperation);

            // 整条三层生命周期应无异常完成。
            Assert.IsNull(showOperation.Result, $"三层异步显示不应产生异常：{showOperation.Result}");
            for (var i = 0; i < panels.Length; i++)
            {
                // 每层都必须完成，并且Before、处理器、After均只执行一次。
                Assert.AreEqual(UIState.Showed, panels[i].State, $"第{i}层UI应完成显示");
                Assert.AreEqual(1, beforeCounts[i], $"第{i}层Before事件应只执行一次");
                Assert.AreEqual(1, actionCounts[i], $"第{i}层显示处理器应只执行一次");
                Assert.AreEqual(1, afterCounts[i], $"第{i}层After事件应只执行一次");
            }
        }

        /// <summary>
        ///  验证既有兄弟UI可以发现运行时新增节点，并在移除组件后不保留旧引用。
        /// </summary>
        [Test]
        public void SiblingRelation_RuntimeAddAndRemove_ShouldUseCurrentHierarchy()
        {
            // 所有UI位于同一非UI容器下，默认互斥组均为零。
            var container = CreateObject("SiblingRoot");
            var firstUI = CreateUI("FirstSibling", container.transform);
            var secondUI = CreateUI("SecondSibling", container.transform);

            // 先让两个既有节点执行兄弟查询，模拟关系已经被使用过。
            firstUI.Show();
            secondUI.Show();
            Assert.AreEqual(UIState.Hided, firstUI.State, "后显示的同组兄弟应隐藏先显示节点");
            Assert.AreEqual(UIState.Showed, secondUI.State, "第二个兄弟应处于显示状态");

            // 在运行时新增第三个兄弟，并让它成为当前显示节点。
            var addedUI = CreateUI("RuntimeAddedSibling", container.transform);
            addedUI.Show();
            Assert.AreEqual(UIState.Hided, secondUI.State, "新增兄弟应立即参与当前互斥关系");

            // 再显示早已存在的firstUI，验证它会重新推导并隐藏新增节点。
            firstUI.Show();
            Assert.AreEqual(UIState.Hided, addedUI.State, "既有节点应发现并隐藏运行时新增兄弟");
            Assert.AreEqual(UIState.Showed, firstUI.State, "既有节点应正常完成本次显示");

            // 仅移除第三个节点的UI组件，保留其Transform验证不存在陈旧UI引用。
            Object.DestroyImmediate(addedUI);
            firstUI.Hide();

            // 重新显示不得访问已销毁组件，也必须保持正常状态提交。
            Assert.DoesNotThrow(firstUI.Show, "移除UI组件后兄弟查询不得访问陈旧节点");
            Assert.AreEqual(UIState.Showed, firstUI.State, "移除兄弟UI后现有节点仍应正常显示");
        }

        /// <summary>
        ///  验证运行时新增直接子UI和换父后，旧父与新父均依据当前Transform层级工作。
        /// </summary>
        [Test]
        public void ChildRelation_RuntimeAddAndReparent_ShouldUseCurrentHierarchy()
        {
            // 两个父UI放入不同非UI分支，避免父UI之间发生互斥隐藏。
            var firstBranch = CreateObject("FirstParentBranch");
            var secondBranch = CreateObject("SecondParentBranch");
            var firstParent = CreateUI("FirstParent", firstBranch.transform);
            var secondParent = CreateUI("SecondParent", secondBranch.transform);

            // 先让旧父节点在没有子UI时执行一次完整隐藏，确保后续新增必须动态发现。
            firstParent.Show();
            firstParent.Hide();

            // 运行时新增直接子UI，显示子UI应递归显示其当前直接父UI。
            var childUI = CreateUI("RuntimeChild", firstParent.transform);
            childUI.Show();
            Assert.AreEqual(UIState.Showed, firstParent.State, "新增子UI显示时应显示当前直接父UI");

            // 父UI隐藏时应立即发现并隐藏运行时新增的直接子UI。
            firstParent.Hide();
            Assert.AreEqual(UIState.Hided, childUI.State, "父UI应发现并隐藏运行时新增子UI");

            // 再次显示子UI后，把它从旧父节点移动到新父节点。
            childUI.Show();
            childUI.transform.SetParent(secondParent.transform, false);

            // 旧父节点不再拥有该子UI，隐藏旧父不能影响已经换父的子UI。
            firstParent.Hide();
            Assert.AreEqual(UIState.Showed, childUI.State, "换父后旧父UI不得继续隐藏该子UI");

            // 重新触发子UI显示，使其依据新层级递归显示新父节点。
            childUI.Hide();
            childUI.Show();
            Assert.AreEqual(UIState.Hided, firstParent.State, "换父后不应重新显示旧父UI");
            Assert.AreEqual(UIState.Showed, secondParent.State, "换父后应显示新的直接父UI");

            // 新父节点隐藏必须依据当前层级找到并隐藏该子UI。
            secondParent.Hide();
            Assert.AreEqual(UIState.Hided, childUI.State, "新父UI应隐藏当前直接子UI");
        }

        /// <summary>
        ///  验证已显示父UI换到隐藏祖先下后，显示子UI仍会修复完整祖先链。
        /// </summary>
        [Test]
        public void ParentRelation_ShownParentReparentedUnderHiddenAncestor_ShouldShowNewAncestor()
        {
            // shownParent先位于没有UI祖先的分支并进入Showed状态。
            var originalBranch = CreateObject("OriginalParentBranch");
            var shownParent = CreateUI("AlreadyShownParent", originalBranch.transform);
            shownParent.Show();

            // 新祖先保持默认Hided，随后把已显示父UI整体移动到其下方。
            var ancestorBranch = CreateObject("HiddenAncestorBranch");
            var hiddenAncestor = CreateUI("HiddenAncestor", ancestorBranch.transform);
            shownParent.transform.SetParent(hiddenAncestor.transform, false);

            // 子UI位于已显示父UI下；直接父Show会短路，因此必须依赖完整祖先快照。
            var childUI = CreateUI("ChildUnderShownParent", shownParent.transform);
            childUI.Show();

            // 显示子UI应先显示新祖先，再确认已显示父UI和当前子UI。
            Assert.AreEqual(UIState.Showed, hiddenAncestor.State,
                "已显示父UI换父后，显示子UI必须补齐新的隐藏祖先");
            Assert.AreEqual(UIState.Showed, shownParent.State, "换父后的直接父UI应保持Showed");
            Assert.AreEqual(UIState.Showed, childUI.State, "子UI应完成显示");
        }

        /// <summary>
        ///  验证异步隐藏接口继承隐藏生命周期基础接口，而不是显示生命周期接口。
        /// </summary>
        [Test]
        public void IHideUIAsync_ShouldInheritIHideUIBase()
        {
            // 接口继承关系属于公开编译契约，需要独立于UIBase多接口实现进行验证。
            Assert.IsTrue(typeof(IHideUIBase).IsAssignableFrom(typeof(IHideUIAsync)),
                "IHideUIAsync必须继承IHideUIBase以公开隐藏生命周期事件");
        }

        /// <summary>
        ///  验证OnStateChanged派发时State属性已经提交为回调参数中的新状态。
        /// </summary>
        [Test]
        public void OnStateChanged_CallbackShouldReadNewStateFromProperty()
        {
            // 默认Hided状态依次执行同步显示和同步隐藏，共产生四次状态变化。
            var container = CreateObject("StateChangedRoot");
            var ui = CreateUI("StateChangedUI", container.transform);
            var previousStates = new List<UIState>();
            var reportedStates = new List<UIState>();
            var readableStates = new List<UIState>();

            // 回调内立即读取State，记录事件参数和属性观察值供统一断言。
            ui.OnStateChanged += (previousState, newState) =>
            {
                previousStates.Add(previousState);
                reportedStates.Add(newState);
                readableStates.Add(ui.State);
            };

            // 执行完整同步显隐生命周期。
            ui.Show();
            ui.Hide();

            // 事件参数应描述连续且完整的状态转换序列。
            CollectionAssert.AreEqual(
                new[] { UIState.Hided, UIState.Showing, UIState.Showed, UIState.Hiding },
                previousStates,
                "OnStateChanged应报告每次转换前的状态");
            CollectionAssert.AreEqual(
                new[] { UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided },
                reportedStates,
                "OnStateChanged应报告每次转换后的状态");

            // 回调中的State属性必须与事件报告的新状态完全一致。
            CollectionAssert.AreEqual(reportedStates, readableStates,
                "OnStateChanged回调读取State时应得到已经提交的新状态");
        }

        /// <summary>
        ///  创建测试GameObject并登记到统一清理集合。
        /// </summary>
        /// <param name="name">测试对象名称。</param>
        /// <param name="parent">可选的父Transform。</param>
        /// <returns>创建完成的GameObject。</returns>
        private GameObject CreateObject(string name, Transform parent = null)
        {
            // 使用普通GameObject即可验证UIBase的层级关系，无需Canvas参与。
            var createdObject = new GameObject(name);
            if (parent) createdObject.transform.SetParent(parent, false);
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>
        ///  创建禁用自动Start初始化的UIBase测试节点。
        /// </summary>
        /// <param name="name">UI测试对象名称。</param>
        /// <param name="parent">可选的父Transform。</param>
        /// <returns>可通过公开API驱动的UIBase实例。</returns>
        private UIBase CreateUI(string name, Transform parent = null)
        {
            // 禁用组件只阻止Unity生命周期自动调用，不影响公开方法和层级查询。
            var ui = CreateObject(name, parent).AddComponent<UIBase>();
            ui.enabled = false;
            return ui;
        }

        /// <summary>
        ///  等待JTask并把异常转换为可在UnityTest主协程中断言的结果。
        /// </summary>
        /// <param name="task">需要观察的JTask。</param>
        /// <returns>正常完成时返回null，失败或取消时返回对应异常。</returns>
        private static async Task<Exception> CaptureException(JTask task)
        {
            try
            {
                // 只等待一次，符合JTask单次消费约束。
                await task;
                return null;
            }
            catch (Exception exception)
            {
                // 异常交给UnityTest主协程做精确类型和副作用断言。
                return exception;
            }
        }

        /// <summary>
        ///  等待系统任务完成，并使用实时钟提供明确超时失败信息。
        /// </summary>
        /// <param name="task">需要等待的系统任务。</param>
        /// <param name="timeout">最长等待实时秒数。</param>
        /// <returns>供Unity Test Runner逐帧推进的迭代器。</returns>
        private static IEnumerator WaitForTask(Task task, float timeout = TestTimeout)
        {
            // 实时钟不受其他测试修改Time.timeScale的影响。
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;

            // 超时通常意味着取消没有传播或组合任务遗留了未完成处理器。
            Assert.IsTrue(task.IsCompleted, $"异步测试任务未在{timeout}秒内完成");

            // CaptureException正常不会Fault，此断言用于暴露测试桥接器自身的问题。
            if (task.IsFaulted) Assert.Fail(task.Exception?.ToString());
        }

        /// <summary>
        ///  创建只会通过指定令牌取消的JTask处理器。
        /// </summary>
        /// <param name="cancellationToken">控制任务取消的独立令牌。</param>
        /// <returns>令牌取消后抛出OperationCanceledException的任务。</returns>
        private static async JTask WaitUntilCanceled(CancellationToken cancellationToken)
        {
            // 永不自然满足的条件确保该任务只能通过令牌结束。
            await JTask.WaitUntil(() => false, cancellationToken);
        }
    }
}
