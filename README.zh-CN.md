# UIManager

UIManager是一款简易的用来管理UI界面显示隐藏的管理插件。

## 安装指南

### 1. 导入UniTask插件

相关链接：https://gitee.com/wmzjzxyh/UniTask#https://gitee.com/link?target=https%3A%2F%2Fgithub.com%2FCysharp%2FUniTask%23upm-package

此链接是UniTask在Gitee上的克隆地址，因为中国大陆防火墙问题，您可能无法直接访问原生的GitHub地址，为此而提供了克隆地址。
您需要根据文档，将UniTask插件导入到您的项目中。

您可以使用：https://gitee.com/wmzjzxyh/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask 来导入UniTask插件。
或者添加以下文本到manifest.json文件中：

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://gitee.com/wmzjzxyh/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
  }
}
```

### 2. 导入Tween插件

由于DoTween插件通常放置在Assets/Plugins目录下，不便于包之间的依赖管理，因此我们提供了一个简易的Tween插件，您可以通过以下地址导入Tween插件：
https://gitee.com/wmzjzxyh/jtween.git
或者添加以下文本到manifest.json文件中：

```json
{
  "dependencies": {
    "com.jh.jtween": "https://gitee.com/wmzjzxyh/jtween.git"
  }
}
```

### 3. 导入模糊匹配插件

您可以使用git添加此包体，地址：https://gitee.com/wmzjzxyh/fuzzy-matching.git
也可以在manifest.json中添加以下代码：

```json
{
  "dependencies": {
    "com.jh.fuzzymatching": "https://gitee.com/wmzjzxyh/fuzzy-matching.git"
  }
}
```

来引入模糊匹配插件。

### 4. 导入UIManager插件

您可以通过地址：https://gitee.com/wmzjzxyh/uimanager.git来导入UIManager插件。
或者添加以下文本到manifest.json文件中：

```json
{
  "dependencies": {
    "com.jh.uimanager": "https://gitee.com/wmzjzxyh/uimanager.git"
  }
}
```

### 4. 至此，您已经成功导入UIManager插件。