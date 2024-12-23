# UIManager

UIManager是一款简易的用来管理UI界面显示隐藏的管理插件。
它依赖于UniTask插件和DoTween插件，前者用来执行异步的显示隐藏操作，后者用来执行UI界面的动画效果。

## 安装指南

### 1. 导入UniTask插件

相关链接：https://gitee.com/wmzjzxyh/UniTask#https://gitee.com/link?target=https%3A%2F%2Fgithub.com%2FCysharp%2FUniTask%23upm-package

此链接是UniTask在Gitee上的克隆地址，因为中国大陆防火墙问题，您可能无法直接访问原生的GitHub地址，为此而提供了克隆地址。
您需要根据文档，将UniTask插件导入到您的项目中。

### 2. 导入DoTween插件

开发过程中，使用的DoTween插件版本为：DOTween Pro 1.0.310;
当您成功引入此插件到您的项目中后，您需要在Dotween设置面板中，生成.asmdef文件。

### 3. 导入UIManager插件

您可以通过地址：https://gitee.com/wmzjzxyh/uimanager.git来导入UIManager插件。
如果出现Dotween相关代码报错，您需要手动在com.weshape3d.uimanager.runtime.asmdef中添加对DoTween的引用。这是由于.meta文件guid不同导致的引用丢失而引发的异常。

### 4. 至此，您已经成功导入UIManager插件。