using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的辅助测试设施。</summary>
    public partial class Test_UIBase
    {
        /// <summary>记录解绑次数的按钮交互实体，验证重绑与销毁清理行为。</summary>
        private sealed class TrackedButtonEntity : ButtonEntity
        {
            /// <summary>该实体实际收到的解绑调用次数。</summary>
            internal int UnbindCount;
            /// <summary>累计解绑次数并调用实际解绑逻辑，便于检测重复订阅与清理遗漏。</summary>
            public override void Unbind() { UnbindCount++; base.Unbind(); }
        }
    }
}
