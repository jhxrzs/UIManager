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
    public class UIThirdAuditCleanupTests
    {
        private GameObject canvasRoot;
        private readonly List<Material> materials = new();
        private readonly List<Action> removeCallbacks = new();

        private sealed class Member
        {
            internal Image Image;
            internal UIFlowMeshEffect Effect;
        }

        [SetUp]
        public void SetUp()
        {
            canvasRoot = new GameObject("ThirdCleanupCanvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var remove in removeCallbacks) remove();
            removeCallbacks.Clear();
            if (canvasRoot) Object.Destroy(canvasRoot);
            yield return null;
            foreach (var material in materials) if (material) Object.Destroy(material);
            materials.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThirdCleanup_ThrowingMemberCallback_ShouldStillRestoreOtherMembers() =>
            VerifyThrowingCleanup(false);

        [UnityTest]
        public IEnumerator ThirdCleanup_DeferredDestroyThrowingMemberCallback_ShouldStillRestoreOtherMembers() =>
            VerifyThrowingCleanup(true);

        private IEnumerator VerifyThrowingCleanup(bool deferredDestroy)
        {
            var group = CreateGroup("ThrowingCleanup", out var content);
            var members = CreateMembers(content, 3);
            yield return null;
            Hide(group);
            foreach (var member in members) AssertHidden(member);
            var thrown = false;
            Member callbackMember = null;
            // 不依赖 HashSet 遍历顺序：无论哪个成员先被清理，第一次清理回调都抛错。
            foreach (var member in members)
            {
                var current = member;
                RegisterDirty(current.Image, () =>
                {
                    if (thrown || current.Effect.EffectAmount != 0f) return;
                    thrown = true;
                    callbackMember = current;
                    throw new InvalidOperationException("third-audit-cleanup-member-failure");
                });
            }
            LogAssert.Expect(LogType.Exception,
                "InvalidOperationException: third-audit-cleanup-member-failure");

            if (deferredDestroy)
            {
                Object.Destroy(group);
                yield return null;
            }
            else Object.DestroyImmediate(group);

            Assert.IsFalse(group);
            Assert.IsTrue(thrown, "必须实际经过成员 Detach 后的 Graphic 标脏回调。");
            foreach (var member in members)
            {
                // 在任何恢复断言失败前记录所有成员的实际网格，避免仅凭进度推断可见性。
                member.Image.Rebuild(CanvasUpdate.PreRender);
                var mesh = member.Image.canvasRenderer.GetMesh();
                var minimumAlpha = 255;
                var maximumAlpha = 0;
                var alphaCount = 0;
                if (mesh)
                {
                    foreach (var color in mesh.colors32)
                    {
                        minimumAlpha = Math.Min(minimumAlpha, color.a);
                        maximumAlpha = Math.Max(maximumAlpha, color.a);
                        alphaCount++;
                    }
                }
                var owner = GetOwner(member.Effect);
                var ownerDescription = ReferenceEquals(owner, null) ? "<null>" :
                    owner ? owner.name : "<destroyed>";
                TestContext.WriteLine("cleanup member=" + member.Image.name +
                    ", delayedDestroy=" + deferredDestroy +
                    ", amount=" + member.Effect.EffectAmount +
                    ", threw=" + ReferenceEquals(member, callbackMember) +
                    ", owner=" + ownerDescription +
                    ", vertices=" + (mesh ? mesh.vertexCount : -1) +
                    ", alphaCount=" + alphaCount +
                    ", alphaRange=" + minimumAlpha + ".." + maximumAlpha);
            }
            foreach (var member in members)
                if (!ReferenceEquals(member, callbackMember))
                    AssertVisible(member, "其他存活成员不能因一个业务回调失败而遗留隐藏网格。");
            AssertVisible(callbackMember);
        }

        [UnityTest]
        public IEnumerator ThirdCleanup_CallbackMovesAnotherMember_ShouldPreserveDestinationHiddenState()
        {
            var source = CreateGroup("CleanupMovingSource", out var sourceContent);
            var destination = CreateGroup("CleanupMovingDestination", out var destinationContent);
            var members = CreateMembers(sourceContent, 2);
            yield return null;
            Hide(source);
            Hide(destination);
            Member moved = null;
            Member detachedFirst = null;
            foreach (var member in members)
            {
                var current = member;
                RegisterDirty(current.Image, () =>
                {
                    if (moved != null || current.Effect.EffectAmount != 0f) return;
                    detachedFirst = current;
                    moved = ReferenceEquals(current, members[0]) ? members[1] : members[0];
                    moved.Image.transform.SetParent(destinationContent, false);
                });
            }

            Object.DestroyImmediate(source);

            Assert.IsFalse(source);
            Assert.IsNotNull(moved);
            Assert.AreSame(destinationContent, moved.Image.transform.parent);
            Assert.AreSame(destination, GetOwner(moved.Effect));
            AssertHidden(moved, "旧控制器的后续清理不能清除新控制器的隐藏状态。");
            AssertVisible(detachedFirst);
        }

        [UnityTest]
        public IEnumerator ThirdCleanup_CallbackDestroysAnotherMember_ShouldFinishSurvivingMembers()
        {
            var group = CreateGroup("CleanupDestroyingMember", out var content);
            var members = CreateMembers(content, 3);
            yield return null;
            Hide(group);
            Member removed = null;
            foreach (var member in members)
            {
                var current = member;
                RegisterDirty(current.Image, () =>
                {
                    if (removed != null || current.Effect.EffectAmount != 0f) return;
                    removed = ReferenceEquals(current, members[0]) ? members[1] : members[0];
                    Object.DestroyImmediate(removed.Image.gameObject);
                });
            }

            Object.DestroyImmediate(group);

            Assert.IsFalse(group);
            Assert.IsNotNull(removed);
            Assert.IsFalse(removed.Effect);
            foreach (var member in members)
                if (!ReferenceEquals(member, removed)) AssertVisible(member);
        }

        [UnityTest]
        public IEnumerator ThirdCleanup_CallbackAddsNestedReplacement_ShouldKeepReplacementOwnership()
        {
            var source = CreateGroup("CleanupReplacedSource", out var content);
            var members = CreateMembers(content, 2);
            yield return null;
            Hide(source);
            UIFlowGroupController replacement = null;
            var replacementStarted = false;
            foreach (var member in members)
            {
                var current = member;
                RegisterDirty(current.Image, () =>
                {
                    if (replacementStarted || current.Effect.EffectAmount != 0f) return;
                    replacementStarted = true;
                    // 在原来普通的 Content 上建立更近的控制器，避免同一物体多控制器的歧义。
                    replacement = content.gameObject.AddComponent<UIFlowGroupController>();
                    Hide(replacement);
                });
            }

            Object.DestroyImmediate(source);

            Assert.IsFalse(source);
            Assert.IsTrue(replacement);
            Assert.AreEqual(UIState.Hided, replacement.GetComponent<UIBase>().State);
            foreach (var member in members)
            {
                Assert.AreSame(replacement, GetOwner(member.Effect));
                AssertHidden(member, "新控制器已取得成员后，旧销毁快照不能再把成员恢复为可见。");
            }
        }

        [UnityTest]
        public IEnumerator ThirdCleanup_DisabledControllerAndInactiveMember_ShouldRestoreOnRemoval()
        {
            var group = CreateGroup("DisabledCleanup", out var content);
            var members = CreateMembers(content, 3);
            yield return null;
            Hide(group);
            group.enabled = false;
            members[2].Image.gameObject.SetActive(false);
            foreach (var member in members) Assert.AreEqual(1f, member.Effect.EffectAmount);

            Object.DestroyImmediate(group);

            Assert.IsFalse(group);
            foreach (var member in members)
            {
                Assert.AreEqual(0f, member.Effect.EffectAmount,
                    "停用控制器和失活成员仍需清理临时组进度。");
                Assert.IsNull(GetOwner(member.Effect));
            }
            members[2].Image.gameObject.SetActive(true);
            foreach (var member in members) AssertVisible(member);
        }

        [UnityTest]
        public IEnumerator ThirdCleanup_DeferredDestroyDuringAsyncHide_ShouldDetachAndKeepOtherHandlerPending()
        {
            var group = CreateGroup("DeferredCleanup", out var content);
            var members = CreateMembers(content, 2);
            typeof(EffectControllerBase).GetField("hideDuration", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(group, 60f);
            typeof(UIFlowGroupController).GetField("delayBetween", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(group, 0f);
            yield return null;
            var ui = group.GetComponent<UIBase>();
            ui.Show();
            var otherHandler = new UniTaskCompletionSource();
            ui.HideAsyncActions += _ => otherHandler.Task;
            var hideOperation = ui.HideAsync(CancellationToken.None).AsTask();
            try
            {
                var progressDeadline = Time.realtimeSinceStartup + 3f;
                while (members[0].Effect.EffectAmount == 0f && Time.realtimeSinceStartup < progressDeadline)
                    yield return null;
                Assert.That(members[0].Effect.EffectAmount, Is.GreaterThan(0f).And.LessThan(1f),
                    "移除前必须真实处于隐藏动画中间帧。");
                Object.Destroy(group); // 正常延迟销毁，不使用 DestroyImmediate。
                yield return null;
                yield return null;

                Assert.IsFalse(group);
                foreach (var member in members) AssertVisible(member);
                Assert.IsFalse(hideOperation.IsCompleted, "移除组动效不能提前完成其他仍在等待的行为。");
                Assert.AreEqual(UIState.Hiding, ui.State);
                otherHandler.TrySetResult();
                var completionDeadline = Time.realtimeSinceStartup + 3f;
                while (!hideOperation.IsCompleted && Time.realtimeSinceStartup < completionDeadline)
                    yield return null;
                Assert.IsTrue(hideOperation.IsCompleted, "控制器销毁后异步显隐必须能完整收尾。");
                Assert.DoesNotThrow(() => hideOperation.GetAwaiter().GetResult());
                Assert.AreEqual(UIState.Hided, ui.State);
                foreach (var member in members) AssertVisible(member);
            }
            finally
            {
                otherHandler.TrySetResult();
            }
        }

        private UIFlowGroupController CreateGroup(string name, out RectTransform content)
        {
            var parent = CreateRect(name + "Container", canvasRoot.transform);
            var group = CreateRect(name, parent).gameObject.AddComponent<UIFlowGroupController>();
            content = CreateRect(name + "Content", group.transform);
            return group;
        }

        private List<Member> CreateMembers(Transform parent, int count)
        {
            var result = new List<Member>();
            for (var i = 0; i < count; i++)
            {
                var rect = CreateRect("Member" + i, parent);
                rect.sizeDelta = new Vector2(100f, 60f);
                rect.anchoredPosition = new Vector2(i * 130f, 0f);
                var image = rect.gameObject.AddComponent<Image>();
                var material = new Material(Shader.Find("UI/Default"));
                materials.Add(material);
                image.material = material;
                result.Add(new Member { Image = image, Effect = rect.gameObject.AddComponent<UIFlowMeshEffect>() });
            }
            return result;
        }

        private void RegisterDirty(Image image, UnityAction callback)
        {
            image.RegisterDirtyVerticesCallback(callback);
            removeCallbacks.Add(() => { if (image) image.UnregisterDirtyVerticesCallback(callback); });
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static UIFlowGroupController GetOwner(UIFlowMeshEffect effect) =>
            (UIFlowGroupController)typeof(UIFlowMeshEffect)
                .GetField("groupController", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect);

        private static void Hide(UIFlowGroupController group)
        {
            var ui = group.GetComponent<UIBase>();
            ui.Show();
            ui.Hide();
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        private static void AssertVisible(Member member, string message = null)
        {
            Assert.That(member.Effect.EffectAmount, Is.Zero, message);
            member.Image.Rebuild(CanvasUpdate.PreRender);
            var mesh = member.Image.canvasRenderer.GetMesh();
            Assert.IsNotNull(mesh);
            Assert.That(mesh.vertexCount, Is.EqualTo(4), message);
            foreach (var color in mesh.colors32) Assert.AreEqual(255, color.a, message);
        }

        private static void AssertHidden(Member member, string message = null)
        {
            Assert.AreEqual(1f, member.Effect.EffectAmount, message);
            member.Image.Rebuild(CanvasUpdate.PreRender);
            var mesh = member.Image.canvasRenderer.GetMesh();
            Assert.IsNotNull(mesh);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), message);
            foreach (var color in mesh.colors32) Assert.AreEqual(0, color.a, message);
        }
    }
}
