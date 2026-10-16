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
    /// <summary>
    ///  验证材质安装回调中的迁移、销毁和用户覆盖，以及最终材质的清理归属。
    /// </summary>
    public class UIThirdAuditMaterialTests
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
        ///  为当前测试创建独立的屏幕覆盖画布。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            // 为每个用例创建独立画布根节点，隔离场景层级。
            canvasRoot = new GameObject("ThirdAuditMaterialCanvas", typeof(RectTransform), typeof(Canvas));
            // 使用 Overlay 模式，使屏幕坐标测试不依赖场景相机。
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// <summary>
        ///  移除测试资源并等待延迟销毁完成，避免影响后续用例。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 销毁画布及其全部测试子节点。
            if (canvasRoot) Object.Destroy(canvasRoot);
            // 等待节点的延迟销毁完成，再释放它们引用的材质。
            yield return null;
            // 逐个释放本用例创建且尚未销毁的运行时材质。
            foreach (var material in materials) if (material) Object.Destroy(material);
            // 清空资源记录，避免下个用例重复清理。
            materials.Clear();
            // 等待材质的延迟销毁完成。
            yield return null;
        }

        /// <summary>
        ///  验证材质安装回调连续迁移三组后，最终组材质及其清理归属不被旧调用覆盖。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator Install_ThreeGroupNestedMigration_ShouldKeepLastMaterialAndCleanupOwnership()
        {
            // 创建最先安装共享材质的来源组。
            var first = CreateGroup("First", out var firstContent);
            // 创建第一次迁移经过的中间组。
            var middle = CreateGroup("Middle", out var middleContent);
            // 创建第二次迁移后最终拥有成员的目标组。
            var last = CreateGroup("Last", out var lastContent);
            // 把测试图形与动效组件放入来源组。
            var image = CreateMember(firstContent, out var effect);
            // 等待各组件完成 Start，排除初始化补同步对回调顺序的影响。
            yield return null;
            // 取得来源组的真实显隐入口。
            var firstUI = first.GetComponent<UIBase>();
            // 取得中间组的面板，用于预先建立显示状态。
            var middleUI = middle.GetComponent<UIBase>();
            // 取得最终目标组的面板，供终态检查。
            var lastUI = last.GetComponent<UIBase>();
            // 让来源组从完整显示状态开始隐藏测试。
            firstUI.Show();
            // 中间组保持显示，接管成员时应下发可见进度。
            middleUI.Show();
            // 最终组保持显示，用于识别旧组隐藏进度是否越权覆盖。
            lastUI.Show();
            // 创建来源组专用的可区分材质实例。
            var firstMaterial = CreateMaterial("FirstInstall");
            // 创建中间组材质，作为第二次迁移的触发标记。
            var middleMaterial = CreateMaterial("MiddleInstall");
            // 创建最终组材质，用引用检查最后一次接管结果。
            var lastMaterial = CreateMaterial("LastInstall");
            // 配置来源模板，等待真实显隐刷新触发安装。
            SetTemplate(first, firstMaterial);
            // 配置中间组接管成员时要安装的模板。
            SetTemplate(middle, middleMaterial);
            // 配置最终组接管成员时要安装的模板。
            SetTemplate(last, lastMaterial);
            // 清空显式材质槽，让 Graphic 返回默认材质并允许共享模板安装。
            image.material = null;

            // 记录已完成的迁移阶段，同时防止清理材质回调重复换父。
            var moves = 0;
            // 在同一材质安装调用栈内安排来源、中间和最终三组的连续接管。
            UnityAction migrate = () =>
            {
                // 只在目标材质安装时迁移，忽略清理旧共享材质引起的嵌套回调。
                if (moves == 0 && image.material == firstMaterial)
                {
                    // 换父前先推进阶段，防止嵌套清理回调重复执行第一次迁移。
                    moves = 1;
                    // 来源材质刚安装时立即迁入中间组。
                    image.transform.SetParent(middleContent, false);
                }
                // 只有中间组材质已经实际写入时才执行第二次迁移。
                else if (moves == 1 && image.material == middleMaterial)
                {
                    // 先记录最终阶段，阻止后续材质回调继续迁移。
                    moves = 2;
                    // 在中间组安装尚未返回时再迁入最终组。
                    image.transform.SetParent(lastContent, false);
                }
            };
            // 订阅 Graphic 材质 setter 真实触发的同步标脏回调。
            image.RegisterDirtyMaterialCallback(migrate);
            // 保存显隐操作是否抛错，随后与迁移结果一起断言。
            Exception failure;
            // 隐藏来源面板，借准备阶段的材质安装触发两层嵌套迁移。
            try { failure = Capture(() => firstUI.Hide()); }
            // 无论操作结果如何都解除迁移回调，避免影响后续销毁检查。
            finally { image.UnregisterDirtyMaterialCallback(migrate); }

            // 记录旧调用全部返回后的归属、进度和材质状态。
            Trace("three nested groups returned", firstUI, image, effect);
            // 连续重入迁移不能导致来源面板操作异常。
            Assert.IsNull(failure, failure?.ToString());
            // 确认两次换父都确实发生在预定材质安装回调内。
            Assert.That(moves, Is.EqualTo(2), "Both migrations must happen inside real material setter callbacks.");
            // 确认实际 Transform 已停留在最终目标组。
            Assert.AreSame(lastContent, image.transform.parent);
            // 成员迁出不应阻止来源面板完成隐藏。
            Assert.AreEqual(UIState.Hided, firstUI.State);
            // 最终目标面板仍保持原来的显示状态。
            Assert.AreEqual(UIState.Showed, lastUI.State);
            // 最终组的可见进度不能被来源组隐藏流程覆盖。
            Assert.That(effect.EffectAmount, Is.Zero);
            // 组件记录的模板应属于最后接管的组。
            Assert.AreSame(lastMaterial, effect.materialPrefab);
            // Graphic 实际材质也应为最终组模板，而非任一旧组模板。
            Assert.AreSame(lastMaterial, image.material);
            // 重建网格，确认迁移成员实际恢复完整可见形状。
            AssertVisible(image);
            // 移除效果组件，进一步检查最终材质的清理所有权。
            Object.DestroyImmediate(effect);
            // 两层旧调用返回后仍应记得安装责任，销毁时恢复默认材质。
            Assert.AreSame(image.defaultMaterial, image.material,
                "The final shared material must retain cleanup ownership after both older calls return.");
        }

        /// <summary>
        ///  验证安装回调中的用户材质覆盖经过迁移、模板替换和组件移除后仍被保留。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator Install_UserCustomMaterialThenMigrate_ShouldPreserveOverrideThroughReplacementAndRemoval()
        {
            // 创建将首次安装共享材质的来源组。
            var source = CreateGroup("CustomSource", out var sourceContent);
            // 创建用户覆盖之后接管成员的目标组。
            var destination = CreateGroup("CustomDestination", out var destinationContent);
            // 在来源组创建带初始自定义材质的测试图形。
            var image = CreateMember(sourceContent, out var effect);
            // 等待常规初始化完成，再设置待观测的材质安装条件。
            yield return null;
            // 取得来源面板的显隐入口。
            var sourceUI = source.GetComponent<UIBase>();
            // 取得目标面板，供接管与后续模板替换使用。
            var destinationUI = destination.GetComponent<UIBase>();
            // 先让来源面板显示，保证后续隐藏会执行刷新。
            sourceUI.Show();
            // 目标组保持显示，让迁入成员立即获得可见进度。
            destinationUI.Show();
            // 创建触发覆盖回调的来源共享材质。
            var sourceMaterial = CreateMaterial("CustomSourceInstall");
            // 创建目标组模板，用于区分组配置与用户实际材质。
            var destinationMaterial = CreateMaterial("CustomDestinationInstall");
            // 创建用户在回调中主动指定、应始终被保留的材质。
            var userMaterial = CreateMaterial("UserOverrideInsideSetter");
            // 设置来源模板，但由接下来的隐藏刷新实际安装。
            SetTemplate(source, sourceMaterial);
            // 设置目标组在迁入时接管的模板配置。
            SetTemplate(destination, destinationMaterial);
            // 恢复默认材质条件，使下一轮来源模板安装能够触发回调。
            image.material = null;

            // 记录用户覆盖是否执行，并阻止 setter 的嵌套回调重复操作。
            var changed = false;
            // 安排在来源材质安装时同步写入用户材质，然后迁移成员。
            UnityAction overrideAndMigrate = () =>
            {
                // 仅首次观察到来源模板真正安装时执行覆盖和迁移。
                if (changed || image.material != sourceMaterial) return;
                // 先锁定已执行标记，再修改材质以避免重入。
                changed = true;
                // 模拟用户主动更换 Graphic 材质，建立应优先保留的覆盖。
                image.material = userMaterial;
                // 带着用户材质迁入目标组，测试新组是否误判安装所有权。
                image.transform.SetParent(destinationContent, false);
            };
            // 绑定真实材质标脏回调以覆盖 setter 重入窗口。
            image.RegisterDirtyMaterialCallback(overrideAndMigrate);
            // 保留来源隐藏过程的异常供结束后检查。
            Exception failure;
            // 从真实隐藏入口触发共享安装、用户覆盖和目标组接管。
            try { failure = Capture(() => sourceUI.Hide()); }
            // 移除回调，后续模板替换仅验证生产逻辑自身的保护。
            finally { image.UnregisterDirtyMaterialCallback(overrideAndMigrate); }

            // 输出迁移后的组配置与 Graphic 实际材质，区分两者归属。
            Trace("custom override after migration", destinationUI, image, effect);
            // 用户同步覆盖与迁移不能使来源隐藏抛错。
            Assert.IsNull(failure, failure?.ToString());
            // 确认测试确实执行了用户覆盖，而非绕过该分支。
            Assert.IsTrue(changed);
            // 确认图形已经迁入目标组层级。
            Assert.AreSame(destinationContent, image.transform.parent);
            // 组件模板配置应更新为目标组模板。
            Assert.AreSame(destinationMaterial, effect.materialPrefab);
            // Graphic 实际材质必须继续引用用户覆盖。
            Assert.AreSame(userMaterial, image.material);
            // 显示中的目标组应将迁入成员恢复为可见进度。
            Assert.That(effect.EffectAmount, Is.Zero);
            // 再创建一个目标组模板，检验后续替换仍保护用户材质。
            var nextTemplate = CreateMaterial("NextDestinationTemplate");
            // 更新目标组模板配置。
            SetTemplate(destination, nextTemplate);
            // 通过目标组隐藏刷新实际触发模板替换逻辑。
            destinationUI.Hide();
            // 模板保护不应阻碍目标组正常驱动隐藏进度。
            Assert.That(effect.EffectAmount, Is.EqualTo(1f));
            // 组件应记录目标组最新模板。
            Assert.AreSame(nextTemplate, effect.materialPrefab);
            // 模板变更不能替换用户选择的实际材质。
            Assert.AreSame(userMaterial, image.material);
            // 先恢复显示，再检查组件移除时的材质处理。
            destinationUI.Show();
            // 触发效果组件真实 OnDestroy 清理。
            Object.DestroyImmediate(effect);
            // 移除效果时也不能清除不属于效果组件的用户材质。
            Assert.AreSame(userMaterial, image.material,
                "Later template replacement and effect removal must preserve a synchronous user override.");
        }

        /// <summary>
        ///  验证安装回调销毁整个图形节点后，面板完成隐藏且其他成员仍收到最终进度。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator Install_DestroyWholeGraphicObject_ShouldFinishPanelAndUpdateSurvivingMember()
        {
            // 创建在成员销毁期间仍需完成显隐的存活组。
            var group = CreateGroup("DestroyGraphic", out var content);
            // 创建将在自身材质回调中销毁的成员。
            var image = CreateMember(content, out var effect);
            // 创建同组幸存成员，用于检验下发是否继续。
            var survivingImage = CreateMember(content, out var survivingEffect);
            // 将幸存图形移开，使两个成员具有独立视觉位置。
            survivingImage.rectTransform.anchoredPosition = new Vector2(220f, 0f);
            // 等待成员与组初始化完成。
            yield return null;
            // 取得组对应的面板，以真实隐藏入口触发回调。
            var ui = group.GetComponent<UIBase>();
            // 建立完整显示的测试起点。
            ui.Show();
            // 创建本轮将安装到默认材质图形上的共享模板。
            var shared = CreateMaterial("DestroyGraphicInstall");
            // 把共享模板配置给存活组。
            SetTemplate(group, shared);
            // 清除待销毁图形的初始自定义材质，允许本轮共享安装。
            image.material = null;
            // 幸存图形也使用默认材质，检验其后续安装是否完成。
            survivingImage.material = null;

            // 记录整节点销毁是否已执行，防止销毁清理触发重入。
            var destroyed = false;
            // 在共享材质实际写入时同步销毁整个图形节点。
            UnityAction destroyGraphic = () =>
            {
                // 只在首次共享安装时销毁，忽略其他材质标脏通知。
                if (destroyed || image.material != shared) return;
                // 先设置防重入标记，再触发整个节点的销毁生命周期。
                destroyed = true;
                // 同时移除 Image、动效组件和 Transform，覆盖原生引用失效路径。
                Object.DestroyImmediate(image.gameObject);
            };
            // 将销毁操作挂到真实 Graphic 材质回调。
            image.RegisterDirtyMaterialCallback(destroyGraphic);
            // 记录组隐藏期间是否访问了已销毁的原生对象。
            Exception failure;
            // 执行隐藏，捕获安装回调销毁节点后的异常。
            try { failure = Capture(() => ui.Hide()); }
            // 仅 Graphic 仍存活时解绑，避免清理测试回调时访问已销毁对象。
            finally { if (image) image.UnregisterDirtyMaterialCallback(destroyGraphic); }

            // 记录幸存成员在另一节点销毁后的进度与材质状态。
            Trace("after whole Graphic object removal", ui, survivingImage, survivingEffect);
            // 确认整节点销毁分支确实触发。
            Assert.IsTrue(destroyed);
            // Image 的 Unity 原生对象必须已经被销毁。
            Assert.IsFalse(image);
            // 同节点效果组件也必须随节点一起销毁。
            Assert.IsFalse(effect);
            // 单个图形节点移除不能使存活面板的隐藏过程失败。
            Assert.IsNull(failure, "The surviving panel must finish after its current Graphic is removed: " + failure);
            // 确认面板完成隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
            // 后续幸存成员仍需收到完整隐藏进度。
            Assert.That(survivingEffect.EffectAmount, Is.EqualTo(1f));
            // 幸存成员的共享材质安装也应继续完成。
            Assert.AreSame(shared, survivingImage.material);
            // 再次显示存活面板，检查销毁后的缓存仍可正常更新。
            ui.Show();
            // 幸存成员应恢复可见进度。
            Assert.That(survivingEffect.EffectAmount, Is.Zero);
            // 验证幸存成员实际网格完整且不透明。
            AssertVisible(survivingImage);
        }

        /// <summary>
        ///  验证清理旧材质时销毁动效组件后，暂停的替换调用不再安装新材质。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator Replacement_DestroyEffectWhileOldMaterialClears_ShouldNotInstallReplacement()
        {
            // 创建会在旧模板清理期间丢失效果组件的流动组。
            var group = CreateGroup("DestroyDuringClear", out var content);
            // 创建用于观测材质清理窗口的图形成员。
            var image = CreateMember(content, out var effect);
            // 等待正常组件初始化结束。
            yield return null;
            // 创建先安装到图形上的旧共享材质。
            var oldMaterial = CreateMaterial("OldBeforeClear");
            // 创建预备替换旧材质的新模板。
            var replacementMaterial = CreateMaterial("ReplacementAfterClear");
            // 将旧材质设为组模板。
            SetTemplate(group, oldMaterial);
            // 恢复默认材质条件，让下一次显示能够安装旧模板。
            image.material = null;
            // 取得面板的真实显隐入口。
            var ui = group.GetComponent<UIBase>();
            // 显示时完成旧共享材质的首次安装。
            ui.Show();
            // 确认旧材质已经实际安装，避免误测首次安装路径。
            Assert.AreSame(oldMaterial, image.material);
            // 只替换配置，保留下一次隐藏中的旧材质清理窗口。
            SetTemplate(group, replacementMaterial);

            // 记录效果销毁是否已执行，阻止嵌套回调重复销毁。
            var destroyed = false;
            // 记录回调发生瞬间的实际材质，用于证明触发阶段。
            Material materialAtCallback = null;
            // 在替换过程中第一次材质标脏时移除效果组件。
            UnityAction destroyWhileClearing = () =>
            {
                // 销毁已发生时忽略后续同步回调。
                if (destroyed) return;
                // 先锁定一次性标记，防止销毁清理重入当前回调。
                destroyed = true;
                // 读取清理瞬间材质，结束后应能验证其为默认材质。
                materialAtCallback = image.material;
                // 在旧模板已清除、新模板尚未安装时销毁效果组件。
                Object.DestroyImmediate(effect);
            };
            // 绑定真实材质回调以观测旧模板清理阶段。
            image.RegisterDirtyMaterialCallback(destroyWhileClearing);
            // 保存隐藏过程是否发生销毁后访问异常。
            Exception failure;
            // 隐藏时触发模板替换，并捕获中途销毁后的结果。
            try { failure = Capture(() => ui.Hide()); }
            // 移除测试回调，避免后续网格重建重复触发测试逻辑。
            finally { image.UnregisterDirtyMaterialCallback(destroyWhileClearing); }

            // 记录旧替换调用返回后的实际材质与组件存活情况。
            Trace("after old material clear destroyed effect", ui, image, effect);
            // 确认销毁操作实际发生。
            Assert.IsTrue(destroyed);
            // 回调时必须已经清除旧模板，证明测到替换间隙而非安装新模板之后。
            Assert.AreSame(image.defaultMaterial, materialAtCallback,
                "The effect must be removed in the old template's clearing callback, before replacement installation.");
            // 效果组件的原生对象应已移除。
            Assert.IsFalse(effect);
            // 失效的旧替换流程应安全退出而不抛错。
            Assert.IsNull(failure, failure?.ToString());
            // 效果组件被移除后面板仍应完成隐藏。
            Assert.AreEqual(UIState.Hided, ui.State);
            // 旧调用不得以已销毁组件的名义继续安装替换材质。
            Assert.AreSame(image.defaultMaterial, image.material,
                "The suspended replacement must not install a material on behalf of a destroyed component.");
            // 没有效果组件的 Image 应恢复自身完整可见网格。
            AssertVisible(image);
        }

        /// <summary>
        ///  验证安装回调在同一节点替换动效组件后，新组件取得最终进度且保留自定义材质。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator Install_DestroyThenAddEffectOnSameNode_ShouldDriveReplacementAndPreserveItsCustomMaterial()
        {
            // 创建需要在重入中替换效果组件的组。
            var group = CreateGroup("ReplaceComponent", out var content);
            // 创建原始效果组件及其所在图形节点。
            var image = CreateMember(content, out var originalEffect);
            // 完成原组件正常初始化，排除 Start 顺序干扰。
            yield return null;
            // 取得后续触发隐藏和显示的面板入口。
            var ui = group.GetComponent<UIBase>();
            // 让组处于显示状态，再开始本轮材质安装。
            ui.Show();
            // 创建原组件将安装的共享模板。
            var shared = CreateMaterial("RemovedComponentShared");
            // 创建替代组件出现前由用户设置的自定义材质。
            var replacementCustom = CreateMaterial("ReplacementComponentCustom");
            // 配置触发组件替换回调的共享模板。
            SetTemplate(group, shared);
            // 恢复默认材质，让下一次隐藏刷新实际安装共享模板。
            image.material = null;

            // 记录同节点组件替换是否已执行，防止清理回调重入。
            var replaced = false;
            // 保存随后动态添加的新效果组件。
            UIFlowMeshEffect replacementEffect = null;
            // 保存旧组件移除后、用户覆盖前的材质状态。
            Material afterRemoval = null;
            // 在原组件材质安装调用尚未返回时移除它并创建替代组件。
            UnityAction replaceEffect = () =>
            {
                // 仅首次安装指定共享材质时执行替换。
                if (replaced || image.material != shared) return;
                // 先设置防重入标记，销毁清理的材质回调不再重复替换。
                replaced = true;
                // 立即销毁原效果，触发其已安装材质的清理。
                Object.DestroyImmediate(originalEffect);
                // 记录旧效果清理是否恢复了默认材质。
                afterRemoval = image.material;
                // 模拟对象池图标：新组件创建之前，图形已经具有用户指定的材质。
                image.material = replacementCustom;
                // 在同一存活图形节点添加新效果，触发其注册与当前组同步。
                replacementEffect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            };
            // 把组件替换挂到真实材质 setter 的同步回调。
            image.RegisterDirtyMaterialCallback(replaceEffect);
            // 保存重入替换过程是否产生异常。
            Exception failure;
            // 隐藏组以触发安装回调，并让新组件参与待完成的组更新。
            try { failure = Capture(() => ui.Hide()); }
            // 移除替换回调，后续检查只观察生产逻辑。
            finally { image.UnregisterDirtyMaterialCallback(replaceEffect); }

            // 记录旧安装调用返回后，新组件的归属、进度与实际材质。
            Trace("after same-node effect replacement", ui, image, replacementEffect);
            // 确认同节点替换路径确实执行。
            Assert.IsTrue(replaced);
            // 旧组件失效不能中断存活面板的显隐流程。
            Assert.IsNull(failure, failure?.ToString());
            // 原效果组件应已销毁。
            Assert.IsFalse(originalEffect);
            // 同节点的新效果组件应仍然存活。
            Assert.IsTrue(replacementEffect);
            // 原效果销毁应正确清理其刚安装的共享材质。
            Assert.AreSame(image.defaultMaterial, afterRemoval);
            // 节点上只能剩下一个效果组件，避免误由两个组件共同完成断言。
            Assert.AreEqual(1, image.GetComponents<UIFlowMeshEffect>().Length);
            // 组隐藏操作应完整结束。
            Assert.AreEqual(UIState.Hided, ui.State);
            // 新注册组件必须收到本轮隐藏终值，不能被旧缓存遗漏。
            Assert.That(replacementEffect.EffectAmount, Is.EqualTo(1f),
                "A newly registered component must receive the pending terminal state, despite the dead cache entry.");
            // 新组件应继承当前组模板配置。
            Assert.AreSame(shared, replacementEffect.materialPrefab);
            // 新组件不能覆盖创建前已经存在的用户材质。
            Assert.AreSame(replacementCustom, image.material);
            // 再次显示组，验证新组件能持续参与后续更新。
            ui.Show();
            // 新组件应正常恢复显示进度。
            Assert.That(replacementEffect.EffectAmount, Is.Zero);
            // 重建实际网格验证新组件显示表现完整。
            AssertVisible(image);
            // 移除替代组件，再次检查自定义材质保护。
            Object.DestroyImmediate(replacementEffect);
            // 替代组件销毁也不能清除用户材质。
            Assert.AreSame(replacementCustom, image.material);
        }

        /// <summary>
        ///  验证安装期间移出所有组后，成员保持独立可见且组件移除时仍能清理已安装材质。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator Install_MoveOutsideAllGroups_ShouldRemainVisibleAndReleaseInstalledMaterialOnRemoval()
        {
            // 创建成员即将离开的来源组。
            var source = CreateGroup("LeaveAllGroups", out var content);
            // 在画布下创建没有任何流动组祖先的独立容器。
            var ungrouped = CreateRect("IndependentContent", canvasRoot.transform);
            // 先在来源组创建图形与效果组件。
            var image = CreateMember(content, out var effect);
            // 等待初始组归属与显隐同步完成。
            yield return null;
            // 取得来源面板，用于触发离组前后的真实显隐。
            var ui = source.GetComponent<UIBase>();
            // 先将来源组显示，确保离组恢复状态可以明确观测。
            ui.Show();
            // 创建成员即将安装、随后仍需负责清理的共享材质。
            var shared = CreateMaterial("LeavingGroupInstall");
            // 配置来源模板，等待隐藏准备阶段实际安装。
            SetTemplate(source, shared);
            // 清空显式材质槽以允许本轮共享安装。
            image.material = null;

            // 记录是否已离组，防止层级变化产生的嵌套回调重复换父。
            var moved = false;
            // 安排在共享材质写入的同一调用栈内离开所有组。
            UnityAction leaveGroup = () =>
            {
                // 只在指定共享材质首次安装时执行离组。
                if (moved || image.material != shared) return;
                // 先设置一次性标记，再触发层级同步。
                moved = true;
                // 迁入独立容器，使最近组控制器查询返回空。
                image.transform.SetParent(ungrouped, false);
            };
            // 绑定实际材质标脏回调。
            image.RegisterDirtyMaterialCallback(leaveGroup);
            // 记录离组导致的重入是否产生异常。
            Exception failure;
            // 隐藏来源面板，以材质安装触发成员离组。
            try { failure = Capture(() => ui.Hide()); }
            // 结束后解绑测试回调，避免后续销毁重复迁移。
            finally { image.UnregisterDirtyMaterialCallback(leaveGroup); }

            // 输出离组后的旧面板状态、成员进度及材质清理记录。
            Trace("after moving outside all groups", ui, image, effect);
            // 离组重入应安全完成而不抛错。
            Assert.IsNull(failure, failure?.ToString());
            // 确认离组分支确实执行。
            Assert.IsTrue(moved);
            // 确认实际父节点已经变为独立容器。
            Assert.AreSame(ungrouped, image.transform.parent);
            // 包括失活祖先在内都不应再找到流动组控制器。
            Assert.IsNull(image.GetComponentInParent<UIFlowGroupController>(true));
            // 脱离组时恢复独立可见网格，但此步骤没有替换或重置材质的契约。
            // 此处仅检查几何恢复，后续再检查组件移除时的材质清理责任。
            Assert.That(effect.EffectAmount, Is.Zero);
            // 验证独立成员的实际网格恢复完整可见。
            AssertVisible(image);
            // 再次显示旧面板，检查迟到刷新不再接管离组成员。
            ui.Show();
            // 再次隐藏旧面板，检验缓存是否仍错误驱动成员。
            ui.Hide();
            // 离组成员必须保持独立可见进度。
            Assert.That(effect.EffectAmount, Is.Zero, "The former group must no longer drive the detached member.");
            // 移除独立效果组件，验证材质清理责任仍然完整。
            Object.DestroyImmediate(effect);
            // 离组不能丢失已安装材质的所有权，组件移除时仍需恢复默认材质。
            Assert.AreSame(image.defaultMaterial, image.material,
                "Leaving a group during installation must not lose the effect's material cleanup record.");
        }

        /// <summary>
        ///  验证迁入停用组后暂不接管成员，重新启用时再同步目标组状态与材质。
        /// </summary>
        /// <returns>交给 Unity 测试运行器执行的协程。</returns>
        [UnityTest]
        public IEnumerator Install_MoveToDisabledGroup_ShouldStayDetachedUntilDestinationReenabled()
        {
            // 创建仍会执行隐藏与材质安装的来源组。
            var source = CreateGroup("DisabledDestinationSource", out var sourceContent);
            // 创建将处于停用状态的目标组。
            var destination = CreateGroup("DisabledDestination", out var destinationContent);
            // 把图形与效果组件先放入来源组。
            var image = CreateMember(sourceContent, out var effect);
            // 等待各组件常规初始化完成。
            yield return null;
            // 取得来源面板的真实显隐入口。
            var sourceUI = source.GetComponent<UIBase>();
            // 取得目标面板以预先建立隐藏状态。
            var destinationUI = destination.GetComponent<UIBase>();
            // 来源组从显示状态开始本轮测试。
            sourceUI.Show();
            // 先显示目标面板，使接下来的隐藏确实执行。
            destinationUI.Show();
            // 将目标组保存为隐藏终态，重新启用时应恢复该进度。
            destinationUI.Hide();
            // 只停用组控制器，使其暂时拒绝新成员注册。
            destination.enabled = false;
            // 创建迁移触发阶段使用的来源模板。
            var sourceMaterial = CreateMaterial("BeforeDisabledDestination");
            // 创建目标组重新启用后应接管的模板。
            var destinationMaterial = CreateMaterial("AfterDestinationReenabled");
            // 配置来源组材质安装条件。
            SetTemplate(source, sourceMaterial);
            // 预先设置停用目标组的材质配置。
            SetTemplate(destination, destinationMaterial);
            // 恢复默认材质，确保来源共享安装触发真实回调。
            image.material = null;

            // 记录迁移是否已发生，并防止后续回调重复换父。
            var moved = false;
            // 安排在来源模板安装时迁入停用的目标组。
            UnityAction moveToDisabled = () =>
            {
                // 只在首次来源材质安装时执行迁移。
                if (moved || image.material != sourceMaterial) return;
                // 换父前先设置防重入标记。
                moved = true;
                // 迁入目标层级，由组件释放旧组但暂不被停用组接管。
                image.transform.SetParent(destinationContent, false);
            };
            // 注册实际材质标脏回调。
            image.RegisterDirtyMaterialCallback(moveToDisabled);
            // 记录迁入停用组过程中是否发生异常。
            Exception failure;
            // 通过来源隐藏准备阶段触发材质安装和迁移。
            try { failure = Capture(() => sourceUI.Hide()); }
            // 解绑迁移回调，重新启用目标组时不再执行测试换父逻辑。
            finally { image.UnregisterDirtyMaterialCallback(moveToDisabled); }

            // 记录目标组仍停用时成员的进度与材质状态。
            Trace("inside disabled destination", destinationUI, image, effect);
            // 迁移至停用组也应安全完成。
            Assert.IsNull(failure, failure?.ToString());
            // 确认测试实际执行了迁移。
            Assert.IsTrue(moved);
            // 成员层级应已位于目标组内容节点下。
            Assert.AreSame(destinationContent, image.transform.parent);
            // 此时目标控制器必须仍然停用，避免误测普通接管。
            Assert.IsFalse(destination.isActiveAndEnabled);
            // 目标面板保留隐藏状态，为启用后的状态同步提供依据。
            Assert.AreEqual(UIState.Hided, destinationUI.State);
            // 停用控制器不接受成员注册，重新启用时才在 OnEnable 中接管。
            Assert.That(effect.EffectAmount, Is.Zero);
            // 目标组尚未接管时，离开旧组的成员应保持可见网格。
            AssertVisible(image);
            // 启用目标控制器，触发 OnEnable 中的成员扫描和接管。
            destination.enabled = true;
            // 记录重新启用后的状态与材质，便于区分前后两阶段。
            Trace("destination reenabled", destinationUI, image, effect);
            // 目标组接管后应下发此前保存的隐藏终值。
            Assert.That(effect.EffectAmount, Is.EqualTo(1f));
            // 效果组件模板应更新为目标组模板。
            Assert.AreSame(destinationMaterial, effect.materialPrefab);
            // Graphic 实际共享材质也应切换至目标组模板。
            Assert.AreSame(destinationMaterial, image.material);
            // 再次显示目标面板，验证接管后可以继续驱动成员。
            destinationUI.Show();
            // 成员应正常恢复可见进度。
            Assert.That(effect.EffectAmount, Is.Zero);
            // 移除效果组件，检验接管后的共享材质清理。
            Object.DestroyImmediate(effect);
            // 最终目标组材质应在组件销毁时恢复为 Graphic 默认材质。
            Assert.AreSame(image.defaultMaterial, image.material);
        }

        /// <summary>
        ///  在独立普通父节点下创建组控制器和内容节点，避免兄弟面板互斥干扰测试。
        /// </summary>
        /// <param name="name">创建对象的名称。</param>
        /// <param name="content">创建的组内容节点，用于挂载成员。</param>
        /// <returns>新建的组控制器。</returns>
        private UIFlowGroupController CreateGroup(string name, out RectTransform content)
        {
            // 使用各自独立的普通父节点，避免兄弟面板互斥干扰测试。
            var container = CreateRect(name + "Container", canvasRoot.transform);
            // 在独立父节点下添加流动组控制器及其必需组件。
            var group = CreateRect(name, container).gameObject.AddComponent<UIFlowGroupController>();
            // 创建一层普通内容节点，使成员采用实际深层注册路径。
            content = CreateRect(name + "Content", group.transform);
            // 返回控制器，调用方通过其 UIBase 执行显隐。
            return group;
        }

        /// <summary>
        ///  创建带自定义初始材质的图形和网格动效成员。
        /// </summary>
        /// <param name="parent">子节点所属的父节点。</param>
        /// <param name="effect">返回添加在新图形节点上的网格动效组件。</param>
        /// <returns>新建的图形成员。</returns>
        private Image CreateMember(Transform parent, out UIFlowMeshEffect effect)
        {
            // 在指定父节点下创建图形成员的矩形对象。
            var rect = CreateRect("Member", parent);
            // 设置明确的非退化尺寸，便于检查四顶点可见网格。
            rect.sizeDelta = new Vector2(100f, 60f);
            // 添加实际 Image，使用其正式材质与网格回调。
            var image = rect.gameObject.AddComponent<Image>();
            // 初始化时先给用户材质，避免可选共享材质查找或安装干扰测试时序。
            image.material = CreateMaterial("SetupCustom");
            // 添加效果组件，让 Awake 和 OnEnable 按正常生命周期完成注册。
            effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            // 返回图形供测试设置材质并订阅标脏回调。
            return image;
        }

        /// <summary>
        ///  创建使用默认 UI 着色器的测试材质，并登记到统一清理列表。
        /// </summary>
        /// <param name="name">创建对象的名称。</param>
        /// <returns>已登记清理责任的运行时材质。</returns>
        private Material CreateMaterial(string name)
        {
            // 使用内置 UI 着色器创建只比较引用归属的测试材质。
            var shader = Shader.Find("UI/Default");
            // 确认内置着色器可用，避免材质创建失败掩盖测试目标。
            Assert.NotNull(shader);
            // 创建名称可区分的材质实例，方便失败日志显示实际归属。
            var material = new Material(shader) { name = name + "_" + materials.Count };
            // 登记材质资源，由用例结束时统一释放。
            materials.Add(material);
            // 返回已登记清理责任的材质实例。
            return material;
        }

        /// <summary>
        ///  在指定父节点下创建保持本地变换的矩形节点。
        /// </summary>
        /// <param name="name">创建对象的名称。</param>
        /// <param name="parent">子节点所属的父节点。</param>
        /// <returns>新建节点的矩形变换。</returns>
        private static RectTransform CreateRect(string name, Transform parent)
        {
            // 创建带 RectTransform 的 UI 节点。
            var go = new GameObject(name, typeof(RectTransform));
            // 挂入指定父级且保持本地变换，构建可预测的 UI 层级。
            go.transform.SetParent(parent, false);
            // 返回新节点的矩形变换供布局与换父操作使用。
            return (RectTransform)go.transform;
        }

        /// <summary>
        ///  通过反射设置组的共享材质模板，保留正常显隐入口来触发安装。
        /// </summary>
        /// <param name="group">需要更改共享模板配置的组控制器。</param>
        /// <param name="material">后续正常刷新时传递给成员的共享材质模板。</param>
        private static void SetTemplate(UIFlowGroupController group, Material material)
        {
            // 取得组序列化模板字段，只改变配置而不直接调用生产内部安装方法。
            var field = typeof(UIFlowGroupController).GetField("materialPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            // 确认模板字段仍存在，字段改名应明确使测试失败。
            Assert.NotNull(field);
            // 写入共享模板，等待后续真实显隐或启用流程触发刷新。
            field.SetValue(group, material);
        }

        /// <summary>
        ///  执行同步操作并返回捕获的异常，便于同时检查异常和操作后的对象状态。
        /// </summary>
        /// <param name="operation">需要执行并捕获异常的同步操作。</param>
        /// <returns>操作抛出的异常；执行成功时为空。</returns>
        private static Exception Capture(Action operation)
        {
            // 执行同步操作，成功时返回空以便同时断言最终对象状态。
            try { operation(); return null; }
            // 保留实际抛出的异常对象与堆栈，交由调用方断言。
            catch (Exception failure) { return failure; }
        }

        /// <summary>
        ///  输出面板状态、成员归属及材质安装记录，便于定位重入后的所有权问题。
        /// </summary>
        /// <param name="stage">当前诊断阶段的描述。</param>
        /// <param name="panel">需要输出状态的面板。</param>
        /// <param name="image">需要输出父级与当前材质的存活图形成员。</param>
        /// <param name="effect">需要诊断所有权的效果组件，允许为空或已销毁。</param>
        private static void Trace(string stage, UIBase panel, Image image, UIFlowMeshEffect effect)
        {
            // 允许读取实例私有字段，以诊断运行时所有权记录。
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            // 区分托管空引用与 Unity 已销毁对象，后者仍可读取旧组记录用于诊断。
            var owner = ReferenceEquals(effect, null) ? null :
                typeof(UIFlowMeshEffect).GetField("groupController", flags).GetValue(effect) as Object;
            // 读取组件记录的已安装材质，和模板配置及 Graphic 实际材质进行对照。
            var installed = ReferenceEquals(effect, null) ? null :
                typeof(UIFlowMeshEffect).GetField("installedSharedFlowMaterial", flags).GetValue(effect) as Object;
            // 输出同一时刻的面板、层级、进度和三种材质信息，定位重入后的覆盖来源。
            TestContext.WriteLine(stage + ": state=" + panel.State + ", effectAlive=" + (bool)effect +
                ", parent=" + Name(image.transform.parent) + ", owner=" + Name(owner) +
                ", amount=" + (ReferenceEquals(effect, null) ? "<null>" : effect.EffectAmount.ToString()) +
                ", template=" + (ReferenceEquals(effect, null) ? "<null>" : Name(effect.materialPrefab)) +
                ", installed=" + Name(installed) + ", actual=" + Name(image.material));
        }

        /// <summary>
        ///  区分空引用、存活对象和已销毁的 Unity 对象，返回诊断用名称。
        /// </summary>
        /// <param name="value">需要格式化名称的 Unity 对象。</param>
        /// <returns>对象名称，或表示空引用、已销毁对象的标记。</returns>
        private static string Name(Object value) => ReferenceEquals(value, null) ? "<null>" : value ? value.name : "<destroyed>";

        /// <summary>
        ///  重建并检查实际渲染网格，确认成员保持完整且不透明的可见几何。
        /// </summary>
        /// <param name="image">需要重建并验证完整四顶点可见网格的图形成员。</param>
        private static void AssertVisible(Image image)
        {
            // 触发 Graphic 的真实预渲染重建，观察最终输出网格。
            image.Rebuild(CanvasUpdate.PreRender);
            // 取得 CanvasRenderer 当前持有的实际网格。
            var mesh = image.canvasRenderer.GetMesh();
            // 确认渲染器已生成有效网格。
            Assert.NotNull(mesh);
            // 完全可见的标准 Image 应恢复原始四顶点，避免残留细分形变。
            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            // 检查每个顶点都完全不透明，避免只恢复进度而未恢复实际表现。
            foreach (var color in mesh.colors32) Assert.That(color.a, Is.EqualTo(255));
        }
    }
}
