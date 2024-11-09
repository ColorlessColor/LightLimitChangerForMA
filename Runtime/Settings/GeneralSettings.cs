namespace io.github.azukimochi;

[Serializable]
public sealed class GeneralSettings
{
    /// <summary>
    /// AnchorOverrideとRoot Boneを上書きする（しかしどこに？）
    /// </summary>
    public bool OverwriteMeshSettings = true;

    /// <summary>
    /// パラメータを圧縮する
    /// </summary>
    public bool CompressionExpressionParameters = false;

    public LightingSettings LightingControl = new();
    public ColorControlSettings ColorControl = new();
}
