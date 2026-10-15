# UIManager

UIManager 是一个基于 UniTask 的 Unity uGUI 界面管理库。通过组件配置父子联动、分组互斥、优先级抢占和过渡效果，使用同步或可取消的异步接口控制界面显隐。

## 功能

- **显隐生命周期**：提供显示前、显示后、隐藏前、隐藏后及状态变化通知。
- **父子联动**：支持补齐父界面显示状态、随父显示和多层级联。
- **分组互斥**：同一父节点下的同组界面进行显示仲裁，支持优先级抢占。
- **异步与取消**：基于 UniTask 执行显隐行为，处理调用方取消、对象销毁和操作替代。
- **动态层级**：依据当前 Transform 层级推导关系，支持运行时新增、移除和换父。
- **过渡与交互**：提供透明度、RectTransform、颜色等效果，以及按钮、Toggle 和键盘交互配置。

## 环境与安装

要求 Unity 6000.0 及以上版本，使用 uGUI。

在项目的 `Packages/manifest.json` 中加入以下依赖，保留项目已有的其他条目：

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
    "com.jh.uimanager": "https://github.com/jhxrzs/UIManager.git"
  }
}
```

也可以在 Unity Package Manager 中通过 Git URL 依次安装 UniTask 和 UIManager。uGUI、Input System 和 Animation 模块会随包依赖安装。使用键盘交互时，需要在项目设置中启用 Input System。

在自定义程序集内使用 UIManager 时，添加 `com.jh.uimanager.runtime` 和 `UniTask` 程序集引用。安装到正式项目时，建议在 Git URL 后指定标签或提交 SHA，以固定依赖版本。

## 快速开始

1. 在 Canvas 下创建一个面板，添加 `UIBase` 组件。
2. 添加效果控制器，例如 `AlphaEffectController`。
3. 在 Inspector 中配置初始状态、显隐时长和缓动曲线。
4. 通过 `Show()`、`Hide()` 或异步接口控制面板。

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using JH;
using UnityEngine;

public class PanelExample : MonoBehaviour
{
    [SerializeField] private UIBase panel;

    public void ShowPanel()
    {
        panel.Show();
    }

    public void HidePanel()
    {
        panel.Hide();
    }

    public async UniTask ShowPanelAsync(CancellationToken cancellationToken)
    {
        await panel.ShowAsync(cancellationToken);
    }

    public async UniTask HidePanelAsync(CancellationToken cancellationToken)
    {
        await panel.HideAsync(cancellationToken);
    }
}
```

同步接口在调度器空闲时立即应用对应的显隐行为；异步接口等待行为结束后再完成本轮生命周期。没有异步行为时，异步接口会执行对应的同步行为。

`UIBase` 负责生命周期和界面关系，具体表现由效果控制器或自定义行为提供。需要直接配置按钮、Toggle 和键盘交互时，可使用 `UIBaseWithShowHideInteraction`。

## 生命周期

| 状态 | 含义 |
| --- | --- |
| `Hided` | 已隐藏 |
| `Showing` | 正在显示 |
| `Showed` | 已显示 |
| `Hiding` | 正在隐藏 |

显示流程为 `Hided → Showing → Showed`，隐藏流程为 `Showed → Hiding → Hided`。被取消、被其他操作取代或执行失败的操作，不会继续提交成功的完成状态。

| 事件 | 用途 |
| --- | --- |
| `BeforeUIShowEvent` / `AfterUIShowedEvent` | 显示前与显示完成通知 |
| `BeforeUIHideEvent` / `AfterUIHidedEvent` | 隐藏前与隐藏完成通知 |
| `OnStateChanged` | 状态变化通知，参数为原状态和新状态 |
| `ShowActions` / `HideActions` | 同步显隐行为 |
| `ShowAsyncActions` / `HideAsyncActions` | 异步显隐行为，委托类型为 `Func<CancellationToken, UniTask>` |

状态及 Before/After 事件用于业务通知；Actions 用于执行显隐表现。同一个界面分别调用同步和异步接口时，需要为两种表现路径提供适用的行为。注册异步 Actions 后，异步接口只执行异步行为；未注册时才回退到同步行为。

业务通知按本轮订阅快照逐个执行。一个同步订阅者抛出异常时，Console 会报告面板、事件名、订阅方法和原始异常栈，其余订阅者及显隐流程继续执行；多个订阅者失败会分别报告。通知中的 `OperationCanceledException` 也按订阅者错误处理，不会单凭异常类型取消显隐。调用方显式取消操作令牌、后续请求取代操作、对象销毁等取消规则继续生效。

