using System;
using LenovoLegionToolkit.Lib.Station.Core;

namespace UiEnhancement;

/// <summary>
/// UI 增强功能模块接口，各功能模块实现此接口即可被插件自动发现与调度。
/// </summary>
public interface IUiFeature : IDisposable
{
    /// <summary>
    /// 初始化并挂载功能（传入宿主扩展上下文）
    /// </summary>
    void Initialize(IExtensionContext context);

    /// <summary>
    /// 获取功能对外暴露的数据（可选）
    /// </summary>
    object? GetData(string key) => null;

    /// <summary>
    /// 设置功能数据或触发指令，若成功处理则返回 true（可选）
    /// </summary>
    bool SetData(string key, object? value) => false;

    /// <summary>
    /// 释放并清理资源
    /// </summary>
    void IDisposable.Dispose() { }
}
