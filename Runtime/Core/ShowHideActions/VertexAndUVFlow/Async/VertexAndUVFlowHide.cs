using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  利用顶点偏移和UV流动来实现的隐藏效果
    /// </summary>
    [RequireComponent(typeof(UIBase))]
    public class VertexAndUVFlowHide : MonoBehaviour, IForceSyncConfig
    {
        /// <summary>  
        ///  需要逐渐隐藏的UI面板
        /// </summary>
        [SerializeField, HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  逐渐隐藏的时间
        /// </summary>
        [SerializeField] protected float duration = 1f;

        /// <summary>
        ///  动画曲线
        /// </summary>
        [SerializeField] protected Ease tweenEase = Ease.InExpo;

        /// <summary>
        ///  配置信息
        /// </summary>
        [SerializeField] private ForceSyncConfig config;

        /// <summary>
        ///  ui相机
        /// </summary>
        [SerializeField, HideInInspector] private Camera uiCamera;

        /// <summary>
        ///  配置信息
        /// </summary>
        public ForceSyncConfig Config => config;

        /// <summary>
        ///  材质中的EffectAmount属性
        /// </summary>
        private static readonly int EffectAmount = Shader.PropertyToID("_EffectAmount");

        /// <summary>
        ///  材质中的EndScreenPoint属性
        /// </summary>
        private static readonly int EndScreenPoint = Shader.PropertyToID("_EndScreenPoint");

        /// <summary>
        ///   目标值
        /// </summary>
        protected virtual float EndEffectAmount => 1;

        private void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            var canvas = uiBase.GetComponentInParent<Canvas>();
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : uiBase.GetComponentInParent<Canvas>().worldCamera;
#if UNITY_EDITOR
            if (config.material == null)
            {
                var find = UnityEditor.AssetDatabase.FindAssets("t:Material", new[] { "Packages" });
                var first = find.Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
                    .FirstOrDefault(path => path.EndsWith("JH_UI_TwistUIEffect.mat"));
                if (first != null) config.material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(first);
            }
#endif
            var totalConfig = GetComponents<IForceSyncConfig>().ToList();
            totalConfig.Remove(this);
            foreach (var each in totalConfig) each.Config.SyncConfigInfo(config);
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的异步隐藏事件，加入效果的动画
        /// </summary>
        protected virtual void Awake()
        {
            uiBase.HideAsyncActions += TweenEffectAmountAsync;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步隐藏事件
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (uiBase != null) uiBase.HideAsyncActions -= TweenEffectAmountAsync;
        }

        /// <summary>
        ///   FadeIn效果
        /// </summary>
        protected async JTask TweenEffectAmountAsync(CancellationToken token)
        {
            var materialTween = MaterialTweenAsync(token);
            var vertexTween = VertexTweenAsync(token);
            await JTask.WhenAll(materialTween, vertexTween);
        }

        /// <summary>
        ///  顶点动效
        /// </summary>
        private async JTask VertexTweenAsync(CancellationToken token)
        {
            var totalCanvasRenders = GetComponentsInChildren<CanvasRenderer>();
            foreach (var each in totalCanvasRenders)
            {
                // 获取rectTransform
                var rectTransform = each.GetComponent<RectTransform>();

                var mesh = CloneMesh(each.GetMesh());
                if (mesh == null) continue;

                var vertices = new Vector3[mesh.vertices.Length];
                for (var i = 0; i < vertices.Length; i++) vertices[i] = mesh.vertices[i];

                var screenVertices = vertices.Select(vertex =>
                    RectTransformUtility.WorldToScreenPoint(uiCamera, rectTransform.TransformPoint(vertex)));

                var minDistanceIndex = screenVertices.Select((vertex, index) => new { vertex, index })
                    .OrderBy(i => Vector2.Distance(i.vertex, Config.endScreenPoint))
                    .Select(i => i.index);

                RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform,
                    Config.endScreenPoint, uiCamera, out var point);
                var endVertex = rectTransform.InverseTransformPoint(point);
                vertices[minDistanceIndex.First()] = endVertex;
                mesh.vertices = vertices;
                each.SetMesh(mesh);
            }
        }

        private Mesh CloneMesh(Mesh origin)
        {
            return new Mesh()
            {
                vertices = origin.vertices,
                triangles = origin.triangles,
                uv = origin.uv,
                uv2 = origin.uv2,
                uv3 = origin.uv3,
                uv4 = origin.uv4,
                colors = origin.colors,
                normals = origin.normals,
                tangents = origin.tangents,
                bounds = origin.bounds,
                bindposes = origin.bindposes,
                boneWeights = origin.boneWeights,
                subMeshCount = origin.subMeshCount
            };
        }

        /// <summary>
        ///  材质动效
        /// </summary>
        private async JTask MaterialTweenAsync(CancellationToken token)
        {
            if (Config.material == null) return;

            // 设置动效材质
            foreach (var each in GetComponentsInChildren<Graphic>())
            {
                if (each.material != Config.material) each.material = Config.material;
                Config.material.SetVector(EndScreenPoint, Config.endScreenPoint);
            }

            // 执行材质动效
            await Config.material
                .Tween(Config.material.GetFloat(EffectAmount), EndEffectAmount,
                    value => Config.material.SetFloat(EffectAmount, value),
                    duration, token, tweenEase);
        }
    }
}