namespace io.github.azukimochi;

[CustomEditor(typeof(LightLimitChangerSettings))]
internal sealed class OldLightLimitChangerEditror : Editor
{
    public override void OnInspectorGUI()
    {
        var target = (LightLimitChangerSettings)base.target;
        LightLimitChangerComponentEditor.CategoryLabel($"{LightLimitChanger.Title} {LightLimitChanger.Version}");
        EditorGUILayout.Space();

        EditorGUILayoutUtils.HelpBox(L10n.Tr("migration:v1/message"), MessageType.Warning);

        if (GUILayout.Button(L10n.Tr("common:label/migrate"), LightLimitChangerComponentEditor.TabButtonStyle))
        {
            var component = Migration.MigrateV1toV2(target);
            EditorGUIUtility.PingObject(component);
        }
    }
}
