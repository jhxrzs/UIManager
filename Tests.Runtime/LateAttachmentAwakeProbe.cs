using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>记录动效应用时机的派生探针，检测基类是否过早使用尚未初始化的派生状态。</summary>
    public sealed class LateAttachmentAwakeProbe : EffectControllerBase
    {
        /// <summary>已经执行的同步显隐行为次数。</summary>
        public int ActionCount { get; private set; }
        /// <summary>是否检测到派生 Awake 完成前就应用动效。</summary>
        public bool AppliedBeforeAwakeFinished { get; private set; }
        /// <summary>派生探针是否已经完成自身 Awake 初始化。</summary>
        private bool _awakeFinished;

        /// <summary>完成基类初始化后标记派生类就绪，用于检测提前应用动效。</summary>
        protected override void Awake()
        {
            // 先执行基类初始化，观察它是否会过早应用尚未就绪的派生状态。
            base.Awake();
            // 标记派生 Awake 已完成，之后应用效果才属于正常时序。
            _awakeFinished = true;
        }

        /// <summary>记录显示行为是否早于派生 Awake 完成，并执行基类显示效果。</summary>
        protected override void OnUIShow()
        {
            // 累计“AppliedBeforeAwakeFinished”的观测标志，保留此前已出现的时序异常。
            AppliedBeforeAwakeFinished |= !_awakeFinished;
            // 更新“ActionCount”的计数，记录回调实际执行次数。
            ActionCount++;
            // 沿用基类显示行为，让探针只记录时序而不改变正常效果。
            base.OnUIShow();
        }

        /// <summary>记录隐藏行为是否早于派生 Awake 完成，并执行基类隐藏效果。</summary>
        protected override void OnUIHide()
        {
            // 累计“AppliedBeforeAwakeFinished”的观测标志，保留此前已出现的时序异常。
            AppliedBeforeAwakeFinished |= !_awakeFinished;
            // 更新“ActionCount”的计数，记录回调实际执行次数。
            ActionCount++;
            // 沿用基类隐藏行为，让探针只记录时序而不改变正常效果。
            base.OnUIHide();
        }
    }
}
