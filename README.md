# UIManager

基于 UniTask 的轻量级 Unity UI 管理库，支持父子界面联动、分组互斥、优先级抢占及可取消的异步显隐。内置缓动过渡，支持自定义效果，通过 Unity Package Manager 集成。

这是使用 UniTask 的 **2.x 版本**。异步返回类型与旧版不兼容；旧 Gitee 仓库和 JTask、JTween 独立包继续服务历史项目，升级由使用项目主动选择。

## 安装

要求 Unity 6000.0 或更新版本。在项目的 `Packages/manifest.json` 中加入以下依赖，并保留原有的其他条目：

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
    "com.jh.uimanager": "https://github.com/jhxrzs/UIManager.git"
  }
}
```

先安装 UniTask，再安装 UIManager。UniTask 固定在 2.5.11；UIManager 发布稳定版本后，也建议在安装地址后追加对应 Git 标签或提交 SHA。

uGUI、Input System 和 Animation 模块由包依赖引入，不需要安装 JTask、JTween 或 DOTween。键盘交互需要在项目设置中启用 Input System。默认安装不要求 Odin Inspector。

## 快速开始

1. 在面板 GameObject 上添加 `UIBase`。
2. 添加 `AlphaEffectController` 或 `RectEffectController`，配置初始状态、显隐时长和曲线。
3. 使用同步或异步 API 控制面板。

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using JH;
using UnityEngine;

public class PanelExample : MonoBehaviour
{
    [SerializeField] private UIBase panel;

    public async UniTask OpenAsync(CancellationToken cancellationToken)
    {
        await panel.ShowAsync(cancellationToken);
    }

    public void CloseImmediately()
    {
        panel.Hide();
    }
}
```

在 Package Manager 中导入“基础示例”可查看示例场景。完整层级和配置说明见 [使用指南](README.zh-CN.md)。

## 关键行为

- 显示生命周期为 `Hided → Showing → Showed`；隐藏为 `Showed → Hiding → Hided`。保留原有 `Hided` 命名。
- 关系依据当前 Transform 层级推导，支持运行时新增、移除和换父。
- 同一直接父节点、互斥组相同的 UI 进行仲裁：相等优先级后请求获胜，更高优先级的有效持有者可拒绝低优先级请求。不同父节点的相同组号互不影响。
- `ShowWithParent` 使直接子 UI 在父 UI 自身行为结束后自动显示，并可向下级联。父 UI 的完成通知会等待级联操作结束。
- 直接显示子 UI 默认自动补齐连续父 UI 链，也可配置为要求父 UI 链已经显示。遇到没有 `UIBase` 的普通 Transform 时停止向上扫描。
- 准入拒绝的 `ShowAsync()` 正常结束且无副作用。需要区分是否成功显示时，等待后检查 `State`。

## 异步与取消

公开异步 API 返回 `UniTask`，异步行为事件为 `Func<CancellationToken, UniTask>`。

```csharp
private async UniTask PrepareAsync(CancellationToken cancellationToken)
{
    await UniTask.NextFrame(cancellationToken: cancellationToken);
    // 加载数据或执行自定义行为。
}

// 在相应生命周期中注册，并使用同一方法引用取消注册。
// panel.ShowAsyncActions += PrepareAsync;
// panel.ShowAsyncActions -= PrepareAsync;
```

预取消不会启动行为或改变状态。调用方取消、对象销毁或新请求取代旧请求时，以 `OperationCanceledException` 传播取消；旧操作不能覆盖新操作的完成状态。

已启动的并行行为全部被观察，结束后才传播失败；多个失败聚合报告，全部属于取消时传播取消异常。处理器需要主动观察令牌；忽略令牌的行为会继续执行，UIManager 会等待它结束，再校验本轮操作。

`UniTask` 按单次消费使用，不要对同一实例重复或并发 `await`；共享结果应按 UniTask 文档使用保留或转换机制。业务调用方负责处理预期取消和其他异常。

## 内置效果与扩展

| 控制器 | 用途 |
| --- | --- |
| `AlphaEffectController` | 透明度显隐 |
| `RectEffectController` | 位置、旋转、缩放、尺寸和轴心过渡 |
| `ColorEffectController` | 颜色过渡 |
| `AnimController` | Unity Animation 动画 |
| `UIFlowGroupController` | UI 顶点与 UV 流式效果 |

继承 `EffectControllerBase` 并重写 `OnEffectAmountChanged(float value)` 可应用自定义效果，进度 `0` 为显示、`1` 为隐藏。

内置进度过渡使用真实时间，游戏暂停时仍可推进。推进器和缓动计算为程序集内部实现；`Ease` 枚举公开用于配置，保留原有枚举数值含义。本包不对外提供通用 Tween API。

## 从 1.x 迁移

1. 在使用项目的分支中把 UIManager 地址改为本仓库，安装 UniTask。历史项目无需升级。
2. 将自定义异步显隐方法的返回类型由 `JTask` 改为 `UniTask`，添加 `using Cysharp.Threading.Tasks;`，同步调整程序集引用。
3. UIManager 不再依赖 JTask、JTween。若项目其他代码仍使用这些包，先完成那些调用的迁移，再决定是否移除依赖。
4. 项目只安装一个版本的 `com.jh.uimanager`。新版保留同一包标识、命名空间和既有 UI 脚本 GUID，用于升级现有场景，不支持新旧版本同时安装。
5. 直接调用 JTween、`EaseFunctionNumeric` 或 `ITweenData<T>` 的业务代码需要单独迁移；新版的扩展入口为 UI 生命周期与效果控制器。

## 测试

包包含 PlayMode 回归测试，覆盖生命周期、父子关系、抢占、取消、异常收尾和内部过渡。在宿主工程安装 Unity Test Framework，并在清单顶层加入以下字段后，可通过 Test Runner 运行：

2026-10-15 已在独立的 Unity 6000.4.5f1 工程中验证：UniTask 2.5.11、uGUI 2.0.0，编译通过，60 项 PlayMode 测试全部通过。

```json
"testables": ["com.jh.uimanager"]
```

## 来源与许可

本仓库保留 [原 UIManager](https://gitee.com/wmzjzxyh/uimanager) 的 Git 历史，迁移起点为 `6d9c8ef`。内置曲线来自作者自研 [JTween](https://gitee.com/wmzjzxyh/jtween)，来源提交为 `8e1fa29`。

UIManager 使用 [MIT License](LICENSE)。[UniTask](https://github.com/Cysharp/UniTask) 是独立 MIT 许可依赖，通过包管理安装，源码未内置于本仓库；分发时应保留所包含第三方组件的版权与许可声明。
