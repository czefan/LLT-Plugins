# 🎨 UI Enhancement Plugin

[Lenovo Legion Toolkit (LLT)](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit) 界面综合增强插件。

---

## 🌟 功能特性

- **自定义模式滑动条重构**：左侧显示最小值，右侧显示最大值，滑动点（Thumb）正上方实时悬浮居中显示当前纯文本数值。
- **中文字体与清晰度优化**：中文环境下默认应用微软雅黑（Microsoft YaHei UI），启用 ClearType 高清文本渲染。
- **界面矢量缩放**：支持 100% ~ 200% 界面矢量缩放，并在“设置 - 外观”中无缝注入调节选项。

---

## 🧩 模块说明

所有功能模块均独立位于 `Features/` 目录中：

| 模块文件 | 说明 |
| :--- | :--- |
| `SliderEnhancer.cs` | 自定义模式滑动条外观重构与纯文本数值悬浮 |
| `ChineseFontOptimizer.cs` | 中文字体（微软雅黑）与清晰度优化 |
| `UiScaler.cs` | 界面矢量缩放与设置项注入 |

> [!TIP]
> 各功能完全解耦。如不需要某项功能，直接删除对应的 `.cs` 文件即可，重新编译依然正常运行。

---

## 🚀 安装使用

将本项目预编译好的 `UiEnhancement/dist/UiEnhancement.dll` 复制到插件目录即可：

```text
%LOCALAPPDATA%\LenovoLegionToolkit\Plugins\
```

### 快捷安装（PowerShell）

在**仓库根目录**下打开 PowerShell 执行以下命令：

```powershell
New-Item -ItemType Directory -Force -Path "$env:LOCALAPPDATA\LenovoLegionToolkit\Plugins"
Copy-Item "UiEnhancement\dist\UiEnhancement.dll" "$env:LOCALAPPDATA\LenovoLegionToolkit\Plugins\UiEnhancement.dll" -Force
```

> 安装后重启 Lenovo Legion Toolkit 即可生效。

---

## 🗑️ 卸载

在**仓库根目录**下执行以下命令，删除插件 DLL 及其配置文件即可：

```powershell
Remove-Item "$env:LOCALAPPDATA\LenovoLegionToolkit\Plugins\UiEnhancement.dll" -Force -ErrorAction Ignore
Remove-Item "$env:LOCALAPPDATA\LenovoLegionToolkit\ui_enhancement.json" -Force -ErrorAction Ignore
```

---

## 🛠️ 自行编译（可选）

在**仓库根目录**下执行：

```powershell
dotnet build .\UiEnhancement\UiEnhancement.csproj -c Release
```

编译产物将自动输出至 `UiEnhancement\dist\UiEnhancement.dll`。
