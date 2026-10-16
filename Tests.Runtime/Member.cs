using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证流动组销毁时的成员恢复、异常隔离和所有权保护。</summary>
    public partial class UIThirdAuditCleanupTests
    {
        /// <summary>组合成员的 Image 与流动效果引用，便于同时断言表现和归属。</summary>
        private sealed class Member
        {
            /// <summary>用于观测材质和实际网格的成员 Image。</summary>
            internal Image Image;
            /// <summary>负责该成员流动变形与组归属的网格效果。</summary>
            internal UIFlowMeshEffect Effect;
        }
    }
}
