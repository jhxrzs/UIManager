using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  利用顶点偏移和UV流动来实现的隐藏效果
    /// </summary>
    [RequireComponent(typeof(UIBase))]
    public class VertexAndUVHide : MonoBehaviour,IForceSyncConfig
    {
        /// <summary>  
        ///  需要逐渐隐藏的UI面板
        /// </summary>
        [SerializeField, HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  配置信息
        /// </summary>
        [SerializeField] private ForceSyncConfig config;

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
            uiBase.HideActions += TweenEffectAmount;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步隐藏事件
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (uiBase != null) uiBase.HideActions -= TweenEffectAmount;
        }
        
        /// <summary>
        ///   动画效果
        /// </summary>
        protected void TweenEffectAmount()
        {
            // 设置动效材质
            foreach (var each in GetComponentsInChildren<Graphic>())
            {
                if (each.material != Config.material) each.material = Config.material;
                Config.material.SetVector(EndScreenPoint, Config.endScreenPoint);
            }
            
            // 设置目标值
           
            Config.material.SetFloat(EffectAmount, EndEffectAmount);
        }
    }
}