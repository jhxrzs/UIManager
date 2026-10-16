using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    ///  验证组控制器销毁时的成员清理、回调异常隔离和新控制器归属保护。
    /// </summary>
    public partial class UIThirdAuditCleanupTests
    {
        /// <summary>
        ///  每个用例独立创建的画布根节点，结束后统一销毁。
        /// </summary>
        private GameObject canvasRoot;
        /// <summary>
        ///  本用例创建并负责释放的运行时材质。
        /// </summary>
        private readonly List<Material> materials = new();
        /// <summary>
        ///  测试结束前需要执行的事件解绑操作，避免清理资源时再次触发测试回调。
        /// </summary>
        private readonly List<Action> removeCallbacks = new();


        /// <summary>
        ///  为当前测试创建独立的屏幕覆盖画布。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            // 创建本用例独立的画布对象，并同时挂载矩形变换与画布组件。
            canvasRoot = new GameObject("ThirdCleanupCanvas", typeof(RectTransform), typeof(Canvas));
            // 使用屏幕覆盖模式，让网格重建不依赖相机设置。
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// <summary>
        ///  移除测试资源并等待延迟销毁完成，避免影响后续用例。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 逐一解绑测试注册的回调，避免随后销毁资源时再次触发测试逻辑。
            foreach (var remove in removeCallbacks) remove();
            // 清空本用例已经执行的解绑操作。
            removeCallbacks.Clear();
            // 若画布仍存活，则安排销毁画布及其子对象。
            if (canvasRoot) Object.Destroy(canvasRoot);
            // 等待画布和成员对象完成延迟销毁。
            yield return null;
            // 逐一销毁本用例创建且仍存活的独立材质。
            foreach (var material in materials) if (material) Object.Destroy(material);
            // 清空已释放材质的记录，避免跨用例保留引用。
            materials.Clear();
            // 等待材质的延迟销毁完成后再结束清理。
            yield return null;
        }

        /// <summary>
        ///  验证立即销毁控制器时，单个成员回调抛错不影响其他成员恢复可见。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdCleanup_ThrowingMemberCallback_ShouldStillRestoreOtherMembers() =>
            VerifyThrowingCleanup(false);

        /// <summary>
        ///  验证延迟销毁控制器时，单个成员回调抛错不阻断其他成员清理。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdCleanup_DeferredDestroyThrowingMemberCallback_ShouldStillRestoreOtherMembers() =>
            VerifyThrowingCleanup(true);

        /// <summary>
        ///  在立即或延迟销毁路径中注入一次成员回调异常，并验证所有成员的网格恢复结果。
        /// </summary>
        /// <param name="deferredDestroy">是否使用帧末延迟销毁路径。</param>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        private IEnumerator VerifyThrowingCleanup(bool deferredDestroy)
        {
            // 创建待销毁的组控制器及其内容节点。
            var group = CreateGroup("ThrowingCleanup", out var content);
            // 创建三个成员，以验证一次回调异常不会截断后续清理。
            var members = CreateMembers(content, 3);
            // 等待组件生命周期完成初始绑定。
            yield return null;
            // 将组切换到隐藏完成状态，为销毁后的恢复提供起点。
            Hide(group);
            // 逐个确认成员已经隐藏，避免恢复断言建立在错误前提上。
            foreach (var member in members) AssertHidden(member);
            // 记录尚未注入异常，确保整个清理过程只抛出一次。
            var thrown = false;
            // 先保留空引用，稍后记录实际触发异常的成员。
            Member callbackMember = null;
            // 不依赖 HashSet 遍历顺序：无论哪个成员先被清理，第一次清理回调都抛错。
            foreach (var member in members)
            {
                // 捕获本轮成员，供对应的顶点标脏回调读取。
                var current = member;
                // 注册清理时触发的顶点标脏回调，用于注入一次业务异常。
                RegisterDirty(current.Image, () =>
                {
                    // 已经抛过异常或成员尚未恢复到可见进度时，跳过本次回调。
                    if (thrown || current.Effect.EffectAmount != 0f) return;
                    // 标记异常已经触发，防止其余成员再次抛错。
                    thrown = true;
                    // 记录抛错成员，以便分别核对它与其他成员的恢复结果。
                    callbackMember = current;
                    // 抛出约定异常，模拟业务回调在成员脱离组时失败。
                    throw new InvalidOperationException("third-audit-cleanup-member-failure");
                });
            }
            // 声明本用例预期的异常日志，区分已注入故障与意外错误。
            LogAssert.Expect(LogType.Exception,
                "InvalidOperationException: third-audit-cleanup-member-failure");

            // 根据参数选择在帧末销毁控制器的路径。
            if (deferredDestroy)
            {
                // 安排帧末销毁控制器，触发正常的 Unity 销毁回调。
                Object.Destroy(group);
                // 等待控制器实际销毁及成员解绑完成。
                yield return null;
            }
            // 未选择延迟路径时立即销毁控制器，同步执行成员清理。
            else Object.DestroyImmediate(group);

            // 确认控制器已经销毁。
            Assert.IsFalse(group);
            // 确认测试确实触发了成员解绑后的异常回调。
            Assert.IsTrue(thrown, "必须实际经过成员 Detach 后的 Graphic 标脏回调。");
            // 逐个收集成员的实际网格与归属，保留完整的故障诊断信息。
            foreach (var member in members)
            {
                // 在任何恢复断言失败前记录所有成员的实际网格，避免仅凭进度推断可见性。
                member.Image.Rebuild(CanvasUpdate.PreRender);
                // 读取重建后的渲染网格，检查真实顶点透明度。
                var mesh = member.Image.canvasRenderer.GetMesh();
                // 将最小透明度的初值设为字节上界，便于逐顶点取最小值。
                var minimumAlpha = 255;
                // 将最大透明度的初值设为字节下界，便于逐顶点取最大值。
                var maximumAlpha = 0;
                // 初始化已统计的顶点颜色数量。
                var alphaCount = 0;
                // 仅在渲染网格存在时读取其顶点颜色。
                if (mesh)
                {
                    // 遍历网格颜色，统计本成员的透明度范围和样本数量。
                    foreach (var color in mesh.colors32)
                    {
                        // 将当前顶点透明度计入最小值。
                        minimumAlpha = Math.Min(minimumAlpha, color.a);
                        // 将当前顶点透明度计入最大值。
                        maximumAlpha = Math.Max(maximumAlpha, color.a);
                        // 记录已经检查一个顶点颜色。
                        alphaCount++;
                    }
                }
                // 读取成员在控制器销毁后的实际组归属。
                var owner = GetOwner(member.Effect);
                // 区分真正的空引用、存活控制器和已销毁的 Unity 对象。
                var ownerDescription = ReferenceEquals(owner, null) ? "<null>" :
                    owner ? owner.name : "<destroyed>";
                // 输出各成员的归属、异常来源和网格透明度，便于定位清理遗漏。
                TestContext.WriteLine("cleanup member=" + member.Image.name +
                    ", delayedDestroy=" + deferredDestroy +
                    ", amount=" + member.Effect.EffectAmount +
                    ", threw=" + ReferenceEquals(member, callbackMember) +
                    ", owner=" + ownerDescription +
                    ", vertices=" + (mesh ? mesh.vertexCount : -1) +
                    ", alphaCount=" + alphaCount +
                    ", alphaRange=" + minimumAlpha + ".." + maximumAlpha);
            }
            // 逐个检查未抛错成员是否均已恢复可见。
            foreach (var member in members)
                // 排除已单独记录的抛错成员，先验证异常对其他成员的影响。
                if (!ReferenceEquals(member, callbackMember))
                    // 确认其他成员没有因一个回调失败而遗留隐藏网格。
                    AssertVisible(member, "其他存活成员不能因一个业务回调失败而遗留隐藏网格。");
            // 再确认抛错成员自身也已恢复可见。
            AssertVisible(callbackMember);
        }

        /// <summary>
        ///  验证清理回调将其他成员迁入新组后，旧组的清理快照不覆盖新组隐藏状态。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdCleanup_CallbackMovesAnotherMember_ShouldPreserveDestinationHiddenState()
        {
            // 创建将在清理过程中释放成员的来源组。
            var source = CreateGroup("CleanupMovingSource", out var sourceContent);
            // 创建接收迁入成员的目标组。
            var destination = CreateGroup("CleanupMovingDestination", out var destinationContent);
            // 在来源组下创建两个成员，以便首个清理回调迁移另一个成员。
            var members = CreateMembers(sourceContent, 2);
            // 等待成员完成初始组绑定。
            yield return null;
            // 隐藏来源组，使解绑会触发可见进度恢复。
            Hide(source);
            // 隐藏目标组，使迁入成员应继承隐藏状态。
            Hide(destination);
            // 初始化迁移记录，确保迁移动作只发生一次。
            Member moved = null;
            // 初始化首个脱离来源组的成员记录。
            Member detachedFirst = null;
            // 为两个成员分别注册回调，使测试不依赖组内遍历顺序。
            foreach (var member in members)
            {
                // 捕获本轮成员，确保回调操作与其对应的成员一致。
                var current = member;
                // 注册解绑时的顶点标脏回调，用于把另一个成员迁入目标组。
                RegisterDirty(current.Image, () =>
                {
                    // 已经迁移过成员或当前成员尚未恢复可见进度时跳过回调。
                    if (moved != null || current.Effect.EffectAmount != 0f) return;
                    // 记录首个被清理的成员，稍后验证它已恢复可见。
                    detachedFirst = current;
                    // 选择尚未被当前回调清理的另一个成员作为迁移对象。
                    moved = ReferenceEquals(current, members[0]) ? members[1] : members[0];
                    // 将待清理成员移入目标组内容节点，并保持本地变换。
                    moved.Image.transform.SetParent(destinationContent, false);
                });
            }

            // 立即销毁来源控制器，在清理期间触发迁移回调。
            Object.DestroyImmediate(source);

            // 确认来源控制器已销毁。
            Assert.IsFalse(source);
            // 确认回调实际选中并迁移了成员。
            Assert.IsNotNull(moved);
            // 确认被迁移成员位于目标组的内容节点下。
            Assert.AreSame(destinationContent, moved.Image.transform.parent);
            // 确认被迁移成员的实际归属已经切换到目标控制器。
            Assert.AreSame(destination, GetOwner(moved.Effect));
            // 确认来源组剩余清理没有覆盖目标组赋予的隐藏状态。
            AssertHidden(moved, "旧控制器的后续清理不能清除新控制器的隐藏状态。");
            // 确认首个正常脱离来源组的成员已经恢复可见。
            AssertVisible(detachedFirst);
        }

        /// <summary>
        ///  验证清理回调销毁另一个成员后，控制器仍能清理所有存活成员。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdCleanup_CallbackDestroysAnotherMember_ShouldFinishSurvivingMembers()
        {
            // 创建用于验证清理期间成员销毁的组控制器。
            var group = CreateGroup("CleanupDestroyingMember", out var content);
            // 创建三个成员，使销毁一个成员后仍有存活成员需要清理。
            var members = CreateMembers(content, 3);
            // 等待成员完成初始组绑定。
            yield return null;
            // 将全部成员隐藏，以便检查销毁控制器后的恢复结果。
            Hide(group);
            // 初始化被移除成员的记录，限定销毁回调只执行一次。
            Member removed = null;
            // 为所有成员注册回调，使任意首个清理成员都能触发场景。
            foreach (var member in members)
            {
                // 捕获本轮成员供对应回调读取。
                var current = member;
                // 注册解绑时的顶点标脏回调，用于销毁另一个待清理成员。
                RegisterDirty(current.Image, () =>
                {
                    // 已移除过成员或当前成员尚未恢复可见进度时跳过回调。
                    if (removed != null || current.Effect.EffectAmount != 0f) return;
                    // 选择另一个成员，避免销毁当前正在执行回调的成员。
                    removed = ReferenceEquals(current, members[0]) ? members[1] : members[0];
                    // 立即销毁所选成员的对象，使后续清理遇到已失效引用。
                    Object.DestroyImmediate(removed.Image.gameObject);
                });
            }

            // 立即销毁控制器，触发包含成员销毁的清理过程。
            Object.DestroyImmediate(group);

            // 确认控制器已经销毁。
            Assert.IsFalse(group);
            // 确认回调实际选中了需要销毁的成员记录。
            Assert.IsNotNull(removed);
            // 确认所选成员的动效组件已经随对象销毁。
            Assert.IsFalse(removed.Effect);
            // 遍历成员记录，检查销毁回调之后的存活成员。
            foreach (var member in members)
                // 跳过已销毁成员，并确认每个存活成员都恢复可见。
                if (!ReferenceEquals(member, removed)) AssertVisible(member);
        }

        /// <summary>
        ///  验证清理回调创建更近的控制器后，旧组保留新控制器取得的成员归属与状态。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdCleanup_CallbackAddsNestedReplacement_ShouldKeepReplacementOwnership()
        {
            // 创建将被替代的来源控制器及其内容节点。
            var source = CreateGroup("CleanupReplacedSource", out var content);
            // 创建两个成员，用于验证替代控制器接管后的归属保护。
            var members = CreateMembers(content, 2);
            // 等待成员完成初始组绑定。
            yield return null;
            // 隐藏来源组，使清理回调在恢复成员进度时触发。
            Hide(source);
            // 初始化替代控制器引用，稍后在清理回调中创建。
            UIFlowGroupController replacement = null;
            // 记录替代流程尚未开始，防止嵌套回调重复创建控制器。
            var replacementStarted = false;
            // 为所有成员注册接管回调，避免依赖清理遍历顺序。
            foreach (var member in members)
            {
                // 捕获本轮成员供其顶点标脏回调检查。
                var current = member;
                // 注册清理回调，在首个成员恢复时创建更近的控制器。
                RegisterDirty(current.Image, () =>
                {
                    // 接管流程已经开始或成员尚未恢复可见进度时跳过回调。
                    if (replacementStarted || current.Effect.EffectAmount != 0f) return;
                    // 在创建组件前标记接管已开始，阻止重入回调重复接管。
                    replacementStarted = true;
                    // 在原来普通的 Content 上建立更近的控制器，避免同一物体多控制器的歧义。
                    replacement = content.gameObject.AddComponent<UIFlowGroupController>();
                    // 隐藏替代控制器，让其接管的成员获得新的隐藏状态。
                    Hide(replacement);
                });
            }

            // 立即销毁来源控制器，触发回调中的替代与接管。
            Object.DestroyImmediate(source);

            // 确认来源控制器已经销毁。
            Assert.IsFalse(source);
            // 确认清理回调创建的替代控制器仍然存活。
            Assert.IsTrue(replacement);
            // 确认替代控制器的界面已完成隐藏。
            Assert.AreEqual(UIState.Hided, replacement.GetComponent<UIBase>().State);
            // 逐个检查所有成员的最终归属与隐藏表现。
            foreach (var member in members)
            {
                // 确认成员已经归属于更近的替代控制器。
                Assert.AreSame(replacement, GetOwner(member.Effect));
                // 确认旧控制器的清理快照未覆盖替代控制器赋予的隐藏状态。
                AssertHidden(member, "新控制器已取得成员后，旧销毁快照不能再把成员恢复为可见。");
            }
        }

        /// <summary>
        ///  验证停用的控制器被移除时，失活成员也释放组归属并恢复独立可见进度。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdCleanup_DisabledControllerAndInactiveMember_ShouldRestoreOnRemoval()
        {
            // 创建用于验证停用组件清理的组控制器。
            var group = CreateGroup("DisabledCleanup", out var content);
            // 创建三个成员，以便同时比较激活与失活成员。
            var members = CreateMembers(content, 3);
            // 等待成员完成初始组绑定。
            yield return null;
            // 先隐藏组，使所有成员都保留完整的组隐藏进度。
            Hide(group);
            // 停用控制器组件，验证销毁清理不依赖组件仍处于启用状态。
            group.enabled = false;
            // 关闭第三个成员对象，验证失活成员也能释放组状态。
            members[2].Image.gameObject.SetActive(false);
            // 逐个确认关闭组件和成员后仍保留原有隐藏进度。
            foreach (var member in members) Assert.AreEqual(1f, member.Effect.EffectAmount);

            // 立即移除已停用的控制器组件，触发销毁清理。
            Object.DestroyImmediate(group);

            // 确认控制器已被移除。
            Assert.IsFalse(group);
            // 逐个检查激活与失活成员的进度和归属是否都已清理。
            foreach (var member in members)
            {
                // 确认每个成员的临时组隐藏进度已经归零。
                Assert.AreEqual(0f, member.Effect.EffectAmount,
                    "停用控制器和失活成员仍需清理临时组进度。");
                // 确认每个成员都已释放对旧控制器的归属引用。
                Assert.IsNull(GetOwner(member.Effect));
            }
            // 重新激活第三个成员，以便检查其实际渲染网格。
            members[2].Image.gameObject.SetActive(true);
            // 逐个确认所有成员最终都恢复为可见网格。
            foreach (var member in members) AssertVisible(member);
        }

        /// <summary>
        ///  验证隐藏动画中延迟销毁控制器后成员恢复可见，而其他异步行为继续等待并正常收尾。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator ThirdCleanup_DeferredDestroyDuringAsyncHide_ShouldDetachAndKeepOtherHandlerPending()
        {
            // 创建将在异步隐藏中销毁的组控制器。
            var group = CreateGroup("DeferredCleanup", out var content);
            // 创建两个成员，用于检查动画中断后的恢复结果。
            var members = CreateMembers(content, 2);
            // 将隐藏动画设为较长时长，确保能在动画中间帧销毁控制器。
            typeof(EffectControllerBase).GetField("hideDuration", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(group, 60f);
            // 将成员间的延迟设为零，使组内成员立即参与隐藏动画。
            typeof(UIFlowGroupController).GetField("delayBetween", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(group, 0f);
            // 等待组件完成初始绑定与启动。
            yield return null;
            // 获取控制器关联的界面，用它发起显隐操作。
            var ui = group.GetComponent<UIBase>();
            // 显示界面，建立异步隐藏的可见起点。
            ui.Show();
            // 创建独立完成源，用于保持另一隐藏处理器仍在等待。
            var otherHandler = new UniTaskCompletionSource();
            // 注册等待独立完成源的异步隐藏处理器。
            ui.HideAsyncActions += _ => otherHandler.Task;
            // 发起不带外部取消的异步隐藏，并保留可观察的任务。
            var hideOperation = ui.HideAsync(CancellationToken.None).AsTask();
            // 在验证完成或断言失败时都保证释放独立等待中的处理器。
            try
            {
                // 记录等待动画启动的时间上限，避免测试无限挂起。
                var progressDeadline = Time.realtimeSinceStartup + 3f;
                // 在动画尚未推进且未超时期间等待后续帧。
                while (members[0].Effect.EffectAmount == 0f && Time.realtimeSinceStartup < progressDeadline)
                    // 让出一帧，允许隐藏动画更新成员进度。
                    yield return null;
                // 确认实际处于隐藏动画中间帧，而非尚未开始或已经结束。
                Assert.That(members[0].Effect.EffectAmount, Is.GreaterThan(0f).And.LessThan(1f),
                    "移除前必须真实处于隐藏动画中间帧。");
                Object.Destroy(group); // 正常延迟销毁，不使用 DestroyImmediate。
                // 等待帧末执行控制器销毁。
                yield return null;
                // 再让出一帧，使销毁触发的异步清理得到处理。
                yield return null;

                // 确认控制器已经销毁。
                Assert.IsFalse(group);
                // 逐个确认控制器销毁后成员立即恢复可见。
                foreach (var member in members) AssertVisible(member);
                // 确认独立处理器仍使整个隐藏请求保持未完成。
                Assert.IsFalse(hideOperation.IsCompleted, "移除组动效不能提前完成其他仍在等待的行为。");
                // 确认界面仍处于隐藏过程中，等待其他处理器收尾。
                Assert.AreEqual(UIState.Hiding, ui.State);
                // 结束独立处理器的等待，允许整个隐藏请求完成。
                otherHandler.TrySetResult();
                // 记录等待隐藏请求收尾的时间上限。
                var completionDeadline = Time.realtimeSinceStartup + 3f;
                // 在请求尚未完成且未超时期间继续等待。
                while (!hideOperation.IsCompleted && Time.realtimeSinceStartup < completionDeadline)
                    // 让出一帧，允许隐藏请求推进完成流程。
                    yield return null;
                // 确认控制器销毁没有使隐藏请求永久挂起。
                Assert.IsTrue(hideOperation.IsCompleted, "控制器销毁后异步显隐必须能完整收尾。");
                // 获取任务结果并确认隐藏请求未因控制器销毁而抛错。
                Assert.DoesNotThrow(() => hideOperation.GetAwaiter().GetResult());
                // 确认其他处理器完成后界面进入隐藏完成状态。
                Assert.AreEqual(UIState.Hided, ui.State);
                // 逐个确认请求收尾没有重新隐藏已经解绑的成员。
                foreach (var member in members) AssertVisible(member);
            }
            finally
            {
                // 即使断言失败也释放独立处理器，避免遗留等待任务。
                otherHandler.TrySetResult();
            }
        }

        /// <summary>
        ///  在独立普通父节点下创建组控制器和内容节点，避免兄弟面板互斥干扰测试。
        /// </summary>
        /// <param name="name">创建对象的名称。</param>
        /// <param name="content">创建的组内容节点，用于挂载成员。</param>
        /// <returns>新建的组控制器。</returns>
        private UIFlowGroupController CreateGroup(string name, out RectTransform content)
        {
            // 在画布下创建独立普通父节点，隔离不同组之间的兄弟互斥。
            var parent = CreateRect(name + "Container", canvasRoot.transform);
            // 创建组节点并挂载组控制器。
            var group = CreateRect(name, parent).gameObject.AddComponent<UIFlowGroupController>();
            // 创建组内容节点，并通过输出参数提供给调用方挂载成员。
            content = CreateRect(name + "Content", group.transform);
            // 返回配置好层级的组控制器。
            return group;
        }

        /// <summary>
        ///  按指定数量创建带独立材质的网格成员，并错开位置便于检查。
        /// </summary>
        /// <param name="parent">子节点所属的父节点。</param>
        /// <param name="count">需要创建的成员数量。</param>
        /// <returns>包含全部新建成员的列表。</returns>
        private List<Member> CreateMembers(Transform parent, int count)
        {
            // 创建保存本次新增成员的结果列表。
            var result = new List<Member>();
            // 按指定数量逐个创建成员及其独立材质。
            for (var i = 0; i < count; i++)
            {
                // 创建当前成员的矩形节点，并挂到指定父节点下。
                var rect = CreateRect("Member" + i, parent);
                // 设置成员尺寸，保证重建时形成有效的矩形网格。
                rect.sizeDelta = new Vector2(100f, 60f);
                // 沿水平方向错开成员位置，便于区分各成员的表现。
                rect.anchoredPosition = new Vector2(i * 130f, 0f);
                // 为成员挂载图像组件，提供可检查的 UI 网格。
                var image = rect.gameObject.AddComponent<Image>();
                // 创建使用默认 UI 着色器的独立材质，隔离成员材质状态。
                var material = new Material(Shader.Find("UI/Default"));
                // 登记材质，以便测试结束时统一释放。
                materials.Add(material);
                // 将独立材质分配给当前图像。
                image.material = material;
                // 添加网格动效组件，并将图像与动效一起记录为成员。
                result.Add(new Member { Image = image, Effect = rect.gameObject.AddComponent<UIFlowMeshEffect>() });
            }
            // 返回全部新建成员供用例操作和验证。
            return result;
        }

        /// <summary>
        ///  注册顶点标脏回调，并登记测试结束时的对应解绑操作。
        /// </summary>
        /// <param name="image">需要创建或检查的图形成员。</param>
        /// <param name="callback">测试注入的顶点标脏回调。</param>
        private void RegisterDirty(Image image, UnityAction callback)
        {
            // 将测试回调注册到图像的顶点标脏事件。
            image.RegisterDirtyVerticesCallback(callback);
            // 登记清理操作，仅在图像仍存活时解除对应回调。
            removeCallbacks.Add(() => { if (image) image.UnregisterDirtyVerticesCallback(callback); });
        }

        /// <summary>
        ///  在指定父节点下创建保持本地变换的矩形节点。
        /// </summary>
        /// <param name="name">创建对象的名称。</param>
        /// <param name="parent">子节点所属的父节点。</param>
        /// <returns>新建节点的矩形变换。</returns>
        private static RectTransform CreateRect(string name, Transform parent)
        {
            // 创建带矩形变换的节点对象。
            var go = new GameObject(name, typeof(RectTransform));
            // 挂到指定父节点下，并保持节点的本地变换。
            go.transform.SetParent(parent, false);
            // 返回节点的矩形变换，供调用方继续配置层级与布局。
            return (RectTransform)go.transform;
        }

        /// <summary>
        ///  通过反射读取成员当前绑定的组控制器，用于验证清理后的真实归属。
        /// </summary>
        /// <param name="effect">需要创建或检查的网格动效组件。</param>
        /// <returns>成员当前绑定的控制器；没有绑定时为空。</returns>
        private static UIFlowGroupController GetOwner(UIFlowMeshEffect effect) =>
            (UIFlowGroupController)typeof(UIFlowMeshEffect)
                .GetField("groupController", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect);

        /// <summary>
        ///  先显示再隐藏指定组，并确认面板已经进入隐藏完成状态。
        /// </summary>
        /// <param name="group">需要配置或隐藏的组控制器。</param>
        private static void Hide(UIFlowGroupController group)
        {
            // 获取控制器关联的界面组件。
            var ui = group.GetComponent<UIBase>();
            // 先显示界面，确保下一次隐藏不是已隐藏状态下的短路请求。
            ui.Show();
            // 同步隐藏界面，让组动效推进到隐藏完成状态。
            ui.Hide();
            // 确认隐藏已经同步完成。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>
        ///  重建并检查实际渲染网格，确认成员保持完整且不透明的可见几何。
        /// </summary>
        /// <param name="member">需要创建或检查的网格成员。</param>
        /// <param name="message">断言失败时提供的附加说明。</param>
        private static void AssertVisible(Member member, string message = null)
        {
            // 确认成员的组隐藏进度已归零。
            Assert.That(member.Effect.EffectAmount, Is.Zero, message);
            // 重建成员的实际渲染网格，避免仅凭进度判断可见性。
            member.Image.Rebuild(CanvasUpdate.PreRender);
            // 获取重建后提交给渲染器的网格。
            var mesh = member.Image.canvasRenderer.GetMesh();
            // 确认可见成员确实生成了网格。
            Assert.IsNotNull(mesh);
            // 确认可见成员保留完整矩形的四个顶点。
            Assert.That(mesh.vertexCount, Is.EqualTo(4), message);
            // 逐个确认顶点完全不透明，验证网格实际可见。
            foreach (var color in mesh.colors32) Assert.AreEqual(255, color.a, message);
        }

        /// <summary>
        ///  检查隐藏进度并重建实际网格，确认所有顶点完全透明。
        /// </summary>
        /// <param name="member">需要创建或检查的网格成员。</param>
        /// <param name="message">断言失败时提供的附加说明。</param>
        private static void AssertHidden(Member member, string message = null)
        {
            // 确认成员的组隐藏进度已经达到终点。
            Assert.AreEqual(1f, member.Effect.EffectAmount, message);
            // 重建成员的实际渲染网格，检查隐藏表现是否应用到顶点。
            member.Image.Rebuild(CanvasUpdate.PreRender);
            // 获取重建后提交给渲染器的网格。
            var mesh = member.Image.canvasRenderer.GetMesh();
            // 确认隐藏成员仍有可检查的渲染网格。
            Assert.IsNotNull(mesh);
            // 确认网格包含顶点，避免空网格使透明度检查误通过。
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), message);
            // 逐个确认顶点完全透明，验证实际隐藏表现。
            foreach (var color in mesh.colors32) Assert.AreEqual(0, color.a, message);
        }
    }
}
