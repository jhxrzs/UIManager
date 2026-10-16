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
    public partial class Test_UIBase
    {
        private UIFlowGroupController CreateThirdAuditAsyncFlow(out UIBase ui, out Image image,
            out UIFlowMeshEffect member, out Material material)
        {
            var go = CreateRuntimeEffectObject("ThirdAuditAsyncFlow", out ui);
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var controller = go.AddComponent<UIFlowGroupController>();
            SetEffectField(controller, "hideDuration", 0.15f);
            var memberObject = new GameObject("AsyncMember", typeof(RectTransform));
            _createdObjects.Add(memberObject);
            memberObject.transform.SetParent(go.transform, false);
            image = memberObject.AddComponent<Image>();
            image.rectTransform.sizeDelta = new Vector2(100f, 60f);
            material = new Material(Shader.Find("UI/Default"));
            image.material = material;
            member = memberObject.AddComponent<UIFlowMeshEffect>();
            go.SetActive(true);
            ui.Show();
            return controller;
        }

        [UnityTest]
        public IEnumerator ThirdAuditAsyncFlow_DestroyDuringPreparation_ShouldWaitForOtherAction()
        {
            var controller = CreateThirdAuditAsyncFlow(out var ui, out var image, out var member, out var material);
            var pending = new UniTaskCompletionSource();
            ui.HideAsyncActions += _ => pending.Task;
            var after = 0;
            ui.AfterUIHidedEvent += () => after++;
            var removed = false;
            UnityAction remove = () =>
            {
                if (removed) return;
                removed = true;
                Object.DestroyImmediate(controller);
            };
            image.RegisterDirtyVerticesCallback(remove);
            try
            {
                var operation = CaptureException(ui.HideAsync(CancellationToken.None));
                Assert.IsTrue(removed);
                Assert.IsFalse(controller);
                Assert.IsFalse(operation.IsCompleted, "Removing Flow must not finish another pending UI action.");
                Assert.AreEqual(UIState.Hiding, ui.State);
                Assert.AreEqual(0, after);
                Assert.That(member.EffectAmount, Is.Zero);
                pending.TrySetResult();
                yield return WaitForTask(operation);
                Assert.IsNull(operation.Result);
                Assert.AreEqual(UIState.Hided, ui.State);
                Assert.AreEqual(1, after);
            }
            finally
            {
                pending.TrySetResult();
                if (image) image.UnregisterDirtyVerticesCallback(remove);
                Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator ThirdAuditAsyncFlow_DestroyAndCancelDuringPreparation_ShouldPreserveCallerCancellation()
        {
            var controller = CreateThirdAuditAsyncFlow(out var ui, out var image, out _, out var material);
            using var caller = new CancellationTokenSource();
            var removed = false;
            var after = 0;
            ui.AfterUIHidedEvent += () => after++;
            UnityAction removeAndCancel = () =>
            {
                if (removed) return;
                removed = true;
                Object.DestroyImmediate(controller);
                caller.Cancel();
            };
            image.RegisterDirtyVerticesCallback(removeAndCancel);
            try
            {
                var operation = CaptureException(ui.HideAsync(caller.Token));
                yield return WaitForTask(operation);
                Assert.IsTrue(removed);
                Assert.IsFalse(controller);
                Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
                Assert.AreEqual(0, after);
                Assert.AreEqual(UIState.Hiding, ui.State);
                ui.Show();
                Assert.AreEqual(UIState.Showed, ui.State, "Later requests must still be usable.");
            }
            finally
            {
                if (image) image.UnregisterDirtyVerticesCallback(removeAndCancel);
                Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator ThirdAuditAsyncFlow_DestroyPanelDuringPreparation_ShouldCancelWithoutStaleAccess()
        {
            CreateThirdAuditAsyncFlow(out var ui, out var image, out _, out var material);
            var removed = false;
            var after = 0;
            ui.AfterUIHidedEvent += () => after++;
            UnityAction remove = () =>
            {
                if (removed) return;
                removed = true;
                Object.DestroyImmediate(ui.gameObject);
            };
            image.RegisterDirtyVerticesCallback(remove);
            try
            {
                var operation = CaptureException(ui.HideAsync(CancellationToken.None));
                yield return WaitForTask(operation);
                Assert.IsTrue(removed);
                Assert.IsFalse(ui);
                Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
                Assert.AreEqual(0, after);
            }
            finally
            {
                if (image) image.UnregisterDirtyVerticesCallback(remove);
                Object.DestroyImmediate(material);
            }
        }
    }
}
