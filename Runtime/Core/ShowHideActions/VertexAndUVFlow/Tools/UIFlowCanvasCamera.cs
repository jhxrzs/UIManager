using UnityEngine;

namespace JH
{
    /// <summary>
    ///  为组路径和子网格统一解析屏幕坐标转换相机。
    /// </summary>
    internal static class UIFlowCanvasCamera
    {
        /// <summary>按根 Canvas 的渲染模式确定坐标转换相机。</summary>
        /// <param name="ownerCanvas">当前 Graphic 所属的 Canvas，可为空。</param>
        /// <returns>相机或世界模式使用的相机；Overlay 或缺少 Canvas 时为空。</returns>
        internal static Camera Resolve(Canvas ownerCanvas)
        {
            // 缺少 Canvas 时使用 Overlay 语义；嵌套 Canvas 继承根 Canvas 的渲染模式。
            if (!ownerCanvas) return null;
            // 优先使用根 Canvas，独立 Canvas 则使用自身渲染配置。
            var rootCanvas = ownerCanvas.rootCanvas ? ownerCanvas.rootCanvas : ownerCanvas;
            // Overlay 坐标变换不需要场景相机。
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;

            // Screen Space Camera未指定相机时按Overlay渲染，不能随场景中的MainCamera改变坐标。
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceCamera) return rootCanvas.worldCamera;

            // World Space允许不配置事件相机，与UGUI一样回退到MainCamera。
            if (rootCanvas.worldCamera) return rootCanvas.worldCamera;
            // 根 Canvas 未指定相机时尝试当前 Canvas 的相机。
            if (ownerCanvas.worldCamera) return ownerCanvas.worldCamera;
            // 世界空间 Canvas 最后回退到场景主相机。
            return Camera.main;
        }
    }
}
