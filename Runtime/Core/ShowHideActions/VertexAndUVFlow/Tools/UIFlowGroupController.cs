using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  为UI面板下的全部流动网格提供统一主路径、形变参数和时间进度。
    /// </summary>
    /// <remarks>
    ///  主路径以全部直属Graphic的视觉中心为起点；每个子物体保留初始队形偏移，
    ///  并在接近统一终点时按配置指数逐渐收束。嵌套Controller拥有独立管理边界。
    /// </remarks>
    [MovedFrom(true, sourceNamespace: "JH", sourceAssembly: "com.jh.uimanager.runtime",
        sourceClassName: "VertexUVFlowController")]
    public partial class UIFlowGroupController : EffectControllerBase
    {
        /// <summary>
        ///  统一主路径最终收束点的屏幕坐标。
        /// </summary>
        [SerializeField] private Vector3 endScreenPoint;

        /// <summary>
        ///  当前最近Canvas用于屏幕坐标转换的相机。
        /// </summary>
        [SerializeField, HideInInspector] private Camera uiCamera;

        /// <summary>
        ///  子物体时间错峰使用的排序模式。
        /// </summary>
        [SerializeField] private SortMode sortMode = SortMode.Distance;

        /// <summary>
        ///  是否反转子物体的时间错峰顺序。
        /// </summary>
        [SerializeField] private bool sortInverse;

        /// <summary>
        ///  角度排序在屏幕空间中使用的零度方向。
        /// </summary>
        [SerializeField] [Tooltip("如果排序方式为角度模式，那么这个向量将作为屏幕空间的原点向量，所有的元素将按照与这个向量的角度进行排序")]
#if ODIN_INSPECTOR
        [EnableIf("sortMode", SortMode.Angle)]
#endif
        private Vector2 screenOriginVector = Vector2.right;

        /// <summary>
        ///  相邻两个子物体之间的启动延时；零表示完全同步。
        /// </summary>
        [SerializeField] [Tooltip("两个元素动效之间的延时")]
        private float delayBetween = 0.1f;

        /// <summary>
        ///  统一主路径控制点沿起终点方向所在的比例。
        /// </summary>
        [SerializeField]
        [Range(
            UIFlowMeshConfiguration.MinimumCurveControlDistanceRatio,
            UIFlowMeshConfiguration.MaximumCurveControlDistanceRatio)]
        private float curveControlDistanceRatio =
            UIFlowMeshConfiguration.DefaultCurveControlDistanceRatio;

        /// <summary>
        ///  统一主路径控制点垂直偏移占起终点距离的比例。
        /// </summary>
        [SerializeField, Range(0f, UIFlowMeshConfiguration.MaximumCurveBendRatio)]
        private float curveBend = 0.24f;

        /// <summary>
        ///  统一主路径控制点垂直偏移的Controller本地坐标上限。
        /// </summary>
        [SerializeField, Min(0f)]
        private float maximumCurveBend = 140f;

        /// <summary>
        ///  子物体相对整体中心的初始队形偏移收束指数。
        /// </summary>
        /// <remarks>
        ///  数值小于一时队形较早聚拢，数值大于一时会更久保留原始排布。
        /// </remarks>
        [SerializeField, Range(0.2f, 4f)]
        private float formationCollapsePower = 1f;

        /// <summary>
        ///  背离终点的顶点相对前沿顶点的空间相位落后强度。
        /// </summary>
        [SerializeField, Range(0f, 0.75f)]
        private float flowDelay = 0.42f;

        /// <summary>
        ///  横截面向终点收束时使用的幂指数。
        /// </summary>
        [SerializeField, Range(0.2f, 2f)]
        private float funnelPower = 0.7f;

        /// <summary>
        ///  顶点透明度开始消失的曲线进度。
        /// </summary>
        [SerializeField, Range(0f, 0.99f)]
        private float fadeStart = 0.84f;

        /// <summary>
        ///  普通Image横向最多生成的细分段数。
        /// </summary>
        [SerializeField]
        [Range(
            UIFlowMeshConfiguration.MinimumGridSegments,
            UIFlowMeshConfiguration.MaximumGridSegments)]
        private int horizontalSegments = UIFlowMeshConfiguration.DefaultHorizontalSegments;

        /// <summary>
        ///  普通Image纵向最多生成的细分段数。
        /// </summary>
        [SerializeField]
        [Range(
            UIFlowMeshConfiguration.MinimumGridSegments,
            UIFlowMeshConfiguration.MaximumGridSegments)]
        private int verticalSegments = UIFlowMeshConfiguration.DefaultVerticalSegments;

        /// <summary>
        ///  获取或设置统一主路径最终收束点的屏幕坐标。
        /// </summary>
        public Vector3 EndScreenPoint
        {
            get => endScreenPoint;
            set
            {
                endScreenPoint = value;

                // 终点变化会改变整组唯一主路径，必须以当前进度重新下发到全部子物体。
                ApplyGroupFlow(effectAmount);
            }
        }

        /// <summary>
        ///  获取或设置单个子物体完成流动形变的持续时间。
        /// </summary>
        public float Duration { get; set; } = 0.5f;


        /// <summary>
        ///  默认Graphic可以共同使用的流动材质资产。
        /// </summary>
        /// <remarks>
        ///  该材质只提供可选Shader增强，已经配置自定义材质的Graphic始终保留原材质。
        /// </remarks>
        [SerializeField] private Material materialPrefab;

        /// <summary>
        ///  默认共享流动材质的精确资源名称。
        /// </summary>
        private const string DefaultMaterialName = "JH_UI_TwistUIEffect.mat";

        /// <summary>
        ///  当前层级中归本Controller直接负责的全部流动网格组件缓存。
        /// </summary>
        private readonly List<UIFlowMeshEffect> flowMeshEffects = new();

        /// <summary>
        ///  每帧复用的排序列表，避免动画进度变化时产生临时集合。
        /// </summary>
        private readonly List<UIFlowMeshEffect> sortedFlowMeshEffects = new();

        /// <summary>
        ///  初始化基类UI显示和隐藏事件订阅。
        /// </summary>
        protected override void Awake()
        {
            // 基类负责绑定UIBase显示和隐藏生命周期，组路径初始化统一留给OnEnable。
            base.Awake();
        }

        /// <summary>
        ///  Controller启用时刷新层级、材质和统一主路径。
        /// </summary>
        private void OnEnable()
        {
            // 运行时Canvas或相机可能与编辑器保存值不同，主路径创建前必须重新解析。
            RefreshUICamera();

            // 启用时通过统一入口恢复直属成员、共享材质和当前组进度。
            RefreshFlowHierarchy();
        }

        /// <summary>
        ///  重新扫描当前直属流动网格并应用材质和现有组进度。
        /// </summary>
        private void RefreshFlowHierarchy()
        {
            // 完整扫描只发生在启用、显隐开始或明确层级变化时，不进入动画逐帧路径。
            RefreshOwnedFlowMeshEffects();

            // 新成员独立核对共享材质所有权，已经配置的自定义材质始终保留。
            SetMaterialPrefab();

            // 成员变化会影响组中心和时间轴，按当前进度重新建立统一主路径。
            ApplyGroupFlow(effectAmount);
        }

        /// <summary>
        ///  按当前时间错峰模式对全部直属流动网格进行稳定排序。
        /// </summary>
        /// <returns>由Controller复用且已经完成排序的流动网格列表。</returns>
        private List<UIFlowMeshEffect> SortFlowMeshEffects()
        {
            // 复用同一个列表，清除上次排序结果但保留已经分配的容量。
            sortedFlowMeshEffects.Clear();

            // 当前直属组件缓存按层级变化维护，进度回调不再重复GetComponentsInChildren。
            sortedFlowMeshEffects.AddRange(flowMeshEffects);

            // 根据当前Inspector模式创建只读比较器。
            IComparer<UIFlowMeshEffect> comparer = sortMode switch
            {
                SortMode.Angle => new AngleComparer(
                    endScreenPoint,
                    screenOriginVector,
                    uiCamera),
                _ => new DistanceComparer(endScreenPoint, uiCamera, transform as RectTransform)
            };

            // List.Sort原地排序，不创建LINQ迭代器和结果集合。
            sortedFlowMeshEffects.Sort(comparer);

            // 返回Controller独占复用列表，调用方必须在下一次排序前完成读取。
            return sortedFlowMeshEffects;
        }

        /// <summary>
        ///  动效进度变化时更新全部子物体的统一路径和独立时间进度。
        /// </summary>
        /// <param name="value">基类限制到零到一范围后的线性进度。</param>
        protected override void OnEffectAmountChanged(float value)
        {
            base.OnEffectAmountChanged(value);
            // 所有时间、路径和网格参数通过唯一入口一起下发，禁止不同回调形成半更新状态。
            ApplyGroupFlow(value);
        }

        /// <summary>
        ///  同步隐藏开始前重新核对深层动态成员，再执行标准组隐藏进度。
        /// </summary>
        protected override void OnUIHide()
        {
            // 深层ToolItem换父不会通知Controller根节点，显隐边界必须主动完成一次全量校正。
            RefreshFlowHierarchy();

            // 基类负责把组进度同步推进到完全隐藏状态。
            base.OnUIHide();
        }

        /// <summary>
        ///  同步显示开始前重新核对深层动态成员，再执行标准组显示进度。
        /// </summary>
        protected override void OnUIShow()
        {
            // 新进入当前层级的深层Graphic需要在首个显示进度前加入统一组路径。
            RefreshFlowHierarchy();

            // 基类负责把组进度同步恢复到完全显示状态。
            base.OnUIShow();
        }

        /// <summary>
        ///  异步隐藏开始前重新核对深层动态成员，再播放标准组隐藏动画。
        /// </summary>
        /// <param name="token">当前UI隐藏操作的取消令牌。</param>
        /// <returns>完整组隐藏动画任务。</returns>
        protected override async UniTask OnUIHideAsync(CancellationToken token)
        {
            // 每轮异步隐藏只扫描一次层级，后续Tween帧只执行无分配缓存所有权检查。
            RefreshFlowHierarchy();

            // 继续复用基类时长、缓动和取消传播语义。
            await base.OnUIHideAsync(token);
        }

        /// <summary>
        ///  异步显示开始前重新核对深层动态成员，再播放标准组显示动画。
        /// </summary>
        /// <param name="token">当前UI显示操作的取消令牌。</param>
        /// <returns>完整组显示动画任务。</returns>
        protected override async UniTask OnUIShowAsync(CancellationToken token)
        {
            // 每轮异步显示只扫描一次层级，保证新成员从动画第一帧进入统一组路径。
            RefreshFlowHierarchy();

            // 继续复用基类时长、缓动和取消传播语义。
            await base.OnUIShowAsync(token);
        }

        /// <summary>
        ///  判断一个流动网格是否由当前Controller直接负责。
        /// </summary>
        /// <param name="flowMeshEffect">当前层级查询到的候选流动网格。</param>
        /// <returns>最近的上级组控制器就是当前实例时返回true。</returns>
        private bool IsOwnedFlowMeshEffect(UIFlowMeshEffect flowMeshEffect)
        {
            // 已经销毁或缺失的Unity对象不能参与路径计算和网格下发。
            if (!flowMeshEffect) return false;

            // 最近父级原则保证嵌套面板只由自己的Controller管理，不被外层重复驱动。
            return flowMeshEffect.GetComponentInParent<UIFlowGroupController>(true) == this;
        }

        /// <summary>
        ///  从稳定缓存中移除已经不再归当前Controller负责的流动网格。
        /// </summary>
        private void PruneUnownedFlowMeshEffects()
        {
            // 反向遍历允许原地移除，不创建临时列表也不会跳过连续失效成员。
            for (var index = flowMeshEffects.Count - 1; index >= 0; index--)
            {
                // 缓存仍符合最近Controller归属时继续保留。
                var flowMeshEffect = flowMeshEffects[index];
                if (IsOwnedFlowMeshEffect(flowMeshEffect)) continue;

                // 先移除缓存，清理网格时的同步回调不能让旧Controller再次消费该成员。
                flowMeshEffects.RemoveAt(index);

                // 只释放仍由当前Controller登记的旧状态，后来Controller的新状态不受影响。
                ReleaseUnownedFlowMeshEffect(flowMeshEffect);
            }
        }

        /// <summary>
        ///  释放一个已经离开当前Controller层级的旧组状态。
        /// </summary>
        /// <param name="flowMeshEffect">已经从当前缓存移除的流动网格。</param>
        private void ReleaseUnownedFlowMeshEffect(UIFlowMeshEffect flowMeshEffect)
        {
            // 已经销毁的组件只需要从托管缓存移除，不再访问Unity对象。
            if (!flowMeshEffect) return;

            // 组件仍登记当前旧Controller时恢复可见；内部所有权校验会保护后来Controller的新状态。
            flowMeshEffect.DetachGroupController(this);
        }

        /// <summary>
        ///  重新扫描并缓存当前Controller直接负责的全部流动网格组件。
        /// </summary>
        private void RefreshOwnedFlowMeshEffects()
        {
            // 复用排序列表暂存旧成员，刷新完成前仍可准确解除已经离组的临时状态。
            sortedFlowMeshEffects.Clear();
            sortedFlowMeshEffects.AddRange(flowMeshEffects);

            // 清除活动缓存但保留容量，随后按当前稳定层级顺序完整重建。
            flowMeshEffects.Clear();

            // 扫描包含隐藏节点的完整层级，池化对象重新启用时已经拥有正确组状态。
            var candidates = GetComponentsInChildren<UIFlowMeshEffect>(true);

            // 按Unity稳定层级顺序逐个建立当前Controller的唯一所有权集合。
            for (var index = 0; index < candidates.Length; index++)
            {
                // 缓存当前候选，避免循环内重复索引数组。
                var candidate = candidates[index];

                // 嵌套Controller的子物体由更近的控制器负责，当前缓存必须排除。
                if (!IsOwnedFlowMeshEffect(candidate)) continue;

                // 当前候选通过所有权检查，可以参与统一主路径和材质下发。
                flowMeshEffects.Add(candidate);
            }

            // 逐个解除旧集合中已经不在当前直属层级的组件状态。
            for (var index = 0; index < sortedFlowMeshEffects.Count; index++)
            {
                // 已销毁成员不再访问Unity对象。
                var previousFlowMeshEffect = sortedFlowMeshEffects[index];
                if (!previousFlowMeshEffect) continue;

                // 仍在新集合中的成员继续由当前Controller负责。
                if (flowMeshEffects.Contains(previousFlowMeshEffect)) continue;

                // 离组成员只释放当前Controller旧状态，后来Controller的新状态不会被覆盖。
                ReleaseUnownedFlowMeshEffect(previousFlowMeshEffect);
            }

            // 临时旧集合在方法退出前清空，避免长期保留已离场景对象引用。
            sortedFlowMeshEffects.Clear();
        }

        /// <summary>
        ///  创建并向全部直属子物体下发统一主路径、形变配置和独立时间进度。
        /// </summary>
        /// <param name="linearProgress">Controller当前零到一线性进度。</param>
        private void ApplyGroupFlow(float linearProgress)
        {
            // 每次消费缓存前执行无分配所有权校验，覆盖禁用组件错过层级回调的极端情况。
            PruneUnownedFlowMeshEffects();

            // 排序只影响可选时间错峰，所有子物体仍然采样同一条空间主路径。
            var sortedFlowMeshEffects = SortFlowMeshEffects();

            // 空面板没有可驱动目标，提前返回并避免负总时长。
            if (sortedFlowMeshEffects.Count == 0) return;

            // 当前Inspector参数只构造一次，同一帧内全部子物体共享完全相同的不可变配置。
            var configuration = CreateFlowConfiguration();

            // 统一主路径以整组视觉中心为起点，并在Controller坐标空间中只计算一次控制点。
            var hasGroupPath = TryCreateGroupScreenPath(
                sortedFlowMeshEffects,
                out var groupScreenPath);

            // 反向排序仅改变启动顺序，不改变主路径形状或组中心。
            if (sortInverse) sortedFlowMeshEffects.Reverse();

            // 负延时没有明确视觉语义，运行时统一按零处理。
            var safeDelayBetween = Mathf.Max(0f, delayBetween);

            // Duration允许外部在运行时赋零，零时长必须使用离散完成语义而不是除零。
            var safeDuration = Mathf.Max(0f, Duration);

            // 总时长由最后一个子物体的延时和单体持续时间共同构成。
            var totalDuration =
                (sortedFlowMeshEffects.Count - 1) * safeDelayBetween + safeDuration;

            // 外部异常进度先限制到标准区间，再映射到当前组时间轴。
            var currentDuration = Mathf.Clamp01(linearProgress) * totalDuration;

            // 按稳定排序逐个计算时间错峰，但路径和形变参数保持完全一致。
            for (var index = 0; index < sortedFlowMeshEffects.Count; index++)
            {
                // 缓存当前子组件，避免同一循环重复访问列表索引。
                var flowMeshEffect = sortedFlowMeshEffects[index];

                // 当前子物体的启动时刻只由排序索引和统一间隔决定。
                var delay = index * safeDelayBetween;

                // 使用独立方法处理零Duration和正常Duration，禁止产生Infinity或NaN。
                var childProgress = ResolveChildProgress(
                    currentDuration,
                    delay,
                    safeDuration,
                    linearProgress);

                // 有效组路径一次写入路径、配置和进度，只触发一次子网格重建。
                if (hasGroupPath)
                {
                    flowMeshEffect.ApplyGroupFlow(
                        this,
                        groupScreenPath,
                        configuration,
                        childProgress);
                    continue;
                }

                // 坐标转换失败时一次写入归属、终点和降级进度，禁止留下半更新状态。
                flowMeshEffect.ApplyGroupFallback(
                    this,
                    endScreenPoint,
                    childProgress);
            }
        }

        /// <summary>
        ///  计算一个子物体在当前组时间轴上的标准化线性进度。
        /// </summary>
        /// <param name="currentDuration">Controller当前进度映射后的组时间。</param>
        /// <param name="delay">当前子物体在组时间轴上的启动延时。</param>
        /// <param name="duration">单个子物体经过非负保护后的持续时间。</param>
        /// <param name="groupProgress">Controller当前零到一线性进度。</param>
        /// <returns>当前子物体零到一之间的线性进度。</returns>
        private static float ResolveChildProgress(
            float currentDuration,
            float delay,
            float duration,
            float groupProgress)
        {
            // 零持续时间在到达启动时刻后立即完成，同时保留组进度为零的初始完整状态。
            if (duration <= Mathf.Epsilon)
            {
                // 尚未到达当前子物体延时时刻时继续保持零进度。
                if (currentDuration < delay) return 0f;

                // 单元素且总时长为零时使用原始组进度判断是否已经开始。
                return groupProgress > 0f ? 1f : 0f;
            }

            // 正常持续时间尚未越过启动时刻时必须保持完整原始网格。
            if (currentDuration <= delay) return 0f;

            // 正常持续时间限制到标准范围，末尾和反向播放都保持稳定。
            return Mathf.Clamp01((currentDuration - delay) / duration);
        }

        /// <summary>
        ///  从当前Controller序列化字段创建同组唯一的不可变网格配置。
        /// </summary>
        /// <returns>全部直属流动网格在当前帧共同使用的安全参数。</returns>
        private UIFlowMeshConfiguration CreateFlowConfiguration()
        {
            // 配置构造函数统一执行运行时边界保护，脚本写入异常值也不会污染Canvas网格。
            return new UIFlowMeshConfiguration(
                horizontalSegments,
                verticalSegments,
                flowDelay,
                curveControlDistanceRatio,
                curveBend,
                maximumCurveBend,
                funnelPower,
                fadeStart,
                formationCollapsePower);
        }

        /// <summary>
        ///  在Controller本地空间创建一次主路径，并转换成所有子物体都能读取的屏幕路径。
        /// </summary>
        /// <param name="flowMeshEffects">当前Controller直接负责的全部流动网格。</param>
        /// <param name="screenPath">成功时返回四个控制点均位于屏幕空间的统一路径。</param>
        /// <returns>Controller、组中心和屏幕终点都能够完成坐标转换时返回true。</returns>
        private bool TryCreateGroupScreenPath(
            IReadOnlyList<UIFlowMeshEffect> flowMeshEffects,
            out UIFlowPath2D screenPath)
        {
            // 所有失败出口都返回确定默认值，禁止调用方误用上一帧路径。
            screenPath = default;

            // 组路径需要至少一个有效子物体和一个UI坐标空间。
            if (flowMeshEffects == null || flowMeshEffects.Count == 0 ||
                !(transform is RectTransform controllerRectTransform))
                return false;

            // 累加所有Graphic视觉中心在Controller本地空间中的位置。
            var groupCenterLocal = Vector2.zero;

            // 有效数量独立统计，运行时被销毁的Unity对象不会使平均中心偏移。
            var validCount = 0;

            // 组中心只计算一次，子物体各自RectTransform和Pivot差异不会改变主路径。
            for (var index = 0; index < flowMeshEffects.Count; index++)
            {
                // 缓存候选组件并跳过已经失效的Unity对象。
                var flowMeshEffect = flowMeshEffects[index];
                if (!flowMeshEffect) continue;

                // 取得Graphic矩形中心的世界坐标，而不是可能偏离视觉中心的Transform Pivot。
                var sourceCenterWorld = flowMeshEffect.GetSourceCenterWorldPoint();

                // 全部视觉中心统一转换到Controller本地坐标后再求平均。
                var sourceCenterLocal =
                    controllerRectTransform.InverseTransformPoint(sourceCenterWorld);

                // 只累加二维UI平面坐标，世界空间Canvas的Z值不参与贝塞尔路径。
                groupCenterLocal += new Vector2(sourceCenterLocal.x, sourceCenterLocal.y);

                // 当前候选已经成功参与组中心计算。
                validCount++;
            }

            // 全部候选在计算期间失效时不能创建有意义的主路径。
            if (validCount == 0) return false;

            // 平均视觉中心比平均Pivot更稳定，非中心Pivot不会扭曲整组起点。
            groupCenterLocal /= validCount;

            // 屏幕终点转换到Controller本地空间，曲线参数继续使用Canvas本地单位。
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    controllerRectTransform,
                    endScreenPoint,
                    uiCamera,
                    out var targetPointLocal))
                return false;

            // 统一S曲线只在Controller空间创建一次，所有子物体共享同一法线和控制点。
            var localPath = UIFlowPath2D.CreateReversibleS(
                groupCenterLocal,
                targetPointLocal,
                curveControlDistanceRatio,
                curveBend,
                maximumCurveBend,
                UIFlowMeshConfiguration.MaximumCurveBendRatio);

            // 四个控制点逐一转换到屏幕空间，使不同父级和缩放下的Graphic能够共同读取。
            screenPath = UIFlowPath2D.CreateCubic(
                ConvertControllerLocalPointToScreen(controllerRectTransform, localPath.Start),
                ConvertControllerLocalPointToScreen(
                    controllerRectTransform,
                    localPath.FirstControlPoint),
                ConvertControllerLocalPointToScreen(
                    controllerRectTransform,
                    localPath.SecondControlPoint),
                ConvertControllerLocalPointToScreen(controllerRectTransform, localPath.End));

            // Controller空间和屏幕空间路径均已完整建立。
            return true;
        }

        /// <summary>
        ///  将Controller本地路径控制点转换成当前Canvas对应的屏幕坐标。
        /// </summary>
        /// <param name="controllerRectTransform">当前组控制器的RectTransform。</param>
        /// <param name="localPoint">Controller本地空间中的路径控制点。</param>
        /// <returns>可以传递给任意同Canvas子物体的屏幕坐标。</returns>
        private Vector2 ConvertControllerLocalPointToScreen(
            RectTransform controllerRectTransform,
            Vector2 localPoint)
        {
            // 先转换到世界坐标，完整保留Controller层级的缩放和旋转。
            var worldPoint = controllerRectTransform.TransformPoint(localPoint);

            // 再使用当前Canvas相机转换到屏幕坐标，Overlay模式允许相机为空。
            return RectTransformUtility.WorldToScreenPoint(uiCamera, worldPoint);
        }

        /// <summary>
        ///  根据当前最近Canvas刷新屏幕与UI坐标转换所需的相机。
        /// </summary>
        private void RefreshUICamera()
        {
            // Controller可能在Prefab编辑阶段暂时没有Canvas，缺失时按Overlay空相机语义降级。
            var canvas = GetComponentInParent<Canvas>();

            // Overlay Canvas不需要相机，Camera和World Space Canvas使用其正式worldCamera。
            uiCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }

        /// <summary>
        ///  在编辑器中查找包内默认共享流动材质。
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
        ///  将可选共享流动材质传递到所有动效控制组件。
        /// </summary>
        private void SetMaterialPrefab()
        {
            // 每个UIFlowMeshEffect自行判断Graphic是否已经拥有必须保留的自定义材质。
            foreach (var flowMeshEffect in flowMeshEffects)
            {
                // 通过统一入口消除父子Awake顺序差异，并保留每个Graphic已有的自定义材质。
                flowMeshEffect.ApplySharedMaterial(materialPrefab);
            }
        }

        /// <summary>
        ///  子物体层级变化后重新建立直属组件集合和统一主路径。
        /// </summary>
        private void OnTransformChildrenChanged()
        {
            // 非活动Controller不主动改写子物体，下一次启用或进度变化会自然刷新。
            if (!isActiveAndEnabled) return;

            // 直接子级变化复用完整层级刷新入口；深层换父会在显隐边界再次兜底核对。
            RefreshFlowHierarchy();
        }

        /// <summary>
        ///  Canvas层级或渲染模式变化后刷新相机并重建屏幕空间主路径。
        /// </summary>
        private void OnCanvasHierarchyChanged()
        {
            // 当前Canvas决定屏幕点和UI本地点之间使用的正式相机。
            RefreshUICamera();

            // 相机或Canvas缩放变化后旧屏幕控制点已经失效，必须立即重新转换。
            ApplyGroupFlow(effectAmount);
        }

        /// <summary>
        ///  Controller销毁时解除子组件持有的临时组路径。
        /// </summary>
        protected override void OnDestroy()
        {
            // Controller禁用期间层级仍可能变化，销毁前重新扫描避免遗漏临时组状态。
            RefreshOwnedFlowMeshEffects();

            // 先让仍然存活的子组件恢复独立模式，禁止继续引用已销毁Controller的最后路径。
            foreach (var flowMeshEffect in flowMeshEffects)
            {
                // 每个子组件只清理非序列化组状态，不改变材质和自身降级参数。
                flowMeshEffect.DetachGroupController(this);
            }

            // 保留基类对UIBase显示和隐藏事件的正式取消订阅流程。
            base.OnDestroy();
        }

