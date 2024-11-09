using System.Collections.Generic;
using System.Reflection;

namespace io.github.azukimochi;

internal class SettingsFieldInfo
{
    private static readonly Dictionary<Type, SettingsFieldInfo> infos = new();
    public SettingOptionsAttribute Options { get; }

    public string Id { get; }
    public string MenuPath { get; }
    public string ParameterPrefix { get; }

    public SettingsFieldInfo(Type type)
    {
        Options = type.GetCustomAttribute<SettingOptionsAttribute>();
        if (Options is null)
        {
            Id = type.FullName;
            MenuPath = type.Name;
            ParameterPrefix = type.Name;
        }
        else
        {
            Id = Options.Id;
            MenuPath = Options.MenuPath;
            ParameterPrefix = Options.ParameterPrefix;
        }

        infos.TryAdd(type, this);
    }

    public static SettingsFieldInfo GetInfo(Type type)
    {
        if (infos.TryGetValue(type, out var info))
            return info;

        info = new SettingsFieldInfo(type);
        return info;
    }
}

internal sealed class SettingsFieldInfo<TSettings> : SettingsFieldInfo where TSettings : ISettings
{
    private static SettingsFieldInfo Instance { get; }

    public static new string Id => Instance.Id;
    public static new string MenuPath => Instance.MenuPath;
    public static new string ParameterPrefix => Instance.ParameterPrefix;

    static SettingsFieldInfo()
    {
        Instance = GetInfo(typeof(TSettings));
    }

    private SettingsFieldInfo() : base(typeof(TSettings))
    {
    }
}