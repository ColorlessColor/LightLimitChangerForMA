using System.Reflection;
using System.Reflection.Emit;

namespace io.github.azukimochi;

internal abstract class EditorGUIUtils
{
    public readonly static Func<MessageType, Texture2D> GetHelpIcon;

    static EditorGUIUtils()
    {
        var method = new DynamicMethod(nameof(GetHelpIcon), typeof(Texture2D), new[] { typeof(MessageType) }, typeof(EditorGUIUtility), true);
        var il = method.GetILGenerator();
        il.Ldarg(0);
        il.Emit(OpCodes.Call, typeof(EditorGUIUtility).GetMethod(nameof(GetHelpIcon), BindingFlags.NonPublic | BindingFlags.Static));
        il.Emit(OpCodes.Ret);
        GetHelpIcon = method.CreateDelegate(typeof(Func<MessageType, Texture2D>)) as Func<MessageType, Texture2D>;
    }
}

internal abstract class EditorGUILayoutUtils : EditorGUIUtils
{
    public static void HelpBox(GUIContent content, MessageType type)
    {
        content.image = GetHelpIcon(type);
        EditorGUILayout.LabelField(GUIContent.none, content, HelpBoxStyle);
    }

    private static GUIStyle HelpBoxStyle = new(EditorStyles.helpBox)
    {
        fontSize = EditorStyles.label.fontSize
    };
}