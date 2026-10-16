using System;
using NUnit.Framework;
using UnityEngine;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        [TestCase(typeof(UIBase), typeof(UIBase))]
        [TestCase(typeof(UIBase), typeof(UIBaseWithShowHideInteraction))]
        [TestCase(typeof(UIBaseWithShowHideInteraction), typeof(UIBase))]
        [TestCase(typeof(UIBaseWithShowHideInteraction), typeof(UIBaseWithShowHideInteraction))]
        public void PanelComponent_AddingSecondBaseOrDerived_ShouldRejectAndKeepOriginalEffect(
            Type firstType, Type secondType)
        {
            var go = CreateObject("UniquePanel", CreateObject("UniquePanelContainer").transform);
            var original = (UIBase)go.AddComponent(firstType);
            original.enabled = false;
            go.AddComponent<AlphaEffectController>();
            original.Show();
            original.Hide();
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.Zero);

            string rejectionMessage = null;
            Application.LogCallback captureRejection = (message, _, _) =>
            {
                if (message.StartsWith("Can't add '" + secondType.Name + "' to UniquePanel", StringComparison.Ordinal))
                    rejectionMessage = message;
            };
            Component duplicate;
            Application.logMessageReceived += captureRejection;
            try { duplicate = go.AddComponent(secondType); }
            finally { Application.logMessageReceived -= captureRejection; }

            Assert.That(rejectionMessage, Does.Contain("already added"), "Unity must explain the rejected configuration.");
            Assert.IsNull(duplicate);
            CollectionAssert.AreEqual(new[] { original }, go.GetComponents<UIBase>());
            original.Show();
            Assert.AreEqual(UIState.Showed, original.State);
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f),
                "Rejecting the duplicate must preserve the original state-to-effect binding.");
        }
    }
}
