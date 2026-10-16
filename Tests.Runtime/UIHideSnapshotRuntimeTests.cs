using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的直接子界面隐藏快照。</summary>
    public partial class Test_UIBase
    {
        /// <summary>验证同步隐藏期间前序子回调关闭父物体后，后续已捕获子界面仍完成隐藏。</summary>
        [Test]
        public void Hide_ChildCallbackDeactivatesParent_ShouldFinishCapturedChildren()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("DeactivateDuringHide", CreateObject("HideSnapshotRoot").transform);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("FirstChild", parent.transform);
            // 创建后续界面（later）并配置测试所需的层级或表现组件。
            var later = CreateUI("LaterUnmarkedChild", parent.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(later, "mutexGroup", 1);
            // 获取“laterGroup”的实际组件引用，供表现与生命周期断言使用。
            var laterGroup = later.GetComponent<CanvasGroup>();
            // 初始化“laterHideCalls”为0，作为后续回调的观测基准。
            var laterHideCalls = 0;
            // 为“parent.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowActions += () => parent.gameObject.SetActive(true);
            // 为“later.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            later.ShowActions += () => laterGroup.alpha = 1f;
            // 为“later.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            later.HideActions += () => { laterHideCalls++; laterGroup.alpha = 0f; };
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
            // 确认“first.gameObject.activeInHierarchy && later.gameObject”的层级激活状态为真。
            Assert.IsTrue(first.gameObject.activeInHierarchy && later.gameObject.activeInHierarchy);
            // 确认后续界面（later）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, later.State);
            // 确认后续界面（later）的随父显示配置为假。
            Assert.IsFalse(later.ShowWithParent);
            // 在“first.HideActions”中关闭物体，构造后续回调面对失活祖先的场景。
            first.HideActions += () => parent.gameObject.SetActive(false);

            // 提交父界面（parent）的隐藏请求，检查子树联动与终态。
            parent.Hide();

            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认首项（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认“parent.gameObject”的层级激活状态为假。
            Assert.IsFalse(parent.gameObject.activeInHierarchy);
            // 提交父界面（parent）的显示请求，触发实际生命周期。
            parent.Show();
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
            // 两个子UI在入口均已活跃并入选；父物体失活不能丢弃仍然直属它的后续隐藏工作。
            Assert.AreEqual(UIState.Hided, later.State,
                "An already selected child must finish hiding even when an earlier callback deactivates its parent.");
            // 确认“laterHideCalls”等于1。
            Assert.AreEqual(1, laterHideCalls,
                "The remaining child's hide behavior must execute exactly once.");
            // 确认“laterGroup”的透明度等于0f。
            Assert.AreEqual(0f, laterGroup.alpha,
                "Reopening the parent must not reveal its unmarked child after a successful hide.");
        }

        /// <summary>验证异步父隐藏期间前序子回调关闭父物体后，后续已捕获子界面仍完成隐藏。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator HideAsync_ChildCallbackDeactivatesParent_ShouldFinishCapturedChildren()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("DeactivateDuringAsyncHide", CreateObject("AsyncHideSnapshotRoot").transform);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("FirstChild", parent.transform);
            // 创建后续界面（later）并配置测试所需的层级或表现组件。
            var later = CreateUI("LaterUnmarkedChild", parent.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(later, "mutexGroup", 1);
            // 获取“laterGroup”的实际组件引用，供表现与生命周期断言使用。
            var laterGroup = later.GetComponent<CanvasGroup>();
            // 初始化“laterHideCalls”为0，作为后续回调的观测基准。
            var laterHideCalls = 0;
            // 为“parent.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.ShowActions += () => parent.gameObject.SetActive(true);
            // 为“later.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            later.ShowActions += () => laterGroup.alpha = 1f;
            // 为“later.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            later.HideActions += () => { laterHideCalls++; laterGroup.alpha = 0f; };
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
            // 确认“first.gameObject.activeInHierarchy && later.gameObject”的层级激活状态为真。
            Assert.IsTrue(first.gameObject.activeInHierarchy && later.gameObject.activeInHierarchy);
            // 确认后续界面（later）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, later.State);
            // 确认后续界面（later）的随父显示配置为假。
            Assert.IsFalse(later.ShowWithParent);
            // 在“first.HideActions”中关闭物体，构造后续回调面对失活祖先的场景。
            first.HideActions += () => parent.gameObject.SetActive(false);
            // 创建手动控制的待完成任务（pending），使动画完成时机由测试显式控制。
            var pending = new UniTaskCompletionSource();
            // 初始化“parentAsyncHideCalls”为0，作为后续回调的观测基准。
            var parentAsyncHideCalls = 0;
            // 为“parent.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            parent.HideAsyncActions += _ => { parentAsyncHideCalls++; return pending.Task; };

            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(parent.HideAsync(CancellationToken.None));
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认“parentAsyncHideCalls”等于1。
                Assert.AreEqual(1, parentAsyncHideCalls);
                // 确认父界面（parent）的界面状态等于隐藏过渡态。
                Assert.AreEqual(UIState.Hiding, parent.State);
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted, "父面板应真实等待自身的异步隐藏行为。");
            }
            finally
            {
                // 完成手动控制的挂起任务，让已启动行为能够收口。
                pending.TrySetResult();
            }

            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);
            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认首项（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认“parent.gameObject”的层级激活状态为假。
            Assert.IsFalse(parent.gameObject.activeInHierarchy);
            // 提交父界面（parent）的显示请求，触发实际生命周期。
            parent.Show();
            // 确认父界面（parent）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parent.State);
            // 确认后续界面（later）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, later.State,
                "An already selected child must finish hiding even when an earlier callback deactivates its parent.");
            // 确认“laterHideCalls”等于1。
            Assert.AreEqual(1, laterHideCalls,
                "The remaining child's hide behavior must execute exactly once.");
            // 确认“laterGroup”的透明度等于0f。
            Assert.AreEqual(0f, laterGroup.alpha,
                "Reopening the parent must not reveal its unmarked child after a successful hide.");
        }

        /// <summary>验证前序隐藏回调移出后续子界面后，旧父快照不再隐藏该节点。</summary>
        [Test]
        public void Hide_ChildCallbackMovesLaterChildOut_ShouldPreserveMovedChild()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("HideSource", CreateObject("HideSourceRoot").transform);
            // 创建“outside”并配置测试所需的层级或表现组件。
            var outside = CreateObject("OutsideHideSource");
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("FirstChild", parent.transform);
            // 创建后续界面（later）并配置测试所需的层级或表现组件。
            var later = CreateUI("MovedChild", parent.transform);
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
            // 初始化“laterHides”为0，作为后续回调的观测基准。
            var laterHides = 0;
            // 为“later.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            later.HideActions += () => laterHides++;
            // 为“first.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            first.HideActions += () => later.transform.SetParent(outside.transform, false);

            // 提交父界面（parent）的隐藏请求，检查子树联动与终态。
            parent.Hide();

            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认首项（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认“later.transform”的直属父节点仍引用“outside.transform”。
            Assert.AreSame(outside.transform, later.transform.parent);
            // 确认后续界面（later）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, later.State,
                "A child that left the parent during a prior callback must not receive stale hide work.");
            // 确认“laterHides”等于0。
            Assert.AreEqual(0, laterHides);
        }

        /// <summary>验证前序隐藏回调销毁后续子界面后安全跳过失效节点。</summary>
        [Test]
        public void Hide_ChildCallbackDestroysLaterChild_ShouldSkipDestroyedChild()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("DestroyDuringHide", CreateObject("DestroyHideSnapshotRoot").transform);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("FirstChild", parent.transform);
            // 创建后续界面（later）并配置测试所需的层级或表现组件。
            var later = CreateUI("DestroyedChild", parent.transform);
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
            // 初始化“laterHides”为0，作为后续回调的观测基准。
            var laterHides = 0;
            // 为“later.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            later.HideActions += () => laterHides++;
            // 在“first.HideActions”中销毁目标，检验同轮后续回调是否安全退出。
            first.HideActions += () => Object.DestroyImmediate(later.gameObject);

            // 提交父界面（parent）的隐藏请求，检查子树联动与终态。
            parent.Hide();

            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认首项（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认后续界面（later）为假。
            Assert.IsFalse(later, "前序子UI的回调必须实际销毁快照中的后续子UI。");
            // 确认“laterHides”等于0。
            Assert.AreEqual(0, laterHides, "已销毁的快照节点不能再接收隐藏行为。");
        }

        /// <summary>验证入口已失活且没有显示预约的子界面不参与父隐藏。</summary>
        [Test]
        public void Hide_ChildAlreadyInactiveAtEntry_ShouldPreserveExcludedChild()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("InactiveAtEntry", CreateObject("InactiveHideSnapshotRoot").transform);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("ActiveChild", parent.transform);
            // 创建后续界面（later）并配置测试所需的层级或表现组件。
            var later = CreateUI("AlreadyInactiveChild", parent.transform);
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(later, "mutexGroup", 1);
            // 提交父界面（parent）的显示请求，触发实际生命周期。
            parent.Show();
            // 提交首项（first）的显示请求，触发实际生命周期。
            first.Show();
            // 提交后续界面（later）的显示请求，触发实际生命周期。
            later.Show();
            // 初始化“firstHides”为0，作为后续回调的观测基准。
            var firstHides = 0;
            // 初始化“laterHides”为0，作为后续回调的观测基准。
            var laterHides = 0;
            // 为“first.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            first.HideActions += () => firstHides++;
            // 为“later.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            later.HideActions += () => laterHides++;
            // 切换“later.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            later.gameObject.SetActive(false);
            // 确认“first.gameObject”的层级激活状态为真。
            Assert.IsTrue(first.gameObject.activeInHierarchy);
            // 确认“later.gameObject”的层级激活状态为假。
            Assert.IsFalse(later.gameObject.activeInHierarchy);
            // 确认后续界面（later）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, later.State);

            // 提交父界面（parent）的隐藏请求，检查子树联动与终态。
            parent.Hide();

            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认首项（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认“firstHides”等于1。
            Assert.AreEqual(1, firstHides);
            // 确认后续界面（later）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, later.State,
                "入口已经失活且没有显示预约的子UI仍不进入隐藏快照。");
            // 确认“laterHides”等于0。
            Assert.AreEqual(0, laterHides);
            // 确认“later.gameObject”的自身激活状态为假。
            Assert.IsFalse(later.gameObject.activeSelf);
        }
    }
}