#if UNITY_EDITOR
        /// <summary>
        ///  为当前控制器层级内的全部Graphic补充并配置UIFlowMeshEffect。
        /// </summary>
        /// <remarks>
        ///  包含隐藏节点，复用已有组件，并跳过嵌套控制器和VertexFlowRibbonGraphic。
        /// </remarks>
        [ContextMenu("自动配置子物体 UIFlowMeshEffect")]
        private void ConfigureChildFlowMeshEffects()
        {
            // 统一Undo名称，保证层级新增组件和序列化赋值属于同一个用户操作。
            const string undoName = "自动配置子物体UIFlowMeshEffect";

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

                // 默认查询后仍没有材质时允许继续配置，核心流动会使用CPU和顶点Alpha路径。
                if (!materialPrefab)
                {
                    // 输出明确降级提示，区分可选Shader增强缺失与核心组件配置失败。
                    Debug.LogWarning(
                        $"UIFlowGroupController“{name}”未找到默认共享流动材质，" +
                        "将保留各Graphic原材质并使用CPU连续曲面。",
                        this);
                }

                // 查询完整层级中的Graphic，包含初始隐藏且运行时才会显示的视觉节点。
                var graphics = GetComponentsInChildren<Graphic>(true);

                // 统计归当前控制器负责且可以配置的Graphic总数。
                var eligibleCount = 0;

                // 统计本次实际新增的UIFlowMeshEffect数量，用于输出可核对结果。
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
                        graphic.GetComponentInParent<UIFlowGroupController>(true);

                    // 子面板存在独立控制器时由子面板负责，父控制器不能重复添加组件。
                    if (ownerController != this) continue;

                    // 当前Graphic已经通过全部资格检查，计入本次配置范围。
                    eligibleCount++;

                    // 优先复用同物体上的现有组件，保证ContextMenu可以安全重复执行。
                    var flowMeshEffect = graphic.GetComponent<UIFlowMeshEffect>();

                    // 仅在组件缺失时通过Undo接口创建，禁止产生重复UIFlowMeshEffect。
                    if (!flowMeshEffect)
                    {
                        // Undo.AddComponent同时登记新增组件和Prefab实例组件覆盖。
                        flowMeshEffect = Undo.AddComponent<UIFlowMeshEffect>(graphic.gameObject);

                        // 新增成功后更新计数，最终日志可以明确本次实际改动规模。
                        addedCount++;
                    }

                    // 写入前记录组件状态，已有配置也必须能够恢复到操作前内容。
                    Undo.RecordObject(flowMeshEffect, undoName);

                    // 通过统一入口传递模板；编辑器阶段不会把共享材质写入Graphic字段。
                    flowMeshEffect.ApplySharedMaterial(materialPrefab);

                    // 同步屏幕终点，保证隐藏节点首次启用时拥有正确的收束位置。
                    flowMeshEffect.EndScreenPoint = endScreenPoint;

                    // 同步当前预览进度，保证新增组件不会在编辑器中出现状态跳变。
                    flowMeshEffect.EffectAmount = effectAmount;

                    // 标记组件序列化数据已经变化，使场景和Prefab保存入口能够发现修改。
                    EditorUtility.SetDirty(flowMeshEffect);

                    // 组件位于Prefab实例时显式登记材质、终点和进度字段覆盖。
                    PrefabUtility.RecordPrefabInstancePropertyModifications(flowMeshEffect);
                }

                // 新增组件已经改变直属集合，统一主路径建立前必须刷新运行时缓存。
                RefreshOwnedFlowMeshEffects();

                // 全部组件补齐后立即建立统一主路径，编辑器预览不会显示旧的各自飞行状态。
                ApplyGroupFlow(effectAmount);

                // 输出扫描范围和新增数量，支持用户在Console中核对批量操作结果。
                Debug.Log(
                    $"UIFlowGroupController“{name}”已配置{eligibleCount}个Graphic，" +
                    $"其中新增{addedCount}个UIFlowMeshEffect。",
                    this);
            }
            finally
            {
                // 无论成功、提前返回或异常，都把本次产生的Undo记录折叠为一个步骤。
                Undo.CollapseUndoOperations(undoGroup);
            }
        }
