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
- 每个目标可独立开关光标移开自动最小化，默认 60 秒；光标位于目标或同进程/所属衍生窗口内会重置计时，0 秒在光标移开后立即最小化，仅从小悬浮窗进入目标时保留 1 秒过渡保护
- 每个目标可独立启用 120 × 40 悬浮窗，支持点击或停留 0.3 秒触发、高 DPI 拖动和屏幕边缘吸附，且不会出现在 Alt+Tab 列表
- 默认以普通权限运行，仅修改任务计划中的开机启动选项时请求管理员权限
- 左侧名称列表、右侧详情的管理布局
- 根据程序路径、窗口类、AppUserModelID 和浏览器 Web App ID 在重启后重新识别窗口
- 导入 TXT 小说，自动识别 UTF-8/GB18030 等常见编码并解析中英文章节
- 使用无标题栏、可调整大小和置顶的阅读悬浮窗；正文不可选中并可从任意正文区域拖动窗口，支持字体、字号与真正的窗口透明度调节，并按书籍记忆章节、阅读位置和窗口尺寸
- 阅读模块复用 120 × 40 小悬浮窗，支持点击或停留 0.3 秒触发；小窗与阅读窗口位置独立，触发后均保持显示
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
文档\LimeLisest\TouchFish\log
```

## 项目边界

- `TouchFish.Contracts`：共享契约
- `TouchFish.Platform.Windows`：Win32 能力
- `TouchFish.UI.FloatingWidgets`：老板键和看书共用的小悬浮窗
- `TouchFish.Modules.BossKey`：老板键业务和界面
- `TouchFish.Modules.Reader`：TXT 书库、章节解析与阅读界面
- `TouchFish.App`：程序入口、分页导航、设置与依赖装配
