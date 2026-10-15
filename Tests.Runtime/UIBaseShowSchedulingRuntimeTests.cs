using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace
namespace JH.Test_UIManager
{
    /// <summary>
    ///  UIBase父链准入和同父同组显示抢占调度的PlayMode回归测试。
    /// </summary>
    public partial class Test_UIBase
    {
        /// <summary>
        ///  验证严格父链策略会对未完成显示的父UI执行零副作用拒绝。
        /// </summary>
        /// <param name="parentState">发起子UI显示请求时的父UI状态。</param>
        [TestCase(UIState.Hided)]
        [TestCase(UIState.Showing)]
        [TestCase(UIState.Hiding)]
        public void Show_RequireParentChainShowed_ParentNotReady_ShouldRejectWithoutSideEffects(
            UIState parentState)
        {
            // 父UI位于非UI容器下，子UI和兄弟UI都使用其作为唯一直接父节点。
            var container = CreateObject($"StrictParent{parentState}Root");
            var parentUI = CreateUI($"StrictParent{parentState}", container.transform);
            var targetUI = CreateUI($"StrictTarget{parentState}", parentUI.transform);
            var siblingUI = CreateUI($"StrictSibling{parentState}", parentUI.transform);
            targetUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 使父UI处于真实初始化但未Showed的测试状态。
            SetUIBasePrivateField(parentUI, "state", parentState);
            SetUIBasePrivateField(parentUI, "_isUIStateInitialized", true);

            // 预置一个同组Showed兄弟，验证拒绝发生在隐藏兄弟之前。
            SetUIBasePrivateField(siblingUI, "state", UIState.Showed);
            SetUIBasePrivateField(siblingUI, "_isUIStateInitialized", true);

            // 记录目标UI所有可观察的显示生命周期副作用。
            var beforeCount = 0;
            var actionCount = 0;
            var afterCount = 0;
            var stateChangedCount = 0;
            targetUI.BeforeUIShowEvent += () => beforeCount++;
            targetUI.ShowActions += () => actionCount++;
            targetUI.AfterUIShowedEvent += () => afterCount++;
            targetUI.OnStateChanged += (_, _) => stateChangedCount++;

            // 父链未就绪时请求显示目标UI。
            targetUI.Show();

            // 目标、父节点和兄弟都必须保持请求前的状态。
            Assert.AreEqual(UIState.Hided, targetUI.State, "父链未就绪时目标UI应保持Hided");
            Assert.AreEqual(parentState, parentUI.State, "被拒绝的子UI请求不得修复或改变父UI");
            Assert.AreEqual(UIState.Showed, siblingUI.State, "准入拒绝前不得先隐藏同组兄弟");

            // 零副作用拒绝不得派发任何显示事件或状态变化。
            Assert.AreEqual(0, beforeCount, "父链准入拒绝不应触发Before事件");
            Assert.AreEqual(0, actionCount, "父链准入拒绝不应启动显示行为");
            Assert.AreEqual(0, afterCount, "父链准入拒绝不应触发After事件");
            Assert.AreEqual(0, stateChangedCount, "父链准入拒绝不应改变状态");
        }

        /// <summary>
        ///  验证未执行过实际生命周期的序列化Showed父UI不会被误判为已就绪。
        /// </summary>
        [Test]
        public void Show_RequireParentChainShowed_UninitializedSerializedShowedParent_ShouldReject()
        {
            // 父UI仅写入序列化Showed状态，不写入运行时初始化标记。
            var container = CreateObject("UninitializedShowedParentRoot");
            var parentUI = CreateUI("UninitializedShowedParent", container.transform);
            var childUI = CreateUI("StrictChildOfUninitializedParent", parentUI.transform);
            SetUIBasePrivateField(parentUI, "state", UIState.Showed);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 子UI的严格直接显示请求必须被拒绝。
            childUI.Show();

            // 序列化假Showed不能被当作已完成的父显示生命周期。
            Assert.AreEqual(UIState.Hided, childUI.State, "未初始化的Showed父UI不应满足严格父链准入");
            Assert.AreEqual(UIState.Showed, parentUI.State, "准入检查不应修改父UI的序列化状态");
        }

        /// <summary>
        ///  验证严格父链策略在缺少直接UI父节点时拒绝显示。
        /// </summary>
        [Test]
        public void Show_RequireParentChainShowed_WithoutUIParent_ShouldReject()
        {
            // 普通GameObject容器不属于连续UIBase父链。
            var container = CreateObject("StrictChildWithoutUIParentRoot");
            var childUI = CreateUI("StrictChildWithoutUIParent", container.transform);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 没有可验证的直接UI父节点时请求显示。
            childUI.Show();

            // 空父链不能以“全部满足”的方式通过严格准入。
            Assert.AreEqual(UIState.Hided, childUI.State, "缺少直接UI父节点时严格显示应被拒绝");
        }

        /// <summary>
        ///  验证完整连续父UI链真实Showed后严格子UI可显示且不会重复调度父链。
        /// </summary>
        [Test]
        public void Show_RequireParentChainShowed_AllParentsReady_ShouldAllowWithoutRedispatchingParents()
        {
            // 构建祖父、直接父和严格子UI的连续UIBase链。
            var container = CreateObject("ReadyStrictParentChainRoot");
            var grandParentUI = CreateUI("ReadyStrictGrandParent", container.transform);
            var parentUI = CreateUI("ReadyStrictParent", grandParentUI.transform);
            var childUI = CreateUI("ReadyStrictChild", parentUI.transform);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 记录父链显示行为，先使直接父通过默认策略补齐祖父。
            var grandParentShowCount = 0;
            var parentShowCount = 0;
            grandParentUI.ShowActions += () => grandParentShowCount++;
            parentUI.ShowActions += () => parentShowCount++;
            parentUI.Show();

            // 父链全部已完成Showed后请求严格子UI显示。
            childUI.Show();

            // 子UI应完成显示，两层父UI都不得被重复派发。
            Assert.AreEqual(UIState.Showed, childUI.State, "完整父链Showed后严格子UI应允许显示");
            Assert.AreEqual(1, grandParentShowCount, "严格子UI不应重复显示祖父UI");
            Assert.AreEqual(1, parentShowCount, "严格子UI不应重复显示直接父UI");
        }

        /// <summary>
        ///  验证异步严格显示被准入拒绝时正常结束且不产生副作用。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_RequireParentChainShowed_ParentNotReady_ShouldCompleteWithoutSideEffects()
        {
            // 父UI保持初始Hided，子UI开启严格父链策略。
            var container = CreateObject("StrictAsyncRejectedRoot");
            var parentUI = CreateUI("StrictAsyncRejectedParent", container.transform);
            var childUI = CreateUI("StrictAsyncRejectedChild", parentUI.transform);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            SetUIBasePrivateField(parentUI, "_isUIStateInitialized", true);

            // 显示处理器计数用于确认拒绝发生在任务启动之前。
            var showActionCount = 0;
            childUI.ShowAsyncActions += _ =>
            {
                showActionCount++;
                return UniTask.CompletedTask;
            };

            // 将UniTask异常转为可在UnityTest主协程中断言的结果。
            Task<Exception> operation = CaptureException(childUI.ShowAsync(CancellationToken.None));
            yield return WaitForTask(operation);

            // 正常仲裁拒绝不是取消或故障，并且不得启动用户处理器。
            Assert.IsNull(operation.Result, $"准入拒绝的ShowAsync应正常结束：{operation.Result}");
            Assert.AreEqual(UIState.Hided, childUI.State, "被拒绝的异步显示应保持Hided");
            Assert.AreEqual(0, showActionCount, "被拒绝的异步显示不应启动处理器");
        }

