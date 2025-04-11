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