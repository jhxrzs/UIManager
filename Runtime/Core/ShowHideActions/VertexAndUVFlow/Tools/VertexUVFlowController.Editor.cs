#if UNITY_EDITOR
using System.Linq;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  顶点和UV流动动画管理器
    /// </summary>
    public partial class VertexUVFlow
    {
        /// <summary>
        ///  默认的材质名称
        /// </summary>
        private const string DefaultMaterialName = "JH_UI_TwistUIEffect.mat";

        /// <summary>
        ///  Shader名称
        /// </summary>
        private const string ShaderName = "JH/UI/VertexAndUVFlow";

        /// <summary>
        ///  如果特效材质没有初始化，直接去Packages目录下找到特效材质
        /// </summary>
        partial void InitMaterialEditor()
        {
            var graphic = GetComponent<Graphic>();
            if (graphic == null) return;
            var graphicMaterial = graphic.material;
            if (graphicMaterial.shader.name == ShaderName) return;

            var find = AssetDatabase.FindAssets("t:Material", new[] { "Packages" });
            var first = find.Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith(DefaultMaterialName));
            if (first != null)
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(first);
                graphic.material = material;
            }
        }

#if ODIN_INSPECTOR
        [Button("在同一个UIBase下同步配置")]
#endif
        [ContextMenu("在同一个UIBase下同步配置")]
        private void SyncConfigUnderUIBase()
        {
            var uiBase = GetComponentInParent<UIBase>();
            if (uiBase == null) return;

            var vertexUVFlows = uiBase.GetComponentsInChildren<VertexUVFlow>();
            foreach (var vertexUVFlow in vertexUVFlows)
            {
                // 仅需同步消失点屏幕坐标和材质
                vertexUVFlow.endScreenPoint = endScreenPoint;
                vertexUVFlow.GetComponent<Graphic>().material = vertexUVFlow.material = material;

                // 重新计算顶点距离排序
                _ = vertexUVFlow.UpdateDistanceIndex();
            }
        }
    }
}
#endif