        /// <summary>
        ///  验证父UI自身显示事务可级联严格子UI，不会要求父UI提前提交Showed。
        /// </summary>
        [Test]
        public void Show_ParentCascade_ShouldBypassRequiredParentAdmission()
        {
            // 严格子UI同时配置随父显示，该组合表示“禁止外部拉起父页”。
            var container = CreateObject("StrictParentCascadeRoot");
            var parentUI = CreateUI("StrictParentCascadeParent", container.transform);
            var childUI = CreateUI("StrictParentCascadeChild", parentUI.transform);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            childUI.ShowWithParent = true;

            // 记录子UI显示行为期间父UI的包围状态。
            var parentStateDuringChildShow = UIState.Hided;
            childUI.ShowActions += () => parentStateDuringChildShow = parentUI.State;

            // 仅显示父UI，子UI应由ParentCascade专用入口处理。
            parentUI.Show();

            // 子UI生命周期属于父显示事务内部，因此观察到的父状态仍为Showing。
            Assert.AreEqual(UIState.Showed, parentUI.State, "父UI应完成显示");
            Assert.AreEqual(UIState.Showed, childUI.State, "父级联应允许显示严格子UI");
            Assert.AreEqual(UIState.Showing, parentStateDuringChildShow,
                "严格父链策略不应迫使父UI提前提交Showed");
        }

        /// <summary>
        ///  验证严格子UI在父级联显示途中换父后不会继续沿用原父事务豁免。
        /// </summary>
        [Test]
        public void Show_ParentCascade_StrictChildReparentedDuringAction_ShouldStopCascadeAdmission()
        {
            // 严格子UI由第一父UI级联显示，第二父UI保持已初始化Hided作为换父目标。
            var container = CreateObject("ParentCascadeReparentRoot");
            var cascadeParent = CreateUI("ParentCascadeReparentSource", container.transform);
            var hiddenParent = CreateUI("ParentCascadeReparentHiddenTarget", container.transform);
            var childUI = CreateUI("ParentCascadeReparentStrictChild", cascadeParent.transform);
            childUI.ShowWithParent = true;
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            SetUIBasePrivateField(hiddenParent, "_isUIStateInitialized", true);

            // 子UI自身显示行为把它移动到另一个未显示父UI，离开原父级联事务关系。
            childUI.ShowActions += () => childUI.transform.SetParent(hiddenParent.transform, false);

            // 显示来源父UI会启动子UI级联，但换父后必须停止对子UI的父链豁免。
            cascadeParent.Show();

            // 原父可以完成自身显示，换父子UI则必须收口且不得拉起隐藏目标父UI。
            Assert.AreEqual(UIState.Showed, cascadeParent.State, "来源父UI应完成自身显示生命周期");
            Assert.AreEqual(UIState.Hided, hiddenParent.State, "父级联换父不得显示新的目标父UI");
            Assert.AreEqual(UIState.Hided, childUI.State, "离开来源父UI后严格子UI应停止级联并收口");
            Assert.AreEqual(hiddenParent.transform, childUI.transform.parent,
                "调度器不应撤销子UI显示行为完成的Transform换父");
        }

        /// <summary>
        ///  验证来源父UI事务在子行为中被取代后，即使子UI移回原父也不再享有级联豁免。
        /// </summary>
        [Test]
        public void Show_ParentCascade_SourceTransactionReplacedDuringChildAction_ShouldHideChild()
        {
            // 临时普通容器用于让父Hide无法通过当前父子关系直接隐藏测试子UI。
            var container = CreateObject("ParentCascadeVersionRoot");
            var temporaryContainer = CreateObject("ParentCascadeVersionTemporaryRoot");
            var parentUI = CreateUI("ParentCascadeVersionParent", container.transform);
            var childUI = CreateUI("ParentCascadeVersionChild", parentUI.transform);
            childUI.ShowWithParent = true;
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 子行为先离开父节点、取代父显示事务，再移回原父节点以单独验证事务版本。
            childUI.ShowActions += () =>
            {
                childUI.transform.SetParent(temporaryContainer.transform, false);
                parentUI.Hide();
                childUI.transform.SetParent(parentUI.transform, false);
            };

            // 父UI启动级联后会在子行为中被同步隐藏并使原显示操作版本失效。
            parentUI.Show();

            // 仅恢复直接父关系不能复活旧事务，父子都必须稳定收口为Hided。
            Assert.AreEqual(UIState.Hided, parentUI.State, "子行为取代来源事务后父UI应保持Hided");
            Assert.AreEqual(UIState.Hided, childUI.State, "失效父事务不得继续提交严格子UI为Showed");
            Assert.AreEqual(parentUI.transform, childUI.transform.parent,
                "测试应在恢复原直接父关系后验证事务版本失效");
        }

        /// <summary>
        ///  验证严格更高优先级UI可抢占低优先级，反向低优先级请求被零副作用拒绝。
        /// </summary>
        [Test]
        public void Show_MutexPriority_HigherShouldPreemptLowerAndLowerShouldNotPreemptHigher()
        {
            // 两个UI共享直接父节点和默认互斥组，只配置不同抢占优先级。
            var container = CreateObject("PriorityPreemptionRoot");
            var lowUI = CreateUI("PriorityLow", container.transform);
            var highUI = CreateUI("PriorityHigh", container.transform);
            highUI.MutexPriority = 1;

            // 高优先级尚处Hided时不预占组，低优先级应先正常显示。
            var lowShowCount = 0;
            lowUI.ShowActions += () => lowShowCount++;
            lowUI.Show();
            Assert.AreEqual(UIState.Showed, lowUI.State, "Hided的高优先级UI不应阻止低优先级显示");

            // 高优先级后请求应隐藏低优先级并取得当前显示权。
            highUI.Show();
            Assert.AreEqual(UIState.Hided, lowUI.State, "高优先级UI应抢占并隐藏低优先级UI");
            Assert.AreEqual(UIState.Showed, highUI.State, "高优先级UI应完成显示");

            // 高优先级活跃期间再次请求低UI，显示行为计数必须保持不变。
            lowUI.Show();
            Assert.AreEqual(UIState.Hided, lowUI.State, "低优先级UI不得反向抢占活跃高优先级UI");
            Assert.AreEqual(UIState.Showed, highUI.State, "被拒绝的低优先级请求不得隐藏高优先级UI");
            Assert.AreEqual(1, lowShowCount, "低优先级被拒绝时不应重复启动显示行为");
        }

