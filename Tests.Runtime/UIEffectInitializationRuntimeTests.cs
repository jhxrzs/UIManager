using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的运行时动效初始化。</summary>
    public partial class Test_UIBase
    {
        /// <summary>沿动效继承链查找指定字段，支持配置基类和派生类的序列化状态。</summary>
        /// <param name="type">需要创建、查找字段或验证的组件类型。</param>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <returns>找到的实例字段反射信息。</returns>
        private static FieldInfo GetEffectField(Type type, string name)
        {
            // 在截止时间内持续观察任务状态，避免测试无限等待。
            while (type != null)
            {
                // 取得“field”的反射入口，仅在测试内访问非公开配置。
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                // 根据“field != null”选择对应测试路径。
                if (field != null) return field;
                // 将组件类型（type）设为“type.BaseType”，建立当前测试条件。
                type = type.BaseType;
            }
            // 抛出预设异常，验证调用方的失败传播与收尾行为。
            throw new InvalidOperationException("Missing effect field: " + name);
        }

        /// <summary>写入动效测试字段，模拟编辑器中的序列化配置。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <param name="value">写入配置或用于断言的测试值。</param>
        private static void SetEffectField(EffectControllerBase effect, string name, object value) =>
            GetEffectField(effect.GetType(), name).SetValue(effect, value);

        /// <summary>读取动效测试字段，验证运行时缓存和配置保留情况。</summary>
        /// <param name="effect">需要配置、采样或断言的动效组件。</param>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <returns>字段当前保存的实际值。</returns>
        private static object ReadEffectField(EffectControllerBase effect, string name) =>
            GetEffectField(effect.GetType(), name).GetValue(effect);

        /// <summary>创建尚未激活的动效宿主及 UIBase，允许在真实 Awake 前配置测试状态。</summary>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <param name="ui">当前测试所操作或输出的 UIBase 宿主。</param>
        /// <returns>尚未激活且已登记清理的动效宿主对象。</returns>
        private GameObject CreateRuntimeEffectObject(string name, out UIBase ui)
        {
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject(name, typeof(RectTransform));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(go);
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            go.transform.SetParent(CreateObject(name + "Container").transform, false);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(false);
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            ui = go.AddComponent<UIBase>();
            // 将界面（ui）的组件启用状态设为false，建立当前测试条件。
            ui.enabled = false;
            // 返回宿主物体（go），交给调用方继续配置或断言。
            return go;
        }

        /// <summary>验证运行时添加动效后能解析引用并订阅界面的显隐生命周期。</summary>
        /// <param name="type">需要创建、查找字段或验证的组件类型。</param>
        /// <param name="activeAtAdd">添加动效组件时宿主是否已经激活。</param>
        [TestCase(typeof(EffectControllerBase), false)]
        [TestCase(typeof(AlphaEffectController), false)]
        [TestCase(typeof(ColorEffectController), false)]
        [TestCase(typeof(RectEffectController), false)]
        [TestCase(typeof(AnimController), false)]
        [TestCase(typeof(UIFlowGroupController), false)]
        [TestCase(typeof(EffectControllerBase), true)]
        [TestCase(typeof(AlphaEffectController), true)]
        [TestCase(typeof(ColorEffectController), true)]
        [TestCase(typeof(RectEffectController), true)]
        [TestCase(typeof(AnimController), true)]
        [TestCase(typeof(UIFlowGroupController), true)]
        public void Effect_RuntimeAdd_ShouldResolveReferencesAndSubscribe(Type type, bool activeAtAdd)
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("RuntimeEffect", out var ui);
            // 根据“type == typeof(ColorEffectController)”选择对应测试路径。
            if (type == typeof(ColorEffectController)) go.AddComponent<Image>();
            // 根据“activeAtAdd”选择对应测试路径。
            if (activeAtAdd) go.SetActive(true);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = (EffectControllerBase)go.AddComponent(type);
            // 根据“!activeAtAdd”选择对应测试路径。
            if (!activeAtAdd)
            {
                // 显式清空缓存，避免测试依赖编辑器是否在 AddComponent 时调用 OnValidate。
                SetEffectField(effect, "uiBase", null);
                // 写入动效测试字段，模拟编辑器中的序列化配置。
                SetEffectField(effect, "canvasGroup", null);
                // 根据“type == typeof(ColorEffectController)”选择对应测试路径。
                if (type == typeof(ColorEffectController)) SetEffectField(effect, "graphic", null);
                // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
                go.SetActive(true);
            }

            // 确认“ReadEffectField(effect, "uiBase")”仍引用界面（ui）。
            Assert.AreSame(ui, ReadEffectField(effect, "uiBase"));
            // 确认“ReadEffectField(effect, "canvasGroup")”仍引用“go.GetComponent<CanvasGroup>()”。
            Assert.AreSame(go.GetComponent<CanvasGroup>(), ReadEffectField(effect, "canvasGroup"));
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 确认“(float)ReadEffectField(effect, "effectAmount")”符合1f。
            Assert.That((float)ReadEffectField(effect, "effectAmount"), Is.EqualTo(1f));
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 确认“(float)ReadEffectField(effect, "effectAmount")”符合0f。
            Assert.That((float)ReadEffectField(effect, "effectAmount"), Is.EqualTo(0f));
            // 根据“type == typeof(ColorEffectController)”选择对应测试路径。
            if (type == typeof(ColorEffectController))
                // 确认“ReadEffectField(effect, "graphic")”仍引用“go.GetComponent<Image>()”。
                Assert.AreSame(go.GetComponent<Image>(), ReadEffectField(effect, "graphic"));
        }

        /// <summary>验证运行时添加的透明度动效支持完整异步显隐。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Effect_RuntimeAlpha_ShouldSupportAsyncShowAndHide()
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("RuntimeAlpha", out var ui);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = go.AddComponent<AlphaEffectController>();
            // 写入动效测试字段，模拟编辑器中的序列化配置。
            SetEffectField(effect, "showDuration", 0f);
            // 写入动效测试字段，模拟编辑器中的序列化配置。
            SetEffectField(effect, "hideDuration", 0f);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 启动并保存“hide”，随后检查等待、取消与完成结果。
            var hide = CaptureException(ui.HideAsync(CancellationToken.None));
            // 等待“WaitForTask(hide)”，再检查操作完成后的状态。
            yield return WaitForTask(hide);
            // 确认“hide”的异步结果为空。
            Assert.IsNull(hide.Result);
            // 确认“go.GetComponent<CanvasGroup>()”的透明度符合0f。
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
            // 启动并保存“show”，随后检查等待、取消与完成结果。
            var show = CaptureException(ui.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(show)”，再检查操作完成后的状态。
            yield return WaitForTask(show);
            // 确认“show”的异步结果为空。
            Assert.IsNull(show.Result);
            // 确认“go.GetComponent<CanvasGroup>()”的透明度符合1f。
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        /// <summary>验证自动引用解析不会覆盖调用方显式指定的组件。</summary>
        [Test]
        public void Effect_RuntimeReferences_ShouldPreserveExplicitConfiguration()
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("ConfiguredEffect", out var ownUI);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateRuntimeEffectObject("ConfiguredTarget", out var targetUI);
            // 切换目标对象（target）的激活状态，覆盖失活与恢复时的生命周期边界。
            target.SetActive(true);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = go.AddComponent<AlphaEffectController>();
            // 获取“targetGroup”的实际组件引用，供表现与生命周期断言使用。
            var targetGroup = target.GetComponent<CanvasGroup>();
            // 写入动效测试字段，模拟编辑器中的序列化配置。
            SetEffectField(effect, "uiBase", targetUI);
            // 写入动效测试字段，模拟编辑器中的序列化配置。
            SetEffectField(effect, "canvasGroup", targetGroup);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 确认“ReadEffectField(effect, "uiBase")”仍引用“targetUI”。
            Assert.AreSame(targetUI, ReadEffectField(effect, "uiBase"));
            // 确认“ReadEffectField(effect, "canvasGroup")”仍引用“targetGroup”。
            Assert.AreSame(targetGroup, ReadEffectField(effect, "canvasGroup"));
            // 提交“targetUI”的显示请求，触发实际生命周期。
            targetUI.Show(); targetUI.Hide();
            // 确认“targetGroup”的透明度符合0f。
            Assert.That(targetGroup.alpha, Is.EqualTo(0f));
            // 提交“ownUI”的显示请求，触发实际生命周期。
            ownUI.Show();
            // 确认“targetGroup”的透明度符合0f。
            Assert.That(targetGroup.alpha, Is.EqualTo(0f), "不得改绑到本物体的UIBase。");
            // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
            Object.DestroyImmediate(effect);
            // 将“targetGroup”的透明度设为0.4f，建立当前测试条件。
            targetGroup.alpha = 0.4f;
            // 提交“targetUI”的显示请求，触发实际生命周期。
            targetUI.Show();
            // 确认“targetGroup”的透明度符合0.4f。
            Assert.That(targetGroup.alpha, Is.EqualTo(0.4f), "销毁组件必须从实际订阅目标解绑。");
        }

        /// <summary>验证颜色动效在运行时解析并缓存 Graphic，且正确应用颜色。</summary>
        [Test]
        public void Effect_RuntimeColor_ShouldApplyAndCacheGraphic()
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("RuntimeColor", out var ui);
            // 添加并保存成员图像（image），供后续生命周期与效果检查使用。
            var image = go.AddComponent<Image>();
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = go.AddComponent<ColorEffectController>();
            // 写入动效测试字段，模拟编辑器中的序列化配置。
            SetEffectField(effect, "graphic", null);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 确认成员图像（image）的颜色符合“Color.white”。
            Assert.That(image.color, Is.EqualTo(Color.white));
            // 确认“ReadEffectField(effect, "graphic")”仍引用成员图像（image）。
            Assert.AreSame(image, ReadEffectField(effect, "graphic"));
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 确认成员图像（image）的颜色符合“Color.clear”。
            Assert.That(image.color, Is.EqualTo(Color.clear));
        }

        /// <summary>验证 Rect 动效无需编辑器 OnValidate 也能使用运行时配置的目标姿态。</summary>
        [Test]
        public void Effect_RuntimeRect_ShouldUseConfiguredTargetsWithoutOnValidate()
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("RuntimeRect", out var ui);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = go.AddComponent<RectEffectController>();
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "showTarget", RectShownPose);
            // 写入 Rect 动效的序列化测试配置。
            SetRectEffectField(effect, "hideTarget", RectHiddenPose);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
            AssertRectPose((RectTransform)go.transform, RectShownPose);
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 验证 Rect 位置、旋转、缩放、大小和透明度均符合预期姿态。
            AssertRectPose((RectTransform)go.transform, RectHiddenPose);
        }

        /// <summary>验证添加动画控制器后再指定动画片段仍能正确执行。</summary>
        [Test]
        public void Effect_RuntimeAnimation_ShouldAllowClipConfigurationAfterAdding()
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("RuntimeAnimation", out var ui);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            go.AddComponent<AnimController>();
            // 获取“animation”的实际组件引用，供表现与生命周期断言使用。
            var animation = go.GetComponent<Animation>();
            // 确认“animation”存在。
            Assert.IsNotNull(animation);
            // 确认“animation.playAutomatically”为假。
            Assert.IsFalse(animation.playAutomatically);
            ui.Show(); ui.Hide(); // 未配置片段的过渡不能抛空引用。
            // 创建“clip”，隔离本用例的资源或观测数据。
            var clip = new AnimationClip { legacy = true };
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 配置动画片段的位置曲线，使采样结果能够由数值直接验证。
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 20));
                // 将测试动画片段注册到 Animation，供控制器按实际片段采样。
                animation.AddClip(clip, "RuntimeClip");
                // 将“animation.clip”设为“clip”，建立当前测试条件。
                animation.clip = clip;
                // 提交界面（ui）的显示请求，触发实际生命周期。
                ui.Show();
                // 确认“go.transform.localPosition.x”符合20f，允许指定的浮点误差。
                Assert.That(go.transform.localPosition.x, Is.EqualTo(20f).Within(0.001f));
                // 提交界面（ui）的隐藏请求，检查子树联动与终态。
                ui.Hide();
                // 确认“go.transform.localPosition.x”符合0f，允许指定的浮点误差。
                Assert.That(go.transform.localPosition.x, Is.EqualTo(0f).Within(0.001f));
            }
            // 无论断言是否通过，都释放本轮临时资源并结束挂起任务。
            finally { Object.DestroyImmediate(clip); }
        }

        /// <summary>验证缓存的 CanvasGroup 被销毁后重新解析并缓存本地组件。</summary>
        [Test]
        public void Effect_DestroyedCanvasCache_ShouldResolveAndCacheLocalComponent()
        {
            // 创建宿主物体（go）并配置测试所需的层级或表现组件。
            var go = CreateRuntimeEffectObject("RuntimeAlpha", out var ui);
            // 切换宿主物体（go）的激活状态，覆盖失活与恢复时的生命周期边界。
            go.SetActive(true);
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = go.AddComponent<AlphaEffectController>();
            // 创建“external”并配置测试所需的层级或表现组件。
            var external = CreateObject("ExternalCanvas");
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            SetEffectField(effect, "canvasGroup", external.AddComponent<CanvasGroup>());
            // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
            Object.DestroyImmediate(external);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show(); ui.Hide();
            // 确认“ReadEffectField(effect, "canvasGroup")”仍引用“go.GetComponent<CanvasGroup>()”。
            Assert.AreSame(go.GetComponent<CanvasGroup>(), ReadEffectField(effect, "canvasGroup"));
            // 确认“go.GetComponent<CanvasGroup>()”的透明度符合0f。
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
        }
    }
}
