using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  顶点和UV流动动画管理器。
    /// </summary>
    /// <remarks>
    ///  普通Image会在Canvas生成网格时被连续细分，并沿屏幕终点形成贝塞尔漏斗曲面。
    ///  <see cref="EffectAmount"/>接收调用方的线性进度，组件内部只应用一次平滑时间曲线。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class VertexUVFlow : MonoBehaviour, IMeshModifier
    {
        /// <summary>
        ///  Shader中的动效进度属性。
        /// </summary>
        private static readonly int Amount = Shader.PropertyToID("_EffectAmount");

        /// <summary>
        ///  Shader中的兼容终点UV属性。
        /// </summary>
        private static readonly int EndPointUV = Shader.PropertyToID("_EndPointUV");

        /// <summary>
        ///  普通Image默认的横向细分段数。
        /// </summary>
        private const int DefaultHorizontalSegments = 24;

        /// <summary>
        ///  普通Image默认的纵向细分段数。
        /// </summary>
        private const int DefaultVerticalSegments = 16;

        /// <summary>
        ///  网格至少保留的单轴细分段数。
        /// </summary>
        private const int MinimumGridSegments = 4;

        /// <summary>
        ///  防止误配置产生过大Canvas网格的单轴上限。
        /// </summary>
        private const int MaximumGridSegments = 48;

        /// <summary>
        ///  每个细分段覆盖的最小本地尺寸，避免小图标生成无意义的过密网格。
        /// </summary>
        private const float MinimumLocalUnitsPerSegment = 3f;

        /// <summary>
        ///  方向和距离计算使用的最小有效长度。
        /// </summary>
        private const float MinimumFlowDistance = 0.001f;

        /// <summary>
        ///  S曲线控制点沿起终点方向所在的标准比例。
        /// </summary>
        private const float CurveControlDistanceRatio = 0.3f;

        /// <summary>
        ///  S曲线控制点垂直偏移占路径长度的安全上限。
        /// </summary>
        private const float MaximumCurveBendRatio =
            VertexFlowPath2D.DefaultMaximumBendRatio;

        /// <summary>
        ///  横向最多生成的细分段数。
        /// </summary>
        [SerializeField, Range(MinimumGridSegments, MaximumGridSegments)]
        private int horizontalSegments = DefaultHorizontalSegments;

        /// <summary>
        ///  纵向最多生成的细分段数。
        /// </summary>
        [SerializeField, Range(MinimumGridSegments, MaximumGridSegments)]
        private int verticalSegments = DefaultVerticalSegments;

        /// <summary>
        ///  背离终点的顶点相对前沿顶点的空间相位落后强度。
        /// </summary>
        [SerializeField, Range(0f, 0.75f)]
        private float flowDelay = 0.42f;

        /// <summary>
        ///  S曲线控制点垂直偏移占起终点距离的比例。
        /// </summary>
        [SerializeField, Range(0f, MaximumCurveBendRatio)]
        private float curveBend = 0.24f;

        /// <summary>
        ///  S曲线控制点垂直偏移的本地坐标上限。
        /// </summary>
        [SerializeField, Min(0f)]
        private float maximumCurveBend = 140f;

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
        ///  调用方写入的线性动效进度。
        /// </summary>
        [SerializeField, Range(0f, 1f)]
        private float effectAmount;

        /// <summary>
        ///  旧版序列化数据保留的原始顶点位置。
        /// </summary>
        [SerializeField, HideInInspector]
        private Vector3[] originVerticesOS;

        /// <summary>
        ///  旧版序列化数据保留的顶点距离顺序。
        /// </summary>
        [SerializeField, HideInInspector]
        private int[] distanceIndex;

        /// <summary>
        ///  曲面最终收束位置的屏幕坐标。
        /// </summary>
        [SerializeField]
        private Vector3 endScreenPoint;

        /// <summary>
        ///  当前根Canvas用于屏幕坐标转换的相机。
        /// </summary>
        [SerializeField, HideInInspector]
        private Camera uiCamera;

        /// <summary>
        ///  当前Graphic对应的CanvasRenderer。
        /// </summary>
        [SerializeField, HideInInspector]
        private CanvasRenderer render;

        /// <summary>
        ///  当前组件负责修改的Graphic。
        /// </summary>
        [SerializeField, HideInInspector]
        private Graphic graphic;

        /// <summary>
        ///  当前物体上的TMP文本组件；普通Image保持为空。
        /// </summary>
        [NonSerialized]
        private TMP_Text textTMP;

        /// <summary>
        ///  当前已经挂接渲染前事件的TMP文本，单独保存以保证取消订阅准确。
        /// </summary>
        [NonSerialized]
        private TMP_Text subscribedTMPText;

        /// <summary>
        ///  当前组件独占并负责销毁的运行时材质实例。
        /// </summary>
        [NonSerialized]
        private Material material;

        /// <summary>
        ///  Graphic进入动效前使用的材质，便于组件销毁时恢复。
        /// </summary>
        [NonSerialized]
        private Material originalMaterial;

        /// <summary>
        ///  当前材质是否由本组件克隆并负责销毁。
        /// </summary>
        private bool ownsMaterial;

        /// <summary>
        ///  缓存Graphic本轮生成的未变形顶点，避免细分和形变阶段重复读取。
        /// </summary>
        private readonly List<UIVertex> sourceVertices =
            new(DefaultHorizontalSegments * DefaultVerticalSegments);

        /// <summary>
        ///  动画材质预制体。
        /// </summary>
        /// <remarks>
        ///  继续保留原有字段名，兼容已有Prefab和<see cref="VertexUVFlowController"/>赋值。
        /// </remarks>
        public Material materialPrefab;

        /// <summary>
        ///  获取或设置曲面最终收束位置的屏幕坐标。
        /// </summary>
        public Vector3 EndScreenPoint
        {
            get => endScreenPoint;
            set
            {
                // 保存屏幕像素终点，Overlay和Camera Canvas都统一从该空间开始转换。
                endScreenPoint = value;

                // 终点变化会改变全部顶点的路径和漏斗朝向。
                MarkVerticesDirty();

                // 继续维护旧版距离数据，保证现有序列化与外部检查逻辑不失效。
                UpdateDistanceIndexFromCachedVertices();

                // 兼容材质仍可读取与终点方向一致的边缘UV。
                UpdateMaterialProperties();
            }
        }

        /// <summary>
        ///  获取或设置零到一之间的线性动效进度。
        /// </summary>
        /// <remarks>
        ///  调用方应使用线性Tween；组件会把该值通过一次SmoothStep转换为最终曲线进度。
        /// </remarks>
        public float EffectAmount
        {
            get => effectAmount;
            set
            {
                // 对异常调用做边界保护，序列化字段始终保持标准化线性语义。
                effectAmount = Mathf.Clamp01(value);

                // 交给Graphic标准重建流程，避免直接SetMesh破坏Mask或被下一次重建覆盖。
                MarkVerticesDirty();

                // Shader与CPU曲面必须共享同一个已经缓动的进度。
                UpdateMaterialProperties();
            }
        }

        /// <summary>
        ///  使用当前组件序列化的弯曲参数创建一条可逆二维S曲线。
        /// </summary>
        /// <param name="startPoint">调用方坐标空间中的路径起点。</param>
        /// <param name="targetPoint">与起点处于同一坐标空间的路径终点。</param>
        /// <returns>旧漏斗网格、刚性主体和Ribbon可以共同采样的不可变路径。</returns>
        public VertexFlowPath2D CreatePath(
            Vector2 startPoint,
            Vector2 targetPoint)
        {
            // 曲线数学集中在VertexFlowPath2D，禁止不同表现层各自维护另一套控制点公式。
            return VertexFlowPath2D.CreateReversibleS(
                startPoint,
                targetPoint,
                CurveControlDistanceRatio,
                curveBend,
                maximumCurveBend,
                MaximumCurveBendRatio);
        }

        private void Awake()
        {
            // 运行时动态创建和旧Prefab都从同一入口补全必要引用。
            InitializeReferences();

            // 每个动效图标需要独占材质参数，禁止多个动画互相覆盖进度。
            InitializeRuntimeMaterial();

            // Unity 6的TMP不经过Graphic网格修改器，必须使用其正式渲染前事件修改全部材质网格。
            SubscribeTMPPreRenderText();

            // 首帧由Graphic重新生成一次连续网格。
            MarkVerticesDirty();
        }

        private void OnEnable()
        {
            // 组件重新启用或池化图标激活时重新解析可能变化的Canvas层级。
            InitializeReferences();

            // 池化TMP重新启用后恢复唯一一次渲染前订阅。
            SubscribeTMPPreRenderText();

            // 重新启用后必须让Graphic抛弃上一次动效末帧。
            MarkVerticesDirty();
        }

        private void OnDisable()
        {
            // 停用期间不允许TMP事件继续修改其他系统触发的文字重建结果。
            UnsubscribeTMPPreRenderText();

            // IMeshModifier停用后标脏，Graphic下一次启用时会恢复自身原始网格。
            if (graphic) graphic.SetVerticesDirty();
        }

        private void OnDestroy()
        {
            // 销毁前无条件解除TMP事件，禁止委托持有已经销毁的VertexUVFlow实例。
            UnsubscribeTMPPreRenderText();

            // 组件被单独移除时先把Graphic恢复为进入动效前的材质。
            if (graphic && ownsMaterial && graphic.material == material)
                graphic.material = originalMaterial;

            // 只销毁当前组件实际创建的材质实例，禁止误删项目资产。
            if (ownsMaterial && material)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }

            // 清空所有权，防止销毁阶段的迟到回调再次访问材质。
            material = null;
            originalMaterial = null;
            ownsMaterial = false;
        }

        private void OnCanvasHierarchyChanged()
        {
            // 工具影子运行时会换到嵌套排序Canvas，必须按新的rootCanvas重新取相机。
            RefreshUICamera();

            // 新Canvas拥有独立的Graphic重建时机，立即标脏以提交正确坐标曲面。
            MarkVerticesDirty();
        }

        private void OnTransformParentChanged()
        {
            // 换父后Graphic引用不变，但Canvas和相机语义可能已经完全不同。
            InitializeReferences();

            // 新父节点下重新生成屏幕终点对应的本地网格。
            MarkVerticesDirty();
        }

        private void OnValidate()
        {
            // 编辑器输入统一限制在实现可承受的网格范围内。
            horizontalSegments = Mathf.Clamp(
                horizontalSegments, MinimumGridSegments, MaximumGridSegments);
            verticalSegments = Mathf.Clamp(
                verticalSegments, MinimumGridSegments, MaximumGridSegments);
            flowDelay = Mathf.Clamp(flowDelay, 0f, 0.75f);
            curveBend = Mathf.Clamp(curveBend, 0f, MaximumCurveBendRatio);
            maximumCurveBend = Mathf.Max(0f, maximumCurveBend);
            funnelPower = Mathf.Clamp(funnelPower, 0.2f, 2f);
            fadeStart = Mathf.Clamp(fadeStart, 0f, 0.99f);
            effectAmount = Mathf.Clamp01(effectAmount);

            // Prefab编辑阶段允许没有Canvas，引用入口会按Overlay语义安全降级。
            InitializeReferences();

            // 编辑器校验只刷新网格，不克隆运行时材质对象。
            MarkVerticesDirty();
        }

        /// <summary>
        ///  使用兼容旧Unity版本的Mesh入口修改Graphic网格。
        /// </summary>
        /// <param name="mesh">Graphic生成并等待修改的网格。</param>
        public void ModifyMesh(Mesh mesh)
        {
            // 空网格没有可供细分或形变的数据。
            if (!mesh) return;

            // VertexHelper保证位置、UV、颜色和索引通道按UGUI格式一起读写。
            using var vertexHelper = new VertexHelper(mesh);

            // 两个IMeshModifier入口复用完全相同的连续曲面逻辑。
            ModifyMesh(vertexHelper);

            // 将修改后的全部顶点通道和拓扑写回Graphic提供的Mesh。
            vertexHelper.FillMesh(mesh);
        }

        /// <summary>
        ///  将Graphic原始网格修改为连续的贝塞尔漏斗曲面。
        /// </summary>
        /// <param name="vertexHelper">Graphic本帧重新生成的可靠源网格。</param>
        public void ModifyMesh(VertexHelper vertexHelper)
        {
            // 停用状态必须保留Graphic自身网格，保证组件开关和遮罩重建稳定。
            if (!isActiveAndEnabled || vertexHelper == null || vertexHelper.currentVertCount == 0)
                return;

            // 动态AddComponent或反序列化恢复时允许网格回调早于常规引用缓存。
            if (!graphic) InitializeReferences();

            // Unity 6的TMP由OnPreRenderText专用路径处理，禁止未来版本意外重复形变。
            if (graphic is TMP_Text) return;

            // 零进度直接保留Graphic原始网格，避免静止UI承担额外顶点成本。
            if (effectAmount <= 0f) return;

            // 每次都从Graphic当帧提供的未变形网格开始，禁止在旧形变结果上累计误差。
            CacheSourceVertices(vertexHelper);
            if (sourceVertices.Count == 0) return;

            // 标准Simple Image的四顶点可以安全重建为高细分连续曲面。
            if (CanBuildSubdividedImageGrid())
            {
                BuildSubdividedImageGrid(vertexHelper);
                return;
            }

            // TMP、Filled/Sliced Image和自定义Graphic保留原拓扑，但全部已有顶点仍参与流动。
            DeformExistingVertices(vertexHelper);
        }

        /// <summary>
        ///  订阅当前TMP文本的正式渲染前网格事件。
        /// </summary>
        /// <remarks>
        ///  Unity 6的TextMeshProUGUI直接上传自身meshInfo，不会进入Graphic的IMeshModifier流程。
        /// </remarks>
        private void SubscribeTMPPreRenderText()
        {
            // 动态组件和反序列化恢复都重新取得当前物体上的TMP引用。
            var currentTMPText = textTMP ? textTMP : GetComponent<TMP_Text>();

            // 已经订阅同一个TMP实例时直接复用，防止Awake和OnEnable造成重复回调。
            if (currentTMPText == subscribedTMPText) return;

            // TMP组件被替换时先从旧实例移除委托，再处理新实例。
            UnsubscribeTMPPreRenderText();
            textTMP = currentTMPText;

            // 普通Image和停用组件不需要持有任何TMP事件。
            if (!isActiveAndEnabled || !textTMP) return;

            // 先移除一次同名委托可以兼容脚本重载后遗留的重复订阅状态。
            textTMP.OnPreRenderText -= HandleTMPPreRenderText;

            // 在TMP把主网格和全部子材质网格上传CanvasRenderer之前修改顶点数组。
            textTMP.OnPreRenderText += HandleTMPPreRenderText;
            subscribedTMPText = textTMP;
        }

        /// <summary>
        ///  解除当前VertexUVFlow持有的TMP渲染前事件。
        /// </summary>
        private void UnsubscribeTMPPreRenderText()
        {
            // 先清空字段所有权，事件移除期间发生的生命周期回调不会再次命中旧实例。
            var previousTMPText = subscribedTMPText;
            subscribedTMPText = null;

            // TMP已经随GameObject销毁时事件本身也会释放，无需访问失效原生对象。
            if (!previousTMPText) return;

            // 精确移除本组件的方法，其他TMP效果订阅者保持不变。
            previousTMPText.OnPreRenderText -= HandleTMPPreRenderText;
        }

        /// <summary>
        ///  在TMP上传网格前让全部材质和全部字形顶点参与同一贝塞尔漏斗形变。
        /// </summary>
        /// <param name="textInfo">TMP本轮刚生成、尚未上传到CanvasRenderer的完整文本网格信息。</param>
        private void HandleTMPPreRenderText(TMP_TextInfo textInfo)
        {
            // 停用、销毁过渡和零进度都必须原样保留TMP刚生成的SDF网格。
            if (!isActiveAndEnabled || effectAmount <= 0f || textInfo == null) return;

            // 没有材质网格时不存在可以计算边界或修改的文字顶点。
            var meshInfos = textInfo.meshInfo;
            if (meshInfos == null || meshInfos.Length == 0) return;

            // 只处理TMP本轮声明为有效的材质数量，忽略容量数组中的历史网格。
            var materialCount = Mathf.Clamp(textInfo.materialCount, 0, meshInfos.Length);
            if (materialCount == 0) return;

            // 所有材质必须先共同计算一次整体边界，主文字和Fallback/Sprite子网格才能流向同一中心。
            if (!TryCalculateTMPBounds(meshInfos, materialCount, out var sourceBounds)) return;

            // 复用Image与复杂Graphic完全相同的屏幕终点、时间曲线和贝塞尔漏斗上下文。
            var flowContext = CreateFlowContext(sourceBounds);

            // 逐材质处理TMP随后会上传的真实顶点数组，包含主字体、Fallback字体和Sprite网格。
            for (var materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                var meshInfo = meshInfos[materialIndex];
                var validVertexCount = GetValidTMPVertexCount(meshInfo);
                if (validVertexCount == 0) continue;

                // TMP_MeshInfo是结构体，但顶点和颜色字段均为引用数组，原地修改会保留到上传阶段。
                var vertices = meshInfo.vertices;
                var colors = meshInfo.colors32;
                for (var vertexIndex = 0; vertexIndex < validVertexCount; vertexIndex++)
                {
                    // 每个字形四角和每个材质子网格都复用现有连续曲面位置公式。
                    vertices[vertexIndex] = DeformPosition(vertices[vertexIndex], flowContext);

                    // 标准TMP SDF材质没有流体Shader进度时，通过现有顶点Alpha完成尾段柔和消失。
                    if (colors != null && vertexIndex < colors.Length)
                        colors[vertexIndex] = ApplyFade(
                            colors[vertexIndex],
                            flowContext.Progress,
                            flowContext.UseVertexFade);
                }
            }
        }

        /// <summary>
        ///  使用全部有效TMP材质顶点计算统一的本地二维边界。
        /// </summary>
        /// <param name="meshInfos">TMP本轮生成的材质网格数组。</param>
        /// <param name="materialCount">数组中本轮有效的材质数量。</param>
        /// <param name="bounds">成功时返回全部字形共同占用的本地边界。</param>
        /// <returns>至少存在一个有效顶点时返回true。</returns>
        private static bool TryCalculateTMPBounds(
            TMP_MeshInfo[] meshInfos,
            int materialCount,
            out Rect bounds)
        {
            // 默认空边界只用于失败返回，成功路径会由第一个有效顶点初始化。
            bounds = default;
            var hasVertex = false;
            var minimum = Vector2.zero;
            var maximum = Vector2.zero;

            // 边界必须跨越全部材质，否则Fallback字形会围绕另一中心产生跳裂。
            for (var materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                var meshInfo = meshInfos[materialIndex];
                var validVertexCount = GetValidTMPVertexCount(meshInfo);
                for (var vertexIndex = 0; vertexIndex < validVertexCount; vertexIndex++)
                {
                    var vertex = meshInfo.vertices[vertexIndex];
                    var position = new Vector2(vertex.x, vertex.y);

                    // 第一个真实顶点负责初始化最小值和最大值，禁止零点把偏移文字边界错误扩大。
                    if (!hasVertex)
                    {
                        minimum = position;
                        maximum = position;
                        hasVertex = true;
                        continue;
                    }

                    minimum = Vector2.Min(minimum, position);
                    maximum = Vector2.Max(maximum, position);
                }
            }

            // 空文本和全部材质零顶点时保持TMP原始空网格。
            if (!hasVertex) return false;

            bounds = new Rect(minimum, maximum - minimum);
            return true;
        }

        /// <summary>
        ///  取得单个TMP材质网格中本轮可以安全访问的有效顶点数。
        /// </summary>
        /// <param name="meshInfo">需要检查的TMP材质网格。</param>
        /// <returns>同时受vertexCount和顶点数组容量约束的非负数量。</returns>
        private static int GetValidTMPVertexCount(TMP_MeshInfo meshInfo)
        {
            // 未分配顶点数组的材质槽位属于TMP容量占位，不参与边界和形变。
            if (meshInfo.vertices == null) return 0;

            // 防御异常第三方TMP扩展写入越界vertexCount，禁止影响整个Canvas重建。
            return Mathf.Clamp(meshInfo.vertexCount, 0, meshInfo.vertices.Length);
        }

        /// <summary>
        ///  请求Graphic提交当前原始网格，并维护旧版公开调用需要的顶点缓存。
        /// </summary>
        public void UpdateOriginVertices()
        {
            // 运行时动态创建组件时补全OnValidate通常负责保存的引用。
            if (!graphic || !render) InitializeReferences();

            // 缺少必要图形组件时没有可以记录的UI网格。
            if (!graphic || !render) return;

            // 请求标准Graphic流程生成当前Sprite或文字对应的可靠源网格。
            graphic.SetVerticesDirty();

            // 当前调用由旧API约定需要立即取得结果，因此主动提交一次Canvas重建。
            Canvas.ForceUpdateCanvases();

            // CanvasRenderer保存Graphic最终提交的网格，空结果在下一帧可自然重试。
            var mesh = render.GetMesh();
            if (!mesh) return;

            // 复制顶点只用于兼容旧序列化和距离排序，不参与逐帧曲面生成。
            originVerticesOS = Array.ConvertAll(mesh.vertices, vertex => vertex);

            // 原始顶点更新后同步维护旧距离顺序。
            UpdateDistanceIndexFromCachedVertices();
        }

        /// <summary>
        ///  缓存Graphic当前未变形网格的全部顶点通道。
        /// </summary>
        /// <param name="vertexHelper">Graphic生成的源网格。</param>
        private void CacheSourceVertices(VertexHelper vertexHelper)
        {
            // List只清除逻辑长度，容量会在后续动画帧复用。
            sourceVertices.Clear();

            // UIVertex同时保存Sprite UV、颜色、法线和切线，细分时必须完整保留。
            for (var index = 0; index < vertexHelper.currentVertCount; index++)
            {
                var vertex = default(UIVertex);
                vertexHelper.PopulateUIVertex(ref vertex, index);
                sourceVertices.Add(vertex);
            }
        }

        /// <summary>
        ///  判断当前Graphic是否可以按标准四角Image生成规则细分。
        /// </summary>
        /// <returns>可以安全重建四边形拓扑时返回true。</returns>
        private bool CanBuildSubdividedImageGrid()
        {
            // TMP和自定义Graphic必须保留自身字形或业务拓扑。
            if (!(graphic is Image image)) return false;

            // Sliced、Tiled和Filled Image拥有额外语义，不能替换成普通矩形网格。
            if (image.type != Image.Type.Simple) return false;

            // 只有标准四角源网格可以通过双线性插值得到无缝Sprite UV。
            return sourceVertices.Count == 4;
        }

        /// <summary>
        ///  将标准Image四边形重建为高细分连续网格并应用漏斗曲面。
        /// </summary>
        /// <param name="vertexHelper">等待替换拓扑的Graphic网格。</param>
        private void BuildSubdividedImageGrid(VertexHelper vertexHelper)
        {
            // 从真实源顶点而非RectTransform推导边界，完整支持preserveAspect和Sprite裁边。
            var sourceBounds = CalculateBounds(sourceVertices);
            if (sourceBounds.width <= Mathf.Epsilon || sourceBounds.height <= Mathf.Epsilon) return;

            // 按空间角点查找源顶点，避免依赖Unity版本可能变化的四顶点排列顺序。
            var bottomLeft = FindClosestVertex(new Vector2(sourceBounds.xMin, sourceBounds.yMin));
            var topLeft = FindClosestVertex(new Vector2(sourceBounds.xMin, sourceBounds.yMax));
            var topRight = FindClosestVertex(new Vector2(sourceBounds.xMax, sourceBounds.yMax));
            var bottomRight = FindClosestVertex(new Vector2(sourceBounds.xMax, sourceBounds.yMin));

            // 小图标按本地尺寸自动减少细分，大图标最多使用24×16默认上限。
            var resolvedHorizontalSegments = ResolveSegmentCount(horizontalSegments, sourceBounds.width);
            var resolvedVerticalSegments = ResolveSegmentCount(verticalSegments, sourceBounds.height);

            // 同一帧全部顶点共享路径上下文，避免逐顶点重复做Canvas和贝塞尔准备计算。
            var flowContext = CreateFlowContext(sourceBounds);

            // 清除原四边形，随后一次性写入连续网格和规则索引。
            vertexHelper.Clear();

            // 先生成全部网格顶点，UV和颜色通过四角双线性插值保持连续。
            for (var y = 0; y <= resolvedVerticalSegments; y++)
            {
                var verticalRatio = y / (float)resolvedVerticalSegments;
                for (var x = 0; x <= resolvedHorizontalSegments; x++)
                {
                    var horizontalRatio = x / (float)resolvedHorizontalSegments;
                    var vertex = BilinearInterpolate(
                        bottomLeft,
                        topLeft,
                        topRight,
                        bottomRight,
                        horizontalRatio,
                        verticalRatio);

                    // 每个细分顶点都沿同一屏幕终点方向参与贝塞尔漏斗形变。
                    vertex.position = DeformPosition(vertex.position, flowContext);

                    // 尾段使用顶点Alpha柔和收口，避免终点残留一个高亮像素。
                    vertex.color = ApplyFade(
                        vertex.color,
                        flowContext.Progress,
                        flowContext.UseVertexFade);
                    vertexHelper.AddVert(vertex);
                }
            }

            // 规则网格每个单元使用相同绕序的两个三角形，避免出现裂缝或棋盘翻面。
            var verticesPerRow = resolvedHorizontalSegments + 1;
            for (var y = 0; y < resolvedVerticalSegments; y++)
            {
                for (var x = 0; x < resolvedHorizontalSegments; x++)
                {
                    var bottomLeftIndex = y * verticesPerRow + x;
                    var bottomRightIndex = bottomLeftIndex + 1;
                    var topLeftIndex = bottomLeftIndex + verticesPerRow;
                    var topRightIndex = topLeftIndex + 1;

                    vertexHelper.AddTriangle(bottomLeftIndex, topLeftIndex, topRightIndex);
                    vertexHelper.AddTriangle(bottomLeftIndex, topRightIndex, bottomRightIndex);
                }
            }
        }

        /// <summary>
        ///  在不替换拓扑的情况下让复杂Graphic的全部顶点参与形变。
        /// </summary>
        /// <param name="vertexHelper">TMP或复杂Image生成的原始网格。</param>
        private void DeformExistingVertices(VertexHelper vertexHelper)
        {
            // 复杂图形沿自身真实顶点范围建立同一漏斗路径。
            var sourceBounds = CalculateBounds(sourceVertices);
            if (sourceBounds.width <= Mathf.Epsilon && sourceBounds.height <= Mathf.Epsilon) return;

            // 同一Graphic内所有字形和顶点共享终点及曲线，避免字形间出现方向跳变。
            var flowContext = CreateFlowContext(sourceBounds);
            for (var index = 0; index < vertexHelper.currentVertCount; index++)
            {
                var vertex = default(UIVertex);
                vertexHelper.PopulateUIVertex(ref vertex, index);
                vertex.position = DeformPosition(vertex.position, flowContext);
                vertex.color = ApplyFade(
                    vertex.color,
                    flowContext.Progress,
                    flowContext.UseVertexFade);
                vertexHelper.SetUIVertex(vertex, index);
            }
        }

        /// <summary>
        ///  根据源边界、屏幕终点和当前进度创建本帧共享的路径参数。
        /// </summary>
        /// <param name="sourceBounds">Graphic原始本地边界。</param>
        /// <returns>全部顶点共用的不可变形变上下文。</returns>
        private FlowContext CreateFlowContext(Rect sourceBounds)
        {
            // 屏幕终点统一转换到当前Graphic本地坐标，避免Canvas缩放混入曲面计算。
            var sourceCenter = sourceBounds.center;
            var targetPoint = GetEndPointOS2D();

            // 旧漏斗与新刚性主体、Ribbon共享完全相同的控制点、导数和反向路径语义。
            var path = CreatePath(sourceCenter, targetPoint);
            var targetDirection = path.ChordDirection;
            var targetDistance = path.ChordLength;

            // 投影半径覆盖矩形在目标方向上的完整半宽，用于计算前沿和尾沿相位。
            var sourceExtents = sourceBounds.size * 0.5f;
            var projectionRadius = Mathf.Abs(targetDirection.x) * sourceExtents.x +
                                   Mathf.Abs(targetDirection.y) * sourceExtents.y;

            // 线性输入只在这里通过一次SmoothStep，CPU和Shader后续都复用该值。
            var progress = EvaluateFlowProgress(effectAmount);

            // 配置了新版动效Shader时由Shader完成一次淡出，TMP或无材质降级路径才使用顶点Alpha。
            var useVertexFade = !HasMaterialProgressProperty();

            return new FlowContext(
                sourceCenter,
                targetPoint,
                targetDirection,
                targetDistance,
                projectionRadius,
                path,
                progress,
                useVertexFade);
        }

        /// <summary>
        ///  计算单个顶点在当前贝塞尔漏斗曲面上的位置。
        /// </summary>
        /// <param name="originPosition">Graphic提供的原始本地顶点。</param>
        /// <param name="flowContext">本帧共享路径上下文。</param>
        /// <returns>保持原Z值的变形后本地顶点。</returns>
        private Vector3 DeformPosition(Vector3 originPosition, FlowContext flowContext)
        {
            // 起终点重合时仍平滑收束，避免除零和方向抖动。
            if (flowContext.TargetDistance <= MinimumFlowDistance)
            {
                var coincidentPosition = Vector2.Lerp(
                    new Vector2(originPosition.x, originPosition.y),
                    flowContext.TargetPoint,
                    flowContext.Progress);
                return new Vector3(coincidentPosition.x, coincidentPosition.y, originPosition.z);
            }

            // 将顶点偏移拆成沿目标方向和垂直目标方向的两个分量。
            var sourceOffset =
                new Vector2(originPosition.x, originPosition.y) - flowContext.SourceCenter;
            var projectedOffset = Vector2.Dot(sourceOffset, flowContext.TargetDirection);

            // 越靠近终点的前沿顶点相位越快，整张网格形成连续拖尾而不是单角尖刺。
            var frontFactor = flowContext.ProjectionRadius > MinimumFlowDistance
                ? Mathf.Clamp01(
                    (projectedOffset + flowContext.ProjectionRadius) /
                    (flowContext.ProjectionRadius * 2f))
                : 0.5f;

            // 幂相位保证所有顶点从第一帧起连续移动，并在终点帧严格同时收束。
            var phaseExponent = 1f + (1f - frontFactor) * flowDelay * 3f;
            var localProgress = Mathf.Pow(flowContext.Progress, phaseExponent);

            // 曲面中心沿屏幕方向的三次贝塞尔路径前进，避免直线缩放的机械感。
            var pathCenter = flowContext.Path.EvaluatePosition(localProgress);

            // 纵向长度和横向宽度分别收束，横向使用较软幂曲线形成漏斗截面。
            var longitudinalOffset = flowContext.TargetDirection * projectedOffset;
            var transverseOffset = sourceOffset - longitudinalOffset;
            var transverseScale = Mathf.Pow(
                Mathf.Max(0f, 1f - localProgress),
                funnelPower);
            var longitudinalScale = 1f - localProgress;

            // 所有顶点最终严格落在同一目标点，同时保留动画期间的连续面拓扑。
            var deformedPosition = pathCenter +
                                   longitudinalOffset * longitudinalScale +
                                   transverseOffset * transverseScale;
            return new Vector3(deformedPosition.x, deformedPosition.y, originPosition.z);
        }

        /// <summary>
        ///  对线性输入应用组件唯一的一次时间缓动。
        /// </summary>
        /// <param name="linearProgress">调用方提供的零到一线性进度。</param>
        /// <returns>首尾速度为零的平滑进度。</returns>
        public static float EvaluateFlowProgress(float linearProgress)
        {
            // 三次SmoothStep没有额外弹性或过冲，便于与外部线性JTween稳定组合。
            var progress = Mathf.Clamp01(linearProgress);
            return progress * progress * (3f - 2f * progress);
        }

        /// <summary>
        ///  在动效尾段柔和降低顶点透明度。
        /// </summary>
        private Color32 ApplyFade(
            Color32 sourceColor,
            float progress,
            bool useVertexFade)
        {
            // 新版流体Shader已经使用同一进度淡出，CPU不能再叠乘第二次透明曲线。
            if (!useVertexFade) return sourceColor;

            // 尾段以前完整保留Graphic原始透明度和软边。
            if (progress <= fadeStart) return sourceColor;

            // 透明度再次使用标准平滑插值，但不改变几何时间进度。
            var fadeProgress = Mathf.InverseLerp(fadeStart, 1f, progress);
            var opacity = 1f - fadeProgress * fadeProgress * (3f - 2f * fadeProgress);
            sourceColor.a = (byte)Mathf.Clamp(
                Mathf.RoundToInt(sourceColor.a * opacity),
                byte.MinValue,
                byte.MaxValue);
            return sourceColor;
        }

        /// <summary>
        ///  根据图形本地尺寸解析实际细分段数。
        /// </summary>
        private static int ResolveSegmentCount(int configuredSegments, float localSize)
        {
            // 小尺寸图形自动减面，大尺寸图形受配置上限保护。
            var sizeBasedSegments =
                Mathf.CeilToInt(Mathf.Abs(localSize) / MinimumLocalUnitsPerSegment);
            return Mathf.Clamp(sizeBasedSegments, MinimumGridSegments, configuredSegments);
        }

        /// <summary>
        ///  计算一组UI顶点在Graphic本地坐标中的轴对齐边界。
        /// </summary>
        private static Rect CalculateBounds(IReadOnlyList<UIVertex> vertices)
        {
            var minimum = new Vector2(vertices[0].position.x, vertices[0].position.y);
            var maximum = minimum;
            for (var index = 1; index < vertices.Count; index++)
            {
                var position =
                    new Vector2(vertices[index].position.x, vertices[index].position.y);
                minimum = Vector2.Min(minimum, position);
                maximum = Vector2.Max(maximum, position);
            }

            return new Rect(minimum, maximum - minimum);
        }

        /// <summary>
        ///  查找最接近指定空间角点的源顶点。
        /// </summary>
        private UIVertex FindClosestVertex(Vector2 cornerPosition)
        {
            var closestVertex = sourceVertices[0];
            var closestDistance =
                (new Vector2(closestVertex.position.x, closestVertex.position.y) -
                 cornerPosition).sqrMagnitude;

            for (var index = 1; index < sourceVertices.Count; index++)
            {
                var vertex = sourceVertices[index];
                var distance =
                    (new Vector2(vertex.position.x, vertex.position.y) -
                     cornerPosition).sqrMagnitude;
                if (distance >= closestDistance) continue;

                closestVertex = vertex;
                closestDistance = distance;
            }

            return closestVertex;
        }

        /// <summary>
        ///  双线性插值四角UI顶点的全部通道。
        /// </summary>
        private static UIVertex BilinearInterpolate(
            UIVertex bottomLeft,
            UIVertex topLeft,
            UIVertex topRight,
            UIVertex bottomRight,
            float horizontalRatio,
            float verticalRatio)
        {
            var bottom = LerpVertex(bottomLeft, bottomRight, horizontalRatio);
            var top = LerpVertex(topLeft, topRight, horizontalRatio);
            return LerpVertex(bottom, top, verticalRatio);
        }

        /// <summary>
        ///  插值两个UI顶点的位置、颜色、全部UV、法线和切线。
        /// </summary>
        private static UIVertex LerpVertex(UIVertex start, UIVertex end, float progress)
        {
            var vertex = start;
            vertex.position = Vector3.LerpUnclamped(start.position, end.position, progress);
            vertex.color = Color32.Lerp(start.color, end.color, progress);
            vertex.uv0 = Vector4.LerpUnclamped(start.uv0, end.uv0, progress);
            vertex.uv1 = Vector4.LerpUnclamped(start.uv1, end.uv1, progress);
            vertex.uv2 = Vector4.LerpUnclamped(start.uv2, end.uv2, progress);
            vertex.uv3 = Vector4.LerpUnclamped(start.uv3, end.uv3, progress);
            vertex.normal = Vector3.LerpUnclamped(start.normal, end.normal, progress);
            vertex.tangent = Vector4.LerpUnclamped(start.tangent, end.tangent, progress);
            return vertex;
        }

        /// <summary>
        ///  将屏幕终点转换为当前Graphic的本地二维坐标。
        /// </summary>
        private Vector2 GetEndPointOS2D()
        {
            var rectTransform = transform as RectTransform;
            if (!rectTransform) return Vector2.zero;

            // 嵌套Canvas换父后始终重新核对rootCanvas相机语义。
            RefreshUICamera();

            // Overlay传空相机，Camera和World Canvas传根Canvas相机，统一得到本地坐标。
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    new Vector2(endScreenPoint.x, endScreenPoint.y),
                    uiCamera,
                    out var localPoint))
                return localPoint;

            // 相机射线无法命中当前平面时保留矩形中心，禁止产生未初始化终点。
            return rectTransform.rect.center;
        }

        /// <summary>
        ///  使用兼容缓存重新计算到屏幕终点的旧版顶点距离顺序。
        /// </summary>
        private void UpdateDistanceIndexFromCachedVertices()
        {
            // 尚未通过UpdateOriginVertices取得缓存时无需主动强制Canvas重建。
            if (originVerticesOS == null || originVerticesOS.Length == 0) return;

            var endPointOS = GetEndPointOS2D();
            distanceIndex = originVerticesOS
                .Select((vertex, index) => new { vertex, index })
                .OrderBy(pair =>
                    (new Vector2(pair.vertex.x, pair.vertex.y) - endPointOS).sqrMagnitude)
                .Select(pair => pair.index)
                .ToArray();
        }

        /// <summary>
        ///  初始化Graphic、CanvasRenderer和根Canvas相机引用。
        /// </summary>
        private void InitializeReferences()
        {
            // VertexUVFlow只修改同一物体上的Graphic和CanvasRenderer。
            if (!graphic) graphic = GetComponent<Graphic>();
            if (!render) render = GetComponent<CanvasRenderer>();

            // TMP引用只用于专用OnPreRenderText路径，普通Image会稳定保持为空。
            textTMP = GetComponent<TMP_Text>();

            // 相机可能随运行时全屏特效层换父而变化，不能只在Awake缓存一次。
            RefreshUICamera();
        }

        /// <summary>
        ///  根据当前Graphic所属的根Canvas刷新屏幕坐标转换相机。
        /// </summary>
        private void RefreshUICamera()
        {
            // Graphic.canvas可正确返回运行时嵌套排序Canvas，缺失时再向父级查找。
            var ownerCanvas = graphic ? graphic.canvas : GetComponentInParent<Canvas>();
            if (!ownerCanvas)
            {
                uiCamera = null;
                return;
            }

            // 嵌套Canvas的renderMode字段不代表最终屏幕语义，必须以rootCanvas为准。
            var rootCanvas = ownerCanvas.rootCanvas ? ownerCanvas.rootCanvas : ownerCanvas;
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = null;
                return;
            }

            // ScreenSpaceCamera和WorldSpace优先使用根Canvas相机，缺失时才安全回退。
            uiCamera = rootCanvas.worldCamera ? rootCanvas.worldCamera : ownerCanvas.worldCamera;
            if (!uiCamera) uiCamera = Camera.main;
        }

        /// <summary>
        ///  在运行时克隆并安装当前组件独占的动效材质。
        /// </summary>
        private void InitializeRuntimeMaterial()
        {
            // 重复Awake或异常生命周期回调不得创建第二份材质。
            if (material || ownsMaterial) return;

            // TMP必须保留自己的SDF材质和字体图集；它只使用本组件的全顶点形变降级路径。
            if (graphic is TMP_Text)
            {
                originalMaterial = graphic.material;
                return;
            }

            // 未配置特效材质时仍允许CPU曲面正常工作，并给出明确降级提示。
            if (!materialPrefab)
            {
                Debug.LogWarning(
                    $"VertexUVFlow“{name}”未配置动效材质，将仅使用CPU连续曲面。",
                    this);
                return;
            }

            // 保存原材质，组件被单独移除时可以恢复Graphic状态。
            originalMaterial = graphic ? graphic.material : null;

            // 每个实例独占Shader进度，多个工具影子或面板不会相互污染。
            material = Instantiate(materialPrefab);
            material.name = $"{materialPrefab.name} ({name} Runtime)";
            material.hideFlags = HideFlags.HideAndDontSave;
            ownsMaterial = true;

            // Graphic会基于该材质继续生成Mask需要的Stencil变体。
            if (graphic)
            {
                graphic.material = material;
                graphic.SetMaterialDirty();
            }

            // 初始化实例的进度和兼容终点参数，避免显示材质资产中残留的预览值。
            UpdateMaterialProperties();
        }

        /// <summary>
        ///  同步CPU曲线进度到基础材质和Mask生成的实际渲染材质。
        /// </summary>
        private void UpdateMaterialProperties()
        {
            // 与几何共享唯一SmoothStep结果，禁止Shader和CPU出现两套时间缓动。
            var progress = EvaluateFlowProgress(effectAmount);
            var endpointUV = CalculateCompatibleEndPointUV();

            // 运行时优先写入组件独占材质；预览工具可使用自身HideAndDontSave材质。
            var baseMaterial = material ? material : graphic ? graphic.material : null;
            SetMaterialProperties(baseMaterial, progress, endpointUV);

            // Mask会生成Stencil材质副本，必须同步参数才能在裁剪层级内保持同一帧进度。
            if (!graphic) return;
            var renderingMaterial = graphic.materialForRendering;
            if (renderingMaterial && renderingMaterial != baseMaterial)
                SetMaterialProperties(renderingMaterial, progress, endpointUV);
        }

        /// <summary>
        ///  判断当前基础材质是否支持由Shader处理动效进度和尾段淡出。
        /// </summary>
        private bool HasMaterialProgressProperty()
        {
            // 运行时使用独占实例，预览和降级路径使用Graphic当前材质。
            var baseMaterial = material ? material : graphic ? graphic.material : null;
            return baseMaterial && baseMaterial.HasProperty(Amount);
        }

        /// <summary>
        ///  安全写入单个运行时或预览材质的动效参数。
        /// </summary>
        private static void SetMaterialProperties(
            Material targetMaterial,
            float progress,
            Vector2 endpointUV)
        {
            if (!targetMaterial) return;

            // 编辑态禁止修改持久化材质资产，但允许HideAndDontSave预览副本参与验证。
            if (!Application.isPlaying &&
                (targetMaterial.hideFlags & HideFlags.DontSave) == 0)
                return;

            if (targetMaterial.HasProperty(Amount))
                targetMaterial.SetFloat(Amount, progress);
            if (targetMaterial.HasProperty(EndPointUV))
                targetMaterial.SetVector(EndPointUV, endpointUV);
        }

        /// <summary>
        ///  将屏幕终点映射到源矩形最近边缘的零到一兼容UV。
        /// </summary>
        private Vector2 CalculateCompatibleEndPointUV()
        {
            var rectTransform = transform as RectTransform;
            if (!rectTransform) return Vector2.zero;

            var rect = rectTransform.rect;
            if (rect.width <= Mathf.Epsilon || rect.height <= Mathf.Epsilon) return Vector2.zero;

            var localPoint = GetEndPointOS2D();
            return new Vector2(
                Mathf.Clamp01(Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x)),
                Mathf.Clamp01(Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y)));
        }

        /// <summary>
        ///  请求Graphic在本帧标准Canvas流程中重新生成顶点。
        /// </summary>
        private void MarkVerticesDirty()
        {
            if (!graphic) graphic = GetComponent<Graphic>();

            // TMP只有在属性变化标记成立时才会重新GenerateTextMesh并触发OnPreRenderText。
            if (!textTMP) textTMP = GetComponent<TMP_Text>();
            if (textTMP) textTMP.havePropertiesChanged = true;

            // 保留标准SetVerticesDirty语义，普通Image和TMP都进入各自的Canvas重建队列。
            if (graphic) graphic.SetVerticesDirty();
        }

        /// <summary>
        ///  保存一帧内全部顶点共享的贝塞尔漏斗参数。
        /// </summary>
        private readonly struct FlowContext
        {
            /// <summary>
            ///  创建不可变的流动路径上下文。
            /// </summary>
            public FlowContext(
                Vector2 sourceCenter,
                Vector2 targetPoint,
                Vector2 targetDirection,
                float targetDistance,
                float projectionRadius,
                VertexFlowPath2D path,
                float progress,
                bool useVertexFade)
            {
                SourceCenter = sourceCenter;
                TargetPoint = targetPoint;
                TargetDirection = targetDirection;
                TargetDistance = targetDistance;
                ProjectionRadius = projectionRadius;
                Path = path;
                Progress = progress;
                UseVertexFade = useVertexFade;
            }

            /// <summary>
            ///  原始图形中心。
            /// </summary>
            public Vector2 SourceCenter { get; }

            /// <summary>
            ///  当前Graphic本地空间中的收束点。
            /// </summary>
            public Vector2 TargetPoint { get; }

            /// <summary>
            ///  从源中心指向终点的单位方向。
            /// </summary>
            public Vector2 TargetDirection { get; }

            /// <summary>
            ///  源中心到终点的本地距离。
            /// </summary>
            public float TargetDistance { get; }

            /// <summary>
            ///  原图形沿目标方向投影后的半径。
            /// </summary>
            public float ProjectionRadius { get; }

            /// <summary>
            ///  当前漏斗与刚性主体、Ribbon共同使用的不可变S曲线路径。
            /// </summary>
            public VertexFlowPath2D Path { get; }

            /// <summary>
            ///  已经应用组件唯一时间曲线的本帧进度。
            /// </summary>
            public float Progress { get; }

            /// <summary>
            ///  当前材质不支持动效淡出时是否使用顶点Alpha降级。
            /// </summary>
            public bool UseVertexFade { get; }
        }
    }
}
