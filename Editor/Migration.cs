
namespace io.github.azukimochi;

internal static class Migration 
{
    public static LightLimitChangerComponent MigrateV1toV2(LightLimitChangerSettings oldComponent)
    {
        var component = Undo.AddComponent<LightLimitChangerComponent>(oldComponent.gameObject);

        {
            var control = component.General.LightingControl;
            if (!oldComponent.Parameters.IsSeparateLightControl)
            {
                control.MinLight.Value = oldComponent.Parameters.DefaultLightValue;
                control.MaxLight.Value = oldComponent.Parameters.DefaultLightValue;
            }
            else
            {
                control.MinLight.Value = oldComponent.Parameters.MinLightValue;
                control.MaxLight.Value = oldComponent.Parameters.MaxLightValue;
            }
            control.Monochrome.Value = oldComponent.Parameters.InitialMonochromeControlValue;
            control.Unlit.Value = oldComponent.Parameters.InitialUnlitControlValue;

        }

        Undo.DestroyObjectImmediate(oldComponent);
        return component;
    }
}
