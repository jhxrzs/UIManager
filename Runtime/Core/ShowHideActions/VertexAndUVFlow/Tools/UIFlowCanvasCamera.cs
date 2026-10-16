using UnityEngine;

namespace JH
{
    /// <summary>
    ///  为组路径和子网格统一解析屏幕坐标转换相机。
    /// </summary>
    internal static class UIFlowCanvasCamera
    {
        internal static Camera Resolve(Canvas ownerCanvas)
        {
            if (!ownerCanvas) return null;
            var rootCanvas = ownerCanvas.rootCanvas ? ownerCanvas.rootCanvas : ownerCanvas;
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;

            // Screen Space Camera未指定相机时按Overlay渲染，不能随场景中的MainCamera改变坐标。
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceCamera) return rootCanvas.worldCamera;

            // World Space允许不配置事件相机，与UGUI一样回退到MainCamera。
            if (rootCanvas.worldCamera) return rootCanvas.worldCamera;
            if (ownerCanvas.worldCamera) return ownerCanvas.worldCamera;
            return Camera.main;
        }
    }
}