        /// <summary>
        ///  验证相等非零优先级仍保留后请求获胜的原有互斥语义。
        /// </summary>
        [Test]
        public void Show_MutexPriority_EqualNonZeroShouldKeepLastRequestWins()
        {
            // 两个同父同组UI设置完全相同的非零优先级。
            var container = CreateObject("EqualPriorityRoot");
            var firstUI = CreateUI("EqualPriorityFirst", container.transform);
            var secondUI = CreateUI("EqualPrioritySecond", container.transform);
            firstUI.MutexPriority = 2;
            secondUI.MutexPriority = 2;

            // 依次显示两个UI，后请求者应取代先请求者。
            firstUI.Show();
            secondUI.Show();

            // 相等优先级不应使用大于等于比较而阻止更新请求。
            Assert.AreEqual(UIState.Hided, firstUI.State, "相等优先级的先请求UI应被隐藏");
            Assert.AreEqual(UIState.Showed, secondUI.State, "相等优先级的后请求UI应获胜");
        }

        /// <summary>
        ///  验证不同互斥组之间不比较抢占优先级。
        /// </summary>
        [Test]
        public void Show_MutexPriority_DifferentGroupsShouldNotCompare()
        {
            // 低优先级UI使用默认组0，高优先级UI改为组1。
            var container = CreateObject("DifferentPriorityGroupsRoot");
            var lowUI = CreateUI("DifferentGroupLow", container.transform);
            var highUI = CreateUI("DifferentGroupHigh", container.transform);
            highUI.MutexPriority = 10;
            SetUIBasePrivateField(highUI, "mutexGroup", 1);

            // 两个UI先后显示时不应产生跨组隐藏或准入拒绝。
            highUI.Show();
            lowUI.Show();

            // 互斥组边界优先于数值优先级。
            Assert.AreEqual(UIState.Showed, lowUI.State, "不同组的低优先级UI应正常显示");
            Assert.AreEqual(UIState.Showed, highUI.State, "不同组的高优先级UI不应被隐藏");
        }

        /// <summary>
        ///  验证显示行为运行期间换父产生的同组兄弟会在提交Showed前重新参与互斥。
        /// </summary>
        [Test]
        public void Show_MutexPriority_SiblingReparentedDuringAction_ShouldReconcileCurrentHierarchy()
        {
            // 请求UI和已显示UI起初位于两个不同父节点下，不构成互斥兄弟。
            var requestContainer = CreateObject("DynamicReparentRequestRoot");
            var sourceContainer = CreateObject("DynamicReparentSourceRoot");
            var requestUI = CreateUI("DynamicReparentRequest", requestContainer.transform);
            var movedUI = CreateUI("DynamicReparentShownSibling", sourceContainer.transform);
            movedUI.Show();

            // 当前UI显示行为执行时，把已显示的相等优先级UI移动到同一直接父节点下。
            requestUI.ShowActions += () => movedUI.transform.SetParent(requestContainer.transform, false);

            // 发起显示后，调度器必须使用行为完成后的最新Transform关系重新协调。
            requestUI.Show();

            // 相等优先级保留后请求获胜，不能因初始快照不同而留下两个Showed兄弟。
            Assert.AreEqual(UIState.Showed, requestUI.State, "动态换父后当前后请求UI应完成显示");
            Assert.AreEqual(UIState.Hided, movedUI.State, "换父进入同组的先显示UI应被重新协调隐藏");
            Assert.AreEqual(requestContainer.transform, movedUI.transform.parent,
                "测试结束时竞争UI应仍位于请求UI的直接父节点下");
        }

        /// <summary>
        ///  验证提交Showed触发的状态回调换父后仍会执行最终优先级协调。
        /// </summary>
        [Test]
        public void Show_MutexPriority_HigherSiblingReparentedByShowedCallback_ShouldHideCurrentUI()
        {
            // 低UI与已显示高UI起初位于不同父节点，因此入口准入应正常通过。
            var requestContainer = CreateObject("ShowedCallbackRequestRoot");
            var sourceContainer = CreateObject("ShowedCallbackSourceRoot");
            var lowUI = CreateUI("ShowedCallbackLow", requestContainer.transform);
            var highUI = CreateUI("ShowedCallbackHigh", sourceContainer.transform);
            highUI.MutexPriority = 1;
            highUI.Show();

            // 低UI刚提交Showed时，把高UI移动为它的同父同组兄弟，制造提交后的关系变化。
            lowUI.OnStateChanged += (_, currentState) =>
            {
                if (currentState is UIState.Showed)
                    highUI.transform.SetParent(requestContainer.transform, false);
            };

            // 低UI显示流程必须在返回前发现新出现的高优先级持权兄弟。
            lowUI.Show();

            // 高UI继续持权，低UI不能以已经触发Showed回调为由留下冲突稳定状态。
            Assert.AreEqual(UIState.Hided, lowUI.State, "Showed回调换父后低UI应被最终协调隐藏");
            Assert.AreEqual(UIState.Showed, highUI.State, "动态进入当前组的高优先级UI应保持Showed");
        }

        /// <summary>
        ///  验证严格UI在Showed状态回调中换到未显示父链后会被最终准入检查收口。
        /// </summary>
        [Test]
        public void Show_RequireParentChainShowed_ReparentedByShowedCallback_ShouldHideCurrentUI()
        {
            // 就绪父UI允许初始请求，另一个已初始化Hided父UI用于制造提交后的父链失效。
            var container = CreateObject("StrictShowedCallbackRoot");
            var readyParent = CreateUI("StrictShowedCallbackReadyParent", container.transform);
            var hiddenParent = CreateUI("StrictShowedCallbackHiddenParent", container.transform);
            var childUI = CreateUI("StrictShowedCallbackChild", readyParent.transform);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            SetUIBasePrivateField(hiddenParent, "_isUIStateInitialized", true);
            readyParent.Show();

            // 子UI提交Showed的状态通知中立即换到未显示父UI下，模拟业务动态调整层级。
            childUI.OnStateChanged += (_, currentState) =>
            {
                if (currentState is UIState.Showed)
                    childUI.transform.SetParent(hiddenParent.transform, false);
            };

            // 初始父链满足准入，但最终返回结果必须以回调后的实际父链为准。
            childUI.Show();

            // 严格子UI不能在Hided父UI下留下Showed状态，且不应反向显示新父UI。
            Assert.AreEqual(UIState.Hided, childUI.State, "Showed回调换到未就绪父链后子UI应收口Hided");
            Assert.AreEqual(UIState.Hided, hiddenParent.State, "最终准入失败不得反向拉起新父UI");
            Assert.AreEqual(hiddenParent.transform, childUI.transform.parent,
                "调度收口不应撤销业务回调完成的Transform换父");
        }