通知事件是同步 `Action`，不要使用 `async void` 承载需要框架观察的异步业务：这类回调的异常不按普通同步调用向外传播，不受这层同步派发保护。需要启动异步业务时，由订阅者自行观察任务并处理异常。显隐 Actions 失败仍按操作失败处理，不会提交成功的完成状态。

## 请求调度

显隐接口在 Unity 主线程调用。公开的 `Show()`、`Hide()`、`ShowAsync()`、`HideAsync()` 都提交到同一个内部队列：空闲时立即消费，正在执行时按提交顺序等待。当前同步调度部分返回后继续消费，不依赖 `Update` 轮询，也不主动等待下一帧。

例如，Showing 通知的订阅者 A 调用 `Hide()`，该请求会先排队；订阅者 B 仍会观察到 Showing。当前同步调度部分结束后，隐藏请求才开始执行。因此在回调中调用同步接口后，紧接着读取 `State`，得到的仍可能是调用前的状态。多个请求按顺序保留，实际执行时重新判断状态、父链及互斥优先级。

父子、互斥等内部联动属于当前调度，直接执行。异步行为等待期间释放调度器，后续请求仍可打断过渡；等待结束后的生命周期处理会重新进入队列。异步接口返回的任务跟踪各自请求的完成、取消或失败，排队期间取消的请求不会启动生命周期。

同步调用返回时，调度器也可能已经处理了回调提交的后续请求，因此应根据当前 `State` 判断最终状态。需要等待某次异步请求的结果时，等待它返回的任务；准入拒绝仍正常返回，不代表界面一定已显示。

显隐行为或调度失败时，空闲同步调用的异常返回给调用方；回调中排队的同步请求执行失败时，通过 Unity 异常日志报告，并继续处理其余请求。异步请求的操作异常通过各自任务传播。业务通知订阅者的异常单独记录，不作为操作失败。业务通知可以提出后续请求，但应避免无条件相互触发显隐形成无限循环。

### 循环调度保护

每个公开请求记录产生它的来源请求。默认情况下，同一条来源链中，同一个 UI 实例的同一种显隐行为最多实际执行 10 次，第 11 次执行前抛出 `UIRequestLoopException`。`Show` 与 `ShowAsync` 共用显示计数，`Hide` 与 `HideAsync` 共用隐藏计数。已经处于目标状态、取消或被准入条件拒绝的请求不计入实际执行。

独立的外部请求建立新链；同一通知提交的并列请求分别沿各自祖先链计数。框架异步等待和恢复保留原来源，即使队列暂时为空或动画跨帧，也不会重置计数。普通父子、互斥联动属于当前请求，不额外建立公开请求节点。

可以在主线程设置 `UIOperationSettings.MaxExecutionsPerBehavior`，取值必须大于 0；每条链使用根请求创建时的配置，后续修改只影响新链。有限重复低于上限时正常执行。该规则限制操作回环，不用于证明任意业务逻辑是否会无限执行；业务自行启动的协程、计时器等延后代码若脱离了框架请求恢复，会作为新的外部请求处理。

异常提供 `Panel`、`IsShow`、`PanelPath`、`ExecutionLimit`、`RequestPath` 和 `RequestOrigin`。错误消息包含被阻止的面板、行为及完整来源路径；Editor 和 Development Build 额外记录入队时的调用栈以定位业务代码。同步排队请求通过 Console 报告并关联目标面板；异步请求通过返回任务报告。超限请求不会开始新的生命周期、取消现有操作或隐藏互斥面板，其余排队工作及已经启动的异步收尾继续执行。

## 父子联动

界面关系以当前 Transform 层级为准，无需额外维护父子和兄弟列表。

### 随父显示

直接子界面勾选 `Show With Parent` 后，会在父界面自身的显示行为完成后自动显示。每一层都可以独立配置，形成多层级联。父界面的显示完成通知会等待这部分子界面流程结束。

未勾选的直接子界面保持原状态。父界面已经处于 `Showed` 时，再次调用 `Show()` 会直接返回，不会恢复被手动隐藏的子界面。

需要同时显示的多个子界面应使用不同互斥组；随父显示仍遵守分组互斥和优先级规则。

### 直接显示子界面

通过 `Parent Show Policy` 配置直接显示子界面时的准入条件：

| 策略 | 行为 |
| --- | --- |
| `AutoShowParentChain` | 自动从上到下补齐连续父界面链的显示生命周期 |
| `RequireParentChainShowedForDirectShow` | 要求直接父节点挂载 `UIBase`，且连续父界面链均已初始化、在层级中激活并处于 `Showed` |

