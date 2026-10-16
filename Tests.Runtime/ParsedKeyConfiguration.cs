using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JH.Test_UIManager
{
    /// <summary>验证键盘名称、别名、数字快捷写法与非法输入的解析契约。</summary>
    public partial class UIKeyNameRuntimeTests
    {
        /// <summary>接收 KeyEntity 序列化解析结果的测试数据结构。</summary>
        [Serializable]
        private class ParsedKeyConfiguration
        {
            /// <summary>KeyEntity 解析后保留的有效物理按键列表。</summary>
            public List<Key> keys;
            /// <summary>解析后仍未识别或不允许的按键名称文本。</summary>
            public string invalidKeyNames;
        }
    }
}
