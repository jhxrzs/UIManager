# UIManager 📱

UIManager 是一款简洁高效的 Unity UI 界面管理插件，专注于提供便捷的 UI 显示隐藏管理功能。

## ⚙️ 安装指南

将以下依赖添加到项目的 `manifest.json` 文件中：

```json
{
  "dependencies": {
    "com.jh.jtask": "https://gitee.com/wmzjzxyh/jtask.git",
    "com.jh.jtween": "https://gitee.com/wmzjzxyh/jtween.git",
    "com.unity.ugui": "2.0.0",
    "com.jh.uimanager": "https://gitee.com/wmzjzxyh/uimanager.git"
  }
}
```

## 🚀 快速开始

### 1. UI 结构设计

在常见项目中，UI 通常以面板为单位进行管理，每个面板包含多个元素，形成层级结构：

[UI层级结构示例](img/img.png)

您可以根据自己的需求搭建类似结构的 UI 界面。为了便于区分，建议给不同面板设置不同的颜色或标识。

### 2. 配置 UI 面板组件

#### 核心组件

每个 UI 面板需要两个关键脚本：

| 脚本类型 | 功能描述 | 必要性 |
|---------|---------|--------|
| `UIBase.cs` 或其子类 | 负责 UI 的核心显示隐藏逻辑 | ✅ 必需 |
| 行为控制器脚本 | 实现具体的显隐效果 | ✅ 必需 |

#### 内置行为控制器

| 控制器名称 | 功能描述 |
|-----------|---------|
| `AlphaEffectController.cs` | 通过调节透明度实现显隐效果 |
| `RectEffectController.cs` | 通过修改 Transform 实现显隐效果 |
| `VertUIFlowController.cs` | 实现复杂的 UI 流式显隐行为 |

#### 挂载步骤

1. 选择 UI 面板，添加 `UIBase.cs` 组件
2. 添加一个行为控制器组件（如 `AlphaEffectController.cs`）

#### 互斥组标识

添加 `UIBase` 后，Hierarchy 面板中会显示带颜色的标识：

[互斥组标识](img/img_3.png)

标识解释：
- **数字**：表示当前面板所在的互斥组（同组 UI 同时只能显示一个）
- **字母 H**：表示此面板默认为隐藏状态 (Hidden)
- **字母 S**：表示此面板默认为显示状态 (Showed)

#### 设置初始状态

在 Inspector 面板中找到 UIBase 组件，修改 State 属性：

[设置状态](img/img_4.png)

### 3. 运行与测试

运行程序后，您可以通过 UIBase 脚本的上下文菜单测试 UI 的显隐行为：

[上下文菜单](img/img_5.png)

## 📚 API 参考

UIBase 提供了以下核心 API：

| 方法 | 描述 | 返回值 |
|------|------|--------|
| `Show()` | 直接显示 UI 面板 | void |
| `Hide()` | 直接隐藏 UI 面板 | void |
| `ShowAsync(CancellationToken)` | 异步显示 UI 面板 | JTask |
| `HideAsync(CancellationToken)` | 异步隐藏 UI 面板 | JTask |

> 💡 注意：`JTask` 可视为等同于 .NET 中的 `Task` 使用。

## ⚠️ 使用注意事项

1. **互斥组规则**：同一互斥组内，同时只能有一个 UI 面板处于显示状态
2. **层级管理**：同一父节点下的不同互斥组 UI 可以同时显示，最后显示的面板会自动置顶
3. **自定义行为**：您可以通过继承现有控制器来实现自定义的显隐效果

## 🔍 示例项目

您可以通过 Unity Package Manager 导入示例项目，查看更多使用场景：

1. 打开 Package Manager
2. 找到 UIManager 包
3. 点击 "Samples" 选项
4. 导入示例项目

## 🔧 进阶配置

想要了解更多高级功能和配置选项，请参考：
- 完整 API 文档
- 源代码注释
- 示例项目中的场景示例

---

📢 如有任何问题或建议，欢迎通过 [Gitee 仓库](https://gitee.com/wmzjzxyh/uimanager) 提交 Issue 或 Pull Request！