        /// <summary>
        ///  验证高优先级UI正在异步显示时会拒绝低优先级请求且不取消自身。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_MutexPriority_HigherShowingShouldRejectLowerWithoutCancelingHigher()
        {
            // 高优先级UI的异步行为由门闩挂起，低UI使用默认优先级。
            var container = CreateObject("HigherShowingBlocksLowerRoot");
            var lowUI = CreateUI("BlockedLowerUI", container.transform);
            var highUI = CreateUI("ShowingHigherUI", container.transform);
            highUI.MutexPriority = 1;
            var releaseHighAction = false;
            highUI.ShowAsyncActions += _ => UniTask.WaitUntil(() => releaseHighAction);

            // 先启动高优先级显示并等待它进入Showing。
            Task<Exception> highOperation = CaptureException(highUI.ShowAsync(CancellationToken.None));
            Task<Exception> lowOperation = null;
            var highStateBeforeRelease = UIState.Hided;
            try
            {
                yield return null;

                // 高UI持有显示预约期间请求低UI，被拒绝的任务应正常完成。
                lowOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));
                yield return WaitForTask(lowOperation);
                highStateBeforeRelease = highUI.State;
            }
            finally
            {
                // 中间等待或被测调用失败时同样释放忽略取消的门闩任务。
                releaseHighAction = true;
            }

            // 门闩释放后完整观察高优先级显示收口。
            yield return WaitForTask(highOperation);

