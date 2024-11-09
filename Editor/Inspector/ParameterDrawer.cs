using System.Runtime.CompilerServices;

namespace io.github.azukimochi;

[CustomPropertyDrawer(typeof(Parameter<>), true)]
internal sealed class ParameterDrawer : PropertyDrawer
{
    private static readonly Lazy<Vector2> FoldoutStyleSize =
        new(() => EditorStyles.foldout.CalcSize(GUIContent.none), false);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => Draw(position, property, label, false, null, null, false);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GetPropertyHeight(property);

    public static float GetPropertyHeight(SerializedProperty property) 
        => EditorGUIUtility.singleLineHeight * (property.isExpanded && LightLimitChangerComponentEditor.SelectedTab != LightLimitChangerComponentEditor.Tab.BasicSettings ? 2 : 1);
    public static float GetPropertyHeight(SerializedProperty property, bool advancedMode = false, bool showMinMaxRangeSlider = false)
    {
        var lineCount = 1;
        if (advancedMode)
        {
            if (property.isExpanded)
            {
                lineCount++;

                if (showMinMaxRangeSlider)
                    lineCount++;
            }
        }
        return EditorGUIUtility.singleLineHeight * lineCount;
    }

    public static void DrawLayout(SerializedProperty property, GUIContent label, bool showInitialSlider = true, Vector2? range = null, Vector2? minMaxRange = null, bool advancedMode = false)
    {
        var position = EditorGUILayout.GetControlRect(label != null, GetPropertyHeight(property, advancedMode, minMaxRange.HasValue));
        Draw(position, property, label, showInitialSlider, range, minMaxRange, advancedMode);
    }

    public static void Draw(Rect position, SerializedProperty property, GUIContent label, bool showInitialSlider = true, Vector2? range = null, Vector2? minMaxRange = null, bool advancedMode = false)
    {
        using var scope = new PropertyScope(position, label, property);
        var valueProp = property.FindPropertyRelative(nameof(Parameter<float>.Value));
        var minMaxRangeProp = property.FindPropertyRelative(nameof(Parameter.MinMaxRange));
        var enableProp = property.FindPropertyRelative(nameof(Parameter.Enable));
        var isAnimatedProp = property.FindPropertyRelative(nameof(Parameter.Animation));
        var savedProp = property.FindPropertyRelative(nameof(Parameter.Saved));
        var syncedProp = property.FindPropertyRelative(nameof(Parameter.Synced));
        position.height = EditorGUIUtility.singleLineHeight;

        var p = position;
        p.width = EditorGUIUtility.labelWidth;
        bool enable;

        if (advancedMode)
        {
            p.x += FoldoutStyleSize.Value.x;
            property.isExpanded = EditorGUI.Foldout(p, property.isExpanded, scope.Label);
            enable = enableProp.boolValue;
        }
        else
        {
            enable = EditorGUI.ToggleLeft(p, scope.Label, enableProp.boolValue);
            enableProp.boolValue = enable;
        }

        p = position;
        p.x += EditorGUIUtility.labelWidth - EditorGUI.indentLevel * 15;
        p.width -= EditorGUIUtility.labelWidth - EditorGUI.indentLevel * 15;

        EditorGUI.BeginDisabledGroup(!enable);
        try
        {
            using (DisableScope.If(!showInitialSlider))
            {
                Vector2? r = null;
                if (range.HasValue)
                    r = range.Value;
                else if (minMaxRange.HasValue)
                    r = minMaxRangeProp.vector2Value;

                if (valueProp.propertyType == SerializedPropertyType.Boolean)
                {
                    PopupCheckbox(p, valueProp, GUIContent.none);
                }
                else if (valueProp.propertyType == SerializedPropertyType.Color)
                {
                    ColorField(p, valueProp, GUIContent.none);
                }
                else if (r is { } v)
                {
                    EditorGUI.BeginChangeCheck();
                    var value = EditorGUI.Slider(p, GUIContent.none, valueProp.floatValue, v.x, v.y);
                    if (EditorGUI.EndChangeCheck())
                    {
                        valueProp.floatValue = value;
                    }
                }
                else
                {
                    EditorGUI.PropertyField(p, valueProp, GUIContent.none);
                }
            }

            DrawPrefixLabel(p, L10n.Tr($"common:label/{(isAnimatedProp.boolValue ? "initial-value" : "override-value")}"));

            if (!property.isExpanded || !advancedMode)
                return;

            if (showInitialSlider && minMaxRange is { } minMax)
            {
                position.y += EditorGUIUtility.singleLineHeight;
                p = position;
                p.x += EditorGUIUtility.labelWidth;
                p.width -= EditorGUIUtility.labelWidth;
                MinMaxSlider(p, minMaxRangeProp, GUIContent.none, minMax);
                DrawPrefixLabel(p, L10n.Tr("common:label/range"));
            }

            position.y += EditorGUIUtility.singleLineHeight;
            p = position;

            p.x += EditorGUIUtility.labelWidth;
            p.width -= EditorGUIUtility.labelWidth;

            p = p with { width = p.width / 4 };
            EditorGUI.EndDisabledGroup();
            DrawEnableButton(ref p, enableProp, L10n.TrStr("common:label/enable"));
            EditorGUI.BeginDisabledGroup(!enable);
            DrawEnableButton(ref p, isAnimatedProp, L10n.TrStr("common:label/animation"));
            DrawEnableButton(ref p, savedProp, L10n.TrStr("common:label/saved"));
            DrawEnableButton(ref p, syncedProp, L10n.TrStr("common:label/synced"));

            DrawPrefixLabel(p, L10n.Tr("common:label/option"));

        }
        finally
        {
            EditorGUI.EndDisabledGroup();
        }
    }

