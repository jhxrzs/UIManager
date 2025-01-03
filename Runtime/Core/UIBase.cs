using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UIManager
{
    /// <summary>
    ///  UI面板基类，实现了UI的显示隐藏以及对应的异步方法
    /// </summary>
    public partial class UIBase : MonoBehaviour, IShowUI, IHideUI, IShowUIAsync, IHideUIAsync
    {
        /// <summary>
        ///  UI互斥组
        /// </summary>
        [SerializeField, Tooltip("UI互斥组，同组UI无法同时显示")]
        private int mutexGroup;

        /// <summary>
        ///  是否固定UI层级
        /// </summary>
        [SerializeField, Tooltip("固定UI层级")] private bool fixedSiblingIndex;

        /// <summary>
        ///  最大的兄弟节点索引
        /// </summary>
        [SerializeField, HideInInspector] private int maxSiblingIndex;

        /// <summary>
        ///  UI面板的父面板集合,从祖先到父亲
        /// </summary>
        [SerializeField, HideInInspector] private List<UIBase> parents;

        /// <summary>
        ///  UI面板的子面板集合
        /// </summary>
        [SerializeField, HideInInspector] private List<UIBase> children;

        /// <summary>
        ///  所有兄弟节点面板
        /// </summary>
        [SerializeField, HideInInspector] private List<UIBase> siblings;

        /// <summary>
        ///  UI显示前触发
        /// </summary>
        public event Action BeforeUIShowEvent;

        /// <summary>
        ///  UI显示后触发
        /// </summary>
        public event Action AfterUIShowedEvent;

        /// <summary>
        ///  UI隐藏前触发
        /// </summary>
        public event Action BeforeUIHideEvent;

        /// <summary>
        ///  UI隐藏时触发
        /// </summary>
        public event Action AfterUIHidedEvent;

        /// <summary>
        ///  增加UI异步显示行为的委托
        /// </summary>
        private Func<CancellationToken, UniTask> _onUIShowAsyncEvent;

        /// <summary>
        ///  增加UI异步隐藏行为的委托
        /// </summary>
        private Func<CancellationToken, UniTask> _onUIHideAsyncEvent;

        /// <summary>
        ///  当前的显示/隐藏行为的取消token
        /// </summary>
        private CancellationTokenSource _currentShowHideCts;

        /// <summary>
        ///  当前的同步显示行为委托
        /// </summary>
        private Action _onUIHideEvent;

        /// <summary>
        ///  当前的同步隐藏行为委托
        /// </summary>
        private Action _onUIShowEvent;

        /// <summary>
        ///  暴露给外部，增加UI同步显示行为的事件，注册此事件以增加显示行为，比如gameObject.SetActive(true)...等
        /// </summary>
        public event Action ShowActions
        {
            add => _onUIShowEvent += value;
            remove => _onUIShowEvent -= value;
        }

        /// <summary>
        ///  暴露给外部，增加UI哦同步隐藏行为的事件，注册此事件以增加隐藏行为，比如gameObject.SetActive(false)...等
        /// </summary>
        public event Action HideActions
        {
            add => _onUIHideEvent += value;
            remove => _onUIHideEvent -= value;
        }

        /// <summary>
        ///  暴露给外部，增加UI异步隐藏行为的事件,可以注册此事件以增加异步显示行为，比如fadeIn/fadeOut/音效/...等
        /// </summary>
        public event Func<CancellationToken, UniTask> HideAsyncActions
        {
            add => _onUIHideAsyncEvent += value;
            remove => _onUIHideAsyncEvent -= value;
        }

        /// <summary>
        ///  暴露给外部，增加UI异步显示行为的事件
        /// </summary>
        public event Func<CancellationToken, UniTask> ShowAsyncActions
        {
            add => _onUIShowAsyncEvent += value;
            remove => _onUIShowAsyncEvent -= value;
        }

        /// <summary>
        ///  当前的UI状态
        /// </summary>
        [field: SerializeField, Tooltip("当前UI状态")]
        public UIState State { get; private set; }


        protected virtual void Start()
        {
            //初始化UI状态
            InitUIState();
        }

        /// <summary>
        ///  当UI显示时执行的逻辑
        /// </summary>
        public void Show()
        {
            // 如果UI已经是显示状态，则直接返回
            if (State == UIState.Showed)return;
            
            // 显示UI，不检查当前状态
            ShowNotCheckState();
        }

        /// <summary>
        ///  当UI隐藏时执行的逻辑
        /// </summary>
        public void Hide()
        {
            // 如果UI已经是隐藏状态，则直接返回
            if (State == UIState.Hided) return;
         
            // 隐藏UI，不检查当前状态
            HideNotCheckState();
        }

        /// <summary>
        ///  当UI异步显示时执行的逻辑
        /// </summary>
        /// <param name="cancellationToken"> 取消异步的token</param>
        /// <returns>返回异步任务</returns>
        public async UniTask ShowAsync(CancellationToken cancellationToken)
        {
            //如果UI已经是显示状态，则直接返回
            if (State == UIState.Showed)  return;
        
            // 异步显示UI，不检查当前状态
            await ShowNotCheckStateAsync(cancellationToken);
        }


        /// <summary>
        ///  异步隐藏UI
        /// </summary>
        /// <param name="cancellationToken"> 取消异步的token</param>
        /// <returns> 返回异步任务</returns>
        public async UniTask HideAsync(CancellationToken cancellationToken)
        {
            //如果UI已经是隐藏状态，则直接返回
            if (State == UIState.Hided)return;
         
            // 异步隐藏UI，不检查当前状态
            await HideNotCheckStateAsync(cancellationToken);
        }

        /// <summary>
        ///  具体的UI显示逻辑，此处会从委托中获取所有的显示行为，然后同步执行
        /// </summary>
        protected virtual void OnUIShow()
        {
            //执行所有的显示行为
            _onUIShowEvent?.Invoke();
        }

        /// <summary>
        ///  具体的UI隐藏逻辑，此处会从委托中获取所有的隐藏行为，然后同步执行
        /// </summary>
        protected virtual void OnUIHide()
        {
            //执行所有的隐藏行为
            _onUIHideEvent?.Invoke();
        }

        /// <summary>
        ///  在UI异步显示时执行的逻辑,此处会从委托中获取所有的异步显示行为，然后同步执行
        /// </summary>
        protected virtual async UniTask OnUIShowAsync(CancellationToken cancellationToken)
        {
            //执行所有的异步显示行为
            if (_onUIShowAsyncEvent == null) return;
            var taskList = _onUIShowAsyncEvent.GetInvocationList().Cast<Func<CancellationToken, UniTask>>();
            await UniTask.WhenAll(taskList.Select(i => i(cancellationToken)));
        }

        /// <summary>
        ///  当UI异步隐藏时执行的逻辑
        /// </summary>
        protected virtual async UniTask OnUIHideAsync(CancellationToken cancellationToken)
        {
            //执行所有的异步隐藏行为
            if (_onUIHideAsyncEvent == null) return;
            var taskList = _onUIHideAsyncEvent.GetInvocationList().Cast<Func<CancellationToken, UniTask>>();
            await UniTask.WhenAll(taskList.Select(i => i.Invoke(cancellationToken)));
        }

        /// <summary>
        ///  显示UI，不检查当前状态
        /// </summary>
        private void ShowNotCheckState()
        {
            //如果当前UI存在显隐行为，立即停止
            StopCurrentShowHideActions();

            // UI显示前的逻辑
            BeforeUIShowInvoke();

            // 具体的UI显示逻辑
            OnUIShow();

            // UI显示后的逻辑
            AfterUIShowedInvoke();
        }

        /// <summary>
        /// 隐藏UI，不检查当前状态
        /// </summary>
        private void HideNotCheckState()
        {
            // UI隐藏前的逻辑
            BeforeUIHideInvoke();

            // 具体的UI隐藏逻辑
            OnUIHide();

            // UI隐藏后的逻辑
            AfterUIHidedInvoke();
        }

        /// <summary>
        ///  异步显示UI，不检查当前状态
        /// </summary>
        private async UniTask ShowNotCheckStateAsync(CancellationToken cancellationToken)
        {
            //如果当前UI存在显隐行为，立即停止
            StopCurrentShowHideActions();

            //如果显示行为为空，则退化为直接显示
            if (_onUIShowAsyncEvent == null)
            {
                ShowNotCheckState();
                return;
            }

            // 组合token,用于取消当前的显示/隐藏行为
            _currentShowHideCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            //显隐同时，异步
            // UI显示前的逻辑
            var beforeTask = BeforeUIShowAsyncInvoke(_currentShowHideCts.Token);

            // 在UI异步显示时执行的逻辑,此处会从委托中获取所有的异步显示行为，然后同步执行
            var showTask = OnUIShowAsync(_currentShowHideCts.Token);

            //等待所有任务完成
            await UniTask.WhenAll(beforeTask, showTask);
            
            // UI显示后的逻辑
            AfterUIShowedInvoke();
        }

        /// <summary>
        /// 异步隐藏UI，不检查当前状态
        /// </summary>
        private async UniTask HideNotCheckStateAsync(CancellationToken cancellationToken)
        {
            // UI隐藏前的逻辑
            BeforeUIHideInvoke();

            // 如果隐藏行为为空，则退化为直接隐藏
            if (_onUIHideAsyncEvent == null)
            {
                HideNotCheckState();
                return;
            }

            // 组合token,用于取消当前的显示/隐藏行为
            _currentShowHideCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // 在UI异步隐藏时执行的逻辑,此处会从委托中获取所有的异步隐藏行为，然后同步执行
            await OnUIHideAsync(_currentShowHideCts.Token);

            // UI隐藏后的逻辑
            AfterUIHidedInvoke();
        }


        /// <summary>
        /// UI隐藏前的逻辑
        /// </summary>
        private void BeforeUIHideInvoke()
        {
            // 如果当前UI存在显隐行为，立即停止
            StopCurrentShowHideActions();

            // 隐藏子节点
            foreach (var eachChild in children)
            {
                eachChild.Hide();
            }

            //设置状态
            State = UIState.Hiding;

            // 触发事件
            BeforeUIHideEvent?.Invoke();
        }

        /// <summary>
        ///  UI隐藏后的逻辑
        /// </summary>
        private void AfterUIHidedInvoke()
        {
            //设置状态
            State = UIState.Hided;

            //触发事件
            AfterUIHidedEvent?.Invoke();
        }

        /// <summary>
        ///  UI显示前的逻辑
        /// </summary>
        private void BeforeUIShowInvoke()
        {
            //设置状态
            State = UIState.Showing;

            // 触发事件
            BeforeUIShowEvent?.Invoke();

            //隐藏互斥兄弟节点中，显示的面板
            foreach (var eachMutexSibling in siblings)
            {
                //如果不在同一个互斥组，则直接忽略
                if (eachMutexSibling.mutexGroup != mutexGroup) continue;

                //隐藏兄弟节点
                eachMutexSibling.Hide();
            }

            //显示父节点中，隐藏的节点
            foreach (var eachParent in parents)
            {
                //显示父节点
                eachParent.Show();
            }
        }

        /// <summary>
        ///  在UI异步显示前执行的逻辑
        /// </summary>
        private async UniTask BeforeUIShowAsyncInvoke(CancellationToken cancellationToken)
        {
            //设置状态
            State = UIState.Showing;

            // 触发事件
            BeforeUIShowEvent?.Invoke();

            //隐藏互斥兄弟节点中，显示的面板
            var taskList = siblings.Where(i => i.mutexGroup == mutexGroup)
                .Select(t => t.HideAsync(cancellationToken)).ToList();

            //显示父节点中，隐藏的节点
            taskList.AddRange(Enumerable.Select(parents, eachParent
                => eachParent.ShowAsync(cancellationToken)));

            //等待所有任务完成
            await UniTask.WhenAll(taskList);
        }

        /// <summary>
        ///  UI显示后的逻辑
        /// </summary>
        private void AfterUIShowedInvoke()
        {
            //设置状态
            State = UIState.Showed;

            //调整transform的层级，让自己处于层级视图最下端，避免被遮挡,固定层级的节点不参与调整
            var flexibleSiblings = siblings.Where(i => !i.fixedSiblingIndex).ToList();
            if (flexibleSiblings.Count > 0 && !fixedSiblingIndex)
                transform.SetSiblingIndex(maxSiblingIndex);

            //触发事件
            AfterUIShowedEvent?.Invoke();
        }

        /// <summary>
        ///  立即停止当前的显隐行为
        /// </summary>
        private void StopCurrentShowHideActions()
        {
            _currentShowHideCts?.Cancel();
        }

        /// <summary>
        ///  初始化UI状态
        /// </summary>
        private void InitUIState()
        {
            //根据当前状态，初始化UI显隐
            if (State == UIState.Showed) ShowNotCheckState();
            else if (State == UIState.Hided) HideNotCheckState();
            else if (State == UIState.Showing)
            {
                HideNotCheckState();
                _ = ShowAsync(destroyCancellationToken);
            }
            else if (State == UIState.Hiding)
            {
                ShowNotCheckState();
                _ = HideAsync(destroyCancellationToken);
            }
        }

        private void OnValidate()
        {
            // 寻找所有的父面板，从祖先到父亲
            FindParents();

            // 寻找所有的子面板
            FindChildren();

            // 找所有的互斥组相同的兄弟面板
            FindSiblings();
        }

        #region 编辑器声明函数

        /// <summary>
        ///  寻找所有的父面板，从祖先到父亲
        /// </summary>
        partial void FindParents();

        /// <summary>
        ///  寻找所有的子面板
        /// </summary>
        partial void FindChildren();

        /// <summary>
        ///  寻找所有的兄弟面板，和最大的兄弟节点索引
        /// </summary>
        partial void FindSiblings();

        #endregion
    }
}