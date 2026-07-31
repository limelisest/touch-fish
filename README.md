# TouchFish

TouchFish 是一个可扩展的 Windows 摸鱼工具集合，当前包含**老板键**与**看书**模块。

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
- 以管理员权限运行，确保当前焦点位于管理员应用时仍可唤起 Chrome/PWA 等普通窗口
- 左侧名称列表、右侧详情的管理布局
- 根据程序路径、窗口类、AppUserModelID 和浏览器 Web App ID 在重启后重新识别窗口
- 导入 TXT 小说，自动识别 UTF-8/GB18030 等常见编码并解析中英文章节
- 使用可调整大小、可置顶的阅读悬浮窗，按书籍记忆章节、阅读位置和窗口尺寸
- 阅读模块复用 120 × 40 小悬浮窗，可点击或停留触发，并自动纳入老板键统一控制
- 开机自启动、静默启动以及版本、编译时间和作者信息

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

配置和书库保存在：

```text
%LocalAppData%\TouchFish\modules\boss-key\settings.v1.json
%LocalAppData%\TouchFish\appsettings.json
文档\LimeLisest\TouchFish\books
```

## 项目边界

- `TouchFish.Contracts`：共享契约
- `TouchFish.Platform.Windows`：Win32 能力
- `TouchFish.UI.FloatingWidgets`：老板键和看书共用的小悬浮窗
- `TouchFish.Modules.BossKey`：老板键业务和界面
- `TouchFish.Modules.Reader`：TXT 书库、章节解析与阅读界面
- `TouchFish.App`：程序入口、分页导航、设置与依赖装配
