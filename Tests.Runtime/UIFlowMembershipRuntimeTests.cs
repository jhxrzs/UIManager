using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证流动成员动态创建、激活和迁移时的组归属与表现同步。</summary>
    public class UIFlowMembershipRuntimeTests
    {
        /// <summary>隔离本用例 UI 层级的 Canvas 根对象。</summary>
        private GameObject canvasRoot;
        /// <summary>本用例拥有的临时材质，结束时统一释放。</summary>
        private readonly List<Material> materials = new();

        /// <summary>创建本用例专用的 Canvas 环境，隔离不同流动组测试的对象与材质。</summary>
        [SetUp]
        public void SetUp()
        {
            // 将画布根节点（canvasRoot）设为“new GameObject("MembershipCanvas", typeof(RectTransform), typeof(Canvas))”，建立当前测试条件。
            canvasRoot = new GameObject("MembershipCanvas", typeof(RectTransform), typeof(Canvas));
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

        /// <summary>验证隐藏组中新加入的深层成员在首次网格重建前同步隐藏进度。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator HiddenGroup_NewDeepMember_ShouldBeHiddenBeforeFirstRebuild()
        {
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup("HiddenGroup");
            // 创建“content”并配置测试所需的层级或表现组件。
            var content = CreateRect("Content", group.transform);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 通过所属 UIBase 的正常生命周期将流动组推进到隐藏终态。
            Hide(group);
            // 创建成员图像（image）并配置测试所需的层级或表现组件。
            var image = CreateImage(content);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();

            // 验证成员进度与实际重建网格均处于隐藏终态。
            AssertHidden(image, effect);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 提交“group.GetComponent<UIBase>()”的隐藏请求，检查子树联动与终态。
            group.GetComponent<UIBase>().Hide();
            // 验证成员进度与实际重建网格均处于隐藏终态。
            AssertHidden(image, effect);
        }

        /// <summary>验证失活成员或禁用动效重新启用时同步所属组的当前状态。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator InactiveMemberAndDisabledEffect_ShouldSynchronizeWhenEnabled()
        {
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup("HiddenGroup");
            // 创建“content”并配置测试所需的层级或表现组件。
            var content = CreateRect("Content", group.transform);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 通过所属 UIBase 的正常生命周期将流动组推进到隐藏终态。
            Hide(group);
            // 创建成员图像（image）并配置测试所需的层级或表现组件。
            var image = CreateImage(content);
            // 切换“image.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            image.gameObject.SetActive(false);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            // 切换“image.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            image.gameObject.SetActive(true);
            // 验证成员进度与实际重建网格均处于隐藏终态。
            AssertHidden(image, effect);

            // 将动效组件（effect）的组件启用状态设为false，建立当前测试条件。
            effect.enabled = false;
            // 验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。
            AssertVisible(image);
            // 将动效组件（effect）的组件启用状态设为true，建立当前测试条件。
            effect.enabled = true;
            // 验证成员进度与实际重建网格均处于隐藏终态。
            AssertHidden(image, effect);

            // 将效果所属组（group）的组件启用状态设为false，建立当前测试条件。
            group.enabled = false;
            // 创建“laterImage”并配置测试所需的层级或表现组件。
            var laterImage = CreateImage(content);
            // 添加并保存“laterEffect”，供后续生命周期与效果检查使用。
            var laterEffect = laterImage.gameObject.AddComponent<UIFlowMeshEffect>();
            // 验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。
            AssertVisible(laterImage);
            // 将效果所属组（group）的组件启用状态设为true，建立当前测试条件。
            group.enabled = true;
            // 验证成员进度与实际重建网格均处于隐藏终态。
            AssertHidden(laterImage, laterEffect);
            // 验证成员进度与实际重建网格均处于隐藏终态。
            AssertHidden(image, effect);
        }

        /// <summary>验证成员移入隐藏组后立即采用新所有者的隐藏状态。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ReparentingIntoHiddenGroup_ShouldUseNewOwnerImmediately()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateGroup("Source");
            // 创建“sourceContent”并配置测试所需的层级或表现组件。
            var sourceContent = CreateRect("SourceContent", source.transform);
            // 创建“destination”并配置测试所需的层级或表现组件。
            var destination = CreateGroup("Destination");
            // 创建“destinationContent”并配置测试所需的层级或表现组件。
            var destinationContent = CreateRect("DestinationContent", destination.transform);
            // 创建成员图像（image）并配置测试所需的层级或表现组件。
            var image = CreateImage(sourceContent);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 提交“source.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            source.GetComponent<UIBase>().Show();
            // 通过所属 UIBase 的正常生命周期将流动组推进到隐藏终态。
            Hide(destination);
            // 验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。
            AssertVisible(image);

            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            image.transform.SetParent(destinationContent, false);
            // 验证成员进度与实际重建网格均处于隐藏终态。
            AssertHidden(image, effect);
            // 提交“source.GetComponent<UIBase>()”的隐藏请求，检查子树联动与终态。
            source.GetComponent<UIBase>().Hide();
            // 提交“source.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            source.GetComponent<UIBase>().Show();
            // 验证成员进度与实际重建网格均处于隐藏终态。
            AssertHidden(image, effect);
        }

        /// <summary>验证控制器同节点上新添加的网格动效能注册为组成员。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator NewEffectOnControllerObject_ShouldRegisterItself()
        {
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup("SameObject");
            // 添加并保存成员图像（image），供后续生命周期与效果检查使用。
            var image = group.gameObject.AddComponent<Image>();
            // 为测试 Graphic 设置独立材质，避免依赖可选流动 Shader 资源。
            UseCustomMaterial(image);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 通过所属 UIBase 的正常生命周期将流动组推进到隐藏终态。
            Hide(group);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = group.gameObject.AddComponent<UIFlowMeshEffect>();
            // 验证成员进度与实际重建网格均处于隐藏终态。
            AssertHidden(image, effect);
        }

        /// <summary>验证 Graphic 标脏回调中新增成员不会破坏正在执行的组遍历。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator MemberCreatedInsideGraphicDirtyCallback_ShouldNotInvalidateGroupTraversal()
        {
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup("ReentrantGroup");
            // 创建“content”并配置测试所需的层级或表现组件。
            var content = CreateRect("Content", group.transform);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateImage(content);
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            first.gameObject.AddComponent<UIFlowMeshEffect>();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 提交“group.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            group.GetComponent<UIBase>().Show();
            // 初始化第二项（second）为空引用，作为后续回调的观测基准。
            Image second = null;
            // 初始化“third”为空引用，作为后续回调的观测基准。
            Image third = null;
            // 初始化“added”为false，作为后续回调的观测基准。
            var added = false;
            // 准备在首个成员标脏时新增两个成员的回调，验证组遍历对动态新增的处理。
            UnityEngine.Events.UnityAction addMembers = () =>
            {
                // 根据“added”选择对应测试路径。
                if (added) return;
                // 将“added”设为true，建立当前测试条件。
                added = true;
                // 将第二项（second）设为“CreateImage(content)”，建立当前测试条件。
                second = CreateImage(content);
                // 添加测试需要的组件，供后续生命周期与效果检查使用。
                second.gameObject.AddComponent<UIFlowMeshEffect>();
                // 将“third”设为“CreateImage(content)”，建立当前测试条件。
                third = CreateImage(content);
                // 添加测试需要的组件，供后续生命周期与效果检查使用。
                third.gameObject.AddComponent<UIFlowMeshEffect>();
            };
            // 注册 Graphic 标脏回调，在真实材质或顶点刷新期间构造重入场景。
            first.RegisterDirtyVerticesCallback(addMembers);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 提交“group.GetComponent<UIBase>()”的隐藏请求，检查子树联动与终态。
                group.GetComponent<UIBase>().Hide();
                // 确认“added”为真。
                Assert.IsTrue(added, "The real Graphic dirty callback must have created members during traversal.");
                // 验证成员进度与实际重建网格均处于隐藏终态。
                AssertHidden(first, first.GetComponent<UIFlowMeshEffect>());
                // 验证成员进度与实际重建网格均处于隐藏终态。
                AssertHidden(second, second.GetComponent<UIFlowMeshEffect>());
                // 验证成员进度与实际重建网格均处于隐藏终态。
                AssertHidden(third, third.GetComponent<UIFlowMeshEffect>());
            }
            finally
            {
                // 解除 Graphic 标脏回调，避免测试监听在清理或后续用例中再次触发。
                first.UnregisterDirtyVerticesCallback(addMembers);
            }
        }

        /// <summary>验证标脏回调中迁移成员后原组不能覆盖新组下发的状态。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator MemberReparentedInsideGraphicDirtyCallback_ShouldKeepNewGroupState()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateGroup("ReentrantSource");
            // 创建“sourceContent”并配置测试所需的层级或表现组件。
            var sourceContent = CreateRect("SourceContent", source.transform);
            // 创建“destination”并配置测试所需的层级或表现组件。
            var destination = CreateGroup("ReentrantDestination");
            // 创建“destinationContent”并配置测试所需的层级或表现组件。
            var destinationContent = CreateRect("DestinationContent", destination.transform);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateImage(sourceContent);
            // 添加并保存“firstEffect”，供后续生命周期与效果检查使用。
            var firstEffect = first.gameObject.AddComponent<UIFlowMeshEffect>();
            // 创建后续界面（later）并配置测试所需的层级或表现组件。
            var later = CreateImage(sourceContent);
            // 添加并保存“laterEffect”，供后续生命周期与效果检查使用。
            var laterEffect = later.gameObject.AddComponent<UIFlowMeshEffect>();
            // 将“later.rectTransform.anchoredPosition”设为“new Vector2(200f, 0f)”，建立当前测试条件。
            later.rectTransform.anchoredPosition = new Vector2(200f, 0f);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 按距离固定 first 先于 later，避免依赖 Unity 实例编号决定遍历顺序。
            source.EndScreenPoint = RectTransformUtility.WorldToScreenPoint(null, first.transform.position);
            // 获取“sourceUI”的实际组件引用，供表现与生命周期断言使用。
            var sourceUI = source.GetComponent<UIBase>();
            // 获取“destinationUI”的实际组件引用，供表现与生命周期断言使用。
            var destinationUI = destination.GetComponent<UIBase>();
            // 提交“sourceUI”的显示请求，触发实际生命周期。
            sourceUI.Show();
            // 提交“destinationUI”的显示请求，触发实际生命周期。
            destinationUI.Show();
            // 验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。
            AssertVisible(first);
            // 验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。
            AssertVisible(later);

            // 初始化“moved”为false，作为后续回调的观测基准。
            var moved = false;
            // 保存“progressBeforeMove”的基准值-1f，供后续比较或恢复。
            var progressBeforeMove = -1f;
            // 保存“progressAfterMove”的基准值-1f，供后续比较或恢复。
            var progressAfterMove = -1f;
            // 准备在较早成员回调中迁移后续成员，验证旧组不会覆盖新组状态。
            UnityEngine.Events.UnityAction moveLaterMember = () =>
            {
                // 隐藏先在零进度刷新材质；只在最终进度遍历中迁移成员，
                // 确保此时两个排序成员都已进入本轮遍历快照。
                if (moved || firstEffect.EffectAmount < 1f) return;
                // 将“moved”设为true，建立当前测试条件。
                moved = true;
                // 将“progressBeforeMove”设为“laterEffect”的流动进度，建立当前测试条件。
                progressBeforeMove = laterEffect.EffectAmount;
                // 在 Content 下换父不会改变任一控制器的直接子节点。
                later.transform.SetParent(destinationContent, false);
                // 将“progressAfterMove”设为“laterEffect”的流动进度，建立当前测试条件。
                progressAfterMove = laterEffect.EffectAmount;
            };
            // 注册 Graphic 标脏回调，在真实材质或顶点刷新期间构造重入场景。
            first.RegisterDirtyVerticesCallback(moveLaterMember);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 提交“sourceUI”的隐藏请求，检查子树联动与终态。
                sourceUI.Hide();
                // 确认“moved”为真。
                Assert.IsTrue(moved, "The actual Graphic dirty callback must move the later member.");
                // 确认“progressBeforeMove”为零。
                Assert.That(progressBeforeMove, Is.Zero, "The moved member must still await its old-group turn.");
                // 确认“progressAfterMove”为零。
                Assert.That(progressAfterMove, Is.Zero, "Reparenting must first apply the shown destination state.");
                // 确认“later.transform”的直属父节点仍引用“destinationContent”。
                Assert.AreSame(destinationContent, later.transform.parent);
                // 确认“sourceUI”的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, sourceUI.State);
                // 确认“destinationUI”的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, destinationUI.State);
                // 确认“laterEffect”的流动进度为零。
                Assert.That(laterEffect.EffectAmount, Is.Zero,
                    "The old group's remaining traversal must not overwrite the new group's shown state.");
                // 验证重建后的 Graphic 具有有效网格且顶点透明度已恢复可见。
                AssertVisible(later);
                // 验证成员进度与实际重建网格均处于隐藏终态。
                AssertHidden(first, firstEffect);
            }
            finally
            {
                // 解除 Graphic 标脏回调，避免测试监听在清理或后续用例中再次触发。
                first.UnregisterDirtyVerticesCallback(moveLaterMember);
            }
        }

        /// <summary>验证材质刷新回调中迁移成员后保留新组材质。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator MemberReparentedInsideMaterialRefresh_ShouldKeepNewGroupMaterial()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateGroup("MaterialSource");
            // 创建“sourceContent”并配置测试所需的层级或表现组件。
            var sourceContent = CreateRect("SourceContent", source.transform);
            // 创建“destination”并配置测试所需的层级或表现组件。
            var destination = CreateGroup("MaterialDestination");
            // 创建“destinationContent”并配置测试所需的层级或表现组件。
            var destinationContent = CreateRect("DestinationContent", destination.transform);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateImage(sourceContent);
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            first.gameObject.AddComponent<UIFlowMeshEffect>();
            // 创建后续界面（later）并配置测试所需的层级或表现组件。
            var later = CreateImage(sourceContent);
            // 添加并保存“laterEffect”，供后续生命周期与效果检查使用。
            var laterEffect = later.gameObject.AddComponent<UIFlowMeshEffect>();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 创建“sourceMaterial”，隔离本用例的资源或观测数据。
            var sourceMaterial = new Material(Shader.Find("UI/Default"));
            // 创建“destinationMaterial”，隔离本用例的资源或观测数据。
            var destinationMaterial = new Material(Shader.Find("UI/Default"));
            // 将本轮对象或观测值加入待清理材质集合（materials），供统一清理或顺序断言使用。
            materials.Add(sourceMaterial);
            // 将本轮对象或观测值加入待清理材质集合（materials），供统一清理或顺序断言使用。
            materials.Add(destinationMaterial);
            // 取得“templateField”的反射入口，仅在测试内访问非公开配置。
            var templateField = typeof(UIFlowGroupController).GetField(
                "materialPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            // 确认“templateField”存在。
            Assert.NotNull(templateField);
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            templateField.SetValue(source, sourceMaterial);
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            templateField.SetValue(destination, destinationMaterial);
            // 使用 Graphic 默认材质，确保经过真实共享材质安装路径。
            first.material = null;
            // 将后续界面（later）的当前材质设为空引用，建立当前测试条件。
            later.material = null;
            // 获取“sourceUI”的实际组件引用，供表现与生命周期断言使用。
            var sourceUI = source.GetComponent<UIBase>();
            // 获取“destinationUI”的实际组件引用，供表现与生命周期断言使用。
            var destinationUI = destination.GetComponent<UIBase>();
            // 提交“sourceUI”的显示请求，触发实际生命周期。
            sourceUI.Show();
            // 提交“destinationUI”的显示请求，触发实际生命周期。
            destinationUI.Show();
            // 确认首项（first）的当前材质仍引用“sourceMaterial”。
            Assert.AreSame(sourceMaterial, first.material);
            // 确认后续界面（later）的当前材质仍引用“sourceMaterial”。
            Assert.AreSame(sourceMaterial, later.material);

            // 初始化“moved”为false，作为后续回调的观测基准。
            var moved = false;
            // 初始化“templateAfterMove”为空引用，作为后续回调的观测基准。
            Material templateAfterMove = null;
            // 初始化“graphicMaterialAfterMove”为空引用，作为后续回调的观测基准。
            Material graphicMaterialAfterMove = null;
            // 准备在较早成员回调中迁移后续成员，验证旧组不会覆盖新组状态。
            UnityEngine.Events.UnityAction moveLaterMember = () =>
            {
                // 根据“moved”选择对应测试路径。
                if (moved) return;
                // 将“moved”设为true，建立当前测试条件。
                moved = true;
                // 材质刷新按层级顺序进行，因此 first 的标脏回调先发生，
                // 随后旧控制器才会在同轮刷新遍历中处理 later。
                later.transform.SetParent(destinationContent, false);
                // 将“templateAfterMove”设为“laterEffect.materialPrefab”，建立当前测试条件。
                templateAfterMove = laterEffect.materialPrefab;
                // 将“graphicMaterialAfterMove”设为后续界面（later）的当前材质，建立当前测试条件。
                graphicMaterialAfterMove = later.material;
            };
            // 注册 Graphic 标脏回调，在真实材质或顶点刷新期间构造重入场景。
            first.RegisterDirtyVerticesCallback(moveLaterMember);
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 提交“sourceUI”的隐藏请求，检查子树联动与终态。
                sourceUI.Hide();
                // 确认“moved”为真。
                Assert.IsTrue(moved, "The material refresh must invoke the real Graphic dirty callback.");
                // 确认“later.transform”的直属父节点仍引用“destinationContent”。
                Assert.AreSame(destinationContent, later.transform.parent);
                // 确认“destinationUI”的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, destinationUI.State);
                // 确认“templateAfterMove”仍引用“destinationMaterial”。
                Assert.AreSame(destinationMaterial, templateAfterMove);
                // 确认“graphicMaterialAfterMove”仍引用“destinationMaterial”。
                Assert.AreSame(destinationMaterial, graphicMaterialAfterMove);
                // 确认“laterEffect.materialPrefab”仍引用“destinationMaterial”。
                Assert.AreSame(destinationMaterial, laterEffect.materialPrefab,
                    "The old material traversal must not replace the new group's template.");
                // 确认后续界面（later）的当前材质仍引用“destinationMaterial”。
                Assert.AreSame(destinationMaterial, later.material,
                    "The old material traversal must not reinstall its material on a moved Graphic.");
            }
            finally
            {
                // 解除 Graphic 标脏回调，避免测试监听在清理或后续用例中再次触发。
                first.UnregisterDirtyVerticesCallback(moveLaterMember);
            }
        }

        /// <summary>创建测试流动组及必要的层级节点，供材质、路径与生命周期测试使用。</summary>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <returns>创建的流动组控制器。</returns>
        private UIFlowGroupController CreateGroup(string name)
        {
            // 使用独立普通父节点，使这些面板不属于彼此的互斥范围。
            var parent = CreateRect(name + "Container", canvasRoot.transform);
            // 返回“CreateRect(name, parent).gameObject.AddComponent<UIFlowGroupController>()”，交给调用方继续配置或断言。
            return CreateRect(name, parent).gameObject.AddComponent<UIFlowGroupController>();
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

        /// <summary>创建可重建网格的测试 Image，供流动效果的顶点与可见性断言使用。</summary>
        /// <param name="parent">新对象的父节点或输出的根界面。</param>
        /// <returns>用于真实网格重建的测试 Image。</returns>
        private Image CreateImage(Transform parent)
        {
            // 创建矩形变换（rect）并配置测试所需的层级或表现组件。
            var rect = CreateRect("Member", parent);
            // 将“rect.sizeDelta”设为“new Vector2(100, 60)”，建立当前测试条件。
            rect.sizeDelta = new Vector2(100, 60);
            // 添加并保存成员图像（image），供后续生命周期与效果检查使用。
            var image = rect.gameObject.AddComponent<Image>();
            // 为测试 Graphic 设置独立材质，避免依赖可选流动 Shader 资源。
            UseCustomMaterial(image);
            // 返回成员图像（image），交给调用方继续配置或断言。
            return image;
        }

        /// <summary>为测试 Graphic 设置独立材质，避免依赖可选流动 Shader 资源。</summary>
        /// <param name="image">用于读取真实 Canvas 网格的 Image。</param>
        private void UseCustomMaterial(Image image)
        {
            // 创建测试材质（material），隔离本用例的资源或观测数据。
            var material = new Material(Shader.Find("UI/Default"));
            // 将本轮对象或观测值加入待清理材质集合（materials），供统一清理或顺序断言使用。
            materials.Add(material);
            // 将成员图像（image）的当前材质设为测试材质（material），建立当前测试条件。
            image.material = material;
        }

        /// <summary>通过所属 UIBase 的正常生命周期将流动组推进到隐藏终态。</summary>
        /// <param name="group">需要配置或验证的流动组控制器。</param>
        private static void Hide(UIFlowGroupController group)
        {
            // 获取界面（ui）的实际组件引用，供表现与生命周期断言使用。
            var ui = group.GetComponent<UIBase>();
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>验证成员进度与实际重建网格均处于隐藏终态。</summary>
        /// <param name="image">用于读取真实 Canvas 网格的 Image。</param>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        private static void AssertHidden(Image image, UIFlowMeshEffect effect)
        {
            // 确认动效组件（effect）的流动进度符合1f。
            Assert.That(effect.EffectAmount, Is.EqualTo(1f));
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            image.Rebuild(CanvasUpdate.PreRender);
            // 保存实际网格（mesh）的基准值“image.canvasRenderer.GetMesh()”，供后续比较或恢复。
            var mesh = image.canvasRenderer.GetMesh();
            // 确认实际网格（mesh）存在。
            Assert.NotNull(mesh);
            // 确认“mesh.vertexCount”满足约束“Is.GreaterThan(0)”。
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            // 逐项检查或处理“var color in mesh.colors32”，覆盖集合中的每个对象。
            foreach (var color in mesh.colors32) Assert.That(color.a, Is.Zero);
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
