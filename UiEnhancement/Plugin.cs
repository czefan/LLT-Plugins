using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Station.Core;

namespace UiEnhancement;

/// <summary>
/// UI 增强插件入口类，负责自动发现并调度所有 IUiFeature 模块。
/// </summary>
public sealed class Plugin : IExtensionProvider
{
    private readonly List<IUiFeature> _features = [];

    public void Initialize(IExtensionContext context)
    {
        _features.Clear();

        var featureTypes = typeof(Plugin).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(IUiFeature).IsAssignableFrom(t));

        foreach (var type in featureTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is IUiFeature feature)
                {
                    feature.Initialize(context);
                    _features.Add(feature);
                }
            }
            catch (Exception ex)
            {
                context.Logger?.Error($"[UiEnhancement] Failed to initialize feature: {type.Name}", ex);
            }
        }
    }

    public Task ExecuteAsync(string action, params object[] args) => Task.CompletedTask;

    public object? GetData(string key) =>
        _features.Select(f => f.GetData(key)).FirstOrDefault(v => v is not null);

    public void SetData(string key, object? value)
    {
        foreach (var feature in _features)
        {
            if (feature.SetData(key, value))
                break;
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var feature in _features)
        {
            try
            {
                feature.Dispose();
            }
            catch { }
        }
        _features.Clear();
        return ValueTask.CompletedTask;
    }
}