#endif

        /// <summary>
        ///  编辑器字段变化时限制参数并刷新材质、组件缓存和统一主路径。
        /// </summary>
        protected override void OnValidate()
        {
            // 延时不接受负数，避免排序后的子物体在组时间轴上出现反向启动点。
            delayBetween = Mathf.Max(0f, delayBetween);

            // 单体持续时间由公开属性提供，编辑器输入异常时同样执行非负保护。
            Duration = Mathf.Max(0f, Duration);

            // 通过配置构造函数集中限制所有形变参数，再把安全值同步回Inspector字段。
            var configuration = CreateFlowConfiguration();
            horizontalSegments = configuration.HorizontalSegments;
            verticalSegments = configuration.VerticalSegments;
            flowDelay = configuration.FlowDelay;
            curveControlDistanceRatio = configuration.CurveControlDistanceRatio;
            curveBend = configuration.CurveBend;
            maximumCurveBend = configuration.MaximumCurveBend;
            funnelPower = configuration.FunnelPower;
            fadeStart = configuration.FadeStart;
            formationCollapsePower = configuration.FormationCollapsePower;

            // Prefab编辑阶段可能没有Canvas，统一相机入口会安全使用Overlay空相机语义。
            RefreshUICamera();

            // Inspector或ContextMenu可能改变子物体组件，材质与路径同步前刷新直属缓存。
            RefreshOwnedFlowMeshEffects();

            // 缺少显式材质时尝试补全包内默认共享材质。
            FindShaderByNameOnValidate();

            // 将材质模板同步到直属子组件，同时保留它们已有的自定义材质。
            SetMaterialPrefab();

            // 基类限制进度并触发OnEffectAmountChanged，统一主路径只从一个入口重建。
            base.OnValidate();
        }
    }
}
