using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证材质和网格回调重入时的组遍历、所有权与销毁清理。</summary>
    public class UIFlowReentrancyRuntimeTests
    {
        /// <summary>隔离本用例 UI 层级的 Canvas 根对象。</summary>
        private GameObject canvasRoot;
        /// <summary>本用例拥有的临时材质，结束时统一释放。</summary>
        private readonly List<Material> materials = new();

        /// <summary>创建本用例专用的 Canvas 环境，隔离不同流动组测试的对象与材质。</summary>
        [SetUp]
        public void SetUp()
        {
            // 将画布根节点（canvasRoot）设为“new GameObject("ReentrantFlowCanvas", typeof(RectTransform), typeof(Canvas))”，建立当前测试条件。
            canvasRoot = new GameObject("ReentrantFlowCanvas", typeof(RectTransform), typeof(Canvas));
            // 将“canvasRoot.GetComponent<Canvas>().renderMode”设为“RenderMode.ScreenSpaceOverlay”，建立当前测试条件。
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// <summary>销毁本用例拥有的对象并恢复修改过的全局设置，隔离后续测试。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 根据画布根节点（canvasRoot）选择对应测试路径。
            if (canvasRoot) Object.Destroy(canvasRoot);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 逐项检查或处理“var material in materials”，覆盖集合中的每个对象。
            foreach (var material in materials) if (material) Object.Destroy(material);
            // 清空待清理材质集合（materials），防止上一步数据影响后续观察。
            materials.Clear();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
        }

        /// <summary>验证安装材质期间当前成员迁移后，原组不会覆盖目标组材质。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator CurrentMember_ReparentDuringMaterialInstall_ShouldKeepDestinationMaterial()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateGroup("Source", out var sourceContent);
            // 创建“destination”并配置测试所需的层级或表现组件。
            var destination = CreateGroup("Destination", out var destinationContent);
            // 创建成员图像（image）并配置测试所需的层级或表现组件。
            var image = CreateMember(sourceContent, out var effect);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 获取“sourceUI”的实际组件引用，供表现与生命周期断言使用。
            var sourceUI = source.GetComponent<UIBase>();
            // 获取“destinationUI”的实际组件引用，供表现与生命周期断言使用。
            var destinationUI = destination.GetComponent<UIBase>();
            // 提交“sourceUI”的显示请求，触发实际生命周期。
            sourceUI.Show();
            // 提交“destinationUI”的显示请求，触发实际生命周期。
            destinationUI.Show();
            // 创建“sourceMaterial”并配置测试所需的层级或表现组件。
            var sourceMaterial = CreateMaterial("SourceInstall");
            // 创建“destinationMaterial”并配置测试所需的层级或表现组件。
            var destinationMaterial = CreateMaterial("DestinationInstall");
            // 配置流动组材质模板，避免通过编辑器验证回调改变测试时序。
            SetTemplate(source, sourceMaterial);
            // 配置流动组材质模板，避免通过编辑器验证回调改变测试时序。
            SetTemplate(destination, destinationMaterial);
            // 将成员图像（image）的当前材质设为空引用，建立当前测试条件。
            image.material = null;

            // 初始化“moved”为false，作为后续回调的观测基准。
            var moved = false;
            // 初始化“installedAtCallback”为空引用，作为后续回调的观测基准。
            Material installedAtCallback = null;
            // 准备在当前成员标脏时将其迁到目标组，验证重入后的所有权保护。
            UnityAction moveCurrentMember = () =>
            {
                // 根据“moved”选择对应测试路径。
                if (moved) return;
                // 将“moved”设为true，建立当前测试条件。
                moved = true;
                // 将“installedAtCallback”设为成员图像（image）的当前材质，建立当前测试条件。
                installedAtCallback = image.material;
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                image.transform.SetParent(destinationContent, false);
                // 记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。
                TraceMember("install callback after reparent", destinationUI, image, effect);
            };
            // 注册 Graphic 标脏回调，在真实材质或顶点刷新期间构造重入场景。
            image.RegisterDirtyMaterialCallback(moveCurrentMember);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 提交“sourceUI”的隐藏请求，检查子树联动与终态。
                sourceUI.Hide();
                // 记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。
                TraceMember("after interrupted source install", sourceUI, image, effect);
                // 确认“moved”为真。
                Assert.IsTrue(moved, "The material setter must invoke its actual dirty callback.");
                // 确认“installedAtCallback”仍引用“sourceMaterial”。
                Assert.AreSame(sourceMaterial, installedAtCallback,
                    "The callback must occur inside installation of the source shared material.");
                // 确认“image.transform”的直属父节点仍引用“destinationContent”。
                Assert.AreSame(destinationContent, image.transform.parent);
                // 确认“destinationUI”的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, destinationUI.State);
                // 确认“effect.materialPrefab”仍引用“destinationMaterial”。
                Assert.AreSame(destinationMaterial, effect.materialPrefab);
                // 确认成员图像（image）的当前材质仍引用“destinationMaterial”。
                Assert.AreSame(destinationMaterial, image.material,
                    "A material installed by the old flow effect must not be mistaken for a user override.");
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { image.UnregisterDirtyMaterialCallback(moveCurrentMember); }
        }

        /// <summary>验证材质安装回调销毁当前效果后恢复原材质并安全完成流程。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator CurrentMember_DestroyEffectDuringMaterialInstall_ShouldRestoreMaterialAndFinish()
        {
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup("DestroyCurrentEffect", out var content);
            // 创建成员图像（image）并配置测试所需的层级或表现组件。
            var image = CreateMember(content, out var effect);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 获取界面（ui）的实际组件引用，供表现与生命周期断言使用。
            var ui = group.GetComponent<UIBase>();
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 创建“sharedMaterial”并配置测试所需的层级或表现组件。
            var sharedMaterial = CreateMaterial("DestroyedEffectInstall");
            // 配置流动组材质模板，避免通过编辑器验证回调改变测试时序。
            SetTemplate(group, sharedMaterial);
            // 将成员图像（image）的当前材质设为空引用，建立当前测试条件。
            image.material = null;

            // 初始化“destroyed”为false，作为后续回调的观测基准。
            var destroyed = false;
            // 初始化“materialAfterDestroy”为空引用，作为后续回调的观测基准。
            Material materialAfterDestroy = null;
            // 准备在材质标脏时销毁当前效果，验证失效组件的后续处理被跳过。
            UnityAction destroyCurrentEffect = () =>
            {
                // 根据“destroyed”选择对应测试路径。
                if (destroyed) return;
                // 将“destroyed”设为true，建立当前测试条件。
                destroyed = true;
                // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
                Object.DestroyImmediate(effect);
                // 将“materialAfterDestroy”设为成员图像（image）的当前材质，建立当前测试条件。
                materialAfterDestroy = image.material;
                // 记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。
                TraceMember("immediately after effect removal", ui, image, effect);
            };
            // 注册 Graphic 标脏回调，在真实材质或顶点刷新期间构造重入场景。
            image.RegisterDirtyMaterialCallback(destroyCurrentEffect);
            // 初始化“failure”为空引用，作为后续回调的观测基准。
            Exception failure = null;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try { ui.Hide(); }
                // 捕获本轮实际异常，供后续核对失败或取消类型。
                catch (Exception exception) { failure = exception; }
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { image.UnregisterDirtyMaterialCallback(destroyCurrentEffect); }

            // 记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。
            TraceMember("after removed effect's old install returns", ui, image, effect);
            // 记录实际状态与表现，便于失败时定位回调时序。
            TestContext.WriteLine("material restored on removal: " + ObjectName(materialAfterDestroy));
            // 确认“destroyed”为真。
            Assert.IsTrue(destroyed, "The effect must be removed during its own material assignment.");
            // 确认动效组件（effect）为假。
            Assert.IsFalse(effect);
            // 确认“failure”为空。
            Assert.IsNull(failure, "Removing one effect must not fail the live panel: " + failure);
            // 确认“materialAfterDestroy”仍引用“image.defaultMaterial”。
            Assert.AreSame(image.defaultMaterial, materialAfterDestroy,
                "Removal must restore the material installed by the removed effect.");
            // 确认成员图像（image）的当前材质仍引用“image.defaultMaterial”。
            Assert.AreSame(image.defaultMaterial, image.material);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>验证材质替换期间成员换父后，旧组不再改写其归属与材质。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator CurrentMember_ReparentDuringMaterialReplacement_ShouldKeepDestinationOwnership()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateGroup("ReplacingSource", out var sourceContent);
            // 创建“destination”并配置测试所需的层级或表现组件。
            var destination = CreateGroup("ReplacementDestination", out var destinationContent);
            // 创建成员图像（image）并配置测试所需的层级或表现组件。
            var image = CreateMember(sourceContent, out var effect);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 创建“originalMaterial”并配置测试所需的层级或表现组件。
            var originalMaterial = CreateMaterial("SourceOriginal");
            // 创建“replacementMaterial”并配置测试所需的层级或表现组件。
            var replacementMaterial = CreateMaterial("SourceReplacement");
            // 创建“destinationMaterial”并配置测试所需的层级或表现组件。
            var destinationMaterial = CreateMaterial("DestinationReplacement");
            // 配置流动组材质模板，避免通过编辑器验证回调改变测试时序。
            SetTemplate(source, originalMaterial);
            // 配置流动组材质模板，避免通过编辑器验证回调改变测试时序。
            SetTemplate(destination, destinationMaterial);
            // 将成员图像（image）的当前材质设为空引用，建立当前测试条件。
            image.material = null;
            // 获取“sourceUI”的实际组件引用，供表现与生命周期断言使用。
            var sourceUI = source.GetComponent<UIBase>();
            // 获取“destinationUI”的实际组件引用，供表现与生命周期断言使用。
            var destinationUI = destination.GetComponent<UIBase>();
            // 提交“sourceUI”的显示请求，触发实际生命周期。
            sourceUI.Show();
            // 提交“destinationUI”的显示请求，触发实际生命周期。
            destinationUI.Show();
            // 确认成员图像（image）的当前材质仍引用“originalMaterial”。
            Assert.AreSame(originalMaterial, image.material);
            // 配置流动组材质模板，避免通过编辑器验证回调改变测试时序。
            SetTemplate(source, replacementMaterial);

            // 初始化“moved”为false，作为后续回调的观测基准。
            var moved = false;
            // 初始化“materialAtCallback”为空引用，作为后续回调的观测基准。
            Material materialAtCallback = null;
            // 初始化“materialAfterMove”为空引用，作为后续回调的观测基准。
            Material materialAfterMove = null;
            // 准备在当前成员标脏时将其迁到目标组，验证重入后的所有权保护。
            UnityAction moveCurrentMember = () =>
            {
                // 根据“moved”选择对应测试路径。
                if (moved) return;
                // 将“moved”设为true，建立当前测试条件。
                moved = true;
                // 将“materialAtCallback”设为成员图像（image）的当前材质，建立当前测试条件。
                materialAtCallback = image.material;
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                image.transform.SetParent(destinationContent, false);
                // 将“materialAfterMove”设为成员图像（image）的当前材质，建立当前测试条件。
                materialAfterMove = image.material;
                // 记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。
                TraceMember("replacement callback after reparent", destinationUI, image, effect);
            };
            // 注册 Graphic 标脏回调，在真实材质或顶点刷新期间构造重入场景。
            image.RegisterDirtyMaterialCallback(moveCurrentMember);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 提交“sourceUI”的隐藏请求，检查子树联动与终态。
                sourceUI.Hide();
                // 记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。
                TraceMember("after interrupted source replacement", sourceUI, image, effect);
                // 确认“moved”为真。
                Assert.IsTrue(moved);
                // 确认“materialAtCallback”仍引用“image.defaultMaterial”。
                Assert.AreSame(image.defaultMaterial, materialAtCallback,
                    "The callback must run while the old installed template is being cleared.");
                // 确认“materialAfterMove”仍引用“destinationMaterial”。
                Assert.AreSame(destinationMaterial, materialAfterMove);
                // 确认“image.transform”的直属父节点仍引用“destinationContent”。
                Assert.AreSame(destinationContent, image.transform.parent);
                // 确认“effect.materialPrefab”仍引用“destinationMaterial”。
                Assert.AreSame(destinationMaterial, effect.materialPrefab,
                    "The interrupted template replacement must not overwrite the new owner configuration.");
                // 确认成员图像（image）的当前材质仍引用“destinationMaterial”。
                Assert.AreSame(destinationMaterial, image.material);
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { image.UnregisterDirtyMaterialCallback(moveCurrentMember); }
        }

        /// <summary>验证材质刷新回调销毁控制器后完整解除成员归属并结束遍历。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Group_DestroyInsideMaterialRefresh_ShouldDetachAndFinish() =>
            VerifyGroupDestroyedInsideDirtyCallback(false);

        /// <summary>验证终点进度回调销毁控制器后不会残留成员隐藏状态。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Group_DestroyInsideTerminalProgress_ShouldDetachAndFinish() =>
            VerifyGroupDestroyedInsideDirtyCallback(true);

        /// <summary>验证在指定标脏阶段销毁组控制器后，成员恢复可见且旧遍历安全退出。</summary>
        /// <param name="terminalProgressOnly">是否只在隐藏终点进度的标脏回调中销毁控制器。</param>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        private IEnumerator VerifyGroupDestroyedInsideDirtyCallback(bool terminalProgressOnly)
        {
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup("DestroyGroup", out var content);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateMember(content, out var firstEffect);
            // 创建后续界面（later）并配置测试所需的层级或表现组件。
            var later = CreateMember(content, out var laterEffect);
            // 将“later.rectTransform.anchoredPosition”设为“new Vector2(200f, 0f)”，建立当前测试条件。
            later.rectTransform.anchoredPosition = new Vector2(200f, 0f);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 将“group.EndScreenPoint”设为“RectTransformUtility.WorldToScreenPoint(null, first.transform.position)”，建立当前测试条件。
            group.EndScreenPoint = RectTransformUtility.WorldToScreenPoint(null, first.transform.position);
            // 获取界面（ui）的实际组件引用，供表现与生命周期断言使用。
            var ui = group.GetComponent<UIBase>();
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 初始化“destroyed”为false，作为后续回调的观测基准。
            var destroyed = false;
            // 准备在成员标脏时销毁组控制器，验证整个快照仍能安全清理。
            UnityAction destroyGroup = () =>
            {
                // 根据“destroyed || (terminalProgressOnly && firstEffect.EffectAmount < 1f)”选择对应测试路径。
                if (destroyed || (terminalProgressOnly && firstEffect.EffectAmount < 1f)) return;
                // 将“destroyed”设为true，建立当前测试条件。
                destroyed = true;
                // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
                Object.DestroyImmediate(group);
            };
            // 注册 Graphic 标脏回调，在真实材质或顶点刷新期间构造重入场景。
            first.RegisterDirtyVerticesCallback(destroyGroup);
            // 初始化“failure”为空引用，作为后续回调的观测基准。
            Exception failure = null;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
                try { ui.Hide(); }
                // 捕获本轮实际异常，供后续核对失败或取消类型。
                catch (Exception exception) { failure = exception; }
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { first.UnregisterDirtyVerticesCallback(destroyGroup); }

            // 记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。
            TraceMember("after group removal, first", ui, first, firstEffect);
            // 记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。
            TraceMember("after group removal, later", ui, later, laterEffect);
            // 确认“destroyed”为真。
            Assert.IsTrue(destroyed);
            // 确认效果所属组（group）为假。
            Assert.IsFalse(group);
            // 确认“failure”为空。
            Assert.IsNull(failure, "Removing the controller during a real dirty callback must finish safely: " + failure);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
            // 组控制器移除后，流动效果应明确恢复独立且可见的网格。
            Assert.That(firstEffect.EffectAmount, Is.Zero);
            // 确认“laterEffect”的流动进度为零。
            Assert.That(laterEffect.EffectAmount, Is.Zero);
            // 验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。
            AssertVisible(first);
            // 验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。
            AssertVisible(later);
        }

        /// <summary>验证成员自身终点回调中换父后保留新组的最终状态。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator CurrentMember_ReparentInsideOwnTerminalDirtyCallback_ShouldKeepDestinationState()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateGroup("VertexSource", out var sourceContent);
            // 创建“destination”并配置测试所需的层级或表现组件。
            var destination = CreateGroup("VertexDestination", out var destinationContent);
            // 创建成员图像（image）并配置测试所需的层级或表现组件。
            var image = CreateMember(sourceContent, out var effect);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 获取“sourceUI”的实际组件引用，供表现与生命周期断言使用。
            var sourceUI = source.GetComponent<UIBase>();
            // 获取“destinationUI”的实际组件引用，供表现与生命周期断言使用。
            var destinationUI = destination.GetComponent<UIBase>();
            // 提交“sourceUI”的显示请求，触发实际生命周期。
            sourceUI.Show();
            // 提交“destinationUI”的显示请求，触发实际生命周期。
            destinationUI.Show();
            // 初始化“moved”为false，作为后续回调的观测基准。
            var moved = false;
            // 准备在当前成员标脏时将其迁到目标组，验证重入后的所有权保护。
            UnityAction moveCurrentMember = () =>
            {
                // 根据“moved || effect.EffectAmount < 1f”选择对应测试路径。
                if (moved || effect.EffectAmount < 1f) return;
                // 将“moved”设为true，建立当前测试条件。
                moved = true;
                // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
                image.transform.SetParent(destinationContent, false);
            };
            // 注册 Graphic 标脏回调，在真实材质或顶点刷新期间构造重入场景。
            image.RegisterDirtyVerticesCallback(moveCurrentMember);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 提交“sourceUI”的隐藏请求，检查子树联动与终态。
                sourceUI.Hide();
                // 确认“moved”为真。
                Assert.IsTrue(moved);
                // 确认“image.transform”的直属父节点仍引用“destinationContent”。
                Assert.AreSame(destinationContent, image.transform.parent);
                // 确认“destinationUI”的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, destinationUI.State);
                // 确认动效组件（effect）的流动进度为零。
                Assert.That(effect.EffectAmount, Is.Zero);
                // 验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。
                AssertVisible(image);
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { image.UnregisterDirtyVerticesCallback(moveCurrentMember); }
        }

        /// <summary>验证材质刷新期间重新启用控制器会延后层级刷新，避免重入破坏缓存。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Group_ReenableInsideMaterialRefresh_ShouldDeferHierarchyRefresh()
        {
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup("ReenabledGroup", out var content);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateMember(content, out var firstEffect);
            // 创建后续界面（later）并配置测试所需的层级或表现组件。
            var later = CreateMember(content, out var laterEffect);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 获取界面（ui）的实际组件引用，供表现与生命周期断言使用。
            var ui = group.GetComponent<UIBase>();
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 初始化“refreshed”为false，作为后续回调的观测基准。
            var refreshed = false;
            // 初始化“added”为空引用，作为后续回调的观测基准。
            Image added = null;
            // 初始化“addedEffect”为空引用，作为后续回调的观测基准。
            UIFlowMeshEffect addedEffect = null;
            // 准备在材质回调中重新启用控制器，验证层级刷新不会重入当前遍历。
            UnityAction refreshFromCallback = () =>
            {
                // 根据“refreshed”选择对应测试路径。
                if (refreshed) return;
                // 将“refreshed”设为true，建立当前测试条件。
                refreshed = true;
                // 通过真实组件启用生命周期进入 OnEnable 并刷新流动层级。
                // 禁用期间新增成员，确保下一次扫描必须发现该成员。
                group.enabled = false;
                // 将“added”设为“CreateMember(content, out addedEffect)”，建立当前测试条件。
                added = CreateMember(content, out addedEffect);
                // 将效果所属组（group）的组件启用状态设为true，建立当前测试条件。
                group.enabled = true;
            };
            // 注册 Graphic 标脏回调，在真实材质或顶点刷新期间构造重入场景。
            first.RegisterDirtyVerticesCallback(refreshFromCallback);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认当前被测调用安全完成，不产生意外异常。
                Assert.DoesNotThrow(() => ui.Hide());
                // 确认“refreshed”为真。
                Assert.IsTrue(refreshed);
                // 确认界面（ui）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, ui.State);
                // 确认“firstEffect”的流动进度符合1f。
                Assert.That(firstEffect.EffectAmount, Is.EqualTo(1f));
                // 确认“laterEffect”的流动进度符合1f。
                Assert.That(laterEffect.EffectAmount, Is.EqualTo(1f));
                // 确认“addedEffect”的流动进度符合1f。
                Assert.That(addedEffect.EffectAmount, Is.EqualTo(1f));
                // 记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。
                TraceMember("public reenable refresh, added", ui, added, addedEffect);
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { first.UnregisterDirtyVerticesCallback(refreshFromCallback); }
        }

        /// <summary>创建测试流动组及必要的层级节点，供材质、路径与生命周期测试使用。</summary>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <param name="content">输出组内放置测试成员的内容节点。</param>
        /// <returns>创建的流动组控制器。</returns>
        private UIFlowGroupController CreateGroup(string name, out RectTransform content)
        {
            // 每个面板使用独立普通父节点，排除兄弟互斥的干扰。
            var container = CreateRect(name + "Container", canvasRoot.transform);
            // 添加并保存效果所属组（group），供后续生命周期与效果检查使用。
            var group = CreateRect(name, container).gameObject.AddComponent<UIFlowGroupController>();
            // 将“content”设为“CreateRect(name + "Content", group.transform)”，建立当前测试条件。
            content = CreateRect(name + "Content", group.transform);
            // 返回效果所属组（group），交给调用方继续配置或断言。
            return group;
        }

        /// <summary>创建带流动网格效果的 Image 成员，输出效果引用供归属与进度断言使用。</summary>
        /// <param name="parent">新对象的父节点或输出的根界面。</param>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <returns>创建的成员 Image。</returns>
        private Image CreateMember(Transform parent, out UIFlowMeshEffect effect)
        {
            // 创建矩形变换（rect）并配置测试所需的层级或表现组件。
            var rect = CreateRect("Member", parent);
            // 将“rect.sizeDelta”设为“new Vector2(100f, 60f)”，建立当前测试条件。
            rect.sizeDelta = new Vector2(100f, 60f);
            // 添加并保存成员图像（image），供后续生命周期与效果检查使用。
            var image = rect.gameObject.AddComponent<Image>();
            // 测试配置模板前使用独立材质，避免可选 Shader 查找干扰 CPU 行为验证。
            image.material = CreateMaterial("MemberCustom");
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            // 返回成员图像（image），交给调用方继续配置或断言。
            return image;
        }

        /// <summary>创建独立测试材质并登记释放，避免共享资源影响材质所有权断言。</summary>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <returns>已登记释放的独立材质。</returns>
        private Material CreateMaterial(string name)
        {
            // 创建测试材质（material），隔离本用例的资源或观测数据。
            var material = new Material(Shader.Find("UI/Default")) { name = name + "_" + materials.Count };
            // 将本轮对象或观测值加入待清理材质集合（materials），供统一清理或顺序断言使用。
            materials.Add(material);
            // 返回测试材质（material），交给调用方继续配置或断言。
            return material;
        }

        /// <summary>记录成员状态、材质和流动进度，辅助定位回调重入发生的阶段。</summary>
        /// <param name="stage">诊断输出对应的业务阶段名称。</param>
        /// <param name="panel">需要记录状态的界面。</param>
        /// <param name="image">用于读取真实 Canvas 网格的 Image。</param>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        private static void TraceMember(string stage, UIBase panel, Image image, UIFlowMeshEffect effect)
        {
            // 即使组件的原生对象已移除，仍读取托管归属字段以诊断残留状态。
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            // 取得“owner”的反射入口，仅在测试内访问非公开配置。
            var owner = typeof(UIFlowMeshEffect).GetField("groupController", flags).GetValue(effect) as Object;
            // 取得“installed”的反射入口，仅在测试内访问非公开配置。
            var installed = typeof(UIFlowMeshEffect).GetField("installedSharedFlowMaterial", flags).GetValue(effect) as Object;
            // 记录实际状态与表现，便于失败时定位回调时序。
            TestContext.WriteLine(stage + ": panel=" + ObjectName(panel) + ", state=" + panel.State +
                ", effectAlive=" + (bool)effect + ", parent=" + ObjectName(image.transform.parent) +
                ", trackedOwner=" + ObjectName(owner) + ", amount=" + effect.EffectAmount +
                ", template=" + ObjectName(effect.materialPrefab) + ", installed=" + ObjectName(installed) +
                ", actual=" + ObjectName(image.material) + ", default=" + ObjectName(image.defaultMaterial));
        }

        /// <summary>区分空引用、已销毁对象与存活对象，输出安全的诊断名称。</summary>
        /// <param name="value">写入配置或用于断言的测试值。</param>
        /// <returns>对象名或表示空引用、已销毁状态的文本。</returns>
        private static string ObjectName(Object value)
        {
            // 根据“ReferenceEquals(value, null)”选择对应测试路径。
            if (ReferenceEquals(value, null)) return "<null>";
            // 返回“value ? value.name : "<destroyed>"”，交给调用方继续配置或断言。
            return value ? value.name : "<destroyed>";
        }

        /// <summary>创建指定父节点下的 RectTransform，建立可控的测试层级。</summary>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <param name="parent">新对象的父节点或输出的根界面。</param>
        /// <returns>指定层级下新建的 RectTransform。</returns>
        private static RectTransform CreateRect(string name, Transform parent)
        {
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject(name, typeof(RectTransform));
            // 将新节点挂到指定父节点，并保持本地变换。
            go.transform.SetParent(parent, false);
            // 返回“(RectTransform)go.transform”，交给调用方继续配置或断言。
            return (RectTransform)go.transform;
        }

        /// <summary>配置流动组材质模板，避免通过编辑器验证回调改变测试时序。</summary>
        /// <param name="group">需要配置或验证的流动组控制器。</param>
        /// <param name="material">需要安装到流动组的材质模板。</param>
        private static void SetTemplate(UIFlowGroupController group, Material material)
        {
            // 取得“field”的反射入口，仅在测试内访问非公开配置。
            var field = typeof(UIFlowGroupController).GetField(
                "materialPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            // 确认“field”存在。
            Assert.NotNull(field);
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            field.SetValue(group, material);
        }

        /// <summary>验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。</summary>
        /// <param name="image">用于读取真实 Canvas 网格的 Image。</param>
        private static void AssertVisible(Image image)
        {
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            image.Rebuild(CanvasUpdate.PreRender);
            // 保存实际网格（mesh）的基准值“image.canvasRenderer.GetMesh()”，供后续比较或恢复。
            var mesh = image.canvasRenderer.GetMesh();
            // 确认实际网格（mesh）存在。
            Assert.NotNull(mesh);
            // 确认“mesh.vertexCount”符合4。
            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            // 逐项检查或处理“var color in mesh.colors32”，覆盖集合中的每个对象。
            foreach (var color in mesh.colors32) Assert.That(color.a, Is.EqualTo(255));
        }
    }
}
