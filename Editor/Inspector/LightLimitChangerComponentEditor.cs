using Target = io.github.azukimochi.LightLimitChangerComponent;

namespace io.github.azukimochi;

[CustomEditor(typeof(Target))]
internal sealed partial class LightLimitChangerComponentEditor : Editor
{
    private static Texture2D linearGrayTexture;
    internal enum Tab
    {
        BasicSettings,
        AdvancedSettings,
        DiescriptionMode,
    }
    
    public static Tab SelectedTab = Tab.BasicSettings;

    public static bool IsAdvancedMode { get => Preferences.Local.AdvancedMode; set => Preferences.Local.AdvancedMode = value; }

    public static bool ShowDescriptions { get => Preferences.Local.ShowDescription; set => Preferences.Local.ShowDescription = value; }

    public bool PresetMode { get; set; }

    public void OnEnable()
    {
        SelectedTab = IsAdvancedMode ? ShowDescriptions ? Tab.DiescriptionMode : Tab.AdvancedSettings : Tab.BasicSettings;
        var target = (Target)base.target;
        PresetMode = target.transform.parent?.GetComponent<Target>() != null;
    }

    public override void OnInspectorGUI()
    {
        var target = (Target)base.target;
        CategoryLabel($"{LightLimitChanger.Title} {LightLimitChanger.Version}");
        EditorGUILayout.Space();
        DrawLanguagePicker();
        DrawTabSelector();

        CategoryLabel(L10n.TrStr("category:preset"));
        EditorGUILayout.Space();
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.DelayedTextField(serializedObject.FindProperty(nameof(Target.PresetName)), GUIContent.none);
            if (GUILayout.Button("S", GUILayout.Width(24)))
            {
                PresetManager.Global.Update(target.PresetName, target);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space();
        CategoryLabel(L10n.TrStr("category:general-settings"));
        EditorGUILayout.Space();

        DoPropertyGUI<LightingSettings>(serializedObject.FindProperty("General.LightingControl"));
        DoPropertyGUI<ColorControlSettings>(serializedObject.FindProperty("General.ColorControl"));

        CategoryLabel(L10n.TrStr("category:material-settings"));
        EditorGUILayout.Space();

        DoPropertyGUI<LilToonSettings>(serializedObject.FindProperty("LilToon"));
        DoPropertyGUI<PoiyomiSettings>(serializedObject.FindProperty("Poiyomi"));
        DoPropertyGUI<UnlitWFSettings>(serializedObject.FindProperty("UnlitWF"));

        if (!PresetMode)
        {
            CategoryLabel(L10n.TrStr("category:other-settings"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(L10n.TrStr("settings:other/excludes/label"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Excludes"), true);
            if(ShowDescriptions)
            {
                EditorGUILayout.HelpBox(L10n.TrStr("settings:other/excludes/description"), MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(L10n.TrStr("settings:other/writedefault/label"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("WriteDefaults"));
            if(ShowDescriptions)
            {
                EditorGUILayout.HelpBox(L10n.TrStr("settings:other/writedefault/description"), MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(L10n.TrStr("settings:other/target_shader/label"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetShader"));
            if(ShowDescriptions)
            {
                EditorGUILayout.HelpBox(L10n.TrStr("settings:other/target_shader/description"), MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(L10n.Tr("settings:other/compress-expression-parameters/label"), EditorStyles.boldLabel);
            ParameterDrawer.PopupCheckbox(EditorGUILayout.GetControlRect(), serializedObject.FindProperty($"General.{nameof(GeneralSettings.CompressionExpressionParameters)}"), L10n.Tr("settings:other/compress-expression-parameters/label"));
            if (ShowDescriptions)
            {
                EditorGUILayout.HelpBox(L10n.TrStr("settings:other/compress-expression-parameters/description"), MessageType.Info);
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }


    private static void CategoryLabel(string title) => EditorGUILayout.LabelField(title, Styles.CategoryLabel.Value);

    private static void DrawSeparator()
    {
        var position = EditorGUILayout.GetControlRect(false, 12);
        position.y += 4;
        position.height = 1;
        position.width -= 4;
        position.x += 2;
        EditorGUI.DrawRect(position, Color.gray with { a = 0.25f });
    }

    private static GUIStyle TabButtonStyle;
    private static readonly GUIContent[] TabContents = new GUIContent[] { new(), new(), new() };

    private static void DrawTabSelector()
    {
        if (TabButtonStyle == null)
        {
            TabButtonStyle = new(GUI.skin.button)
            {
                fixedHeight = EditorGUIUtility.singleLineHeight * 1.25f
            };
        }

        var tabs = TabContents;
        _ = tabs.Length;
        tabs[0].text = L10n.TrStr("category:mode/basic");
        tabs[1].text = L10n.TrStr("category:mode/advanced");
        tabs[2].text = L10n.TrStr("category:mode/description");

        EditorGUI.BeginChangeCheck();
        SelectedTab = (Tab)GUILayout.Toolbar((int)SelectedTab, tabs, TabButtonStyle);
        if (EditorGUI.EndChangeCheck())
        {
            IsAdvancedMode = SelectedTab >= Tab.AdvancedSettings;
            ShowDescriptions = SelectedTab > Tab.AdvancedSettings;
        }
        EditorGUILayout.Space();
    }

    private static void DrawLanguagePicker()
    {
        var position = EditorGUILayout.GetControlRect(true, L10n.Localization.GetDrawLanguagePickerHeight());
        var p = position;
        p.width = EditorGUIUtility.labelWidth;
        EditorGUI.LabelField(p, /*L10n.Tr("common:language")*/ "Language");
        position.x += p.width + 4;
        position.width -= p.width + 4;
        L10n.Localization.DrawLanguagePicker(position);
    }
}
