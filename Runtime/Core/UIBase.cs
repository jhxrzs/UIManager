using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace JH
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
        ///  当UI状态改变时触发
        ///  参数： 原始状态，新状态
        /// </summary>
        public event Action<UIState, UIState> OnStateChanged;

        /// <summary>
        ///  最大的兄弟节点索引
        /// </summary>
        private int MaxSiblingIndex => Mathf.Max(Siblings
            .Where(i => !i.fixedSiblingIndex)
            .Max(i => i.transform.GetSiblingIndex()), transform.GetSiblingIndex());

        /// <summary>
        ///  UI面板的父面板集合,从祖先到父亲
        /// </summary>
        private List<UIBase> Parents => FindParents();

        /// <summary>
        ///  UI面板的子面板集合
        /// </summary>
        private List<UIBase> Children => FindChildren();

        /// <summary>
        ///  所有兄弟节点面板
        /// </summary>
        private List<UIBase> Siblings => FindSiblings();

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
        private Func<CancellationToken, JTask> _onUIShowAsyncEvent;

        /// <summary>
        ///  增加UI异步隐藏行为的委托
        /// </summary>
        private Func<CancellationToken, JTask> _onUIHideAsyncEvent;

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
        ///  当前UI的状态
        /// </summary>
        [SerializeField, Tooltip("当前UI状态")] private UIState state;

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
        public event Func<CancellationToken, JTask> HideAsyncActions
        {
            add => _onUIHideAsyncEvent += value;
            remove => _onUIHideAsyncEvent -= value;
        }

        /// <summary>
        ///  暴露给外部，增加UI异步显示行为的事件
        /// </summary>
        public event Func<CancellationToken, JTask> ShowAsyncActions
        {
            add => _onUIShowAsyncEvent += value;
            remove => _onUIShowAsyncEvent -= value;
        }

        /// <summary>
        ///  当前的UI状态
        /// </summary>

        public UIState State
        {
            get => state;
            private set
            {
                if (value == state) return;
                OnStateChanged?.Invoke(state, value);
                state = value;
            }
        }


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
            if (State == UIState.Showed) return;

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
        public async JTask ShowAsync(CancellationToken cancellationToken)
        {
            try
            {
                //如果UI已经是显示状态，则直接返回
                if (State == UIState.Showed) return;

                // 异步显示UI，不检查当前状态
                await ShowNotCheckStateAsync(cancellationToken);
            }
            catch (OperationCanceledException e)
            {
                Debug.Log("检测到取消操作");
                throw;
            }
        }

        /// <summary>
        ///  异步隐藏UI
        /// </summary>
        /// <param name="cancellationToken"> 取消异步的token</param>
        /// <returns> 返回异步任务</returns>
        public async JTask HideAsync(CancellationToken cancellationToken)
        {
            try
            {
                //如果UI已经是隐藏状态，则直接返回
                if (State == UIState.Hided) return;

                // 异步隐藏UI，不检查当前状态
                await HideNotCheckStateAsync(cancellationToken);
            }
            catch (OperationCanceledException e)
            {
                Debug.Log("检测到取消操作");
                throw;
            }
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
        protected virtual async JTask OnUIShowAsync(CancellationToken cancellationToken)
        {
            try
            {
                //执行所有的异步显示行为
                if (_onUIShowAsyncEvent == null) return;
                var taskList = _onUIShowAsyncEvent.GetInvocationList().Cast<Func<CancellationToken, JTask>>();
                await JTask.WhenAll(taskList.Select(i => i(cancellationToken)));
            }
            catch (OperationCanceledException e)
            {
                Debug.Log("检测到取消操作");
                throw;
            }
        }

        /// <summary>
        ///  当UI异步隐藏时执行的逻辑
        /// </summary>
        protected virtual async JTask OnUIHideAsync(CancellationToken cancellationToken)
        {
            try
            {
                //执行所有的异步隐藏行为
                if (_onUIHideAsyncEvent == null) return;
                var taskList = _onUIHideAsyncEvent.GetInvocationList().Cast<Func<CancellationToken, JTask>>();
                await JTask.WhenAll(taskList.Select(i => i.Invoke(cancellationToken)));
            }
            catch (OperationCanceledException e)
            {
                Debug.Log("检测到取消操作");
                throw;
            }
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
        private async JTask ShowNotCheckStateAsync(CancellationToken cancellationToken)
        {
            try
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
                await JTask.WhenAll(_currentShowHideCts.Token, beforeTask, showTask);

                // UI显示后的逻辑
                AfterUIShowedInvoke();
            }
            catch (OperationCanceledException e)
            {
                Debug.Log("检测到取消操作");
                throw;
            }
        }

        /// <summary>
        /// 异步隐藏UI，不检查当前状态
        /// </summary>
        private async JTask HideNotCheckStateAsync(CancellationToken cancellationToken)
        {
            try
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
            catch (OperationCanceledException e)
            {
                Debug.Log("检测到取消操作");
                throw;
            }
        }


        /// <summary>
        /// UI隐藏前的逻辑
        /// </summary>
        private void BeforeUIHideInvoke()
        {
            // 如果当前UI存在显隐行为，立即停止
            StopCurrentShowHideActions();

            // 隐藏子节点
            foreach (var eachChild in Children)
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
            foreach (var eachMutexSibling in Siblings)
            {
                //如果不在同一个互斥组，则直接忽略
                if (eachMutexSibling.mutexGroup != mutexGroup) continue;

                //隐藏兄弟节点
                eachMutexSibling.Hide();
            }

            //显示父节点中，隐藏的节点
            foreach (var eachParent in Parents)
            {
                //显示父节点
                eachParent.Show();
            }
        }

        /// <summary>
        ///  在UI异步显示前执行的逻辑
        /// </summary>
        private async JTask BeforeUIShowAsyncInvoke(CancellationToken cancellationToken)
        {
            try
            {
                //设置状态
                State = UIState.Showing;

                // 触发事件
                BeforeUIShowEvent?.Invoke();

                //隐藏互斥兄弟节点中，显示的面板
                var taskList = Siblings.Where(i => i.mutexGroup == mutexGroup)
                    .Select(t => t.HideAsync(cancellationToken)).ToList();

                //显示父节点中，隐藏的节点
                taskList.AddRange(Parents.Select(eachParent
                    => eachParent.ShowAsync(cancellationToken)));

                //等待所有任务完成
                await JTask.WhenAll(taskList);
            }
            catch (OperationCanceledException e)
            {
                Debug.Log("检测到取消操作");
                throw;
            }
        }

        /// <summary>
        ///  UI显示后的逻辑
        /// </summary>
        private void AfterUIShowedInvoke()
        {
            //设置状态
            State = UIState.Showed;

            //调整transform的层级，让自己处于层级视图最下端，避免被遮挡,固定层级的节点不参与调整
            var flexibleSiblings = Siblings.Where(i => !i.fixedSiblingIndex).ToList();
            if (flexibleSiblings.Count > 0 && !fixedSiblingIndex)
                transform.SetSiblingIndex(MaxSiblingIndex);

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

        /// <summary>
        ///  寻找所有的父面板，从祖先到父亲
        /// </summary>
        private List<UIBase> FindParents()
        {
            //循环找到所有的父面板
            var parents = new List<UIBase>();
            var current = transform.parent;
            while (current != null)
            {
                var uiBase = current.GetComponent<UIBase>();
                if (uiBase != null)
                {
                    parents.Add(uiBase);
                }
                else
                {
                    break;
                }

                current = current.parent;
            }

            //反转父面板集合，从祖先到父亲
            parents.Reverse();

            return parents;
        }

        /// <summary>
        ///  寻找所有的兄弟面板，和最大的兄弟节点索引
        /// </summary>
        private List<UIBase> FindSiblings()
        {
            //尝试找到父节点
            var parent = transform.parent;

            //所有的兄弟节点
            var siblings = new List<UIBase>();

            //如果没有父节点，那么就是根节点，那么就找到所有的根节点,并且排除自己
            if (parent == null)
            {
                var scene = gameObject.scene;
                if (!scene.isLoaded) return new List<UIBase>();

                // 获取所有的兄弟节点,并且排除自己和未激活的节点
                siblings = scene.GetRootGameObjects()
                    .Select(i => i.GetComponent<UIBase>())
                    .Where(i => i != null && i != this && i.gameObject.activeInHierarchy).ToList();

                return siblings;
            }

            //如果有父面板，那么就找到所有的子面板，并且排除自己
            for (int i = 0; i < transform.parent.childCount; i++)
            {
                var child = transform.parent.GetChild(i);

                //过滤掉未激活的节点
                if (!child.gameObject.activeInHierarchy) continue;

                if (child != transform)
                {
                    var uiBase = child.GetComponent<UIBase>();
                    if (uiBase != null)
                    {
                        siblings.Add(uiBase);
                    }
                }
            }

            return siblings;
        }

        /// <summary>
        /// 初始化UI面板的子面板集合
        /// </summary>
        private List<UIBase> FindChildren()
        {
            return GetComponentsInChildren<UIBase>()
                .Where(i => i.transform.parent == transform)
                .Where(i => i != null && i.gameObject.activeInHierarchy).ToList();
        }
    }
}