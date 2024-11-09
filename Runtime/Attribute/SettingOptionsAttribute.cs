namespace io.github.azukimochi;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class SettingOptionsAttribute : Attribute
{
    public SettingOptionsAttribute(string id, string menuPath, string parameterPrefix = null)
    {
        Id = id;
        MenuPath = menuPath;
        ParameterPrefix = parameterPrefix;
    }

    /// <summary>
    /// ローカライズとかに使うID
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// メニューのパス
    /// </summary>
    public string MenuPath { get; }

    /// <summary>
    /// パラメーター名の接頭辞
    /// </summary>
    public string ParameterPrefix { get; }
}