using System.Reflection.Emit;
using System.Reflection;
using System.Linq;
using System.Text;

namespace io.github.azukimochi;

partial class LightLimitChangerComponentEditor
{
    internal static void DoPropertyGUI<TSettings>(SerializedProperty property, bool insertSpaceToEnd = true) where TSettings : ISettings
    {
        if (!property.hasChildren)
            return;
        var settings = (TSettings)property.boxedValue;
        using var scope = new ShurikenHeaderGroupScope(property, L10n.TrStr($"category:{SettingsFieldInfo<TSettings>.Id}"));
        if (scope.IsOpened)
            DynamicGUIBuilder<TSettings>.OnGUI(settings, property);
    }

    internal static class DynamicGUIBuilder<TSettings> where TSettings : ISettings
    {
        public delegate void OnGUIDelegate(TSettings settings, SerializedProperty property);

        public static readonly OnGUIDelegate OnGUI;

        static DynamicGUIBuilder()
        {
            var method = new DynamicMethod($"{typeof(TSettings).Name}_OnGUI", null, typeof(OnGUIDelegate).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance).GetParameters().Select(x => x.ParameterType).ToArray());
            var il = method.GetILGenerator();

            StringBuilder sb = new();
            var local_getShowDescription = il.DeclareLocal(typeof(bool));
            var local_isAdvancedMode = il.DeclareLocal(typeof(bool));
            var local_range = il.DeclareLocal(typeof(Vector2?));
            var local_minMaxRange = il.DeclareLocal(typeof(Vector2?));
            var local_property = il.DeclareLocal(typeof(SerializedProperty));
            var local_foldout = il.DeclareLocal(typeof(bool));

            // showDesc = LightLimitChangerComponentEditor.ShowDescriptions;
            il.GetProperty<LightLimitChangerComponentEditor>(nameof(ShowDescriptions));
            il.Stloc(local_getShowDescription);

            il.GetProperty<LightLimitChangerComponentEditor>(nameof(IsAdvancedMode));
            il.Stloc(local_isAdvancedMode);

            ProcSettings(typeof(TSettings));

            void ProcSettings(Type type)
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(x => x.IsPublic || (x.IsPrivate && x.GetCustomAttribute<SerializeField>() != null));
                var settingsInfo = SettingsFieldInfo.GetInfo(type);

                foreach (var field in fields)
                {
                    if (typeof(Parameter).IsAssignableFrom(field.FieldType))
                    {
                        var info = new ParameterFieldInfo(field);
                        _ = nameof(ParameterDrawer.DrawLayout);

                        void CreateNullableVector2(float min, float max)
                        {
                            il.Float(min);
                            il.Float(max);
                            il.NewObj<Func<float, float, Vector2>>();
                            il.NewObj<Func<Vector2, Vector2?>>();
                        }

                        if (info.RangeAttribute is { } range)
                        {
                            CreateNullableVector2(range.Min, range.Max);
                            il.Stloc(local_range);
                        }
                        else
                        {
                            il.Ldloca(local_range);
                            il.Emit(OpCodes.Initobj, typeof(Vector2?));
                        }

                        if (info.MinMaxRangeAttribute is { } minmax)
                        {
                            CreateNullableVector2(minmax.Min, minmax.Max);
                            il.Stloc(local_minMaxRange);
                        }
                        else
                        {
                            il.Ldloca(local_minMaxRange);
                            il.Emit(OpCodes.Initobj, typeof(Vector2?));
                        }

                        il.Ldarg(1);
                        il.Ldstr(field.Name);
                        il.Call<SerializedProperty, Func<string, SerializedProperty>>(x => x.FindPropertyRelative);
                        il.Ldstr($"settings:{settingsInfo.Id}/{field.Name.ToKebabCase()}/label");
                        il.Call<Func<string, GUIContent>>(L10n.Tr);
                        il.Int(info.DisableInitialValueSliderAttribute is null ? 1 : 0);
                        il.Ldloc(local_range);
                        il.Ldloc(local_minMaxRange);
                        il.Ldloc(local_isAdvancedMode);
                        il.Call(ParameterDrawer.DrawLayout);

                        il.Ldloc(local_getShowDescription);
                        il.If(() =>
                        {
                            il.Ldstr(StringExt.Create(sb, $"settings:{settingsInfo.Id}/{field.Name.ToKebabCase()}/description"));
                            il.Call<Func<string, string>>(L10n.TrStr);
                            il.Int((int)MessageType.Info);
                            il.Call<Action<string, MessageType>>(EditorGUILayout.HelpBox);
                        });

                        il.Ldarg(1);
                        il.GetProperty<SerializedProperty>(nameof(SerializedProperty.isExpanded));
                        il.Ldloc(local_isAdvancedMode);
                        il.Emit(OpCodes.And);
                        il.If(() =>
                        {
                            il.Call<Action>(DrawSeparator);
                        });
                    }
                    else if (typeof(ISettings).IsAssignableFrom(field.FieldType))
                    {
                        il.Call<Action>(DrawSeparator);

                        il.Ldarg(1);
                        il.Ldstr(field.Name);
                        il.Call<SerializedProperty, Func<string, SerializedProperty>>(x => x.FindPropertyRelative);
                        il.Int(0);
                        il.Emit(OpCodes.Call, typeof(LightLimitChangerComponentEditor).GetMethod(nameof(DoPropertyGUI), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(field.FieldType));

                    }
                }
            }

            il.Emit(OpCodes.Ret);

            OnGUI = method.CreateDelegate(typeof(OnGUIDelegate)) as OnGUIDelegate;
        }
    }
}
