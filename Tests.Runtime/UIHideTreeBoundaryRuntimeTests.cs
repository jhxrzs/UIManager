using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的深层隐藏资格与关系边界。</summary>
    public partial class Test_UIBase
    {
        /// <summary>创建可同时显示的先后两个子分支，固定顺序以验证回调对后续隐藏的影响。</summary>
        /// <param name="parent">新对象的父节点或输出的根界面。</param>
        /// <param name="first">输出按层级顺序先处理的子分支。</param>
        /// <param name="later">输出按层级顺序后处理的子分支。</param>
        private void CreateHideTreeBoundaryBranches(out UIBase parent, out UIBase first, out UIBase later)
        {
            // 将父界面（parent）设为“CreateUI("HideTreeBoundaryParent", CreateObject("HideTreeBoundaryContainer").transform)”，建立当前测试条件。
            parent = CreateUI("HideTreeBoundaryParent", CreateObject("HideTreeBoundaryContainer").transform);
            // 将首项（first）设为“CreateUI("FirstBranch", parent.transform)”，建立当前测试条件。
            first = CreateUI("FirstBranch", parent.transform);
            // 将后续界面（later）设为“CreateUI("LaterBranch", parent.transform)”，建立当前测试条件。
            later = CreateUI("LaterBranch", parent.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(later, "mutexGroup", 1);
            // 提交父界面（parent）的显示请求，触发实际生命周期。
            parent.Show();
            // 提交首项（first）的显示请求，触发实际生命周期。
            first.Show();
            // 提交后续界面（later）的显示请求，触发实际生命周期。
            later.Show();
            // 固定兄弟层级顺序，确保回调按测试指定的先后次序发生。
            first.transform.SetSiblingIndex(0);
            // 固定兄弟层级顺序，确保回调按测试指定的先后次序发生。
            later.transform.SetSiblingIndex(1);
        }

        /// <summary>分别记录给定面板的隐藏行为次数，检测递归遗漏或重复调用。</summary>
        /// <param name="panels">需要分别统计隐藏行为次数的面板集合。</param>
        /// <returns>与传入面板顺序对应的隐藏次数数组。</returns>
        private static int[] ObserveHideTreeBoundaryCalls(params UIBase[] panels)
        {
            // 创建回调次数（calls），隔离本用例的资源或观测数据。
            var calls = new int[panels.Length];
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var i = 0; i < panels.Length; i++)
            {
                // 保存“index”的基准值“i”，供后续比较或恢复。
                var index = i;
                // 为“panels[i].HideActions”追加观测或重入回调，验证通知次数及执行顺序。
                panels[i].HideActions += () => calls[index]++;
            }
            // 返回回调次数（calls），交给调用方继续配置或断言。
            return calls;
        }

        /// <summary>验证五层活动链在祖先失活后完整隐藏，同时保留入口失活分支与叶节点。</summary>
        [Test]
        public void HideTreeBoundary_FiveLevels_ShouldFinishActiveChainAndPreserveInitiallyInactiveNodes()
        {
            // 创建可同时显示的先后两个子分支，固定顺序以验证回调对后续隐藏的影响。
            CreateHideTreeBoundaryBranches(out var parent, out var first, out var later);
            // 创建“levelTwo”并配置测试所需的层级或表现组件。
            var levelTwo = CreateUI("LevelTwo", later.transform);
            // 创建“levelThree”并配置测试所需的层级或表现组件。
            var levelThree = CreateUI("LevelThree", levelTwo.transform);
            // 创建“activeLeaf”并配置测试所需的层级或表现组件。
            var activeLeaf = CreateUI("ActiveLeaf", levelThree.transform);
            // 创建“inactiveLeaf”并配置测试所需的层级或表现组件。
            var inactiveLeaf = CreateUI("InitiallyInactiveLeaf", levelThree.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(inactiveLeaf, "mutexGroup", 1);
            // 创建“inactiveBranch”并配置测试所需的层级或表现组件。
            var inactiveBranch = CreateUI("InitiallyInactiveBranch", parent.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(inactiveBranch, "mutexGroup", 2);
            // 创建“excludedLeaf”并配置测试所需的层级或表现组件。
            var excludedLeaf = CreateUI("LeafInsideInactiveBranch", inactiveBranch.transform);
            // 提交“activeLeaf”的显示请求，触发实际生命周期。
            activeLeaf.Show();
            // 提交“inactiveLeaf”的显示请求，触发实际生命周期。
            inactiveLeaf.Show();
            // 提交“excludedLeaf”的显示请求，触发实际生命周期。
            excludedLeaf.Show();
            // 切换“inactiveLeaf.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            inactiveLeaf.gameObject.SetActive(false);
            // 切换“inactiveBranch.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            inactiveBranch.gameObject.SetActive(false);
            // 固定兄弟层级顺序，确保回调按测试指定的先后次序发生。
            first.transform.SetSiblingIndex(0);
            // 固定兄弟层级顺序，确保回调按测试指定的先后次序发生。
            later.transform.SetSiblingIndex(1);
            // 创建“activeChain”，隔离本用例的资源或观测数据。
            var activeChain = new[] { parent, first, later, levelTwo, levelThree, activeLeaf };
            // 保存“activeCalls”的基准值“ObserveHideTreeBoundaryCalls(activeChain)”，供后续比较或恢复。
            var activeCalls = ObserveHideTreeBoundaryCalls(activeChain);
            // 保存“excludedCalls”的基准值“ObserveHideTreeBoundaryCalls(inactiveLeaf, inactiveBranch, excludedLeaf)”，供后续比较或恢复。
            var excludedCalls = ObserveHideTreeBoundaryCalls(inactiveLeaf, inactiveBranch, excludedLeaf);
            // 在“first.HideActions”中关闭物体，构造后续回调面对失活祖先的场景。
            first.HideActions += () => parent.gameObject.SetActive(false);
            // 逐项检查或处理“var panel in activeChain”，覆盖集合中的每个对象。
            foreach (var panel in activeChain)
            {
                // 确认“panel.gameObject”的层级激活状态为真。
                Assert.IsTrue(panel.gameObject.activeInHierarchy, panel.name);
                // 确认面板（panel）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, panel.State, panel.name);
            }
            // 确认“inactiveLeaf.gameObject”的层级激活状态为假。
            Assert.IsFalse(inactiveLeaf.gameObject.activeInHierarchy);
            // 确认“excludedLeaf.gameObject”的层级激活状态为假。
            Assert.IsFalse(excludedLeaf.gameObject.activeInHierarchy);

            // 提交父界面（parent）的隐藏请求，检查子树联动与终态。
            parent.Hide();

            // 逐项检查或处理“var panel in activeChain”，覆盖集合中的每个对象。
            foreach (var panel in activeChain)
                // 确认面板（panel）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, panel.State, panel.name);
            // 确认“activeCalls”符合“new[] { 1, 1, 1, 1, 1, 1 }”。
            Assert.That(activeCalls, Is.EqualTo(new[] { 1, 1, 1, 1, 1, 1 }),
                "Ancestor deactivation must preserve recursive work through every initially active UI level.");
            // 确认“inactiveLeaf”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, inactiveLeaf.State);
            // 确认“inactiveBranch”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, inactiveBranch.State);
            // 确认“excludedLeaf”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, excludedLeaf.State);
            // 确认“excludedCalls”符合“new[] { 0, 0, 0 }”。
            Assert.That(excludedCalls, Is.EqualTo(new[] { 0, 0, 0 }),
                "Initial eligibility must not include inactive leaves or descendants of inactive branches.");
        }

        /// <summary>验证后层扫描仍发现移入或新激活的节点，并保留新分支内部失活前的隐藏资格。</summary>
        [Test]
        public void HideTreeBoundary_LaterScan_ShouldDiscoverMovedActiveBranchAndNewlyActivatedChild()
        {
            // 创建可同时显示的先后两个子分支，固定顺序以验证回调对后续隐藏的影响。
            CreateHideTreeBoundaryBranches(out var parent, out var first, out var later);
            // 创建“reactivated”并配置测试所需的层级或表现组件。
            var reactivated = CreateUI("InitiallyInactiveChild", later.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(reactivated, "mutexGroup", 1);
            // 提交“reactivated”的显示请求，触发实际生命周期。
            reactivated.Show();
            // 切换“reactivated.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            reactivated.gameObject.SetActive(false);
            // 创建“movedBranch”并配置测试所需的层级或表现组件。
            var movedBranch = CreateUI("MovedActiveBranch", CreateObject("OutsideBeforeHide").transform);
            // 创建“movedFirst”并配置测试所需的层级或表现组件。
            var movedFirst = CreateUI("MovedFirstChild", movedBranch.transform);
            // 创建“movedLater”并配置测试所需的层级或表现组件。
            var movedLater = CreateUI("MovedLaterChild", movedBranch.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(movedLater, "mutexGroup", 1);
            // 创建“movedLeaf”并配置测试所需的层级或表现组件。
            var movedLeaf = CreateUI("MovedDeepLeaf", movedLater.transform);
            // 提交“movedFirst”的显示请求，触发实际生命周期。
            movedFirst.Show();
            // 提交“movedLeaf”的显示请求，触发实际生命周期。
            movedLeaf.Show();
            // 固定兄弟层级顺序，确保回调按测试指定的先后次序发生。
            movedFirst.transform.SetSiblingIndex(0);
            // 固定兄弟层级顺序，确保回调按测试指定的先后次序发生。
            movedLater.transform.SetSiblingIndex(1);
            // 创建“panels”，隔离本用例的资源或观测数据。
            var panels = new[] { parent, first, later, reactivated, movedBranch, movedFirst, movedLater, movedLeaf };
            // 保存回调次数（calls）的基准值“ObserveHideTreeBoundaryCalls(panels)”，供后续比较或恢复。
            var calls = ObserveHideTreeBoundaryCalls(panels);
            // 为“first.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            first.HideActions += () =>
            {
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                movedBranch.transform.SetParent(later.transform, false);
                // 切换“reactivated.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
                reactivated.gameObject.SetActive(true);
            };
            // 在“movedFirst.HideActions”中关闭物体，构造后续回调面对失活祖先的场景。
            movedFirst.HideActions += () => movedBranch.gameObject.SetActive(false);
            // 确认“movedLeaf.gameObject”的层级激活状态为真。
            Assert.IsTrue(movedLeaf.gameObject.activeInHierarchy);
            // 确认“reactivated.gameObject”的层级激活状态为假。
            Assert.IsFalse(reactivated.gameObject.activeInHierarchy);

            // 提交父界面（parent）的隐藏请求，检查子树联动与终态。
            parent.Hide();

            // 确认“movedBranch.transform”的直属父节点仍引用“later.transform”。
            Assert.AreSame(later.transform, movedBranch.transform.parent);
            // 确认“reactivated.gameObject”的自身激活状态为真。
            Assert.IsTrue(reactivated.gameObject.activeSelf);
            // 确认“movedBranch.gameObject”的自身激活状态为假。
            Assert.IsFalse(movedBranch.gameObject.activeSelf);
            // 逐项检查或处理“var panel in panels”，覆盖集合中的每个对象。
            foreach (var panel in panels)
                // 确认面板（panel）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, panel.State, panel.name);
            // 确认回调次数（calls）符合“new[] { 1, 1, 1, 1, 1, 1, 1, 1 }”。
            Assert.That(calls, Is.EqualTo(new[] { 1, 1, 1, 1, 1, 1, 1, 1 }),
                "Each level must still discover currently active children, then protect the new branch from its own earlier callbacks.");
        }

        /// <summary>验证深层节点移出、销毁或换到失活新父后，旧父子关系资格不再作用于它们。</summary>
        [Test]
        public void HideTreeBoundary_ChangedDeepRelationships_ShouldSkipMovedDestroyedAndNewlyInactiveChildren()
        {
            // 创建可同时显示的先后两个子分支，固定顺序以验证回调对后续隐藏的影响。
            CreateHideTreeBoundaryBranches(out var parent, out var first, out var later);
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("OriginalDeepParent", later.transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("NewDeepParent", later.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(target, "mutexGroup", 1);
            // 创建“movedOutside”并配置测试所需的层级或表现组件。
            var movedOutside = CreateUI("MovedOutside", source.transform);
            // 创建“destroyed”并配置测试所需的层级或表现组件。
            var destroyed = CreateUI("DestroyedBeforeVisit", source.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(destroyed, "mutexGroup", 1);
            // 创建“movedWithinTree”并配置测试所需的层级或表现组件。
            var movedWithinTree = CreateUI("MovedToAnotherInactiveParent", source.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(movedWithinTree, "mutexGroup", 2);
            // 提交“movedOutside”的显示请求，触发实际生命周期。
            movedOutside.Show();
            // 提交“destroyed”的显示请求，触发实际生命周期。
            destroyed.Show();
            // 提交“movedWithinTree”的显示请求，触发实际生命周期。
            movedWithinTree.Show();
            // 提交目标对象（target）的显示请求，触发实际生命周期。
            target.Show();
            // 创建“outside”并配置测试所需的层级或表现组件。
            var outside = CreateObject("OutsideAfterHide");
            // 保存“processedCalls”的基准值“ObserveHideTreeBoundaryCalls(parent, first, later, source, target)”，供后续比较或恢复。
            var processedCalls = ObserveHideTreeBoundaryCalls(parent, first, later, source, target);
            // 保存“skippedCalls”的基准值“ObserveHideTreeBoundaryCalls(movedOutside, destroyed, movedWithinTree)”，供后续比较或恢复。
            var skippedCalls = ObserveHideTreeBoundaryCalls(movedOutside, destroyed, movedWithinTree);
            // 为“first.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            first.HideActions += () =>
            {
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                movedOutside.transform.SetParent(outside.transform, false);
                // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
                Object.DestroyImmediate(destroyed.gameObject);
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                movedWithinTree.transform.SetParent(target.transform, false);
                // 切换“parent.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
                parent.gameObject.SetActive(false);
            };
            // 确认“movedOutside.gameObject”的层级激活状态为真。
            Assert.IsTrue(movedOutside.gameObject.activeInHierarchy);
            // 确认“destroyed.gameObject”的层级激活状态为真。
            Assert.IsTrue(destroyed.gameObject.activeInHierarchy);
            // 确认“movedWithinTree.gameObject”的层级激活状态为真。
            Assert.IsTrue(movedWithinTree.gameObject.activeInHierarchy);

            // 提交父界面（parent）的隐藏请求，检查子树联动与终态。
            parent.Hide();

            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认后续界面（later）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, later.State);
            // 确认原来源对象（source）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, source.State);
            // 确认目标对象（target）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, target.State);
            // 确认“processedCalls”符合“new[] { 1, 1, 1, 1, 1 }”。
            Assert.That(processedCalls, Is.EqualTo(new[] { 1, 1, 1, 1, 1 }));
            // 确认“movedOutside.transform”的直属父节点仍引用“outside.transform”。
            Assert.AreSame(outside.transform, movedOutside.transform.parent);
            // 确认“movedOutside”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, movedOutside.State);
            // 确认“destroyed”为假。
            Assert.IsFalse(destroyed);
            // 确认“movedWithinTree.transform”的直属父节点仍引用“target.transform”。
            Assert.AreSame(target.transform, movedWithinTree.transform.parent);
            // 确认“movedWithinTree.gameObject”的层级激活状态为假。
            Assert.IsFalse(movedWithinTree.gameObject.activeInHierarchy);
            // 确认“movedWithinTree”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, movedWithinTree.State);
            // 确认“skippedCalls”符合“new[] { 0, 0, 0 }”。
            Assert.That(skippedCalls, Is.EqualTo(new[] { 0, 0, 0 }),
                "Captured eligibility belongs to the original parent-child pair and cannot follow a moved child into an inactive new relationship.");
        }

        /// <summary>验证隐藏资格捕获不会穿透 Hided 状态短路或普通 Transform 的关系边界。</summary>
        [Test]
        public void HideTreeBoundary_HidedBranchAndPlainTransform_ShouldKeepTheirExistingTraversalBoundaries()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("TraversalBoundaryParent", CreateObject("TraversalBoundaryContainer").transform);
            // 创建“hiddenBranch”并配置测试所需的层级或表现组件。
            var hiddenBranch = CreateUI("AlreadyHiddenBranch", parent.transform);
            // 提交“hiddenBranch”的显示请求，触发实际生命周期。
            hiddenBranch.Show();
            // 提交“hiddenBranch”的隐藏请求，检查子树联动与终态。
            hiddenBranch.Hide();
            // 创建“movedBelowHidden”并配置测试所需的层级或表现组件。
            var movedBelowHidden = CreateUI("ShownBeforeReparent", CreateObject("OutsideHiddenBranch").transform);
            // 提交“movedBelowHidden”的显示请求，触发实际生命周期。
            movedBelowHidden.Show();
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            movedBelowHidden.transform.SetParent(hiddenBranch.transform, false);
            // 创建“plainTransform”并配置测试所需的层级或表现组件。
            var plainTransform = CreateObject("PlainTransformBoundary", parent.transform);
            // 创建“isolated”并配置测试所需的层级或表现组件。
            var isolated = CreateUI("UIBelowPlainTransform", plainTransform.transform);
            // 提交“isolated”的显示请求，触发实际生命周期。
            isolated.Show();
            // 保存回调次数（calls）的基准值“ObserveHideTreeBoundaryCalls(parent, hiddenBranch, movedBelowHidden, isolated)”，供后续比较或恢复。
            var calls = ObserveHideTreeBoundaryCalls(parent, hiddenBranch, movedBelowHidden, isolated);
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
            // 确认“hiddenBranch”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, hiddenBranch.State);
            // 确认“movedBelowHidden”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, movedBelowHidden.State);
            // 确认“movedBelowHidden.gameObject”的层级激活状态为真。
            Assert.IsTrue(movedBelowHidden.gameObject.activeInHierarchy);

            // 提交父界面（parent）的隐藏请求，检查子树联动与终态。
            parent.Hide();

            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认“hiddenBranch”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, hiddenBranch.State);
            // 确认“movedBelowHidden”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, movedBelowHidden.State,
                "A Hided intermediate UI still short-circuits its lifecycle, including child traversal.");
            // 确认“isolated”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, isolated.State,
                "An ordinary Transform still breaks the direct UI parent-child chain.");
            // 确认回调次数（calls）符合“new[] { 1, 0, 0, 0 }”。
            Assert.That(calls, Is.EqualTo(new[] { 1, 0, 0, 0 }),
                "Capturing eligibility must not flatten traversal across state or hierarchy boundaries.");
        }
    }
}
