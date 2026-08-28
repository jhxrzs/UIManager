using System;
using System.Collections.Generic;
using System.Linq;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  针对UI面板下所有的VertexUVFlow脚本的控制器
    /// </summary>
    public partial class VertexUVFlowController : EffectControllerBase
    {
        /// <summary>
        ///  消失点屏幕坐标
        /// </summary>
        [SerializeField] private Vector3 endScreenPoint;

        /// <summary>
        ///  ui相机
        /// </summary>
        [SerializeField, HideInInspector] private Camera uiCamera;

        /// <summary>
        /// 排序模式
        /// </summary>
        [SerializeField] private SortMode sortMode = SortMode.Distance;

        /// <summary>
        ///  是否反向排序
        /// </summary>
        [SerializeField] private bool sortInverse;

        /// <summary>
        ///  当排序模式为角度模式时，在屏幕空间和这个向量求角度
        /// </summary>
        [SerializeField] [Tooltip("如果排序方式为角度模式，那么这个向量将作为屏幕空间的原点向量，所有的元素将按照与这个向量的角度进行排序")]
#if ODIN_INSPECTOR
        [EnableIf("sortMode", SortMode.Angle)]
#endif
        private Vector2 screenOriginVector = Vector2.right;

        /// <summary>
        ///  两个效果之间的延时
        /// </summary>
        [SerializeField] [Tooltip("两个元素动效之间的延时")]
        private float delayBetween = 0.1f;

        /// <summary>
        ///  消失点屏幕坐标
        /// </summary>
        public Vector3 EndScreenPoint
        {
            get => endScreenPoint;
            set
            {
                endScreenPoint = value;

                foreach (var each in TotalVertexUVFlows)
                {
                    each.EndScreenPoint = value;
                }
            }
        }

        /// <summary>
        ///  动效用时
        /// </summary>
        public float Duration { get; set; } = 0.5f;


        /// <summary>
        /// 特效材质预制体
        /// </summary>
        [SerializeField] private Material materialPrefab;

        /// <summary>
        ///  默认的材质名称
        /// </summary>
        private const string DefaultMaterialName = "JH_UI_TwistUIEffect.mat";

        /// <summary>
        ///   全部的动效控制组件
        /// </summary>
        private VertexUVFlow[] TotalVertexUVFlows => GetComponentsInChildren<VertexUVFlow>();

        protected override void Awake()
        {
            base.Awake();
            SetMaterialPrefab();
        }

        /// <summary>
        ///  对所有的动效控制组件进行排序
        /// </summary>
        private List<VertexUVFlow> SortVertexUVFlows()
        {
            IComparer<VertexUVFlow> comparer = sortMode switch
            {
                SortMode.Angle => new AngleComparer(endScreenPoint, screenOriginVector, uiCamera,
                    transform as RectTransform),
                _ => new DistanceComparer(endScreenPoint, uiCamera, transform as RectTransform)
            };

            return TotalVertexUVFlows.OrderBy(vertexUVFlow => vertexUVFlow, comparer).ToList();
        }

        /// <summary>
        ///  当特效值改变时
        /// </summary>
        protected override void OnEffectAmountChanged(float value)
        {
            base.OnEffectAmountChanged(value);
            // 排序
            var sortedVertexUVFlows = SortVertexUVFlows();
            if (sortInverse) sortedVertexUVFlows.Reverse();

            // 总元素个数
            var count = sortedVertexUVFlows.Count;

            //计算总用时 = 最后一个元素的延时 + 动效用时
            var totalDuration = (count - 1) * delayBetween + Duration;

            // 计算当前时长
            var currentDuration = effectAmount * totalDuration;

            for (var index = 0; index < sortedVertexUVFlows.Count; index++)
            {
                var vertexUVFlow = sortedVertexUVFlows[index];
                var delay = index * delayBetween;
                if (currentDuration <= delay)
                {
                    vertexUVFlow.EffectAmount = 0;
                    continue;
                }

                var duration = currentDuration - delay;
                vertexUVFlow.EffectAmount = duration / Duration;
            }
        }

        /// <summary>
        ///  查找默认的材质预制体
        /// </summary>
        private void FindShaderByNameOnValidate()
        {
#if UNITY_EDITOR
            if (materialPrefab != null) return;
            var find = AssetDatabase.FindAssets("t:Material", new[] { "Packages" });
            var first = find.Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith(DefaultMaterialName));
            if (first != null) materialPrefab = AssetDatabase.LoadAssetAtPath<Material>(first);
#endif
        }

        /// <summary>
        ///  传递材质预制体到所有的动效控制组件
        /// </summary>
        private void SetMaterialPrefab()
        {
            if (materialPrefab == null) throw new Exception($" 没有特效材质 。");
            foreach (var vertexUVFlow in TotalVertexUVFlows)
            {
                vertexUVFlow.materialPrefab = materialPrefab;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        ///  为当前控制器层级内的全部Graphic补充并配置VertexUVFlow。
        /// </summary>
        /// <remarks>
        ///  包含隐藏节点，复用已有组件，并跳过嵌套控制器和VertexFlowRibbonGraphic。
        /// </remarks>
        [ContextMenu("自动配置子物体 VertexUVFlow")]
        private void ConfigureChildVertexUVFlows()
        {
            // 统一Undo名称，保证层级新增组件和序列化赋值属于同一个用户操作。
            const string undoName = "自动配置子物体VertexUVFlow";

            // 创建独立Undo组，避免与用户此前的Inspector操作合并。
            Undo.IncrementCurrentGroup();

            // 缓存当前Undo组编号，结束时只折叠本次自动配置产生的记录。
            var undoGroup = Undo.GetCurrentGroup();

            // 设置可读名称，用户可以在Unity撤销历史中识别本次批量操作。
            Undo.SetCurrentGroupName(undoName);

            try
            {
                // 控制器没有材质时才执行默认资源查询，保留用户已经指定的材质。
                if (!materialPrefab)
                {
                    // 查询前记录控制器，默认材质自动赋值必须能够随整个操作一起撤销。
                    Undo.RecordObject(this, undoName);

                    // 复用OnValidate的唯一默认材质查询规则，禁止维护第二套搜索路径。
                    FindShaderByNameOnValidate();

                    // 查询成功后才把控制器序列化变化登记为需要保存的资源修改。
                    if (materialPrefab)
                    {
                        // 标记控制器数据已变化，保证场景或Prefab资产能够保存默认材质。
                        EditorUtility.SetDirty(this);

                        // 控制器位于Prefab实例时显式登记材质字段覆盖。
                        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                    }
                }

                // 默认查询后仍没有材质时禁止生成只能降级运行的不完整组件。
                if (!materialPrefab)
                {
                    // 输出包含控制器名称和失败原因的上下文错误，便于定位缺失资源。
                    Debug.LogError(
                        $"VertexUVFlowController“{name}”未找到默认特效材质，无法自动配置子物体。",
                        this);

                    // 材质前置条件不成立，本次ContextMenu操作不再改变任何子物体。
                    return;
                }

                // 查询完整层级中的Graphic，包含初始隐藏且运行时才会显示的视觉节点。
                var graphics = GetComponentsInChildren<Graphic>(true);

                // 统计归当前控制器负责且可以配置的Graphic总数。
                var eligibleCount = 0;

                // 统计本次实际新增的VertexUVFlow数量，用于输出可核对结果。
                var addedCount = 0;

                // 按Unity返回的稳定层级顺序逐个配置Graphic。
                for (var index = 0; index < graphics.Length; index++)
                {
                    // 缓存当前Graphic，避免后续判断重复访问数组。
                    var graphic = graphics[index];

                    // 已经失效的编辑器对象不能添加组件或写入序列化字段。
                    if (!graphic) continue;

                    // Ribbon本身已经是流体路径表现，不能再次套用漏斗网格变形。
                    if (graphic is VertexFlowRibbonGraphic) continue;

                    // 查找离Graphic最近的控制器，建立嵌套面板的唯一管理边界。
                    var ownerController =
                        graphic.GetComponentInParent<VertexUVFlowController>(true);

                    // 子面板存在独立控制器时由子面板负责，父控制器不能重复添加组件。
                    if (ownerController != this) continue;

                    // 当前Graphic已经通过全部资格检查，计入本次配置范围。
                    eligibleCount++;

                    // 优先复用同物体上的现有组件，保证ContextMenu可以安全重复执行。
                    var vertexUVFlow = graphic.GetComponent<VertexUVFlow>();

                    // 仅在组件缺失时通过Undo接口创建，禁止产生重复VertexUVFlow。
                    if (!vertexUVFlow)
                    {
                        // Undo.AddComponent同时登记新增组件和Prefab实例组件覆盖。
                        vertexUVFlow = Undo.AddComponent<VertexUVFlow>(graphic.gameObject);

                        // 新增成功后更新计数，最终日志可以明确本次实际改动规模。
                        addedCount++;
                    }

                    // 写入前记录组件状态，已有配置也必须能够恢复到操作前内容。
                    Undo.RecordObject(vertexUVFlow, undoName);

                    // 每个VertexUVFlow统一使用当前控制器指定的特效材质模板。
                    vertexUVFlow.materialPrefab = materialPrefab;

                    // 同步屏幕终点，保证隐藏节点首次启用时拥有正确的收束位置。
                    vertexUVFlow.EndScreenPoint = endScreenPoint;

                    // 同步当前预览进度，保证新增组件不会在编辑器中出现状态跳变。
                    vertexUVFlow.EffectAmount = effectAmount;

                    // 标记组件序列化数据已经变化，使场景和Prefab保存入口能够发现修改。
                    EditorUtility.SetDirty(vertexUVFlow);

                    // 组件位于Prefab实例时显式登记材质、终点和进度字段覆盖。
                    PrefabUtility.RecordPrefabInstancePropertyModifications(vertexUVFlow);
                }

                // 输出扫描范围和新增数量，支持用户在Console中核对批量操作结果。
                Debug.Log(
                    $"VertexUVFlowController“{name}”已配置{eligibleCount}个Graphic，" +
                    $"其中新增{addedCount}个VertexUVFlow。",
                    this);
            }
            finally
            {
                // 无论成功、提前返回或异常，都把本次产生的Undo记录折叠为一个步骤。
                Undo.CollapseUndoOperations(undoGroup);
            }
        }
#endif

        protected override void OnValidate()
        {
            // 初始化UI相机
            var canvas = GetComponentInParent<Canvas>();
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            FindShaderByNameOnValidate();
            SetMaterialPrefab();
            EndScreenPoint = endScreenPoint;

            base.OnValidate();
        }
    }
}
