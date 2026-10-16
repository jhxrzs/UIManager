using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private void CreateHideTreeBoundaryBranches(out UIBase parent, out UIBase first, out UIBase later)
        {
            parent = CreateUI("HideTreeBoundaryParent", CreateObject("HideTreeBoundaryContainer").transform);
            first = CreateUI("FirstBranch", parent.transform);
            later = CreateUI("LaterBranch", parent.transform);
            SetUIBasePrivateField(later, "mutexGroup", 1);
            parent.Show();
            first.Show();
            later.Show();
            first.transform.SetSiblingIndex(0);
            later.transform.SetSiblingIndex(1);
        }

        private static int[] ObserveHideTreeBoundaryCalls(params UIBase[] panels)
        {
            var calls = new int[panels.Length];
            for (var i = 0; i < panels.Length; i++)
            {
                var index = i;
                panels[i].HideActions += () => calls[index]++;
            }
            return calls;
        }

        [Test]
        public void HideTreeBoundary_FiveLevels_ShouldFinishActiveChainAndPreserveInitiallyInactiveNodes()
        {
            CreateHideTreeBoundaryBranches(out var parent, out var first, out var later);
            var levelTwo = CreateUI("LevelTwo", later.transform);
            var levelThree = CreateUI("LevelThree", levelTwo.transform);
            var activeLeaf = CreateUI("ActiveLeaf", levelThree.transform);
            var inactiveLeaf = CreateUI("InitiallyInactiveLeaf", levelThree.transform);
            SetUIBasePrivateField(inactiveLeaf, "mutexGroup", 1);
            var inactiveBranch = CreateUI("InitiallyInactiveBranch", parent.transform);
            SetUIBasePrivateField(inactiveBranch, "mutexGroup", 2);
            var excludedLeaf = CreateUI("LeafInsideInactiveBranch", inactiveBranch.transform);
            activeLeaf.Show();
            inactiveLeaf.Show();
            excludedLeaf.Show();
            inactiveLeaf.gameObject.SetActive(false);
            inactiveBranch.gameObject.SetActive(false);
            first.transform.SetSiblingIndex(0);
            later.transform.SetSiblingIndex(1);
            var activeChain = new[] { parent, first, later, levelTwo, levelThree, activeLeaf };
            var activeCalls = ObserveHideTreeBoundaryCalls(activeChain);
            var excludedCalls = ObserveHideTreeBoundaryCalls(inactiveLeaf, inactiveBranch, excludedLeaf);
            first.HideActions += () => parent.gameObject.SetActive(false);
            foreach (var panel in activeChain)
            {
                Assert.IsTrue(panel.gameObject.activeInHierarchy, panel.name);
                Assert.AreEqual(UIState.Showed, panel.State, panel.name);
            }
            Assert.IsFalse(inactiveLeaf.gameObject.activeInHierarchy);
            Assert.IsFalse(excludedLeaf.gameObject.activeInHierarchy);

            parent.Hide();

            foreach (var panel in activeChain)
                Assert.AreEqual(UIState.Hided, panel.State, panel.name);
            Assert.That(activeCalls, Is.EqualTo(new[] { 1, 1, 1, 1, 1, 1 }),
                "Ancestor deactivation must preserve recursive work through every initially active UI level.");
            Assert.AreEqual(UIState.Showed, inactiveLeaf.State);
            Assert.AreEqual(UIState.Showed, inactiveBranch.State);
            Assert.AreEqual(UIState.Showed, excludedLeaf.State);
            Assert.That(excludedCalls, Is.EqualTo(new[] { 0, 0, 0 }),
                "Initial eligibility must not include inactive leaves or descendants of inactive branches.");
        }

        [Test]
        public void HideTreeBoundary_LaterScan_ShouldDiscoverMovedActiveBranchAndNewlyActivatedChild()
        {
            CreateHideTreeBoundaryBranches(out var parent, out var first, out var later);
            var reactivated = CreateUI("InitiallyInactiveChild", later.transform);
            SetUIBasePrivateField(reactivated, "mutexGroup", 1);
            reactivated.Show();
            reactivated.gameObject.SetActive(false);
            var movedBranch = CreateUI("MovedActiveBranch", CreateObject("OutsideBeforeHide").transform);
            var movedFirst = CreateUI("MovedFirstChild", movedBranch.transform);
            var movedLater = CreateUI("MovedLaterChild", movedBranch.transform);
            SetUIBasePrivateField(movedLater, "mutexGroup", 1);
            var movedLeaf = CreateUI("MovedDeepLeaf", movedLater.transform);
            movedFirst.Show();
            movedLeaf.Show();
            movedFirst.transform.SetSiblingIndex(0);
            movedLater.transform.SetSiblingIndex(1);
            var panels = new[] { parent, first, later, reactivated, movedBranch, movedFirst, movedLater, movedLeaf };
            var calls = ObserveHideTreeBoundaryCalls(panels);
            first.HideActions += () =>
            {
                movedBranch.transform.SetParent(later.transform, false);
                reactivated.gameObject.SetActive(true);
            };
            movedFirst.HideActions += () => movedBranch.gameObject.SetActive(false);
            Assert.IsTrue(movedLeaf.gameObject.activeInHierarchy);
            Assert.IsFalse(reactivated.gameObject.activeInHierarchy);

            parent.Hide();

            Assert.AreSame(later.transform, movedBranch.transform.parent);
            Assert.IsTrue(reactivated.gameObject.activeSelf);
            Assert.IsFalse(movedBranch.gameObject.activeSelf);
            foreach (var panel in panels)
                Assert.AreEqual(UIState.Hided, panel.State, panel.name);
            Assert.That(calls, Is.EqualTo(new[] { 1, 1, 1, 1, 1, 1, 1, 1 }),
                "Each level must still discover currently active children, then protect the new branch from its own earlier callbacks.");
        }

        [Test]
        public void HideTreeBoundary_ChangedDeepRelationships_ShouldSkipMovedDestroyedAndNewlyInactiveChildren()
        {
            CreateHideTreeBoundaryBranches(out var parent, out var first, out var later);
            var source = CreateUI("OriginalDeepParent", later.transform);
            var target = CreateUI("NewDeepParent", later.transform);
            SetUIBasePrivateField(target, "mutexGroup", 1);
            var movedOutside = CreateUI("MovedOutside", source.transform);
            var destroyed = CreateUI("DestroyedBeforeVisit", source.transform);
            SetUIBasePrivateField(destroyed, "mutexGroup", 1);
            var movedWithinTree = CreateUI("MovedToAnotherInactiveParent", source.transform);
            SetUIBasePrivateField(movedWithinTree, "mutexGroup", 2);
            movedOutside.Show();
            destroyed.Show();
            movedWithinTree.Show();
            target.Show();
            var outside = CreateObject("OutsideAfterHide");
            var processedCalls = ObserveHideTreeBoundaryCalls(parent, first, later, source, target);
            var skippedCalls = ObserveHideTreeBoundaryCalls(movedOutside, destroyed, movedWithinTree);
            first.HideActions += () =>
            {
                movedOutside.transform.SetParent(outside.transform, false);
                Object.DestroyImmediate(destroyed.gameObject);
                movedWithinTree.transform.SetParent(target.transform, false);
                parent.gameObject.SetActive(false);
            };
            Assert.IsTrue(movedOutside.gameObject.activeInHierarchy);
            Assert.IsTrue(destroyed.gameObject.activeInHierarchy);
            Assert.IsTrue(movedWithinTree.gameObject.activeInHierarchy);

            parent.Hide();

            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, later.State);
            Assert.AreEqual(UIState.Hided, source.State);
            Assert.AreEqual(UIState.Hided, target.State);
            Assert.That(processedCalls, Is.EqualTo(new[] { 1, 1, 1, 1, 1 }));
            Assert.AreSame(outside.transform, movedOutside.transform.parent);
            Assert.AreEqual(UIState.Showed, movedOutside.State);
            Assert.IsFalse(destroyed);
            Assert.AreSame(target.transform, movedWithinTree.transform.parent);
            Assert.IsFalse(movedWithinTree.gameObject.activeInHierarchy);
            Assert.AreEqual(UIState.Showed, movedWithinTree.State);
            Assert.That(skippedCalls, Is.EqualTo(new[] { 0, 0, 0 }),
                "Captured eligibility belongs to the original parent-child pair and cannot follow a moved child into an inactive new relationship.");
        }

        [Test]
        public void HideTreeBoundary_HidedBranchAndPlainTransform_ShouldKeepTheirExistingTraversalBoundaries()
        {
            var parent = CreateUI("TraversalBoundaryParent", CreateObject("TraversalBoundaryContainer").transform);
            var hiddenBranch = CreateUI("AlreadyHiddenBranch", parent.transform);
            hiddenBranch.Show();
            hiddenBranch.Hide();
            var movedBelowHidden = CreateUI("ShownBeforeReparent", CreateObject("OutsideHiddenBranch").transform);
            movedBelowHidden.Show();
            movedBelowHidden.transform.SetParent(hiddenBranch.transform, false);
            var plainTransform = CreateObject("PlainTransformBoundary", parent.transform);
            var isolated = CreateUI("UIBelowPlainTransform", plainTransform.transform);
            isolated.Show();
            var calls = ObserveHideTreeBoundaryCalls(parent, hiddenBranch, movedBelowHidden, isolated);
            Assert.AreEqual(UIState.Showed, parent.State);
            Assert.AreEqual(UIState.Hided, hiddenBranch.State);
            Assert.AreEqual(UIState.Showed, movedBelowHidden.State);
            Assert.IsTrue(movedBelowHidden.gameObject.activeInHierarchy);

            parent.Hide();

            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, hiddenBranch.State);
            Assert.AreEqual(UIState.Showed, movedBelowHidden.State,
                "A Hided intermediate UI still short-circuits its lifecycle, including child traversal.");
            Assert.AreEqual(UIState.Showed, isolated.State,
                "An ordinary Transform still breaks the direct UI parent-child chain.");
            Assert.That(calls, Is.EqualTo(new[] { 1, 0, 0, 0 }),
                "Capturing eligibility must not flatten traversal across state or hierarchy boundaries.");
        }
    }
}
