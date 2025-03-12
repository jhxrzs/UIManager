using System;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;

namespace JH
{
    /// <summary>
    ///  顶点和UV流动动画配置，在显示，隐藏，异步显示，异步隐藏4个Mono上强制同步
    /// </summary>
    [Serializable]
    public class ForceSyncConfig
    {
#if ODIN_INSPECTOR
        [LabelText("消失点屏幕坐标")]
#endif
        public Vector2 endScreenPoint;

#if ODIN_INSPECTOR
        [LabelText("动画材质")]
#endif
        public Material material;

        /// <summary>
        ///  同步配置信息
        /// </summary>
        public void SyncConfigInfo(ForceSyncConfig other)
        {
            endScreenPoint = other.endScreenPoint;
            material = other.material;
        }
    }
}