父链扫描遇到没有 `UIBase` 的普通 Transform 时停止。严格父链条件未满足时，显示请求不会改变状态、隐藏兄弟界面或触发显示通知。

随父显示由父界面发起，不受外部直接显示的父链准入条件限制。

## 分组互斥与优先级

互斥作用于同一直接父节点下、互斥组相同的界面。不同父节点的相同组号互不影响，同一父节点下的不同互斥组可以同时显示。

- **相同优先级**：后接受的显示请求获胜。
- **不同优先级**：持有显示权的界面可以拒绝优先级严格低于自己的请求。
- **已隐藏的界面**：不会预占互斥组。
- **隐藏失败或取消**：不会继续持有显示权，后续获准请求会将残留过渡状态收口为 `Hided`。

Hierarchy 中会显示“互斥组/抢占优先级 状态”标记。`H` 表示隐藏或隐藏中，`S` 表示显示或显示中。

被准入仲裁拒绝的 `ShowAsync()` 会正常结束且不产生副作用。调用方需要确认界面是否已显示时，应在等待结束后检查 `State`。

## 异步与取消

显隐接口接收 `CancellationToken`。调用方取消、界面对象销毁，或当前操作被后续显隐操作取代时，通过 `OperationCanceledException` 传播取消。

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JH;

public static class PanelOperations
{
    public static async UniTask ShowAsync(UIBase panel, CancellationToken cancellationToken)
    {
        try
        {
            await panel.ShowAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 按业务需要处理本次显示取消。
        }
    }
}
```

异步行为需要主动观察传入的令牌。UIManager 会等待已经启动的并行行为全部结束，再报告失败；多个失败会聚合报告，全部属于取消时传播取消异常。忽略令牌的行为会继续执行，结束后仍会校验本轮操作是否有效。

预取消的请求不会启动行为、改变状态或触发通知。`UniTask` 通常按单次消费使用，不要直接对同一实例重复或并发等待；需要共享结果时，请按 UniTask 文档选择保留或转换机制。

## 内置效果

| 控制器 | 用途 |
| --- | --- |
| `AlphaEffectController` | 透明度过渡 |
| `RectEffectController` | 位置、旋转、缩放、尺寸和轴心过渡 |
| `ColorEffectController` | 颜色过渡 |
| `AnimController` | 播放 Unity Animation 动画 |
| `UIFlowGroupController` | UI 顶点与 UV 流式效果 |

基于 `EffectControllerBase` 的进度过渡使用真实时间，`Time.timeScale == 0` 时仍可推进。可以分别配置显示和隐藏时长，以及 `Ease` 缓动曲线。

### 自定义效果

继承 `EffectControllerBase`，重写 `OnEffectAmountChanged(float value)`，将统一进度应用到目标属性：`0` 表示完全显示，`1` 表示完全隐藏。

```csharp
using JH;
using UnityEngine;

public class ScaleEffectController : EffectControllerBase
{
    protected override void OnEffectAmountChanged(float value)
    {
        transform.localScale = Vector3.one * (1f - value);
    }
}
```

基类负责进度推进、取消和显隐事件订阅。需要更复杂的异步行为时，也可以通过 `ShowAsyncActions`、`HideAsyncActions` 注册处理器，并在适当的生命周期解除订阅。

## 常用 API

| 成员 | 作用 |
| --- | --- |
| `Show()` / `Hide()` | 同步显隐 |
| `ShowAsync(CancellationToken)` / `HideAsync(CancellationToken)` | 可取消的异步显隐，返回 `UniTask` |
| `State` | 当前生命周期状态 |
| `ShowWithParent` | 是否随直接父界面显示 |
| `ParentShowPolicy` | 直接显示时的父链准入策略 |
| `MutexPriority` | 同父同组界面的抢占优先级 |

## 示例与测试

在 Unity Package Manager 中选择 UIManager，导入“基础示例”，打开其中的 `TestUIManager.unity` 场景即可体验。运行时也可以通过 `UIBase` 组件的上下文菜单触发显隐操作。

包内包含生命周期、动态层级、抢占、取消、异常收尾和进度过渡的 PlayMode 测试。运行测试时，在宿主工程安装 Unity Test Framework，并在 `Packages/manifest.json` 的顶层加入：

```json
"testables": ["com.jh.uimanager"]
```

随后在 Unity Test Runner 中运行 PlayMode 测试。

## 许可证与反馈

UIManager 使用 [MIT License](LICENSE)。UniTask 的许可与文档见 [官方仓库](https://github.com/Cysharp/UniTask)。

问题反馈和功能建议请提交至 [GitHub Issues](https://github.com/jhxrzs/UIManager/issues)。
