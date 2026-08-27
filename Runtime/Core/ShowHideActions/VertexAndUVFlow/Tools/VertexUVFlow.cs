using System;
using System.Linq;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  顶点和UV流动动画管理器
    /// </summary>
    public class VertexUVFlow : MonoBehaviour
    {
        /// <summary>
        ///  特效进度
        /// </summary>
        private static readonly int Amount = Shader.PropertyToID("_EffectAmount");

        /// <summary>
        ///  消失点UV坐标
        /// </summary>
        private static readonly int EndPointUV = Shader.PropertyToID("_EndPointUV");

        /// <summary>
        ///  特效进度
        /// </summary>
        [SerializeField, Range(0f, 1f)] private float effectAmount;

        /// <summary>
        ///  原始顶点位置，模型空间
        /// </summary>
        [SerializeField, HideInInspector] private Vector3[] originVerticesOS;

        /// <summary>
        ///  根据到消失点距离从小到大的顺序排序的顶点索引
        /// </summary>
        [SerializeField, HideInInspector] private int[] distanceIndex;

        /// <summary>
        ///  消失点屏幕坐标
        /// </summary>
        [SerializeField] private Vector3 endScreenPoint;

        /// <summary>
        ///  消失点屏幕坐标
        /// </summary>
        public Vector3 EndScreenPoint
        {
            get => endScreenPoint;
            set
            {
                endScreenPoint = value;

                //消失点一旦调整，必须调整顶点距离排序
                UpdateDistanceIndex();
            }
        }

        /// <summary>
        ///  ui相机
        /// </summary>
        [SerializeField, HideInInspector] private Camera uiCamera;

        /// <summary>
        ///  渲染器
        /// </summary>
        [SerializeField, HideInInspector] private CanvasRenderer render;

        /// <summary>
        ///  图形
        /// </summary>
        [SerializeField, HideInInspector] private Graphic graphic;

        /// <summary>
        ///  动画材质
        /// </summary>
        [NonSerialized] private Material material;

        /// <summary>
        ///  运行时复用的顶点动效网格，避免每帧创建Native Mesh。
        /// </summary>
        private Mesh effectMesh;

        /// <summary>
        ///  当前material是否由本组件克隆并负责销毁。
        /// </summary>
        private bool ownsMaterial;

        /// <summary>
        ///  文本组件
        /// </summary>
        private TMPro.TMP_Text TextTMP => GetComponent<TMPro.TMP_Text>();

        /// <summary>
        ///  是否是TMP组件
        /// </summary>
        private bool IsTMP => TextTMP != null;

        /// <summary>
        ///  动画材质
        /// </summary>
        public Material materialPrefab;

        /// <summary>
        /// 用于判定是否需要更新顶点距离索引排序的哈希值
        /// </summary>
        private int _originHash;

        /// <summary>
        ///  动画材质
        /// </summary>
        private Material Material
        {
            get => material ??= graphic.material;
            set
            {
                material = value;

                // 更新材质到图形图像上
                graphic.material = Material;

                //更新材质后，需要重新设置特效进度
                EffectAmount = effectAmount;
            }
        }

        /// <summary>
        ///  特效进度
        /// </summary>
        public float EffectAmount
        {
            get => effectAmount;
            set
            {
                effectAmount = Mathf.Clamp01(value);

                // 更新顶点位置
                UpdateMeshEffectAmount();

                // 更新材质特效进度
                UpdateMaterialEffectAmount();
            }
        }

        private void Awake()
        {
            // 运行时动态创建或旧Prefab未保存OnValidate结果时重新取得必要组件。
            InitializeReferences();

            // 没有动效材质时禁止克隆空资源，并保留包含当前对象的明确错误。
            if (!materialPrefab)
            {
                Debug.LogError($"VertexUVFlow“{name}”未配置动效材质。", this);
                enabled = false;
                return;
            }

            //克隆材质，需要保证每个UI面板都有自己的材质，因为参数不同
            var runtimeMaterial = Instantiate(materialPrefab);

            //运行时材质只服务当前组件，禁止被编辑器意外写入场景或Prefab。
            runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;

            //将当前组件独占的材质交给Graphic渲染。
            Material = runtimeMaterial;
            ownsMaterial = true;

            // Graphic开始参与Canvas渲染后记录一次可靠的原始顶点和距离顺序。
            UpdateDistanceIndex();
        }

        private void Update()
        {
            // 在需要更新时，更新顶点距离索引排序
            if (NeedUpdateDistanceIndex()) UpdateDistanceIndex();
        }


        private void OnDestroy()
        {
            // 释放当前组件创建并复用的唯一运行时网格。
            if (effectMesh)
            {
                // 编辑器退出Prefab内容模式时必须立即销毁非持久临时对象。
                if (Application.isPlaying) Destroy(effectMesh);
                else DestroyImmediate(effectMesh);
            }

            // 只销毁当前组件实际克隆并持有的材质，销毁阶段禁止重新读取Graphic材质。
            if (ownsMaterial && material)
            {
                // 编辑器OnValidate创建的临时材质不能等待不存在的播放帧销毁。
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }

        private void OnValidate()
        {
            // 编辑器和运行时共用同一个引用初始化入口，并允许Prefab资源尚未处于Canvas下。
            InitializeReferences();

            //播放状态中的材质由Awake创建，校验时不能清除正在使用的运行时实例。
            if (Application.isPlaying) return;

            //编辑态只保存资源和组件引用，材质实例统一延迟到运行时创建。
            material = null;
            ownsMaterial = false;
        }

        /// <summary>
        ///  判定是否需要更新顶点距离索引排序，检测仅发生在特效进度为0的情况下
        ///  多种情况下检测：
        ///    1是模型顶点自身改变
        ///    2是模型世界空间位置改变
        ///    3是消失点屏幕坐标改变【这个情况在属性<see cref="EndScreenPoint"/>中已处理】
        /// </summary>
        private bool NeedUpdateDistanceIndex()
        {
            // 组件引用损坏或动态创建后尚未初始化时先尝试恢复一次。
            if (!render) InitializeReferences();

            // 没有CanvasRenderer时不存在可以检查的UI网格。
            if (!render) return false;

            //如果获取mesh为空，可能是因为canvas还没有更新
            var mesh = render.GetMesh();
            if (mesh == null)
            {
                // 强制更新
                Canvas.ForceUpdateCanvases();

                //再次获取mesh
                mesh = render.GetMesh();

                // 如果还是为空，直接返回不需要更新
                if (mesh == null) return false;
            }

            var currentHash = CalcHash(mesh);
            var ret = EffectAmount == 0 && currentHash != _originHash;
            // 更新哈希值
            if (ret) _originHash = currentHash;
            return ret;
        }

        /// <summary>
        /// 计算哈希值，用于判定模型顶点自身改变或者模型世界空间位置改变
        /// </summary>
        private int CalcHash(Mesh mesh)
        {
            var currentPositionWs = transform.position;
            var vertices = mesh.vertices;

            unchecked
            {
                int hash = 17;
                hash = hash * 23 + currentPositionWs.GetHashCode();

                // 遍历顶点计算哈希
                return vertices.Aggregate(hash, (current, t) => current * 23 + t.GetHashCode());
            }
        }

        /// <summary>
        ///  更新材质特效进度
        /// </summary>
        private void UpdateMaterialEffectAmount()
        {
            // 材质、顶点顺序或渲染器尚未准备完成时等待下一次网格刷新。
            if (!material || distanceIndex == null || distanceIndex.Length == 0 || !render) return;

            Material.SetFloat(Amount, effectAmount);
            var nearestIndex = distanceIndex[0];
            var mesh = render.GetMesh();
            if (mesh == null) return;
            var uv = mesh.uv;

            // 不完整网格没有可供材质采样的有效UV索引。
            if (uv == null || nearestIndex < 0 || nearestIndex >= uv.Length) return;

            Material.SetVector(EndPointUV, uv[nearestIndex]);
        }

        /// <summary>
        ///  更新顶点动画
        /// </summary>
        private void UpdateMeshEffectAmount()
        {
            // 原始顶点或距离顺序尚未建立时不能生成变形后的网格。
            if (originVerticesOS == null ||
                originVerticesOS.Length == 0 ||
                distanceIndex == null ||
                distanceIndex.Length == 0 ||
                !render)
                return;

            // Prefab校验阶段只保存参数和材质，不向尚未进入Canvas生命周期的网格提交变形。
            if (!Application.isPlaying) return;

            // 根据当前进度生成新的顶点位置
            var newVertices = GenerateNewVerticesOS();

            // 取得Graphic当前提交的源网格；Sprite或文字刷新后它会替换上一轮动效网格。
            var sourceMesh = render.GetMesh();
            if (sourceMesh == null) return;

            // 每个VertexUVFlow只创建一份动态网格并在全部动画帧之间复用。
            if (!effectMesh)
            {
                // HideAndDontSave禁止运行时临时网格被写入场景或Prefab资源。
                effectMesh = new Mesh
                {
                    name = $"{name} VertexUVFlow Mesh",
                    hideFlags = HideFlags.HideAndDontSave
                };

                // 动态标记提示渲染后端该网格的顶点会频繁变化。
                effectMesh.MarkDynamic();
            }

            // Graphic重新生成网格时一次性复制拓扑和全部顶点通道，平常帧不重复分配这些数组。
            if (sourceMesh != effectMesh)
            {
                // 清除上一种Sprite或文字结构后再复制当前真实UI网格。
                effectMesh.Clear();
                effectMesh.indexFormat = sourceMesh.indexFormat;
                effectMesh.vertices = sourceMesh.vertices;
                effectMesh.triangles = sourceMesh.triangles;
                effectMesh.uv = sourceMesh.uv;
                effectMesh.uv2 = sourceMesh.uv2;
                effectMesh.uv3 = sourceMesh.uv3;
                effectMesh.uv4 = sourceMesh.uv4;
                effectMesh.colors = sourceMesh.colors;
                effectMesh.normals = sourceMesh.normals;
                effectMesh.tangents = sourceMesh.tangents;
            }

            // 每帧只替换顶点位置，拓扑、UV、颜色和切线继续复用缓存网格中的数据。
            effectMesh.vertices = newVertices;

            // 顶点移动后刷新包围盒，避免流向原矩形外时被Canvas错误裁剪。
            effectMesh.RecalculateBounds();

            // 将唯一缓存网格重新提交给CanvasRenderer，不产生逐帧Native对象泄漏。
            render.SetMesh(effectMesh);

            // 当动效为0且是TMP_Text组件时，需要刷新TMP_Text组件
            if (effectAmount == 0 && IsTMP) TextTMP.ForceMeshUpdate();
        }

        /// <summary>
        ///  根据当前进度生成新的顶点位置
        /// </summary>
        private Vector3[] GenerateNewVerticesOS()
        {
            var newVertices = new Vector3[originVerticesOS.Length];
            for (var i = 0; i < originVerticesOS.Length; i++)
            {
                // 如果是TMP_Text组件，则所有顶点都需要偏移，因为TMP没有自定义材质，只能通过顶点偏移来实现动效
                // 否则只偏移距离最近的顶点
                if (!IsTMP)
                {
                    // 除了距离最近的顶点，其他顶点位置不变
                    if (i != distanceIndex[0])
                    {
                        newVertices[i] = originVerticesOS[i];
                        continue;
                    }
                }

                // 获取原始顶点位置
                var originVertexOS = originVerticesOS[i];

                // 获取目标顶点位置
                var vertexOS = GetEndPointOS();

                // 如果是TMP，多乘一个因子
                var tmpFactor = FancySurface(i, EffectAmount, originVerticesOS.Length - 1);
                var factor = IsTMP ? tmpFactor : 1;

                //根据当前特效进度，计算新的顶点位置
                newVertices[i] =
                    EaseFunctionNumeric.Tween(originVertexOS, vertexOS, Ease.InOutCirc, EffectAmount * factor);
            }

            return newVertices;
        }

        /// <summary>
        ///  生成TMP特效因子
        /// </summary>
        private float FancySurface(float a, float b, float n)
        {
            // 涟漪效果
            float ripple = Mathf.Sin(Mathf.Sqrt((a * a + b * b) * 10)) * 0.2f;

            // 螺旋波
            float spiral = Mathf.Sin(a * b * Mathf.PI * 2 +
                                     Mathf.Atan2(b, a) * 3) * 0.25f;

            // 边界混合
            float edgeBlend = (1 - b) * (1 - a / n);

            // 指数衰减
            float decay = Mathf.Exp(-(a * a / (n * n) + b * b));

            // 确保边界条件
            float boundaryControl = Mathf.Lerp(
                1, // 起始值
                1 + (ripple + spiral) * decay, // 中间变化
                edgeBlend // 混合因子
            );

            return boundaryControl;
        }

        /// <summary>
        ///  获取消失点在模型空间的位置
        /// </summary>
        private Vector3 GetEndPointOS()
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                transform as RectTransform, EndScreenPoint, uiCamera, out var vertexWs);

            return transform.InverseTransformPoint(vertexWs);
        }

        /// <summary>
        ///  更新原始顶点
        /// </summary>
        public void UpdateOriginVertices()
        {
            // 运行时动态创建组件时补全OnValidate通常负责保存的引用。
            if (!graphic || !render) InitializeReferences();

            // 缺少必要图形组件时没有可供记录的原始网格。
            if (!graphic || !render) return;

            //关闭graphic 
            if (graphic) graphic.enabled = false;

            //打开graphic，此时会刷新mesh
            if (graphic) graphic.enabled = true;

            // 强制更新布局
            Canvas.ForceUpdateCanvases();

            // 记录原始Mesh信息
            var mesh = render.GetMesh();

            // 如果获取不到mesh，直接返回
            if (mesh == null) return;

            // 获取原始顶点
            originVerticesOS = Array.ConvertAll(mesh.vertices, v => v);
        }

        /// <summary>
        ///  更新顶点距离排序
        /// </summary>
        private void UpdateDistanceIndex()
        {
            // 更新原始顶点
            UpdateOriginVertices();

            // 如果没有原始顶点，直接返回
            if (originVerticesOS == null || originVerticesOS.Length == 0) return;

            // 获取消失点在模型空间的位置
            var endPointOS = GetEndPointOS();

            // 根据到消失点距离从小到大的顺序排序的顶点索引
            distanceIndex = originVerticesOS.Select((v, i) => new { v, i })
                .OrderBy(p => Vector3.Distance(p.v, endPointOS))
                .Select(p => p.i)
                .ToArray();
        }

        /// <summary>
        ///  初始化Graphic、CanvasRenderer和可选Canvas相机引用。
        /// </summary>
        /// <remarks>
        ///  Prefab资源在编辑器中可能暂时不位于Canvas下，此时按Overlay语义使用空相机即可。
        /// </remarks>
        private void InitializeReferences()
        {
            // VertexUVFlow只能驱动当前物体上的Graphic网格。
            graphic = GetComponent<Graphic>();

            // CanvasRenderer保存Graphic实际提交给Canvas的渲染网格。
            render = GetComponent<CanvasRenderer>();

            // Prefab编辑阶段允许暂时找不到Canvas，运行时会在Awake重新解析。
            var canvas = GetComponentInParent<Canvas>();

            // Overlay模式和无Canvas的Prefab资源都使用空相机完成屏幕坐标转换。
            uiCamera = !canvas || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }
    }
}
