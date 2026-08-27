# 🧩 Lenovo Legion Toolkit Plugins

个人自用的 [Lenovo Legion Toolkit (LLT)](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit) 功能扩展插件集。

LLT 原生支持基于 `IExtensionProvider` 接口的扩展加载机制，无需修改官方主程序源码，即可通过插件扩展界面与系统控制功能。

---

## 📂 插件列表

| 插件 | 目录 | 说明 |
| :--- | :--- | :--- |
| **🎨 UI Enhancement** | [`UiEnhancement`](./UiEnhancement) | 界面矢量缩放、微软雅黑与 ClearType 字体优化、自定义模式滑块悬浮数值重构 |

---

## 🚀 快捷安装与使用

在**仓库根目录**下打开 PowerShell 执行以下命令，将所有预编译插件一键部署至系统：

```powershell
New-Item -ItemType Directory -Force -Path "$env:LOCALAPPDATA\LenovoLegionToolkit\Plugins"
Get-ChildItem -Path "*\dist\*.dll" | Copy-Item -Destination "$env:LOCALAPPDATA\LenovoLegionToolkit\Plugins" -Force
```

> 安装后重启 Lenovo Legion Toolkit 即可生效。如需查看各插件的具体功能与单独配置，请参见对应插件目录下的 `README.md`。

---

## 🗑️ 卸载

在**仓库根目录**下执行以下命令，清理所有插件 DLL 及其配置文件：

```powershell
Remove-Item "$env:LOCALAPPDATA\LenovoLegionToolkit\Plugins\*.dll" -Force -ErrorAction Ignore
Remove-Item "$env:LOCALAPPDATA\LenovoLegionToolkit\ui_enhancement.json" -Force -ErrorAction Ignore
```

---

## 🛠️ 开发与本地编译（面向开发者）

### 1. 官方依赖源码管理 (`LenovoLegionToolkit-master`)

本项目引用了官方主程序仓库的项目依赖（`LenovoLegionToolkit.Lib` 与 `LenovoLegionToolkit.WPF`）。

- **首次拉取官方源码**：

  ```powershell
  git clone --depth 1 https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit.git LenovoLegionToolkit-master
  ```

- **同步官方最新代码**：

  ```powershell
  git -C LenovoLegionToolkit-master pull
  ```

### 2. 一键编译所有插件

在**仓库根目录**下执行：

```powershell
Get-ChildItem *\*.csproj | ForEach-Object { dotnet build $_.FullName -c Release }
```

编译产物将自动输出至各插件子目录的 `dist\` 目录下。

### 3. 插件加载机制 (`IExtensionProvider`)

程序启动时会自动扫描并加载 `%LOCALAPPDATA%\LenovoLegionToolkit\Plugins\` 目录中的所有 `.dll` 文件。插件需实现 `IExtensionProvider` 接口：

```csharp
namespace LenovoLegionToolkit.Lib.Station.Core;

public interface IExtensionProvider : IAsyncDisposable
{
    void Initialize(IExtensionContext context);
    Task ExecuteAsync(string action, params object[] args);
    object? GetData(string key);
    void SetData(string key, object? value);
}
```
