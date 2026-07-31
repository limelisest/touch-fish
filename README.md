# TouchFish

TouchFish 是一个可扩展的 Windows 摸鱼工具集合。当前实现第一个模块：**老板键**。

## 当前功能

- 设置并持久化全局快捷键
- 点击按钮进入选择模式：自动隐藏 TouchFish，单击目标窗口添加，Esc 取消
- 修改规则名称和标题关键词
- 删除、刷新和定位目标窗口
- 快捷键按当前整体状态统一全部最小化或统一全部显示
- 关闭设置窗口后驻留系统托盘
- 可靠跟随 Windows 应用浅色/深色模式，并在系统主题变化时实时切换
- 支持调整目标顺序：列表顶部的窗口最后恢复并显示在最上层
- 每个目标可独立开关失焦自动最小化，默认 60 秒；0 秒表示失焦后立即最小化
- 每个目标可独立启用 120 × 40 悬浮窗，支持点击或停留 0.3 秒触发、正确映射高 DPI 拖动、屏幕边缘吸附
- 左侧名称列表、右侧详情的管理布局
- 根据程序路径、窗口类、AppUserModelID 和浏览器 Web App ID 在重启后重新识别窗口

## 识别 Chrome / Edge Web App

TouchFish 会读取窗口的 `System.AppUserModel.ID` 与 `System.AppUserModel.RelaunchCommand`。如果 Chrome/Edge
将 Telegram 安装为 Web App，并在重启命令中提供 `--app-id`，TouchFish 会优先保存这个稳定 ID，而不是依赖窗口句柄或聊天标题。

如果“浏览器 App ID”为空，规则会回退到：程序路径 + 窗口类 + 标题包含。此时建议把标题关键词修改为 `Telegram` 等稳定内容。

## 开发

需要 Windows 和 .NET 10 SDK：

```powershell
dotnet restore
dotnet build TouchFish.slnx
dotnet run --project src/TouchFish.App
```

配置保存在：

```text
%LocalAppData%\TouchFish\modules\boss-key\settings.v1.json
```

## 项目边界

- `TouchFish.Contracts`：共享契约
- `TouchFish.Platform.Windows`：Win32 能力
- `TouchFish.Modules.BossKey`：老板键业务和界面
- `TouchFish.App`：程序入口与依赖装配