            // 低UI不产生副作用，高UI不被取消并最终提交Showed。
            Assert.IsNull(lowOperation.Result, $"优先级准入拒绝应正常结束：{lowOperation.Result}");
            Assert.AreEqual(UIState.Hided, lowUI.State, "高优先级Showing时低UI应保持Hided");
            Assert.AreEqual(UIState.Showing, highStateBeforeRelease, "低UI请求不得取消高优先级Showing");
            Assert.IsNull(highOperation.Result, $"高优先级显示不应被低UI取消：{highOperation.Result}");
            Assert.AreEqual(UIState.Showed, highUI.State, "释放处理器后高优先级UI应完成显示");
        }

        /// <summary>
        ///  验证高优先级同步请求会取消正在异步显示的低优先级旧请求。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_MutexPriority_HigherShouldPreemptLowerShowingAndCancelOldRequest()
        {
            // 低优先级异步行为故意忽略取消令牌，用于验证过期任务不会提交Showed。
            var container = CreateObject("HigherPreemptsLowerShowingRoot");
            var lowUI = CreateUI("PreemptedLowerShowing", container.transform);
            var highUI = CreateUI("PreemptingHigher", container.transform);
            highUI.MutexPriority = 1;
            var releaseLowAction = false;
            lowUI.ShowAsyncActions += _ => UniTask.WaitUntil(() => releaseLowAction);

            // 先启动低优先级异步显示并让它进入Showing。
            Task<Exception> lowOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));
            var lowStateBeforePreemption = UIState.Hided;
            var lowStateAfterPreemption = UIState.Hided;
            var highStateAfterPreemption = UIState.Hided;
            try
            {
                yield return null;
                lowStateBeforePreemption = lowUI.State;

                // 高优先级同步显示应隐藏低UI并取得组显示权。
                highUI.Show();
                lowStateAfterPreemption = lowUI.State;
                highStateAfterPreemption = highUI.State;
            }
            finally
            {
                // 被测抢占路径抛错时也必须释放忽略令牌的旧处理器。
                releaseLowAction = true;
            }

            // 旧处理器结束后等待原低优先级任务传播取消并收口。
            yield return WaitForTask(lowOperation);

            // 旧低UI请求必须传播更新取消，不得覆盖高UI已提交的状态。
            Assert.AreEqual(UIState.Showing, lowStateBeforePreemption, "抢占前低UI应正在异步显示");
            Assert.AreEqual(UIState.Hided, lowStateAfterPreemption, "高优先级抢占应完整隐藏低UI");
            Assert.AreEqual(UIState.Showed, highStateAfterPreemption, "高优先级抢占后应完成显示");
            Assert.That(lowOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被抢占的低优先级旧任务应传播OperationCanceledException");
            Assert.AreEqual(UIState.Hided, lowUI.State, "旧任务收口后低UI不得回写Showed");
            Assert.AreEqual(UIState.Showed, highUI.State, "旧任务收口后高UI应保持Showed");
        }

        /// <summary>
        ///  验证更新高优先级显示在取消旧任务前已登记预约，可阻止取消回调重入低UI。
        /// </summary>
        [UnityTest]
        public IEnumerator Show_MutexPriority_NewReservationShouldBlockLowerCancellationCallbackReentry()
        {
            // 高低UI位于同一父节点和互斥组，高UI配置严格更高的抢占优先级。
            var container = CreateObject("ReservationBlocksReentryRoot");
            var lowUI = CreateUI("ReservationReentryLow", container.transform);
            var highUI = CreateUI("ReservationReentryHigh", container.transform);
            highUI.MutexPriority = 1;
            var releaseOldShow = false;
            var cancellationCallbackCount = 0;
            var callbackRegistration = default(CancellationTokenRegistration);

            // 旧异步显示被更新取消时，同步回调会立即尝试重入显示低优先级UI。
            highUI.ShowAsyncActions += cancellationToken =>
            {
                callbackRegistration = cancellationToken.Register(() =>
                {
                    cancellationCallbackCount++;
                    lowUI.Show();
                });
                return UniTask.WaitUntil(() => releaseOldShow);
            };

            // 启动旧高UI显示，并等待处理器完成取消回调登记。
            Task<Exception> oldHighOperation = CaptureException(highUI.ShowAsync(CancellationToken.None));
            try
            {
                yield return null;

                // 同步更新同一高UI会在Begin阶段取消旧令牌并同步执行低UI重入回调。
                highUI.Show();

                // 允许忽略取消的旧处理器结束，再完整观察旧任务传播更新取消。
                releaseOldShow = true;
                yield return WaitForTask(oldHighOperation);
            }
            finally
            {
                // 任意中间失败都释放旧门闩并清理测试注册，避免遗留后台任务或回调。
                releaseOldShow = true;
                callbackRegistration.Dispose();
            }

            // 取消回调应实际执行，但必须观察到新高UI预约并零副作用拒绝低UI。
            Assert.AreEqual(1, cancellationCallbackCount, "更新显示应且只应取消一次旧高UI令牌");
            Assert.That(oldHighOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被同步显示更新取代的旧高UI任务应传播OperationCanceledException");
            Assert.AreEqual(UIState.Hided, lowUI.State, "取消回调中的低优先级重入应被新预约阻止");
            Assert.AreEqual(UIState.Showed, highUI.State, "新高优先级同步显示应最终提交Showed");
        }

        /// <summary>
        ///  验证相等优先级的取消回调重入仍保留最后一个请求获胜语义。
        /// </summary>
        [UnityTest]
        public IEnumerator Show_MutexPriority_EqualCancellationCallbackReentryShouldLetLatestRequestWin()
        {
            // 两个同父同组UI使用相同非零优先级，避免该测试退化成数值高低比较。
            var container = CreateObject("EqualReservationReentryRoot");
            var firstUI = CreateUI("EqualReservationFirst", container.transform);
            var latestUI = CreateUI("EqualReservationLatest", container.transform);
            firstUI.MutexPriority = 1;
            latestUI.MutexPriority = 1;
            var releaseOldShow = false;
            var cancellationCallbackCount = 0;
            var callbackRegistration = default(CancellationTokenRegistration);

            // 更新firstUI时，旧任务的同步取消回调会产生时间上更晚的latestUI显示请求。
            firstUI.ShowAsyncActions += cancellationToken =>
            {
                callbackRegistration = cancellationToken.Register(() =>
                {
                    cancellationCallbackCount++;
                    latestUI.Show();
                });
                return UniTask.WaitUntil(() => releaseOldShow);
            };

            // 先使firstUI持有旧异步显示，再用同步请求触发取消回调重入。
            Task<Exception> oldFirstOperation = CaptureException(firstUI.ShowAsync(CancellationToken.None));
            try
            {
                yield return null;
                firstUI.Show();

                // 重入请求结束后释放旧处理器，并观察最初异步任务的取消结果。
                releaseOldShow = true;
                yield return WaitForTask(oldFirstOperation);
            }
            finally
            {
                // 中间断言或调度异常同样不能留下忽略令牌的门闩任务。
                releaseOldShow = true;
                callbackRegistration.Dispose();
            }

            // 相等优先级不互相阻止，取消回调中的最新请求应隐藏firstUI并获胜。
            Assert.AreEqual(1, cancellationCallbackCount, "旧firstUI令牌应且只应触发一次取消回调");
            Assert.That(oldFirstOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被更新取代的旧firstUI任务应传播OperationCanceledException");
            Assert.AreEqual(UIState.Hided, firstUI.State, "相等优先级的较早请求应由最新请求隐藏");
            Assert.AreEqual(UIState.Showed, latestUI.State, "相等优先级的取消回调最新请求应获胜");
        }

        /// <summary>
        ///  验证同步更新显示的取消回调改变关系后，二次准入失败会收口旧Showing状态。
        /// </summary>
        [UnityTest]
        public IEnumerator Show_MutexPriority_CancellationCallbackAddsHigherSibling_ShouldCloseStaleShowing()
        {
            // 低UI正在异步显示，高UI先在另一父节点完成显示并保持更高优先级。
            var requestContainer = CreateObject("SyncSecondAdmissionRequestRoot");
            var sourceContainer = CreateObject("SyncSecondAdmissionSourceRoot");
            var lowUI = CreateUI("SyncSecondAdmissionLow", requestContainer.transform);
            var highUI = CreateUI("SyncSecondAdmissionHigh", sourceContainer.transform);
            highUI.MutexPriority = 1;
            highUI.Show();
            var releaseOldShow = false;
            var callbackRegistration = default(CancellationTokenRegistration);

            // 旧低UI任务被更新时只修改兄弟关系，不直接调用任何显隐API。
            lowUI.ShowAsyncActions += cancellationToken =>
            {
                callbackRegistration = cancellationToken.Register(() =>
                    highUI.transform.SetParent(requestContainer.transform, false));
                return UniTask.WaitUntil(() => releaseOldShow);
            };

            // 启动旧异步显示后，以同步Show更新同一UI并触发关系变更回调。
            Task<Exception> oldLowOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));
            try
            {
                yield return null;
                Assert.AreEqual(UIState.Showing, lowUI.State, "更新前低UI应处于旧异步Showing阶段");
                lowUI.Show();

                // 释放忽略取消的旧处理器并等待原任务传播更新取消。
                releaseOldShow = true;
                yield return WaitForTask(oldLowOperation);
            }
            finally
            {
                // 中间失败同样释放门闩并清理同步取消回调注册。
                releaseOldShow = true;
                callbackRegistration.Dispose();
            }

            // 二次准入发现高UI后必须执行隐藏收口，不能让失效请求永久残留Showing。
            Assert.That(oldLowOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被同步请求更新的旧低UI任务应传播OperationCanceledException");
            Assert.AreEqual(UIState.Hided, lowUI.State, "二次准入失败应把旧低UI过渡状态收口为Hided");
            Assert.AreEqual(UIState.Showed, highUI.State, "取消回调换父后的高UI应继续保持Showed");
        }

        /// <summary>
        ///  验证异步更新显示的取消回调改变关系后，二次准入失败会取消新请求并收口。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_MutexPriority_CancellationCallbackAddsHigherSibling_ShouldCancelReplacement()
        {
            // 低UI正在异步显示，高UI先在独立父节点取得稳定Showed状态。
            var requestContainer = CreateObject("AsyncSecondAdmissionRequestRoot");
            var sourceContainer = CreateObject("AsyncSecondAdmissionSourceRoot");
            var lowUI = CreateUI("AsyncSecondAdmissionLow", requestContainer.transform);
            var highUI = CreateUI("AsyncSecondAdmissionHigh", sourceContainer.transform);
            highUI.MutexPriority = 1;
            highUI.Show();
            var releaseOldShow = false;
            var callbackRegistration = default(CancellationTokenRegistration);

            // 旧低UI令牌取消时把高UI移入当前组，使刚登记的替代请求失去准入资格。
            lowUI.ShowAsyncActions += cancellationToken =>
            {
                callbackRegistration = cancellationToken.Register(() =>
                    highUI.transform.SetParent(requestContainer.transform, false));
                return UniTask.WaitUntil(() => releaseOldShow);
            };

            // 第二个ShowAsync替代旧请求，并在Begin内部同步执行旧令牌取消回调。
            Task<Exception> oldLowOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));
            Task<Exception> replacementOperation = null;
            try
            {
                yield return null;
                replacementOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));

                // 新请求应立即因二次准入失败取消，旧处理器仍需释放后才能完整结束。
                releaseOldShow = true;
                yield return WaitForTask(Task.WhenAll(oldLowOperation, replacementOperation));
            }
            finally
            {
                // 任意等待失败都释放旧门闩并注销回调，避免污染后续PlayMode用例。
                releaseOldShow = true;
                callbackRegistration.Dispose();
            }

            // 两轮任务都被更新隐藏取代，最终低UI稳定Hided且高UI继续持权。
            Assert.That(oldLowOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "旧低UI任务应传播OperationCanceledException");
            Assert.That(replacementOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "二次准入失败的替代请求应传播OperationCanceledException");
            Assert.AreEqual(UIState.Hided, lowUI.State, "异步替代请求失败后低UI应稳定收口Hided");
            Assert.AreEqual(UIState.Showed, highUI.State, "换父进入当前组的高UI应保持Showed");
        }

        /// <summary>
        ///  验证高优先级UI在Hiding完成前持续占权，且被拒绝的低UI不会自动排队。
        /// </summary>
        [UnityTest]
        public IEnumerator HideAsync_MutexPriority_HigherHidingShouldBlockUntilHidedWithoutQueue()
        {
            // 高UI先进入Showed，再用门闩挂起其异步隐藏行为。
            var container = CreateObject("HigherHidingBlocksLowerRoot");
            var lowUI = CreateUI("BlockedDuringHigherHiding", container.transform);
            var highUI = CreateUI("HigherHiding", container.transform);
            highUI.MutexPriority = 1;
            highUI.Show();
            var releaseHighHide = false;
            highUI.HideAsyncActions += _ => UniTask.WaitUntil(() => releaseHighHide);

            // 启动高UI隐藏并等待其进入Hiding。
            Task<Exception> hideOperation = CaptureException(highUI.HideAsync(CancellationToken.None));
            var lowStateDuringHighHide = UIState.Showed;
            try
            {
                yield return null;

                // Hiding期间请求低UI，请求应被拒绝且不创建延迟队列。
                lowUI.Show();
                lowStateDuringHighHide = lowUI.State;
            }
            finally
            {
                // 中间调度失败时也释放异步隐藏门闩，避免污染后续测试。
                releaseHighHide = true;
            }

            // 释放高UI隐藏并等待它真正到达Hided。
            yield return WaitForTask(hideOperation);
            var lowStateAfterHighHided = lowUI.State;

            // 不实现隐式排队，调用方需要在高UI完成隐藏后重新请求。
            lowUI.Show();

            // Hiding占权、无队列和重试成功三个阶段均应符合预期。
            Assert.AreEqual(UIState.Hided, lowStateDuringHighHide, "高优先级Hiding期间应阻止低UI");
            Assert.IsNull(hideOperation.Result, $"高UI异步隐藏应正常完成：{hideOperation.Result}");
            Assert.AreEqual(UIState.Hided, lowStateAfterHighHided, "先前被拒绝的低UI不应在高UI隐藏后自动弹出");
            Assert.AreEqual(UIState.Showed, lowUI.State, "高UI已Hided后重新请求应允许低UI显示");
        }

        /// <summary>
        ///  验证高优先级异步隐藏被取消后不会以残留Hiding状态永久阻塞同组低UI。
        /// </summary>
        [UnityTest]
        public IEnumerator HideAsync_MutexPriority_CanceledHigherHideShouldReleaseGroupForRecovery()
        {
            // 高UI先显示，并让其异步隐藏只能通过调用方令牌取消。
            var container = CreateObject("CanceledHigherHideRecoveryRoot");
            var lowUI = CreateUI("CanceledHigherHideLow", container.transform);
            var highUI = CreateUI("CanceledHigherHideHigh", container.transform);
            highUI.MutexPriority = 1;
            highUI.Show();
            highUI.HideAsyncActions += WaitUntilCanceled;
            using var cancellationTokenSource = new CancellationTokenSource();

            // 启动隐藏并等待状态进入真实执行中的Hiding阶段。
            Task<Exception> hideOperation = CaptureException(highUI.HideAsync(cancellationTokenSource.Token));
            yield return null;
            Assert.AreEqual(UIState.Hiding, highUI.State, "取消前高优先级UI应处于有效Hiding阶段");

            // 取消本轮隐藏并完整观察任务收口，当前实现保留中断位置用于业务诊断。
            cancellationTokenSource.Cancel();
            yield return WaitForTask(hideOperation);
            Assert.That(hideOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "调用方取消高UI隐藏应传播OperationCanceledException");
            Assert.AreEqual(UIState.Hiding, highUI.State, "取消后的高UI应保留未完成隐藏的Hiding状态");

            // 低UI的新请求应取得准入，并通过一次同步隐藏把高UI残留状态恢复为Hided。
            lowUI.Show();

            // 已结束的隐藏操作不再占权，恢复过程也不能留下两个同组可见UI。
            Assert.AreEqual(UIState.Hided, highUI.State, "后续同组请求应完整收口取消后的高UI");
            Assert.AreEqual(UIState.Showed, lowUI.State, "取消的高UI隐藏不应永久阻塞低UI");
        }

        /// <summary>
        ///  验证高优先级异步隐藏故障后不会以残留Hiding状态永久阻塞同组低UI。
        /// </summary>
        [UnityTest]
        public IEnumerator HideAsync_MutexPriority_FailedHigherHideShouldReleaseGroupForRecovery()
        {
            // 高UI先显示，并使用延后一帧抛错的处理器覆盖异步故障收口路径。
            var container = CreateObject("FailedHigherHideRecoveryRoot");
            var lowUI = CreateUI("FailedHigherHideLow", container.transform);
            var highUI = CreateUI("FailedHigherHideHigh", container.transform);
            highUI.MutexPriority = 1;
            highUI.Show();
            highUI.HideAsyncActions += ThrowHideFailureAfterYield;

            // 等待高UI隐藏处理器故障并记录未完成隐藏所保留的状态。
            Task<Exception> hideOperation = CaptureException(highUI.HideAsync(CancellationToken.None));
            yield return WaitForTask(hideOperation);
            Assert.That(hideOperation.Result, Is.InstanceOf<InvalidOperationException>(),
                "高UI隐藏处理器故障应保留原始InvalidOperationException");
            Assert.AreEqual(UIState.Hiding, highUI.State, "故障后的高UI应保留未完成隐藏的Hiding状态");

            // 后续低UI请求应通过新隐藏生命周期修复高UI，而不是被残留状态永久拒绝。
            lowUI.Show();

            // 故障操作已经结束后不再持有优先级，组内最终仍只能有一个Showed面板。
            Assert.AreEqual(UIState.Hided, highUI.State, "后续同组请求应完整收口故障后的高UI");
            Assert.AreEqual(UIState.Showed, lowUI.State, "故障的高UI隐藏不应永久阻塞低UI");
        }

        /// <summary>
        ///  验证父UI在严格子UI异步显示期间隐藏时会取消子请求并完整收口。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_RequireParentChainShowed_ParentHiddenDuringChildAction_ShouldCancelChild()
        {
            // 父UI先真实显示，使严格子UI的初始准入检查通过。
            var container = CreateObject("ParentHiddenDuringStrictChildRoot");
            var parentUI = CreateUI("ParentHiddenDuringStrictChild", container.transform);
            var childUI = CreateUI("StrictChildInterruptedByParent", parentUI.transform);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            parentUI.Show();

            // 子UI显示处理器忽略取消令牌，验证父隐藏的版本接管足以阻止旧请求提交。
            var releaseChildAction = false;
            var afterShowCount = 0;
            childUI.ShowAsyncActions += _ => UniTask.WaitUntil(() => releaseChildAction);
            childUI.AfterUIShowedEvent += () => afterShowCount++;

            // 启动子UI异步显示并等待进入Showing。
            Task<Exception> childOperation = CaptureException(childUI.ShowAsync(CancellationToken.None));
            var childStateBeforeParentHide = UIState.Hided;
            var childStateAfterParentHide = UIState.Showed;
            try
            {
                yield return null;
                childStateBeforeParentHide = childUI.State;

                // 父UI隐藏必须通过子UI关系联动取消当前子显示。
                parentUI.Hide();
                childStateAfterParentHide = childUI.State;
            }
            finally
            {
                // 父隐藏路径异常时也必须释放忽略令牌的旧子处理器。
                releaseChildAction = true;
            }

            // 释放旧子处理器后，等待原显示请求传播更新取消。
            yield return WaitForTask(childOperation);

            // 父隐藏应完整隐藏子UI，旧子请求不得派发显示After。
            Assert.AreEqual(UIState.Showing, childStateBeforeParentHide, "父隐藏前子UI应正在显示");
            Assert.AreEqual(UIState.Hided, childStateAfterParentHide, "父UI隐藏应立即收口子UI为Hided");
            Assert.That(childOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被父隐藏取代的子显示应传播OperationCanceledException");
            Assert.AreEqual(0, afterShowCount, "被取消的子显示不得触发After事件");
            Assert.AreEqual(UIState.Hided, childUI.State, "旧子请求收口后不得覆盖Hided状态");
        }

        /// <summary>
        ///  验证父级联在启动行为前就跳过同组严格低优先级候选。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_ParentCascade_ShouldSkipLowerPriorityCandidateBeforeStartingHandler()
        {
            // 低优先级子UI先创建以保证其Hierarchy顺序位于高优先级之前。
            var container = CreateObject("ParentCascadePriorityRoot");
            var parentUI = CreateUI("ParentCascadePriorityParent", container.transform);
            var lowChild = CreateUI("ParentCascadeLowCandidate", parentUI.transform);
            var highChild = CreateUI("ParentCascadeHighCandidate", parentUI.transform);
            lowChild.ShowWithParent = true;
            highChild.ShowWithParent = true;
            highChild.MutexPriority = 1;

            // 两个处理器都立即完成，计数可直接表明低候选是否被错误启动。
            var lowActionCount = 0;
            var highActionCount = 0;
            lowChild.ShowAsyncActions += _ =>
            {
                lowActionCount++;
                return UniTask.CompletedTask;
            };
            highChild.ShowAsyncActions += _ =>
            {
                highActionCount++;
                return UniTask.CompletedTask;
            };

            // 异步显示父UI，由父级联批次选择同组默认子UI。
            Task<Exception> parentOperation = CaptureException(parentUI.ShowAsync(CancellationToken.None));
            yield return WaitForTask(parentOperation);

            // 批次应在行为启动前选中高优先级，结果不得依赖Hierarchy顺序。
            Assert.IsNull(parentOperation.Result, $"父UI异步显示应正常完成：{parentOperation.Result}");
            Assert.AreEqual(0, lowActionCount, "父级联不应启动必然被抢占的低优先级处理器");
            Assert.AreEqual(1, highActionCount, "父级联应且只应启动一次高优先级处理器");
            Assert.AreEqual(UIState.Hided, lowChild.State, "低优先级默认子UI应保持Hided");
            Assert.AreEqual(UIState.Showed, highChild.State, "高优先级默认子UI应完成显示");
        }

        /// <summary>
        ///  验证低优先级默认子UI的显式请求优先于隐藏的高优先级父级联候选。
        /// </summary>
        [Test]
        public void Show_LowerPriorityMarkedChildRequestedDirectly_ShouldBeatHigherDefaultCascade()
        {
            // 两个同组子UI均配置随父显示，默认兄弟的数值优先级更高。
            var container = CreateObject("ExplicitLowerPriorityMarkedRoot");
            var parentUI = CreateUI("ExplicitLowerPriorityParent", container.transform);
            var requestedChild = CreateUI("ExplicitLowerPriorityChild", parentUI.transform);
            var defaultHighChild = CreateUI("DefaultHigherPriorityChild", parentUI.transform);
            requestedChild.ShowWithParent = true;
            defaultHighChild.ShowWithParent = true;
            defaultHighChild.MutexPriority = 1;

            // 记录高优先级默认候选是否被父级联错误启动。
            var requestedShowCount = 0;
            var defaultHighShowCount = 0;
            requestedChild.ShowActions += () => requestedShowCount++;
            defaultHighChild.ShowActions += () => defaultHighShowCount++;

            // 显式请求低优先级子UI，它会通过默认父链策略拉起父UI。
            requestedChild.Show();

            // 隐藏的高优先级候选不预占组，父级联也必须让显式请求先完成。
            Assert.AreEqual(UIState.Showed, parentUI.State, "显式子UI请求应正常补齐父UI");
            Assert.AreEqual(UIState.Showed, requestedChild.State, "低优先级显式子UI应完成显示");
            Assert.AreEqual(UIState.Hided, defaultHighChild.State, "高优先级默认候选不得反向取代显式请求");
            Assert.AreEqual(1, requestedShowCount, "显式子UI显示行为应且只应执行一次");
            Assert.AreEqual(0, defaultHighShowCount, "高优先级默认候选不应启动显示行为");
        }

        /// <summary>
        ///  验证Start会将父链未就绪的严格序列化Showed子UI规范化为Hided。
        /// </summary>
        [UnityTest]
        public IEnumerator Start_RequiredSerializedShowedChildWithUnreadyParent_ShouldNormalizeToHided()
        {
            // CreateUI默认禁用组件，因此可以在Start前写入精确序列化初态。
            var container = CreateObject("StrictSerializedStartRoot");
            var parentUI = CreateUI("UnreadySerializedShowedParent", container.transform);
            var childUI = CreateUI("StrictSerializedShowedChild", parentUI.transform);
            SetUIBasePrivateField(parentUI, "state", UIState.Showed);
            SetUIBasePrivateField(childUI, "state", UIState.Showed);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 记录Start初始化实际选择的显示或隐藏生命周期。
            var showActionCount = 0;
            var hideActionCount = 0;
            childUI.ShowActions += () => showActionCount++;
            childUI.HideActions += () => hideActionCount++;

            // 仅启用子UI组件，父UI保持未初始化的序列化Showed状态。
            childUI.enabled = true;
            yield return null;

            // 不可见的假Showed初态应通过一次完整隐藏生命周期收口。
            Assert.AreEqual(UIState.Hided, childUI.State, "严格子UI的未就绪Showed初态应被规范化为Hided");
            Assert.AreEqual(0, showActionCount, "父链未就绪时Start不应启动显示行为");
            Assert.AreEqual(1, hideActionCount, "父链未就绪时Start应执行一次隐藏行为完成收口");
        }

        /// <summary>
        ///  验证父子都启用且序列化Showed时，严格子UI先执行Start也会等待父UI初始化。
        /// </summary>
        [UnityTest]
        public IEnumerator Start_RequiredSerializedShowedParentAndChild_ChildStartsFirstShouldRemainShowed()
        {
            // 父子UI都在Start前写入Showed，子UI要求真实父链Showed后才允许直接显示。
            var container = CreateObject("StrictSerializedStartOrderRoot");
            var parentUI = CreateUI("StrictSerializedStartOrderParent", container.transform);
            var childUI = CreateUI("StrictSerializedStartOrderChild", parentUI.transform);
            SetUIBasePrivateField(parentUI, "state", UIState.Showed);
            SetUIBasePrivateField(childUI, "state", UIState.Showed);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 两个组件都处于可启动状态，手动先调用子Start以稳定复现Unity未保证的执行顺序。
            parentUI.enabled = true;
            childUI.enabled = true;
            var parentShowCount = 0;
            var childShowCount = 0;
            var childHideCount = 0;
            parentUI.ShowActions += () => parentShowCount++;
            childUI.ShowActions += () => childShowCount++;
            childUI.HideActions += () => childHideCount++;
            InvokeUIBaseStartForTest(childUI);
            Assert.AreEqual(0, childShowCount, "父UI尚未初始化时严格子Start应先延后显示生命周期");
            Assert.AreEqual(0, childHideCount, "可启动父UI尚未执行Start时不应提前把子初态规范化为Hided");

            // 父Start先完成真实显示，延后的子初始化应在后续帧按最新父链状态恢复显示。
            InvokeUIBaseStartForTest(parentUI);
            yield return null;
            yield return null;

            // 无论Unity随后如何调度自动Start，父子都只应初始化一次并稳定保持Showed。
            Assert.AreEqual(1, parentShowCount, "父UI序列化Showed初态应且只应执行一次显示行为");
            Assert.AreEqual(1, childShowCount, "严格子UI应在父UI就绪后且只执行一次显示行为");
            Assert.AreEqual(0, childHideCount, "父子同为Showed初态时不应因Start顺序误隐藏子UI");
            Assert.AreEqual(UIState.Showed, parentUI.State, "父UI初始化后应保持Showed");
            Assert.AreEqual(UIState.Showed, childUI.State, "子UI延后初始化后应保持Showed");
        }

        /// <summary>
        ///  验证直接父UI已经确定Hided时不会因更上层父UI待Start而无意义延后严格子初态。
        /// </summary>
        [Test]
        public void Start_RequiredSerializedShowedChild_WithHidedParentShouldNotWaitForGrandParent()
        {
            // 祖父UI启用但未初始化，直接父UI已经初始化为Hided，严格父链结果已经确定失败。
            var container = CreateObject("StrictStartKnownFailureRoot");
            var grandParentUI = CreateUI("StrictStartPendingGrandParent", container.transform);
            var parentUI = CreateUI("StrictStartHidedParent", grandParentUI.transform);
            var childUI = CreateUI("StrictStartKnownFailureChild", parentUI.transform);
            SetUIBasePrivateField(parentUI, "_isUIStateInitialized", true);
            SetUIBasePrivateField(childUI, "state", UIState.Showed);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            grandParentUI.enabled = true;
            childUI.enabled = true;

            // 记录子UI是否在本次Start调用内立即选择完整隐藏生命周期。
            var hideActionCount = 0;
            childUI.HideActions += () => hideActionCount++;
            InvokeUIBaseStartForTest(childUI);

            // 直接父Hided已经足够拒绝显示，不应等待与最终结论无关的祖父Start。
            Assert.AreEqual(UIState.Hided, childUI.State, "确定失败的直接父链应立即规范化子UI为Hided");
            Assert.AreEqual(1, hideActionCount, "确定失败时子Start应立即执行一次隐藏行为");
        }

        /// <summary>
        ///  验证严格子初态等待期间GameObject失活后，重新激活会恢复延后初始化流程。
        /// </summary>
        [UnityTest]
        public IEnumerator Start_RequiredSerializedShowedChild_DeactivatedWhileDeferredShouldResumeOnEnable()
        {
            // 父子都启用并写入Showed，手动先启动子UI以进入等待父Start的协程。
            var container = CreateObject("StrictStartDeactivateResumeRoot");
            var parentUI = CreateUI("StrictStartDeactivateResumeParent", container.transform);
            var childUI = CreateUI("StrictStartDeactivateResumeChild", parentUI.transform);
            SetUIBasePrivateField(parentUI, "state", UIState.Showed);
            SetUIBasePrivateField(childUI, "state", UIState.Showed);
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            parentUI.enabled = true;
            childUI.enabled = true;
            var childShowCount = 0;
            var childHideCount = 0;
            childUI.ShowActions += () => childShowCount++;
            childUI.HideActions += () => childHideCount++;
            InvokeUIBaseStartForTest(childUI);

            // 失活子GameObject会停止其托管协程，随后单独完成父UI真实显示初始化。
            childUI.gameObject.SetActive(false);
            InvokeUIBaseStartForTest(parentUI);
            yield return null;
            Assert.AreEqual(0, childShowCount, "失活期间严格子UI不应执行显示行为");

            // 重新激活时OnEnable必须续接未完成初态，并按已经Showed的父链恢复子UI。
            childUI.gameObject.SetActive(true);
            yield return null;

            // 恢复流程只能初始化一次，不能留下未初始化假Showed或错误隐藏行为。
            Assert.AreEqual(1, childShowCount, "重新激活后严格子初态应且只应执行一次显示行为");
            Assert.AreEqual(0, childHideCount, "父链已就绪时恢复延后初态不应执行隐藏行为");
            Assert.AreEqual(UIState.Showed, parentUI.State, "恢复子UI前父UI应保持真实Showed");
            Assert.AreEqual(UIState.Showed, childUI.State, "重新激活后严格子UI应完成真实Showed初始化");
        }

        /// <summary>
        ///  通过反射调用UIBase受保护的Start，仅用于构造确定的父子初始化顺序。
        /// </summary>
        /// <param name="ui">需要执行Start初始化的UIBase测试实例。</param>
        private static void InvokeUIBaseStartForTest(UIBase ui)
        {
            // 反射范围固定在UIBase实例非公开方法，不扩大生产代码的公开生命周期接口。
            var startMethod = typeof(UIBase).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(startMethod, "UIBase中不存在测试所需的受保护Start方法");

            // 目标方法无参数，调用异常由NUnit直接报告并保留内部失败原因。
            startMethod.Invoke(ui, null);
        }

        /// <summary>
        ///  延后一帧后抛出固定隐藏故障，供Hiding占权恢复测试使用。
        /// </summary>
        /// <param name="cancellationToken">当前异步隐藏操作令牌。</param>
        /// <returns>延后一帧后以InvalidOperationException失败的任务。</returns>
        /// <exception cref="InvalidOperationException">延后一帧后始终抛出。</exception>
        private static async UniTask ThrowHideFailureAfterYield(CancellationToken cancellationToken)
        {
            // 先让HideAsync进入可观察的异步生命周期，再产生稳定业务故障。
            await UniTask.NextFrame(cancellationToken: cancellationToken);

            // 固定异常类型用于区分业务失败与调度器主动取消。
            throw new InvalidOperationException("测试异步隐藏故障。");
        }
    }
}
