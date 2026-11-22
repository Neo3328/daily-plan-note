# 每日计划 · Daily Plan Note

**简体中文** · [English](README.en.md)

<a href="https://github.com/Neo3328/daily-plan-note/releases/latest/download/DailyPlanNote-Setup.exe"><img src="assets/windows-setup.png" width="185" alt="下载 Windows 安装包"></a>
<a href="https://github.com/Neo3328/daily-plan-note/releases/latest/download/DailyPlanNote.exe"><img src="assets/windows-portable.png" width="185" alt="下载 Windows 免安装 EXE"></a>
<a href="https://github.com/Neo3328/daily-plan-note/releases/latest/download/DailyPlanNote-macOS.dmg"><img src="assets/macos-dmg.png" width="185" alt="下载 macOS DMG"></a>

[查看发布说明和文件校验值](https://github.com/Neo3328/daily-plan-note/releases/latest)

适用于 Windows 和 macOS 的桌面每日计划便签，支持按天记录、勾选完成、中英文界面和浅色／深色主题。

| 平台 | 安装方法 |
| --- | --- |
| Windows 安装版 | 下载并运行 `DailyPlanNote-Setup.exe`。首次使用时按提示安装。升级时直接覆盖现有版本，**无需先卸载**。同一 Windows 账户下已有的计划、勾选状态和设置都会保留。如果提示关闭正在运行的便签，选择允许并继续安装。 |
| Windows 免安装版 | 将 `DailyPlanNote.exe` 放在固定文件夹，双击运行。 |
| macOS 13 或更高版本 | 打开 DMG，将 Daily Plan Note 拖到 Applications，再从“应用程序”启动。通用程序包含 Apple 芯片和 Intel 两种架构。 |

macOS 版使用 SwiftUI 原生界面。应用内提供日期选择、逐日清单、置顶、主题、语言和登录时打开等选项。数据保存在当前 macOS 用户的偏好设置中。此版本采用临时签名，尚未经过 Apple 公证。Windows 版使用 Windows Forms，以下截图来自 Windows。

## 界面预览

<table>
  <tr><th>浅色便签</th><th>深色便签</th></tr>
  <tr>
    <td align="center"><img src="docs/screenshots/light.png" width="320" alt="浅色中文便签"></td>
    <td align="center"><img src="docs/screenshots/dark.png" width="320" alt="深色英文便签"></td>
  </tr>
  <tr><th>中文日历</th><th>English calendar</th></tr>
  <tr>
    <td align="center"><img src="docs/screenshots/calendar-zh.png" width="320" alt="浅色中文日历"></td>
    <td align="center"><img src="docs/screenshots/calendar-en.png" width="320" alt="深色英文日历"></td>
  </tr>
</table>

## Windows 使用

将下载的 `DailyPlanNote.exe` 放到准备长期使用的文件夹，双击运行。首次启动默认启用登录自启，可在便签底部关闭。

- 直接输入计划，按 Enter 新建一条。日期栏可切换到其他日期，「今天」可返回当天。
- 拖动标题区移动便签，拖右下角调整大小。应用会记住窗口布局。
- 点击齿轮可选择跟随系统、浅色或深色主题，也可切换语言和动效。
- 点击右上角的 × 收起到系统托盘。点击托盘图标恢复，右键菜单可退出。

计划、完成状态和偏好保存在当前账户的 `%LOCALAPPDATA%\DailyPlanNote`。登录自启使用当前用户注册表中的 `DailyPlanNote` 启动项。

## 从源码构建

在 Windows 上安装或启用 .NET Framework 4.8。在仓库根目录运行：

```bat
build.cmd
```

生成的程序位于 `bin\DailyPlanNote.exe`。源码使用 C# 和 Windows Forms。构建脚本调用系统的 .NET Framework C# 编译器，无需安装 NuGet 包。

在 macOS 上安装 Xcode 后，运行 `bash mac/build.sh`，生成的通用安装镜像位于 `bin/mac/DailyPlanNote-macOS.dmg`。Windows 安装包可在运行 `build.cmd` 后，用 Inno Setup 6 编译 `installer/windows.iss` 生成。

## 许可与致谢

本项目原创代码与图标采用 [MIT 许可证](LICENSE)。部分动效的缓动公式与交互时序参考了龙腾猫跃的 Plain Craft Launcher 2，相关说明保留在源码中。
