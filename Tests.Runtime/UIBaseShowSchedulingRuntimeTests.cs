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
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI($"StrictParent{parentState}", container.transform);
            // 创建“targetUI”并配置测试所需的层级或表现组件。
            var targetUI = CreateUI($"StrictTarget{parentState}", parentUI.transform);
            // 创建“siblingUI”并配置测试所需的层级或表现组件。
            var siblingUI = CreateUI($"StrictSibling{parentState}", parentUI.transform);
            // 将“targetUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            targetUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 使父UI处于真实初始化但未Showed的测试状态。
            SetUIBasePrivateField(parentUI, "state", parentState);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parentUI, "_isUIStateInitialized", true);

            // 预置一个同组Showed兄弟，验证拒绝发生在隐藏兄弟之前。
            SetUIBasePrivateField(siblingUI, "state", UIState.Showed);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(siblingUI, "_isUIStateInitialized", true);

            // 记录目标UI所有可观察的显示生命周期副作用。
            var beforeCount = 0;
            // 初始化“actionCount”为0，作为后续回调的观测基准。
            var actionCount = 0;
            // 初始化“afterCount”为0，作为后续回调的观测基准。
            var afterCount = 0;
            // 初始化“stateChangedCount”为0，作为后续回调的观测基准。
            var stateChangedCount = 0;
            // 为“targetUI.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            targetUI.BeforeUIShowEvent += () => beforeCount++;
            // 为“targetUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            targetUI.ShowActions += () => actionCount++;
            // 为“targetUI.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            targetUI.AfterUIShowedEvent += () => afterCount++;
            // 为“targetUI.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            targetUI.OnStateChanged += (_, _) => stateChangedCount++;

            // 父链未就绪时请求显示目标UI。
            targetUI.Show();

            // 目标、父节点和兄弟都必须保持请求前的状态。
            Assert.AreEqual(UIState.Hided, targetUI.State, "父链未就绪时目标UI应保持Hided");
            // 确认“parentUI”的界面状态等于“parentState”。
            Assert.AreEqual(parentState, parentUI.State, "被拒绝的子UI请求不得修复或改变父UI");
            // 确认“siblingUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, siblingUI.State, "准入拒绝前不得先隐藏同组兄弟");

            // 零副作用拒绝不得派发任何显示事件或状态变化。
            Assert.AreEqual(0, beforeCount, "父链准入拒绝不应触发Before事件");
            // 确认“actionCount”等于0。
            Assert.AreEqual(0, actionCount, "父链准入拒绝不应启动显示行为");
            // 确认“afterCount”等于0。
            Assert.AreEqual(0, afterCount, "父链准入拒绝不应触发After事件");
            // 确认“stateChangedCount”等于0。
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
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("UninitializedShowedParent", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictChildOfUninitializedParent", parentUI.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parentUI, "state", UIState.Showed);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 子UI的严格直接显示请求必须被拒绝。
            childUI.Show();

            // 序列化假Showed不能被当作已完成的父显示生命周期。
            Assert.AreEqual(UIState.Hided, childUI.State, "未初始化的Showed父UI不应满足严格父链准入");
            // 确认“parentUI”的界面状态等于显示终态。
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
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictChildWithoutUIParent", container.transform);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
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
            // 创建“grandParentUI”并配置测试所需的层级或表现组件。
            var grandParentUI = CreateUI("ReadyStrictGrandParent", container.transform);
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("ReadyStrictParent", grandParentUI.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("ReadyStrictChild", parentUI.transform);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 记录父链显示行为，先使直接父通过默认策略补齐祖父。
            var grandParentShowCount = 0;
            // 初始化“parentShowCount”为0，作为后续回调的观测基准。
            var parentShowCount = 0;
            // 为“grandParentUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            grandParentUI.ShowActions += () => grandParentShowCount++;
            // 为“parentUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parentUI.ShowActions += () => parentShowCount++;
            // 提交“parentUI”的显示请求，触发实际生命周期。
            parentUI.Show();

            // 父链全部已完成Showed后请求严格子UI显示。
            childUI.Show();

            // 子UI应完成显示，两层父UI都不得被重复派发。
            Assert.AreEqual(UIState.Showed, childUI.State, "完整父链Showed后严格子UI应允许显示");
            // 确认“grandParentShowCount”等于1。
            Assert.AreEqual(1, grandParentShowCount, "严格子UI不应重复显示祖父UI");
            // 确认“parentShowCount”等于1。
            Assert.AreEqual(1, parentShowCount, "严格子UI不应重复显示直接父UI");
        }

        /// <summary>
        ///  验证异步严格显示被准入拒绝时正常结束且不产生副作用。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_RequireParentChainShowed_ParentNotReady_ShouldCompleteWithoutSideEffects()
        {
            // 父UI保持初始Hided，子UI开启严格父链策略。
            var container = CreateObject("StrictAsyncRejectedRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("StrictAsyncRejectedParent", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictAsyncRejectedChild", parentUI.transform);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parentUI, "_isUIStateInitialized", true);

            // 显示处理器计数用于确认拒绝发生在任务启动之前。
            var showActionCount = 0;
            // 为“childUI.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.ShowAsyncActions += _ =>
            {
                // 更新“showActionCount”的计数，记录回调实际执行次数。
                showActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };

            // 将UniTask异常转为可在UnityTest主协程中断言的结果。
            Task<Exception> operation = CaptureException(childUI.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 正常仲裁拒绝不是取消或故障，并且不得启动用户处理器。
            Assert.IsNull(operation.Result, $"准入拒绝的ShowAsync应正常结束：{operation.Result}");
            // 确认“childUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, childUI.State, "被拒绝的异步显示应保持Hided");
            // 确认“showActionCount”等于0。
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
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("StrictParentCascadeParent", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictParentCascadeChild", parentUI.transform);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 将“childUI”的随父显示配置设为true，建立当前测试条件。
            childUI.ShowWithParent = true;

            // 记录子UI显示行为期间父UI的包围状态。
            var parentStateDuringChildShow = UIState.Hided;
            // 为“childUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.ShowActions += () => parentStateDuringChildShow = parentUI.State;

            // 仅显示父UI，子UI应由ParentCascade专用入口处理。
            parentUI.Show();

            // 子UI生命周期属于父显示事务内部，因此观察到的父状态仍为Showing。
            Assert.AreEqual(UIState.Showed, parentUI.State, "父UI应完成显示");
            // 确认“childUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, childUI.State, "父级联应允许显示严格子UI");
            // 确认“parentStateDuringChildShow”等于显示过渡态。
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
            // 创建“cascadeParent”并配置测试所需的层级或表现组件。
            var cascadeParent = CreateUI("ParentCascadeReparentSource", container.transform);
            // 创建“hiddenParent”并配置测试所需的层级或表现组件。
            var hiddenParent = CreateUI("ParentCascadeReparentHiddenTarget", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("ParentCascadeReparentStrictChild", cascadeParent.transform);
            // 将“childUI”的随父显示配置设为true，建立当前测试条件。
            childUI.ShowWithParent = true;
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(hiddenParent, "_isUIStateInitialized", true);

            // 子UI自身显示行为把它移动到另一个未显示父UI，离开原父级联事务关系。
            childUI.ShowActions += () => childUI.transform.SetParent(hiddenParent.transform, false);

            // 显示来源父UI会启动子UI级联，但换父后必须停止对子UI的父链豁免。
            cascadeParent.Show();

            // 原父可以完成自身显示，换父子UI则必须收口且不得拉起隐藏目标父UI。
            Assert.AreEqual(UIState.Showed, cascadeParent.State, "来源父UI应完成自身显示生命周期");
            // 确认“hiddenParent”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, hiddenParent.State, "父级联换父不得显示新的目标父UI");
            // 确认“childUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, childUI.State, "离开来源父UI后严格子UI应停止级联并收口");
            // 确认“childUI.transform”的直属父节点等于“hiddenParent.transform”。
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
            // 创建“temporaryContainer”并配置测试所需的层级或表现组件。
            var temporaryContainer = CreateObject("ParentCascadeVersionTemporaryRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("ParentCascadeVersionParent", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("ParentCascadeVersionChild", parentUI.transform);
            // 将“childUI”的随父显示配置设为true，建立当前测试条件。
            childUI.ShowWithParent = true;
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 子行为先离开父节点、取代父显示事务，再移回原父节点以单独验证事务版本。
            childUI.ShowActions += () =>
            {
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                childUI.transform.SetParent(temporaryContainer.transform, false);
                // 提交“parentUI”的隐藏请求，检查子树联动与终态。
                parentUI.Hide();
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                childUI.transform.SetParent(parentUI.transform, false);
            };

            // 父UI启动级联后会在子行为中被同步隐藏并使原显示操作版本失效。
            parentUI.Show();

            // 仅恢复直接父关系不能复活旧事务，父子都必须稳定收口为Hided。
            Assert.AreEqual(UIState.Hided, parentUI.State, "子行为取代来源事务后父UI应保持Hided");
            // 确认“childUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, childUI.State, "失效父事务不得继续提交严格子UI为Showed");
            // 确认“childUI.transform”的直属父节点等于“parentUI.transform”。
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
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("PriorityLow", container.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("PriorityHigh", container.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;

            // 高优先级尚处Hided时不预占组，低优先级应先正常显示。
            var lowShowCount = 0;
            // 为“lowUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            lowUI.ShowActions += () => lowShowCount++;
            // 提交“lowUI”的显示请求，触发实际生命周期。
            lowUI.Show();
            // 确认“lowUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, lowUI.State, "Hided的高优先级UI不应阻止低优先级显示");

            // 高优先级后请求应隐藏低优先级并取得当前显示权。
            highUI.Show();
            // 确认“lowUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowUI.State, "高优先级UI应抢占并隐藏低优先级UI");
            // 确认“highUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, highUI.State, "高优先级UI应完成显示");

            // 高优先级活跃期间再次请求低UI，显示行为计数必须保持不变。
            lowUI.Show();
            // 确认“lowUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowUI.State, "低优先级UI不得反向抢占活跃高优先级UI");
            // 确认“highUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, highUI.State, "被拒绝的低优先级请求不得隐藏高优先级UI");
            // 确认“lowShowCount”等于1。
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
            // 创建“firstUI”并配置测试所需的层级或表现组件。
            var firstUI = CreateUI("EqualPriorityFirst", container.transform);
            // 创建“secondUI”并配置测试所需的层级或表现组件。
            var secondUI = CreateUI("EqualPrioritySecond", container.transform);
            // 将“firstUI”的互斥优先级设为2，建立当前测试条件。
            firstUI.MutexPriority = 2;
            // 将“secondUI”的互斥优先级设为2，建立当前测试条件。
            secondUI.MutexPriority = 2;

            // 依次显示两个UI，后请求者应取代先请求者。
            firstUI.Show();
            // 提交“secondUI”的显示请求，触发实际生命周期。
            secondUI.Show();

            // 相等优先级不应使用大于等于比较而阻止更新请求。
            Assert.AreEqual(UIState.Hided, firstUI.State, "相等优先级的先请求UI应被隐藏");
            // 确认“secondUI”的界面状态等于显示终态。
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
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("DifferentGroupLow", container.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("DifferentGroupHigh", container.transform);
            // 将“highUI”的互斥优先级设为10，建立当前测试条件。
            highUI.MutexPriority = 10;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(highUI, "mutexGroup", 1);

            // 两个UI先后显示时不应产生跨组隐藏或准入拒绝。
            highUI.Show();
            // 提交“lowUI”的显示请求，触发实际生命周期。
            lowUI.Show();

            // 互斥组边界优先于数值优先级。
            Assert.AreEqual(UIState.Showed, lowUI.State, "不同组的低优先级UI应正常显示");
            // 确认“highUI”的界面状态等于显示终态。
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
            // 创建“sourceContainer”并配置测试所需的层级或表现组件。
            var sourceContainer = CreateObject("DynamicReparentSourceRoot");
            // 创建“requestUI”并配置测试所需的层级或表现组件。
            var requestUI = CreateUI("DynamicReparentRequest", requestContainer.transform);
            // 创建“movedUI”并配置测试所需的层级或表现组件。
            var movedUI = CreateUI("DynamicReparentShownSibling", sourceContainer.transform);
            // 提交“movedUI”的显示请求，触发实际生命周期。
            movedUI.Show();

            // 当前UI显示行为执行时，把已显示的相等优先级UI移动到同一直接父节点下。
            requestUI.ShowActions += () => movedUI.transform.SetParent(requestContainer.transform, false);

            // 发起显示后，调度器必须使用行为完成后的最新Transform关系重新协调。
            requestUI.Show();

            // 相等优先级保留后请求获胜，不能因初始快照不同而留下两个Showed兄弟。
            Assert.AreEqual(UIState.Showed, requestUI.State, "动态换父后当前后请求UI应完成显示");
            // 确认“movedUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, movedUI.State, "换父进入同组的先显示UI应被重新协调隐藏");
            // 确认“movedUI.transform”的直属父节点等于“requestContainer.transform”。
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
            // 创建“sourceContainer”并配置测试所需的层级或表现组件。
            var sourceContainer = CreateObject("ShowedCallbackSourceRoot");
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("ShowedCallbackLow", requestContainer.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("ShowedCallbackHigh", sourceContainer.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;
            // 提交“highUI”的显示请求，触发实际生命周期。
            highUI.Show();

            // 低UI刚提交Showed时，把高UI移动为它的同父同组兄弟，制造提交后的关系变化。
            lowUI.OnStateChanged += (_, currentState) =>
            {
                // 根据“currentState is UIState.Showed”选择对应测试路径。
                if (currentState is UIState.Showed)
                    // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                    highUI.transform.SetParent(requestContainer.transform, false);
            };

            // 低UI显示流程必须在返回前发现新出现的高优先级持权兄弟。
            lowUI.Show();

            // 高UI继续持权，低UI不能以已经触发Showed回调为由留下冲突稳定状态。
            Assert.AreEqual(UIState.Hided, lowUI.State, "Showed回调换父后低UI应被最终协调隐藏");
            // 确认“highUI”的界面状态等于显示终态。
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
            // 创建“readyParent”并配置测试所需的层级或表现组件。
            var readyParent = CreateUI("StrictShowedCallbackReadyParent", container.transform);
            // 创建“hiddenParent”并配置测试所需的层级或表现组件。
            var hiddenParent = CreateUI("StrictShowedCallbackHiddenParent", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictShowedCallbackChild", readyParent.transform);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(hiddenParent, "_isUIStateInitialized", true);
            // 提交“readyParent”的显示请求，触发实际生命周期。
            readyParent.Show();

            // 子UI提交Showed的状态通知中立即换到未显示父UI下，模拟业务动态调整层级。
            childUI.OnStateChanged += (_, currentState) =>
            {
                // 根据“currentState is UIState.Showed”选择对应测试路径。
                if (currentState is UIState.Showed)
                    // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                    childUI.transform.SetParent(hiddenParent.transform, false);
            };

            // 初始父链满足准入，但最终返回结果必须以回调后的实际父链为准。
            childUI.Show();

            // 严格子UI不能在Hided父UI下留下Showed状态，且不应反向显示新父UI。
            Assert.AreEqual(UIState.Hided, childUI.State, "Showed回调换到未就绪父链后子UI应收口Hided");
            // 确认“hiddenParent”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, hiddenParent.State, "最终准入失败不得反向拉起新父UI");
            // 确认“childUI.transform”的直属父节点等于“hiddenParent.transform”。
            Assert.AreEqual(hiddenParent.transform, childUI.transform.parent,
                "调度收口不应撤销业务回调完成的Transform换父");
        }

        /// <summary>
        ///  验证高优先级UI正在异步显示时会拒绝低优先级请求且不取消自身。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_MutexPriority_HigherShowingShouldRejectLowerWithoutCancelingHigher()
        {
            // 高优先级UI的异步行为由门闩挂起，低UI使用默认优先级。
            var container = CreateObject("HigherShowingBlocksLowerRoot");
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("BlockedLowerUI", container.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("ShowingHigherUI", container.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;
            // 初始化“releaseHighAction”为false，作为后续回调的观测基准。
            var releaseHighAction = false;
            // 为“highUI.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            highUI.ShowAsyncActions += _ => UniTask.WaitUntil(() => releaseHighAction);

            // 先启动高优先级显示并等待它进入Showing。
            Task<Exception> highOperation = CaptureException(highUI.ShowAsync(CancellationToken.None));
            // 初始化“lowOperation”为空引用，作为后续回调的观测基准。
            Task<Exception> lowOperation = null;
            // 保存“highStateBeforeRelease”的基准值隐藏终态，供后续比较或恢复。
            var highStateBeforeRelease = UIState.Hided;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;

                // 高UI持有显示预约期间请求低UI，被拒绝的任务应正常完成。
                lowOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));
                // 等待“WaitForTask(lowOperation)”，再检查操作完成后的状态。
                yield return WaitForTask(lowOperation);
                // 将“highStateBeforeRelease”设为“highUI”的界面状态，建立当前测试条件。
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
            // 确认“lowUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowUI.State, "高优先级Showing时低UI应保持Hided");
            // 确认“highStateBeforeRelease”等于显示过渡态。
            Assert.AreEqual(UIState.Showing, highStateBeforeRelease, "低UI请求不得取消高优先级Showing");
            // 确认“highOperation”的异步结果为空。
            Assert.IsNull(highOperation.Result, $"高优先级显示不应被低UI取消：{highOperation.Result}");
            // 确认“highUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, highUI.State, "释放处理器后高优先级UI应完成显示");
        }

        /// <summary>
        ///  验证高优先级同步请求会取消正在异步显示的低优先级旧请求。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_MutexPriority_HigherShouldPreemptLowerShowingAndCancelOldRequest()
        {
            // 低优先级异步行为故意忽略取消令牌，用于验证过期任务不会提交Showed。
            var container = CreateObject("HigherPreemptsLowerShowingRoot");
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("PreemptedLowerShowing", container.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("PreemptingHigher", container.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;
            // 初始化“releaseLowAction”为false，作为后续回调的观测基准。
            var releaseLowAction = false;
            // 为“lowUI.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            lowUI.ShowAsyncActions += _ => UniTask.WaitUntil(() => releaseLowAction);

            // 先启动低优先级异步显示并让它进入Showing。
            Task<Exception> lowOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));
            // 保存“lowStateBeforePreemption”的基准值隐藏终态，供后续比较或恢复。
            var lowStateBeforePreemption = UIState.Hided;
            // 保存“lowStateAfterPreemption”的基准值隐藏终态，供后续比较或恢复。
            var lowStateAfterPreemption = UIState.Hided;
            // 保存“highStateAfterPreemption”的基准值隐藏终态，供后续比较或恢复。
            var highStateAfterPreemption = UIState.Hided;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 将“lowStateBeforePreemption”设为“lowUI”的界面状态，建立当前测试条件。
                lowStateBeforePreemption = lowUI.State;

                // 高优先级同步显示应隐藏低UI并取得组显示权。
                highUI.Show();
                // 将“lowStateAfterPreemption”设为“lowUI”的界面状态，建立当前测试条件。
                lowStateAfterPreemption = lowUI.State;
                // 将“highStateAfterPreemption”设为“highUI”的界面状态，建立当前测试条件。
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
            // 确认“lowStateAfterPreemption”等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowStateAfterPreemption, "高优先级抢占应完整隐藏低UI");
            // 确认“highStateAfterPreemption”等于显示终态。
            Assert.AreEqual(UIState.Showed, highStateAfterPreemption, "高优先级抢占后应完成显示");
            // 确认“lowOperation”的异步结果保留取消语义，不被报告为成功。
            Assert.That(lowOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被抢占的低优先级旧任务应传播OperationCanceledException");
            // 确认“lowUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowUI.State, "旧任务收口后低UI不得回写Showed");
            // 确认“highUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, highUI.State, "旧任务收口后高UI应保持Showed");
        }

        /// <summary>
        ///  验证更新高优先级显示在取消旧任务前已登记预约，可阻止取消回调重入低UI。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Show_MutexPriority_NewReservationShouldBlockLowerCancellationCallbackReentry()
        {
            // 高低UI位于同一父节点和互斥组，高UI配置严格更高的抢占优先级。
            var container = CreateObject("ReservationBlocksReentryRoot");
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("ReservationReentryLow", container.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("ReservationReentryHigh", container.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;
            // 初始化“releaseOldShow”为false，作为后续回调的观测基准。
            var releaseOldShow = false;
            // 初始化“cancellationCallbackCount”为0，作为后续回调的观测基准。
            var cancellationCallbackCount = 0;
            // 初始化“callbackRegistration”为“default(CancellationTokenRegistration)”，作为后续回调的观测基准。
            var callbackRegistration = default(CancellationTokenRegistration);

            // 旧异步显示被更新取消时，同步回调会立即尝试重入显示低优先级UI。
            highUI.ShowAsyncActions += cancellationToken =>
            {
                // 注册旧操作的取消回调并保存注册句柄，构造取消期间改变环境的重入场景。
                callbackRegistration = cancellationToken.Register(() =>
                {
                    // 更新“cancellationCallbackCount”的计数，记录回调实际执行次数。
                    cancellationCallbackCount++;
                    // 提交“lowUI”的显示请求，触发实际生命周期。
                    lowUI.Show();
                });
                // 返回“UniTask.WaitUntil(() => releaseOldShow)”，交给调用方继续配置或断言。
                return UniTask.WaitUntil(() => releaseOldShow);
            };

            // 启动旧高UI显示，并等待处理器完成取消回调登记。
            Task<Exception> oldHighOperation = CaptureException(highUI.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;

                // 同步更新同一高UI会在Begin阶段取消旧令牌并同步执行低UI重入回调。
                highUI.Show();

                // 允许忽略取消的旧处理器结束，再完整观察旧任务传播更新取消。
                releaseOldShow = true;
                // 等待“WaitForTask(oldHighOperation)”，再检查操作完成后的状态。
                yield return WaitForTask(oldHighOperation);
            }
            finally
            {
                // 任意中间失败都释放旧门闩并清理测试注册，避免遗留后台任务或回调。
                releaseOldShow = true;
                // 释放“callbackRegistration”，恢复注册或全局配置。
                callbackRegistration.Dispose();
            }

            // 取消回调应实际执行，但必须观察到新高UI预约并零副作用拒绝低UI。
            Assert.AreEqual(1, cancellationCallbackCount, "更新显示应且只应取消一次旧高UI令牌");
            // 确认“oldHighOperation”的异步结果保留取消语义，不被报告为成功。
            Assert.That(oldHighOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被同步显示更新取代的旧高UI任务应传播OperationCanceledException");
            // 确认“lowUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowUI.State, "取消回调中的低优先级重入应被新预约阻止");
            // 确认“highUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, highUI.State, "新高优先级同步显示应最终提交Showed");
        }

        /// <summary>
        ///  验证相等优先级的取消回调重入仍保留最后一个请求获胜语义。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Show_MutexPriority_EqualCancellationCallbackReentryShouldLetLatestRequestWin()
        {
            // 两个同父同组UI使用相同非零优先级，避免该测试退化成数值高低比较。
            var container = CreateObject("EqualReservationReentryRoot");
            // 创建“firstUI”并配置测试所需的层级或表现组件。
            var firstUI = CreateUI("EqualReservationFirst", container.transform);
            // 创建“latestUI”并配置测试所需的层级或表现组件。
            var latestUI = CreateUI("EqualReservationLatest", container.transform);
            // 将“firstUI”的互斥优先级设为1，建立当前测试条件。
            firstUI.MutexPriority = 1;
            // 将“latestUI”的互斥优先级设为1，建立当前测试条件。
            latestUI.MutexPriority = 1;
            // 初始化“releaseOldShow”为false，作为后续回调的观测基准。
            var releaseOldShow = false;
            // 初始化“cancellationCallbackCount”为0，作为后续回调的观测基准。
            var cancellationCallbackCount = 0;
            // 初始化“callbackRegistration”为“default(CancellationTokenRegistration)”，作为后续回调的观测基准。
            var callbackRegistration = default(CancellationTokenRegistration);

            // 更新firstUI时，旧任务的同步取消回调会产生时间上更晚的latestUI显示请求。
            firstUI.ShowAsyncActions += cancellationToken =>
            {
                // 注册旧操作的取消回调并保存注册句柄，构造取消期间改变环境的重入场景。
                callbackRegistration = cancellationToken.Register(() =>
                {
                    // 更新“cancellationCallbackCount”的计数，记录回调实际执行次数。
                    cancellationCallbackCount++;
                    // 提交“latestUI”的显示请求，触发实际生命周期。
                    latestUI.Show();
                });
                // 返回“UniTask.WaitUntil(() => releaseOldShow)”，交给调用方继续配置或断言。
                return UniTask.WaitUntil(() => releaseOldShow);
            };

            // 先使firstUI持有旧异步显示，再用同步请求触发取消回调重入。
            Task<Exception> oldFirstOperation = CaptureException(firstUI.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 提交“firstUI”的显示请求，触发实际生命周期。
                firstUI.Show();

                // 重入请求结束后释放旧处理器，并观察最初异步任务的取消结果。
                releaseOldShow = true;
                // 等待“WaitForTask(oldFirstOperation)”，再检查操作完成后的状态。
                yield return WaitForTask(oldFirstOperation);
            }
            finally
            {
                // 中间断言或调度异常同样不能留下忽略令牌的门闩任务。
                releaseOldShow = true;
                // 释放“callbackRegistration”，恢复注册或全局配置。
                callbackRegistration.Dispose();
            }

            // 相等优先级不互相阻止，取消回调中的最新请求应隐藏firstUI并获胜。
            Assert.AreEqual(1, cancellationCallbackCount, "旧firstUI令牌应且只应触发一次取消回调");
            // 确认“oldFirstOperation”的异步结果保留取消语义，不被报告为成功。
            Assert.That(oldFirstOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被更新取代的旧firstUI任务应传播OperationCanceledException");
            // 确认“firstUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, firstUI.State, "相等优先级的较早请求应由最新请求隐藏");
            // 确认“latestUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, latestUI.State, "相等优先级的取消回调最新请求应获胜");
        }

        /// <summary>
        ///  验证同步更新显示的取消回调改变关系后，二次准入失败会收口旧Showing状态。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Show_MutexPriority_CancellationCallbackAddsHigherSibling_ShouldCloseStaleShowing()
        {
            // 低UI正在异步显示，高UI先在另一父节点完成显示并保持更高优先级。
            var requestContainer = CreateObject("SyncSecondAdmissionRequestRoot");
            // 创建“sourceContainer”并配置测试所需的层级或表现组件。
            var sourceContainer = CreateObject("SyncSecondAdmissionSourceRoot");
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("SyncSecondAdmissionLow", requestContainer.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("SyncSecondAdmissionHigh", sourceContainer.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;
            // 提交“highUI”的显示请求，触发实际生命周期。
            highUI.Show();
            // 初始化“releaseOldShow”为false，作为后续回调的观测基准。
            var releaseOldShow = false;
            // 初始化“callbackRegistration”为“default(CancellationTokenRegistration)”，作为后续回调的观测基准。
            var callbackRegistration = default(CancellationTokenRegistration);

            // 旧低UI任务被更新时只修改兄弟关系，不直接调用任何显隐API。
            lowUI.ShowAsyncActions += cancellationToken =>
            {
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                callbackRegistration = cancellationToken.Register(() =>
                    highUI.transform.SetParent(requestContainer.transform, false));
                // 返回“UniTask.WaitUntil(() => releaseOldShow)”，交给调用方继续配置或断言。
                return UniTask.WaitUntil(() => releaseOldShow);
            };

            // 启动旧异步显示后，以同步Show更新同一UI并触发关系变更回调。
            Task<Exception> oldLowOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 确认“lowUI”的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, lowUI.State, "更新前低UI应处于旧异步Showing阶段");
                // 提交“lowUI”的显示请求，触发实际生命周期。
                lowUI.Show();

                // 释放忽略取消的旧处理器并等待原任务传播更新取消。
                releaseOldShow = true;
                // 等待“WaitForTask(oldLowOperation)”，再检查操作完成后的状态。
                yield return WaitForTask(oldLowOperation);
            }
            finally
            {
                // 中间失败同样释放门闩并清理同步取消回调注册。
                releaseOldShow = true;
                // 释放“callbackRegistration”，恢复注册或全局配置。
                callbackRegistration.Dispose();
            }

            // 二次准入发现高UI后必须执行隐藏收口，不能让失效请求永久残留Showing。
            Assert.That(oldLowOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被同步请求更新的旧低UI任务应传播OperationCanceledException");
            // 确认“lowUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowUI.State, "二次准入失败应把旧低UI过渡状态收口为Hided");
            // 确认“highUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, highUI.State, "取消回调换父后的高UI应继续保持Showed");
        }

        /// <summary>
        ///  验证异步更新显示的取消回调改变关系后，二次准入失败会取消新请求并收口。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_MutexPriority_CancellationCallbackAddsHigherSibling_ShouldCancelReplacement()
        {
            // 低UI正在异步显示，高UI先在独立父节点取得稳定Showed状态。
            var requestContainer = CreateObject("AsyncSecondAdmissionRequestRoot");
            // 创建“sourceContainer”并配置测试所需的层级或表现组件。
            var sourceContainer = CreateObject("AsyncSecondAdmissionSourceRoot");
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("AsyncSecondAdmissionLow", requestContainer.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("AsyncSecondAdmissionHigh", sourceContainer.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;
            // 提交“highUI”的显示请求，触发实际生命周期。
            highUI.Show();
            // 初始化“releaseOldShow”为false，作为后续回调的观测基准。
            var releaseOldShow = false;
            // 初始化“callbackRegistration”为“default(CancellationTokenRegistration)”，作为后续回调的观测基准。
            var callbackRegistration = default(CancellationTokenRegistration);

            // 旧低UI令牌取消时把高UI移入当前组，使刚登记的替代请求失去准入资格。
            lowUI.ShowAsyncActions += cancellationToken =>
            {
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                callbackRegistration = cancellationToken.Register(() =>
                    highUI.transform.SetParent(requestContainer.transform, false));
                // 返回“UniTask.WaitUntil(() => releaseOldShow)”，交给调用方继续配置或断言。
                return UniTask.WaitUntil(() => releaseOldShow);
            };

            // 第二个ShowAsync替代旧请求，并在Begin内部同步执行旧令牌取消回调。
            Task<Exception> oldLowOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));
            // 初始化“replacementOperation”为空引用，作为后续回调的观测基准。
            Task<Exception> replacementOperation = null;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 将“replacementOperation”设为“CaptureException(lowUI.ShowAsync(CancellationToken.None))”，建立当前测试条件。
                replacementOperation = CaptureException(lowUI.ShowAsync(CancellationToken.None));

                // 新请求应立即因二次准入失败取消，旧处理器仍需释放后才能完整结束。
                releaseOldShow = true;
                // 等待“WaitForTask(Task.WhenAll(oldLowOperation, replacementOperation))”，再检查操作完成后的状态。
                yield return WaitForTask(Task.WhenAll(oldLowOperation, replacementOperation));
            }
            finally
            {
                // 任意等待失败都释放旧门闩并注销回调，避免污染后续PlayMode用例。
                releaseOldShow = true;
                // 释放“callbackRegistration”，恢复注册或全局配置。
                callbackRegistration.Dispose();
            }

            // 两轮任务都被更新隐藏取代，最终低UI稳定Hided且高UI继续持权。
            Assert.That(oldLowOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "旧低UI任务应传播OperationCanceledException");
            // 确认“replacementOperation”的异步结果保留取消语义，不被报告为成功。
            Assert.That(replacementOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "二次准入失败的替代请求应传播OperationCanceledException");
            // 确认“lowUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowUI.State, "异步替代请求失败后低UI应稳定收口Hided");
            // 确认“highUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, highUI.State, "换父进入当前组的高UI应保持Showed");
        }

        /// <summary>
        ///  验证高优先级UI在Hiding完成前持续占权，且被拒绝的低UI不会自动排队。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator HideAsync_MutexPriority_HigherHidingShouldBlockUntilHidedWithoutQueue()
        {
            // 高UI先进入Showed，再用门闩挂起其异步隐藏行为。
            var container = CreateObject("HigherHidingBlocksLowerRoot");
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("BlockedDuringHigherHiding", container.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("HigherHiding", container.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;
            // 提交“highUI”的显示请求，触发实际生命周期。
            highUI.Show();
            // 初始化“releaseHighHide”为false，作为后续回调的观测基准。
            var releaseHighHide = false;
            // 为“highUI.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            highUI.HideAsyncActions += _ => UniTask.WaitUntil(() => releaseHighHide);

            // 启动高UI隐藏并等待其进入Hiding。
            Task<Exception> hideOperation = CaptureException(highUI.HideAsync(CancellationToken.None));
            // 保存“lowStateDuringHighHide”的基准值显示终态，供后续比较或恢复。
            var lowStateDuringHighHide = UIState.Showed;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;

                // Hiding期间请求低UI，请求应被拒绝且不创建延迟队列。
                lowUI.Show();
                // 将“lowStateDuringHighHide”设为“lowUI”的界面状态，建立当前测试条件。
                lowStateDuringHighHide = lowUI.State;
            }
            finally
            {
                // 中间调度失败时也释放异步隐藏门闩，避免污染后续测试。
                releaseHighHide = true;
            }

            // 释放高UI隐藏并等待它真正到达Hided。
            yield return WaitForTask(hideOperation);
            // 保存“lowStateAfterHighHided”的基准值“lowUI”的界面状态，供后续比较或恢复。
            var lowStateAfterHighHided = lowUI.State;

            // 不实现隐式排队，调用方需要在高UI完成隐藏后重新请求。
            lowUI.Show();

            // Hiding占权、无队列和重试成功三个阶段均应符合预期。
            Assert.AreEqual(UIState.Hided, lowStateDuringHighHide, "高优先级Hiding期间应阻止低UI");
            // 确认隐藏操作（hideOperation）的异步结果为空。
            Assert.IsNull(hideOperation.Result, $"高UI异步隐藏应正常完成：{hideOperation.Result}");
            // 确认“lowStateAfterHighHided”等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowStateAfterHighHided, "先前被拒绝的低UI不应在高UI隐藏后自动弹出");
            // 确认“lowUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, lowUI.State, "高UI已Hided后重新请求应允许低UI显示");
        }

        /// <summary>
        ///  验证高优先级异步隐藏被取消后不会以残留Hiding状态永久阻塞同组低UI。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator HideAsync_MutexPriority_CanceledHigherHideShouldReleaseGroupForRecovery()
        {
            // 高UI先显示，并让其异步隐藏只能通过调用方令牌取消。
            var container = CreateObject("CanceledHigherHideRecoveryRoot");
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("CanceledHigherHideLow", container.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("CanceledHigherHideHigh", container.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;
            // 提交“highUI”的显示请求，触发实际生命周期。
            highUI.Show();
            // 为“highUI.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            highUI.HideAsyncActions += WaitUntilCanceled;
            // 创建“cancellationTokenSource”，控制本次显隐的主动取消时机。
            using var cancellationTokenSource = new CancellationTokenSource();

            // 启动隐藏并等待状态进入真实执行中的Hiding阶段。
            Task<Exception> hideOperation = CaptureException(highUI.HideAsync(cancellationTokenSource.Token));
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 确认“highUI”的界面状态等于隐藏过渡态。
            Assert.AreEqual(UIState.Hiding, highUI.State, "取消前高优先级UI应处于有效Hiding阶段");

            // 取消本轮隐藏并完整观察任务收口，当前实现保留中断位置用于业务诊断。
            cancellationTokenSource.Cancel();
            // 等待“WaitForTask(hideOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(hideOperation);
            // 确认隐藏操作（hideOperation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(hideOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "调用方取消高UI隐藏应传播OperationCanceledException");
            // 确认“highUI”的界面状态等于隐藏过渡态。
            Assert.AreEqual(UIState.Hiding, highUI.State, "取消后的高UI应保留未完成隐藏的Hiding状态");

            // 低UI的新请求应取得准入，并通过一次同步隐藏把高UI残留状态恢复为Hided。
            lowUI.Show();

            // 已结束的隐藏操作不再占权，恢复过程也不能留下两个同组可见UI。
            Assert.AreEqual(UIState.Hided, highUI.State, "后续同组请求应完整收口取消后的高UI");
            // 确认“lowUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, lowUI.State, "取消的高UI隐藏不应永久阻塞低UI");
        }

        /// <summary>
        ///  验证高优先级异步隐藏故障后不会以残留Hiding状态永久阻塞同组低UI。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator HideAsync_MutexPriority_FailedHigherHideShouldReleaseGroupForRecovery()
        {
            // 高UI先显示，并使用延后一帧抛错的处理器覆盖异步故障收口路径。
            var container = CreateObject("FailedHigherHideRecoveryRoot");
            // 创建“lowUI”并配置测试所需的层级或表现组件。
            var lowUI = CreateUI("FailedHigherHideLow", container.transform);
            // 创建“highUI”并配置测试所需的层级或表现组件。
            var highUI = CreateUI("FailedHigherHideHigh", container.transform);
            // 将“highUI”的互斥优先级设为1，建立当前测试条件。
            highUI.MutexPriority = 1;
            // 提交“highUI”的显示请求，触发实际生命周期。
            highUI.Show();
            // 为“highUI.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            highUI.HideAsyncActions += ThrowHideFailureAfterYield;

            // 等待高UI隐藏处理器故障并记录未完成隐藏所保留的状态。
            Task<Exception> hideOperation = CaptureException(highUI.HideAsync(CancellationToken.None));
            // 等待“WaitForTask(hideOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(hideOperation);
            // 确认隐藏操作（hideOperation）的异步结果满足约束“Is.InstanceOf<InvalidOperationException>()”。
            Assert.That(hideOperation.Result, Is.InstanceOf<InvalidOperationException>(),
                "高UI隐藏处理器故障应保留原始InvalidOperationException");
            // 确认“highUI”的界面状态等于隐藏过渡态。
            Assert.AreEqual(UIState.Hiding, highUI.State, "故障后的高UI应保留未完成隐藏的Hiding状态");

            // 后续低UI请求应通过新隐藏生命周期修复高UI，而不是被残留状态永久拒绝。
            lowUI.Show();

            // 故障操作已经结束后不再持有优先级，组内最终仍只能有一个Showed面板。
            Assert.AreEqual(UIState.Hided, highUI.State, "后续同组请求应完整收口故障后的高UI");
            // 确认“lowUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, lowUI.State, "故障的高UI隐藏不应永久阻塞低UI");
        }

        /// <summary>
        ///  验证父UI在严格子UI异步显示期间隐藏时会取消子请求并完整收口。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_RequireParentChainShowed_ParentHiddenDuringChildAction_ShouldCancelChild()
        {
            // 父UI先真实显示，使严格子UI的初始准入检查通过。
            var container = CreateObject("ParentHiddenDuringStrictChildRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("ParentHiddenDuringStrictChild", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictChildInterruptedByParent", parentUI.transform);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 提交“parentUI”的显示请求，触发实际生命周期。
            parentUI.Show();

            // 子UI显示处理器忽略取消令牌，验证父隐藏的版本接管足以阻止旧请求提交。
            var releaseChildAction = false;
            // 初始化“afterShowCount”为0，作为后续回调的观测基准。
            var afterShowCount = 0;
            // 为“childUI.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.ShowAsyncActions += _ => UniTask.WaitUntil(() => releaseChildAction);
            // 为“childUI.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.AfterUIShowedEvent += () => afterShowCount++;

            // 启动子UI异步显示并等待进入Showing。
            Task<Exception> childOperation = CaptureException(childUI.ShowAsync(CancellationToken.None));
            // 保存“childStateBeforeParentHide”的基准值隐藏终态，供后续比较或恢复。
            var childStateBeforeParentHide = UIState.Hided;
            // 保存“childStateAfterParentHide”的基准值显示终态，供后续比较或恢复。
            var childStateAfterParentHide = UIState.Showed;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
                yield return null;
                // 将“childStateBeforeParentHide”设为“childUI”的界面状态，建立当前测试条件。
                childStateBeforeParentHide = childUI.State;

                // 父UI隐藏必须通过子UI关系联动取消当前子显示。
                parentUI.Hide();
                // 将“childStateAfterParentHide”设为“childUI”的界面状态，建立当前测试条件。
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
            // 确认“childStateAfterParentHide”等于隐藏终态。
            Assert.AreEqual(UIState.Hided, childStateAfterParentHide, "父UI隐藏应立即收口子UI为Hided");
            // 确认“childOperation”的异步结果保留取消语义，不被报告为成功。
            Assert.That(childOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被父隐藏取代的子显示应传播OperationCanceledException");
            // 确认“afterShowCount”等于0。
            Assert.AreEqual(0, afterShowCount, "被取消的子显示不得触发After事件");
            // 确认“childUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, childUI.State, "旧子请求收口后不得覆盖Hided状态");
        }

        /// <summary>
        ///  验证父级联在启动行为前就跳过同组严格低优先级候选。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_ParentCascade_ShouldSkipLowerPriorityCandidateBeforeStartingHandler()
        {
            // 低优先级子UI先创建以保证其Hierarchy顺序位于高优先级之前。
            var container = CreateObject("ParentCascadePriorityRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("ParentCascadePriorityParent", container.transform);
            // 创建“lowChild”并配置测试所需的层级或表现组件。
            var lowChild = CreateUI("ParentCascadeLowCandidate", parentUI.transform);
            // 创建“highChild”并配置测试所需的层级或表现组件。
            var highChild = CreateUI("ParentCascadeHighCandidate", parentUI.transform);
            // 将“lowChild”的随父显示配置设为true，建立当前测试条件。
            lowChild.ShowWithParent = true;
            // 将“highChild”的随父显示配置设为true，建立当前测试条件。
            highChild.ShowWithParent = true;
            // 将“highChild”的互斥优先级设为1，建立当前测试条件。
            highChild.MutexPriority = 1;

            // 两个处理器都立即完成，计数可直接表明低候选是否被错误启动。
            var lowActionCount = 0;
            // 初始化“highActionCount”为0，作为后续回调的观测基准。
            var highActionCount = 0;
            // 为“lowChild.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            lowChild.ShowAsyncActions += _ =>
            {
                // 更新“lowActionCount”的计数，记录回调实际执行次数。
                lowActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };
            // 为“highChild.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            highChild.ShowAsyncActions += _ =>
            {
                // 更新“highActionCount”的计数，记录回调实际执行次数。
                highActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };

            // 异步显示父UI，由父级联批次选择同组默认子UI。
            Task<Exception> parentOperation = CaptureException(parentUI.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(parentOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(parentOperation);

            // 批次应在行为启动前选中高优先级，结果不得依赖Hierarchy顺序。
            Assert.IsNull(parentOperation.Result, $"父UI异步显示应正常完成：{parentOperation.Result}");
            // 确认“lowActionCount”等于0。
            Assert.AreEqual(0, lowActionCount, "父级联不应启动必然被抢占的低优先级处理器");
            // 确认“highActionCount”等于1。
            Assert.AreEqual(1, highActionCount, "父级联应且只应启动一次高优先级处理器");
            // 确认“lowChild”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, lowChild.State, "低优先级默认子UI应保持Hided");
            // 确认“highChild”的界面状态等于显示终态。
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
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("ExplicitLowerPriorityParent", container.transform);
            // 创建“requestedChild”并配置测试所需的层级或表现组件。
            var requestedChild = CreateUI("ExplicitLowerPriorityChild", parentUI.transform);
            // 创建“defaultHighChild”并配置测试所需的层级或表现组件。
            var defaultHighChild = CreateUI("DefaultHigherPriorityChild", parentUI.transform);
            // 将“requestedChild”的随父显示配置设为true，建立当前测试条件。
            requestedChild.ShowWithParent = true;
            // 将“defaultHighChild”的随父显示配置设为true，建立当前测试条件。
            defaultHighChild.ShowWithParent = true;
            // 将“defaultHighChild”的互斥优先级设为1，建立当前测试条件。
            defaultHighChild.MutexPriority = 1;

            // 记录高优先级默认候选是否被父级联错误启动。
            var requestedShowCount = 0;
            // 初始化“defaultHighShowCount”为0，作为后续回调的观测基准。
            var defaultHighShowCount = 0;
            // 为“requestedChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            requestedChild.ShowActions += () => requestedShowCount++;
            // 为“defaultHighChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            defaultHighChild.ShowActions += () => defaultHighShowCount++;

            // 显式请求低优先级子UI，它会通过默认父链策略拉起父UI。
            requestedChild.Show();

            // 隐藏的高优先级候选不预占组，父级联也必须让显式请求先完成。
            Assert.AreEqual(UIState.Showed, parentUI.State, "显式子UI请求应正常补齐父UI");
            // 确认“requestedChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, requestedChild.State, "低优先级显式子UI应完成显示");
            // 确认“defaultHighChild”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, defaultHighChild.State, "高优先级默认候选不得反向取代显式请求");
            // 确认“requestedShowCount”等于1。
            Assert.AreEqual(1, requestedShowCount, "显式子UI显示行为应且只应执行一次");
            // 确认“defaultHighShowCount”等于0。
            Assert.AreEqual(0, defaultHighShowCount, "高优先级默认候选不应启动显示行为");
        }

        /// <summary>
        ///  验证Start会将父链未就绪的严格序列化Showed子UI规范化为Hided。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Start_RequiredSerializedShowedChildWithUnreadyParent_ShouldNormalizeToHided()
        {
            // CreateUI默认禁用组件，因此可以在Start前写入精确序列化初态。
            var container = CreateObject("StrictSerializedStartRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("UnreadySerializedShowedParent", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictSerializedShowedChild", parentUI.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parentUI, "state", UIState.Showed);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(childUI, "state", UIState.Showed);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 记录Start初始化实际选择的显示或隐藏生命周期。
            var showActionCount = 0;
            // 初始化“hideActionCount”为0，作为后续回调的观测基准。
            var hideActionCount = 0;
            // 为“childUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.ShowActions += () => showActionCount++;
            // 为“childUI.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.HideActions += () => hideActionCount++;

            // 仅启用子UI组件，父UI保持未初始化的序列化Showed状态。
            childUI.enabled = true;
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 不可见的假Showed初态应通过一次完整隐藏生命周期收口。
            Assert.AreEqual(UIState.Hided, childUI.State, "严格子UI的未就绪Showed初态应被规范化为Hided");
            // 确认“showActionCount”等于0。
            Assert.AreEqual(0, showActionCount, "父链未就绪时Start不应启动显示行为");
            // 确认“hideActionCount”等于1。
            Assert.AreEqual(1, hideActionCount, "父链未就绪时Start应执行一次隐藏行为完成收口");
        }

        /// <summary>
        ///  验证父子都启用且序列化Showed时，严格子UI先执行Start也会等待父UI初始化。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Start_RequiredSerializedShowedParentAndChild_ChildStartsFirstShouldRemainShowed()
        {
            // 父子UI都在Start前写入Showed，子UI要求真实父链Showed后才允许直接显示。
            var container = CreateObject("StrictSerializedStartOrderRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("StrictSerializedStartOrderParent", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictSerializedStartOrderChild", parentUI.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parentUI, "state", UIState.Showed);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(childUI, "state", UIState.Showed);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;

            // 两个组件都处于可启动状态，手动先调用子Start以稳定复现Unity未保证的执行顺序。
            parentUI.enabled = true;
            // 将“childUI”的组件启用状态设为true，建立当前测试条件。
            childUI.enabled = true;
            // 初始化“parentShowCount”为0，作为后续回调的观测基准。
            var parentShowCount = 0;
            // 初始化“childShowCount”为0，作为后续回调的观测基准。
            var childShowCount = 0;
            // 初始化“childHideCount”为0，作为后续回调的观测基准。
            var childHideCount = 0;
            // 为“parentUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parentUI.ShowActions += () => parentShowCount++;
            // 为“childUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.ShowActions += () => childShowCount++;
            // 为“childUI.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.HideActions += () => childHideCount++;
            // 调用 UIBase 的真实初始化入口，确定性验证指定父子 Start 顺序。
            InvokeUIBaseStartForTest(childUI);
            // 确认“childShowCount”等于0。
            Assert.AreEqual(0, childShowCount, "父UI尚未初始化时严格子Start应先延后显示生命周期");
            // 确认“childHideCount”等于0。
            Assert.AreEqual(0, childHideCount, "可启动父UI尚未执行Start时不应提前把子初态规范化为Hided");

            // 父Start先完成真实显示，延后的子初始化应在后续帧按最新父链状态恢复显示。
            InvokeUIBaseStartForTest(parentUI);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 无论Unity随后如何调度自动Start，父子都只应初始化一次并稳定保持Showed。
            Assert.AreEqual(1, parentShowCount, "父UI序列化Showed初态应且只应执行一次显示行为");
            // 确认“childShowCount”等于1。
            Assert.AreEqual(1, childShowCount, "严格子UI应在父UI就绪后且只执行一次显示行为");
            // 确认“childHideCount”等于0。
            Assert.AreEqual(0, childHideCount, "父子同为Showed初态时不应因Start顺序误隐藏子UI");
            // 确认“parentUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parentUI.State, "父UI初始化后应保持Showed");
            // 确认“childUI”的界面状态等于显示终态。
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
            // 创建“grandParentUI”并配置测试所需的层级或表现组件。
            var grandParentUI = CreateUI("StrictStartPendingGrandParent", container.transform);
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("StrictStartHidedParent", grandParentUI.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictStartKnownFailureChild", parentUI.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parentUI, "_isUIStateInitialized", true);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(childUI, "state", UIState.Showed);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 将“grandParentUI”的组件启用状态设为true，建立当前测试条件。
            grandParentUI.enabled = true;
            // 将“childUI”的组件启用状态设为true，建立当前测试条件。
            childUI.enabled = true;

            // 记录子UI是否在本次Start调用内立即选择完整隐藏生命周期。
            var hideActionCount = 0;
            // 为“childUI.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.HideActions += () => hideActionCount++;
            // 调用 UIBase 的真实初始化入口，确定性验证指定父子 Start 顺序。
            InvokeUIBaseStartForTest(childUI);

            // 直接父Hided已经足够拒绝显示，不应等待与最终结论无关的祖父Start。
            Assert.AreEqual(UIState.Hided, childUI.State, "确定失败的直接父链应立即规范化子UI为Hided");
            // 确认“hideActionCount”等于1。
            Assert.AreEqual(1, hideActionCount, "确定失败时子Start应立即执行一次隐藏行为");
        }

        /// <summary>
        ///  验证严格子初态等待期间GameObject失活后，重新激活会恢复延后初始化流程。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Start_RequiredSerializedShowedChild_DeactivatedWhileDeferredShouldResumeOnEnable()
        {
            // 父子都启用并写入Showed，手动先启动子UI以进入等待父Start的协程。
            var container = CreateObject("StrictStartDeactivateResumeRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("StrictStartDeactivateResumeParent", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("StrictStartDeactivateResumeChild", parentUI.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parentUI, "state", UIState.Showed);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(childUI, "state", UIState.Showed);
            // 将“childUI”的父链准入策略设为“UIParentShowPolicy.RequireParentChainShowedForDirectShow”，建立当前测试条件。
            childUI.ParentShowPolicy = UIParentShowPolicy.RequireParentChainShowedForDirectShow;
            // 将“parentUI”的组件启用状态设为true，建立当前测试条件。
            parentUI.enabled = true;
            // 将“childUI”的组件启用状态设为true，建立当前测试条件。
            childUI.enabled = true;
            // 初始化“childShowCount”为0，作为后续回调的观测基准。
            var childShowCount = 0;
            // 初始化“childHideCount”为0，作为后续回调的观测基准。
            var childHideCount = 0;
            // 为“childUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.ShowActions += () => childShowCount++;
            // 为“childUI.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.HideActions += () => childHideCount++;
            // 调用 UIBase 的真实初始化入口，确定性验证指定父子 Start 顺序。
            InvokeUIBaseStartForTest(childUI);

            // 失活子GameObject会停止其托管协程，随后单独完成父UI真实显示初始化。
            childUI.gameObject.SetActive(false);
            // 调用 UIBase 的真实初始化入口，确定性验证指定父子 Start 顺序。
            InvokeUIBaseStartForTest(parentUI);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 确认“childShowCount”等于0。
            Assert.AreEqual(0, childShowCount, "失活期间严格子UI不应执行显示行为");

            // 重新激活时OnEnable必须续接未完成初态，并按已经Showed的父链恢复子UI。
            childUI.gameObject.SetActive(true);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 恢复流程只能初始化一次，不能留下未初始化假Showed或错误隐藏行为。
            Assert.AreEqual(1, childShowCount, "重新激活后严格子初态应且只应执行一次显示行为");
            // 确认“childHideCount”等于0。
            Assert.AreEqual(0, childHideCount, "父链已就绪时恢复延后初态不应执行隐藏行为");
            // 确认“parentUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parentUI.State, "恢复子UI前父UI应保持真实Showed");
            // 确认“childUI”的界面状态等于显示终态。
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
            // 确认“startMethod”存在。
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
