using System;

namespace io.github.azukimochi;

internal readonly ref struct ShurikenHeaderGroupScope
{
    private static readonly GUIStyle InnerBoxStyle;

    private readonly bool InsertSpaceToEnd;
    public readonly bool IsOpened;

    static ShurikenHeaderGroupScope()
    {
        InnerBoxStyle = new("HelpBox") 
        {
            margin = new RectOffset(),
            padding = new RectOffset(6,6,6,6),
        };
    }

    public ShurikenHeaderGroupScope(SerializedProperty group, string title, bool insertSpaceToEnd = true)
    {
        InsertSpaceToEnd = insertSpaceToEnd;

        if (!(IsOpened = group.isExpanded = Foldout(title, group.isExpanded)))
            return;
        GUILayout.BeginHorizontal();
        GUILayout.Space(EditorGUI.indentLevel * 15f);

        EditorGUILayout.BeginVertical(InnerBoxStyle);
    }

    public void Dispose()
    {
        if (!IsOpened)
            return;

        EditorGUILayout.EndVertical();
        GUILayout.EndHorizontal();
        if (InsertSpaceToEnd)
            EditorGUILayout.Space();
    }

    private static bool Foldout(string title, bool display)
    {
        var style = Styles.ShurikenTitle.Value;
        var rect = EditorGUILayout.GetControlRect(false, 20f, style);// GUILayoutUtility.GetRect(16f, 20f, style);
        rect = EditorGUI.IndentedRect(rect);
        GUI.Box(rect, title, style);

        var e = Event.current;

        var toggleRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
        if (e.type == EventType.Repaint)
        {
            EditorStyles.foldout.Draw(toggleRect, false, false, display, false);
        }

        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            display = !display;
            e.Use();
        }

        return display;
    }

}