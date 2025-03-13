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
    public partial class VertexUVFlow : MonoBehaviour
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
        [SerializeField] private Vector3[] originVerticesOS;

        /// <summary>
        ///  根据到消失点距离从小到大的顺序排序的顶点索引
        /// </summary>
        [SerializeField] private int[] distanceIndex;

        /// <summary>
        ///  消失点屏幕坐标
        /// </summary>
        [SerializeField] private Vector3 endScreenPoint;

        /// <summary>
        ///  消失点屏幕坐标
        /// </summary>
        private Vector3 EndScreenPoint
        {
            get => endScreenPoint;
            set
            {
                endScreenPoint = value;

                //消失点一旦调整，必须调整顶点距离排序
                _ = UpdateDistanceIndex();
            }
        }

        /// <summary>
        ///  ui相机
        /// </summary>
        [SerializeField, HideInInspector] private Camera uiCamera;

        /// <summary>
        ///  动画材质
        /// </summary>
        [SerializeField] private Material material;

        /// <summary>
        ///  特效进度
        /// </summary>
        public float EffectAmount
        {
            get => effectAmount;
            set
            {
                effectAmount = value;

                // 更新顶点位置
                UpdateMeshEffectAmount();

                // 更新材质特效进度
                UpdateMaterialEffectAmount();
            }
        }

        /// <summary>
        ///  编辑器下初始化材质
        /// </summary>
        partial void InitMaterialEditor();

        private void OnValidate()
        {
            // 初始化UI相机
            var canvas = GetComponentInParent<Canvas>();
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            // 初始化材质
            InitMaterialEditor();

            //更新顶点距离排序
            _ = UpdateDistanceIndex();

            // 编辑器下更新特效进度
            EffectAmount = effectAmount;
        }

        /// <summary>
        ///  更新材质特效进度
        /// </summary>
        private void UpdateMaterialEffectAmount()
        {
            material.SetFloat(Amount, effectAmount);
            material.SetVector(EndPointUV, new Vector2(Mathf.Clamp01(EndScreenPoint.x / Screen.width),
                Mathf.Clamp01(EndScreenPoint.y / Screen.height)));
        }

        /// <summary>
        ///  更新顶点动画
        /// </summary>
        private void UpdateMeshEffectAmount()
        {
            // 根据当前进度生成新的顶点位置
            var newVertices = GenerateNewVerticesOS();

            // 克隆mesh
            var render = GetComponent<CanvasRenderer>();
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
        }

        /// <summary>
        ///  根据当前进度生成新的顶点位置
        /// </summary>
        private Vector3[] GenerateNewVerticesOS()
        {
            var newVertices = new Vector3[originVerticesOS.Length];
            for (var i = 0; i < originVerticesOS.Length; i++)
            {
                // 除了距离最近的顶点，其他顶点位置不变
                if (i != distanceIndex[0])
                {
                    newVertices[i] = originVerticesOS[i];
                    continue;
                }

                // 获取原始顶点位置
                var originVertexOS = originVerticesOS[i];

                // 获取目标顶点位置
                var vertexOS = GetEndPointOS();

                //根据当前特效进度，计算新的顶点位置
                newVertices[i] = EaseFunctionNumeric.Tween(originVertexOS, vertexOS, Ease.InOutCirc, effectAmount);
            }

            return newVertices;
        }

        /// <summary>
        ///  获取消失点在模型空间的位置
        /// </summary>
        private Vector3 GetEndPointOS()
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                transform as RectTransform, EndScreenPoint, uiCamera, out var vertexWS);
            return transform.InverseTransformPoint(vertexWS);
        }

        /// <summary>
        ///  更新原始顶点
        /// </summary>
        public async JTask UpdateOriginVertices()
        {
            //关闭graphic 
            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.enabled = false;

            //打开graphic，此时会刷新mesh
            if (graphic != null) graphic.enabled = true;

            await JTask.Yield();
            await JTask.SwitchToMainThread();

            // 记录原始Mesh信息
            var render = GetComponent<CanvasRenderer>();
            var mesh = render.GetMesh();
            if (mesh == null) Debug.LogError("未找到Mesh组件", gameObject);
            if (mesh != null) originVerticesOS = Array.ConvertAll(mesh.vertices, v => v);
        }

#if ODIN_INSPECTOR
        [Button("初始化距离数组")]
#endif
        [ContextMenu("初始化距离数组")]
        private async JTask UpdateDistanceIndex()
        {
            // 更新原始顶点
            await UpdateOriginVertices();

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