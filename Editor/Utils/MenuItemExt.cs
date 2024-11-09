namespace io.github.azukimochi;

using VRC.SDK3.Avatars.ScriptableObjects;
using MenuItem = nadena.dev.modular_avatar.core.ModularAvatarMenuItem;

internal static class MenuItemExt
{
    public struct MenuItemProperties
    {
        public VRCExMenuControlType? ControlType;
        public string ParameterName;
        public float? Value;

        public static implicit operator MenuItemProperties(ValueTuple<VRCExMenuControlType, string> tuple) => new() { ControlType = tuple.Item1, ParameterName = tuple.Item2 };
        public static implicit operator MenuItemProperties(ValueTuple<VRCExMenuControlType, string, float> tuple) => new() { ControlType = tuple.Item1, ParameterName = tuple.Item2, Value = tuple.Item3 };
    }

    public static MenuItem GetOrAdd(this MenuItem menu, string path, Func<MenuItem, MenuItemProperties> factory = null, Texture2D icon = null)
    {
        void Action(MenuItem menu)
        {
            if (factory == null)
                return;

            var x = factory(menu);
            if (x.ControlType != null)
                menu.Control.type = x.ControlType.Value;
            if (x.ParameterName != null)
            {
                var p = new VRCExpressionsMenu.Control.Parameter() { name = x.ParameterName };
                if (x.ControlType == VRCExMenuControlType.RadialPuppet)
                {
                    menu.Control.subParameters = new[] { p };
                }
                else
                {
                    menu.Control.parameter = p;
                }
            }
            if (x.Value != null)
                menu.Control.value = x.Value.Value;
        }
        return menu.GetOrAdd(path, Action, icon);
    }

    public static MenuItem GetOrAdd(this MenuItem menu, string path, Action<MenuItem> action, Texture2D icon = null)
    {
        var split = path.Split("/");
        bool flag = false;
        for (int i = 0; i < split.Length; i++)
        {
            var name = split[i];
            var child = menu.transform.Find(name);
            if (child == null)
            {
                var obj = new GameObject(name);
                obj.transform.parent = menu.transform;
                child = obj.transform;
            }

            menu = child.GetComponent<MenuItem>();
            flag = menu == null;
            if (flag)
            {
                menu = child.gameObject.AddComponent<MenuItem>();
                menu.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                menu.MenuSource = nadena.dev.modular_avatar.core.SubmenuSource.Children;
                menu.Control.icon = icon;
            }
        }

        if (flag)
            action?.Invoke(menu);

        return menu;
    }
}