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
        /// <summary>统一封装按钮与 Toggle 的测试输入及外部业务监听。</summary>
        private sealed class InteractionControl
        {
            /// <summary>负责将控件输入转为显隐请求的交互实体。</summary>
            internal ShowHideInteractionEntity Entity;
            /// <summary>当前测试使用的按钮；Toggle 分支中为空。</summary>
            internal Button Button;
            /// <summary>当前测试使用的 Toggle；按钮分支中为空。</summary>
            internal Toggle Toggle;
            /// <summary>触发当前按钮或 Toggle 的真实 UnityEvent，供交互绑定测试观察。</summary>
            internal void Trigger()
            {
                // 按钮分支派发点击事件，走实际绑定的监听链。
                if (Button) Button.onClick.Invoke();
                // Toggle 分支派发值变为 true 的事件，模拟显示输入。
                else Toggle.onValueChanged.Invoke(true);
            }
            /// <summary>在外部控件上追加独立业务监听，验证框架解绑不会删除其他监听。</summary>
            /// <param name="listener">与框架监听并存的独立业务监听。</param>
            internal void AddListener(Action listener)
            {
                // 在按钮点击事件上追加独立业务监听。
                if (Button) Button.onClick.AddListener(() => listener());
                // 在 Toggle 值变化事件上追加同一业务监听。
                else Toggle.onValueChanged.AddListener(_ => listener());
            }
        }
    }
}
