using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证移除或禁用流动效果后的 Image 与 TMP 原始网格恢复。</summary>
    public partial class UIFlowMeshLifecycleRuntimeTests
    {
        /// <summary>独立保存网格顶点和颜色的只读测试快照。</summary>
        private readonly struct MeshSnapshot
        {
            /// <summary>网格快照中的顶点位置副本。</summary>
            public readonly Vector3[] Vertices;
            /// <summary>网格快照中的顶点颜色副本。</summary>
            public readonly Color32[] Colors;
            /// <summary>保存调用方提供的顶点与颜色数组，供移除效果前后比较。</summary>
            /// <param name="vertices">快照保存的顶点位置数组。</param>
            /// <param name="colors">快照保存的顶点颜色数组。</param>
            public MeshSnapshot(Vector3[] vertices, Color32[] colors)
            {
                // 保存调用方提供的顶点数组，供后续几何比较。
                Vertices = vertices;
                // 保存调用方提供的颜色数组，供后续透明度与颜色比较。
                Colors = colors;
            }
        }
    }
}
