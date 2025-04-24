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
        [SerializeField, HideInInspector] private Material material;

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
            //克隆材质，需要保证每个UI面板都有自己的材质，因为参数不同
            Material = Instantiate(materialPrefab);
        }

        private void Update()
        {
            // 在需要更新时，更新顶点距离索引排序
            if (NeedUpdateDistanceIndex()) UpdateDistanceIndex();
        }


        private void OnDestroy()
        {
            //销毁材质
            Destroy(Material);
        }

        private void OnValidate()
        {
            // 任意改动都先将材质设置为null
            material = null;

            //初始化图像
            graphic = GetComponent<Graphic>();

            // 初始化渲染器
            render = GetComponent<CanvasRenderer>();

            // 初始化UI相机
            var canvas = GetComponentInParent<Canvas>();
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            // 初始化距离索引数组
            UpdateDistanceIndex();

            // 如果特效材质为空，实例化一个
            var otherMaterials = GetComponentInParent<UIBase>().GetComponentsInChildren<Graphic>()
                .Where(i => i != graphic).Select(i => i.material).ToList();
            if (Material == null || Material.name == "Default UI Material"
                                 || Material == materialPrefab || otherMaterials.Contains(Material))
                Material = Instantiate(materialPrefab);

            // 编辑器下更新特效进度
            EffectAmount = effectAmount;
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
            Material.SetFloat(Amount, effectAmount);
            var nearestIndex = distanceIndex[0];
            var mesh = render.GetMesh();
            if (mesh == null) return;
            var uv = mesh.uv;
            Material.SetVector(EndPointUV, uv[nearestIndex]);
        }

        /// <summary>
        ///  更新顶点动画
        /// </summary>
        private void UpdateMeshEffectAmount()
        {
            // 根据当前进度生成新的顶点位置
            var newVertices = GenerateNewVerticesOS();

            // 克隆mesh
            var mesh = render.GetMesh();
            if (mesh == null) return;
            var newMesh = new Mesh
            {
                vertices = newVertices,
                triangles = Array.ConvertAll(mesh.triangles, t => t),
                uv = Array.ConvertAll(mesh.uv, v => v),
                uv2 = Array.ConvertAll(mesh.uv2, v => v),
                uv3 = Array.ConvertAll(mesh.uv3, v => v),
                uv4 = Array.ConvertAll(mesh.uv4, v => v),
                colors = Array.ConvertAll(mesh.colors, v => v),
                normals = Array.ConvertAll(mesh.normals, v => v),
                tangents = Array.ConvertAll(mesh.tangents, v => v),
            };

            // 更新mesh
            render.SetMesh(newMesh);

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
            //关闭graphic 
            if (graphic != null) graphic.enabled = false;

            //打开graphic，此时会刷新mesh
            if (graphic != null) graphic.enabled = true;

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
    }
}