    private static void DrawPrefixLabel(Rect position, GUIContent label)
    {
        position.width = EditorStyles.label.CalcSize(label).x;
        position.x -= position.width + 8;
        EditorGUI.LabelField(position, label);
    }

    private static void DrawEnableButton(ref Rect p, SerializedProperty prop, string label = null)
    {
        label ??= prop.displayName;
        EditorGUI.BeginChangeCheck();
        var v = EditorGUI.ToggleLeft(p, label, prop.boolValue);
        if (EditorGUI.EndChangeCheck())
        {
            prop.boolValue = v;
        }
        p.x += p.width;
    }

    public static void MinMaxSlider(Rect position, SerializedProperty property, GUIContent label, Vector2 range)
    {
        if (property.propertyType != SerializedPropertyType.Vector2)
        {
            return;
        }
        //using var scope = new PropertyScope(position, label, property);
        var vector = property.vector2Value;

        position = EditorGUI.PrefixLabel(position, label);
        position.x -= EditorGUI.indentLevel * 15f;
        position.width += EditorGUI.indentLevel * 15f;


        float floatFieldWidth = Mathf.Max(position.width * 0.1f, 50);
        float padding = 10f;

        var left = position with { width = floatFieldWidth };
        var mid = position with { width = position.width - (floatFieldWidth * 2 + padding * 2), x = position.x + left.width + padding };
        var right = position with { width = floatFieldWidth, x = position.x + left.width + mid.width + padding * 2 };

        EditorGUI.BeginChangeCheck();
        var f = EditorGUI.FloatField(left, GUIContent.none, vector.x);
        if (EditorGUI.EndChangeCheck())
        {
            vector.x = Mathf.Clamp(f, range.x, vector.y);
            property.vector2Value = vector;
        }
        EditorGUI.BeginChangeCheck();
        EditorGUI.MinMaxSlider(mid, GUIContent.none, ref vector.x, ref vector.y, range.x, range.y);
        if (EditorGUI.EndChangeCheck())
        {
            const float N = 0.025f;
            vector.x = Mathf.Ceil(vector.x / N) * N;
            vector.y = Mathf.Ceil(vector.y / N) * N;
            property.vector2Value = vector;
        }

        EditorGUI.BeginChangeCheck();
        f = EditorGUI.FloatField(right, GUIContent.none, vector.y);
        if (EditorGUI.EndChangeCheck())
        {
            vector.y = Mathf.Clamp(f, vector.x, range.y);
            property.vector2Value = vector;
        }
    }

    private static GUIContent[] TogglePopupContents = new GUIContent[] { new(""), new("") };

    public static void PopupCheckbox(Rect position, SerializedProperty property, GUIContent label)
    {
        var contents = TogglePopupContents;
        _ = contents.Length;
        contents[0].text = L10n.TrStr("common:label/false");
        contents[1].text = L10n.TrStr("common:label/true");

        int index = property.boolValue ? 1 : 0;
        EditorGUI.BeginChangeCheck();
        index = EditorGUI.Popup(position, label, index, contents);
        if (EditorGUI.EndChangeCheck())
        {
            property.boolValue = index != 0;
        }
    }

    public static void ColorField(Rect position, SerializedProperty property, GUIContent label, bool hdr = true)
    {
        var color = property.colorValue;
        var intensity = Mathf.Max(color.maxColorComponent, 1);

        const float TextFieldWidth = 58;

        var p = position with { width = position.width - TextFieldWidth - 4 } ;
        var value = EditorGUI.ColorField(p, label, color, true, true, hdr);
        var color2 = (value / intensity) with { a = value.a };
        p.x += p.width + 4;

        p.width = TextFieldWidth;
        EditorGUI.BeginChangeCheck();
        var hex = EditorGUI.DelayedTextField(p, GUIContent.none, GetColorCodeRGB(color2));
        if (EditorGUI.EndChangeCheck() && ColorUtility.TryParseHtmlString(hex, out color2))
        {
            value = (color2 * intensity) with { a = value.a };
        }

        if (color != value)
        {
            property.colorValue = value;
        }

        static string GetColorCodeRGB(Color color)
        {
            var c = Unsafe.As<Color, System.Numerics.Vector3>(ref color);
            if (c == new System.Numerics.Vector3(1,1,1))
            {
                return "#FFFFFF";
            }
            else if (c == new System.Numerics.Vector3(0, 0, 0))
            {
                return "#000000";
            }

            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }
    }
}
