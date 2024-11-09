using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Anatawa12.AvatarOptimizer;
using gomoru.su;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor.Animations;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace io.github.azukimochi;

internal sealed class LightLimitChangerProcessor : IDisposable
{
    public GameObject AvatarRootObject { get; }
    public LightLimitChangerComponent Component { get; }
    public Object AssetContainer { get; }

    private readonly BuildContext context;
    private readonly List<ShaderProcessor> processors = new();
    private readonly HashSet<ParameterConfig> avatarParameters = new();

    private ModularAvatarMenuItem menuRoot;
    private DirectBlendTree blendTree;
    private Renderer[] targetRenderers;
    private Material[] targetMaterials;
    private ImmutableDictionary<ShaderProcessor, Material[]> shaderMaterialPair;
    private AnimatorController animatorController;
    private AnimationClip blankClip;

    public ReadOnlySpan<ShaderProcessor> Processors => processors.AsSpan();
    public ReadOnlySpan<Renderer> TargetRenderers => targetRenderers;
    public ReadOnlySpan<Material> TargetMaterials => targetMaterials;

    #region ParameterNames

    private const string ParameterName_ParameterSyncerIndex = "LightLimitChangerParameterSyncerIndex";
    private const string ParameterName_ParameterSyncerValue = "LightLimitChangerParameterSyncerValue";
    private const string ParameterName_ParameterSyncerOverrideIndex = "LightLimitChangerParameterSyncerOverrideIndex";

    #endregion

    public LightLimitChangerProcessor(BuildContext context)
    {
        this.context = context;
        AvatarRootObject = context.AvatarRootObject;
        Component = context.AvatarRootObject.GetComponentInChildren<LightLimitChangerComponent>(false);
        AssetContainer = context.AssetContainer;
    }

    public LightLimitChangerProcessor(GameObject avatarRootObject, Object assetContainer)
    {
        AvatarRootObject = avatarRootObject;
        Component = avatarRootObject.GetComponentInChildren<LightLimitChangerComponent>(false);
        AssetContainer = assetContainer;
    }

    public void Run()
    {
        if (Component == null)
            return;

        var excludes = Component.Excludes.ToHashSet();
        targetRenderers = AvatarRootObject.GetComponentsInChildren<Renderer>()
            .Where(x => !excludes.Contains(x.gameObject))
            .ToArray();

        CloneMaterials();

        SetupAnimations();

        ConfigureSettings(Component.General.LightingControl);
        ConfigureSettings(Component.General.ColorControl);
        ConfigureSettings(Component.LilToon);
        ConfigureSettings(Component.LilToon.DistanceFade);
        ConfigureSettings(Component.LilToon.Backlight);
        ConfigureSettings(Component.Poiyomi);

        GenerateParameters();

        CompressionAnimatorParameters();

        var layer = blendTree.ToAnimatorControllerLayer(animatorController);
        animatorController.AddLayer(layer);

        CreatePresetLoader();

        RemoveEmptySubMenus(menuRoot);
    }

    private void CloneMaterials()
    {
        var components = AvatarRootObject.GetComponentsInChildren<Component>(true);
        var cloner = new MaterialCloner(this);

        foreach (var component in components)
        {
            switch (component)
            {
                // skip some known unrelated components
                case Transform:
                case ParticleSystem:
                    break;
                case SkinnedMeshRenderer renderer:
                    {
                        var mats = renderer.sharedMaterials;
                        foreach(ref var mat in mats.AsSpan())
                        {
                            mat = cloner.MapObject(mat);
                        }
                        renderer.sharedMaterials = mats;
                    }
                    break;
                default:
                    {
                        using var serializedObject = new SerializedObject(component);

                        foreach (var objectReferenceProperty in serializedObject.ObjectReferenceProperties())
                        {
                            objectReferenceProperty.objectReferenceValue = cloner.MapObject(objectReferenceProperty.objectReferenceValue);
                        }

                        serializedObject.ApplyModifiedPropertiesWithoutUndo();

                        break;
                    }
            }
        }

        targetMaterials = cloner.Cloned.Values.ToArray();
        shaderMaterialPair = cloner.ShaderMaterialPair.ToImmutableDictionary(x => x.Key, x => x.Value.ToArray());

        foreach (var processor in Processors)
        {
            processor.OnMaterialCloned(targetMaterials);
        }

        foreach (var pair in shaderMaterialPair)
        {
            foreach (var mat in pair.Value.AsSpan())
            {
                pair.Key.NormalizeMaterial(mat);
            }
        }
    }

    private void SetupAnimations()
    {
        var go = Component.gameObject;
        blendTree = new DirectBlendTree() { Name = LightLimitChanger.Title };
        animatorController = new AnimatorController() { name = LightLimitChanger.Title };

        AssetDatabase.AddObjectToAsset(animatorController, AssetContainer);
        animatorController.AddParameter(new() { defaultFloat = 1, name = "1", type = AnimatorControllerParameterType.Float });

        var mama = go.GetOrAddComponent<ModularAvatarMergeAnimator>();
        mama.animator = animatorController;
        mama.matchAvatarWriteDefaults = Component.WriteDefaults == WriteDefaultsSetting.MatchAvatar;
        mama.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
        mama.pathMode = MergeAnimatorPathMode.Absolute;
        mama.deleteAttachedAnimator = true;

        if (!go.TryGetComponent<ModularAvatarMenuInstaller>(out var mami))
        {
            mami = go.AddComponent<ModularAvatarMenuInstaller>();
        }

        var mam = go.AddComponent<ModularAvatarMenuItem>();
        mam.Control.type = VRCExMenuControlType.SubMenu;
        mam.MenuSource = SubmenuSource.Children;
        menuRoot = mam;

        var blank = new AnimationClip() { name = "Blank" };
        AssetDatabase.AddObjectToAsset(blank, AssetContainer);
        blankClip = blank;
    }

    private void ConfigureSettings<TSettings>(TSettings settings) where TSettings : ISettings, new()
    {
        var menuGroup = menuRoot.GetOrAdd(SettingsFieldInfo<TSettings>.DisplayName);
        if (typeof(TSettings).GetCustomAttribute<MenuIconAttribute>() is { } groupIconAttr)
        {
            menuGroup.Control.icon = AssetUtils.FromGUID<Texture2D>(groupIconAttr.Guid);
        }

        var parameterBuffer = (stackalloc float[8]);
        Dictionary<string, List<ParameterInfo>> entries = new();

        foreach (var field in settings.AllParameterFields())
        {
            if (field.FieldType.BaseType != typeof(Parameter))
                continue;

            var parameterInfo = new ParameterInfo(settings, field);
            var parameter = parameterInfo.Parameter;

            string key = parameterInfo.VectorFieldAttribute != null ? parameterInfo.VectorFieldAttribute.Group ?? SettingsFieldInfo<TSettings>.ParameterPrefix : "";
            var list = entries.GetOrAdd(key, _ => new());
            list.Add(parameterInfo);
        }

        foreach (var entry in entries)
        {
            var items = entry.Value.AsSpan();
            bool generateEmpty = entry.Value.Any(x => x.VectorFieldAttribute != null && x.Parameter.Enable && x.Parameter.IsAnimated);
            foreach (var parameterInfo in items)
            {
                var parameter = parameterInfo.Parameter;
                var t = parameterInfo.ParameterType;

                if (parameter.Enable)
                {
                    foreach (var pair in shaderMaterialPair)
                    {
                        if (!parameterInfo.MaterialProperties.TryGetValue(pair.Key.QualifiedName, out var propertyName))
                            propertyName = pair.Key.GetMaterialPropertyName(parameterInfo);

                        if (propertyName == null)
                            continue;

                        pair.Key.OverrideMaterialValue(new OverrideMaterialValueContext() { ParameterInfo = parameterInfo, PropertyName = propertyName, Materials = pair.Value });
                    }
                }

                if ((!parameter.Enable || !parameter.IsAnimated) && !generateEmpty)
                    continue;

                var group = GetBlendTreeGroup(SettingsFieldInfo<TSettings>.ParameterPrefix);
                ReadOnlySpan<float> values = parameterBuffer[..parameterInfo.Parameter.GetValues(parameterBuffer)];

                for (int i = 0; i < values.Length; i++)
                {
                    var value = values[i];
                    string postfix = values.Length == 1 ? "" : t == typeof(Vector4) ? $".{"xyzw"[i]}" : $".{"rgba"[i]}";
                    var name = $"{parameterInfo.Name}{postfix}";
                    var anim = new AnimationClip() { name = $"{LightLimitChanger.Title} {name}" };
                    AssetDatabase.AddObjectToAsset(anim, AssetContainer);

                    if (!parameter.IsAnimated)
                    {
                        var tree = group.AddMotion(anim);
                        var context = new ConfigureEmptyAnimationContext()
                        {
                            ParameterInfo = parameterInfo,
                            PropertyName = null,
                            Renderers = targetRenderers,
                            AnimationClip = anim,
                            Type = parameterInfo.GeneralControlType,
                        };

                        foreach (var processor in processors.AsSpan())
                        {
                            var propertyName = parameterInfo.GetPropertyName(processor);
                            if (string.IsNullOrEmpty(propertyName))
                                continue;

                            context.PropertyName = $"{propertyName}{postfix}";
                            var buffer = parameterBuffer[4..];
                            buffer = buffer[..ParameterCache<TSettings>.InitialParameters[parameterInfo.Name].GetValues(buffer)];
                            context.Value = buffer[i];

                            processor.ConfigreEmptyAnimation(context);
                        }
                    }
                    else
                    {
                        var tree = group.AddMotionTime(name);
                        tree.Animation = anim;
                        var avatarParameter = new ParameterConfig()
                        {
                            nameOrPrefix = $"{SettingsFieldInfo<TSettings>.ParameterPrefix}{name}",
                            defaultValue = Utils.NormalizeInRange(value, parameterInfo.Range.x, parameterInfo.Range.y),
                            syncType =
                            t == typeof(bool) ? ParameterSyncType.Bool :
                            t == typeof(int) ? ParameterSyncType.Int :
                            t == typeof(float) || t == typeof(Vector4) || t == typeof(Color) ? ParameterSyncType.Float :
                            ParameterSyncType.NotSynced,
                            saved = parameter.Saved,
                            localOnly = !parameter.Synced,
                        };
                        tree.ParameterName = avatarParameter.nameOrPrefix;
                        avatarParameters.Add(avatarParameter);

                        var context = new ConfigureGeneralAnimationContext()
                        {
                            ParameterInfo = parameterInfo,
                            PropertyName = null,
                            Renderers = targetRenderers,
                            AnimationClip = anim,
                            AvatarParameter = avatarParameter,
                            Type = parameterInfo.GeneralControlType,
                        };

                        foreach (var processor in processors.AsSpan())
                        {
                            context.Range = parameterInfo.Range; // Range is mutable

                            var propertyName = parameterInfo.GetPropertyName(processor);
                            if (string.IsNullOrEmpty(propertyName))
                                continue;

                            context.PropertyName = $"{propertyName}{postfix}";

                            if (parameterInfo.ShaderFeatureAttribute is null)
                            {
                                processor.ConfigureGeneralAnimation(context);
                            }
                            else
                            {
                                var names = parameterInfo.ShaderFeatureAttribute.QualifiedNames;
                                if (!names.Contains(processor.QualifiedName))
                                    continue;

                                processor.ConfigureShaderSpecificAnimation(context);
                            }
                            var menuPath = $"{parameterInfo.Name}{(values.Length == 1 ? "" : $"/{(char)(postfix[1] & ~0x20)}")}";
                            var menuItem = menuGroup.GetOrAdd(menuPath, (MAMenuItem menu) =>
                            {
                                if (parameterInfo.ParameterType == typeof(bool))
                                {
                                    return (VRCExMenuControlType.Toggle, avatarParameter.nameOrPrefix, 1);
                                }
                                else if (Component.General.CompressionExpressionParameters)
                                {
                                    menu.Control.parameter = new() { name = ParameterName_ParameterSyncerOverrideIndex };
                                    return (VRCExMenuControlType.RadialPuppet, avatarParameter.nameOrPrefix, avatarParameters.Count);
                                }
                                else
                                {
                                    return (VRCExMenuControlType.RadialPuppet, avatarParameter.nameOrPrefix, 0);
                                }
                            }, parameterInfo.Icon);
                        }
                    }
                }
            }
        }
    }

    private void GenerateParameters()
    {
        var mapa = Component.gameObject.GetOrAddComponent<ModularAvatarParameters>();
        mapa.parameters.AddRange(avatarParameters);

        foreach (var parameter in mapa.parameters.AsSpan())
        {
            animatorController.AddParameter(new AnimatorControllerParameter()
            {
                name = parameter.nameOrPrefix,
                defaultFloat = parameter.defaultValue,
                defaultBool = parameter.defaultValue != 0,
                defaultInt = (int)parameter.defaultValue,
                type = parameter.syncType switch
                {
                    //ParameterSyncType.Int => AnimatorControllerParameterType.Int,
                    //ParameterSyncType.Bool => AnimatorControllerParameterType.Bool,
                    //ParameterSyncType.Float => AnimatorControllerParameterType.Float,
                    //_ => default,
                    _ => AnimatorControllerParameterType.Float, // Floatへ自動変換されるのでこれでOK
                },
            });
        }
    }

    private void CompressionAnimatorParameters()
    {
        if (!Component.General.CompressionExpressionParameters)
            return;

        //TODO: やりかけ
        var mapa = Component.gameObject.GetOrAddComponent<ModularAvatarParameters>();
        foreach(ref var param in mapa.parameters.AsSpan())
        {
            param.localOnly = true;
        }

        var controller = new AnimatorController() { name = $"{LightLimitChanger.Title} Parameter Compressor" };
        AssetDatabase.AddObjectToAsset(controller, AssetContainer);

        const string Index = ParameterName_ParameterSyncerIndex;
        const string Value = ParameterName_ParameterSyncerValue;
        const string Override = ParameterName_ParameterSyncerOverrideIndex;

        controller.AddParameter(Index, AnimatorControllerParameterType.Int);
        controller.AddParameter(Value, AnimatorControllerParameterType.Float);
        controller.AddParameter(Override, AnimatorControllerParameterType.Int);

        controller.AddLayer(controller.name);
        var wd = Component.WriteDefaults != WriteDefaultsSetting.OFF;
        var layer = controller.layers[0];
        var stateMachine = layer.stateMachine;

        var idle = stateMachine.AddState("Idle");
        idle.writeDefaultValues = wd;
        idle.motion = blankClip;

        const string IsLocal = "IsLocal";
        {
            var remote = stateMachine.AddState("Remote");
            remote.writeDefaultValues = wd;
            remote.motion = blankClip;
            Transit(idle, remote, transit => transit.AddCondition(AnimatorConditionMode.IfNot, 0, IsLocal));
            var parameters = mapa.parameters.AsSpan();

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var set = stateMachine.AddState($"Set {param.nameOrPrefix}");
                set.writeDefaultValues = wd;
                set.motion = blankClip;

                Transit(remote, set, transit => transit.AddCondition(AnimatorConditionMode.Equals, i + 1, Index));

                var dr = set.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                dr.parameters.Add(new() { type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy, source = Value, name = param.nameOrPrefix });
                dr = set.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                //dr.parameters.Add(new() { type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set, name = Index, value = 0 });
                //dr.parameters.Add(new() { type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set, name = Value, value = 0 });

                //Transit(remote, remote, transit => transit.AddCondition(AnimatorConditionMode.IfNot, 0, IsLocal));
                Transit(set, remote, transit => transit.AddCondition(AnimatorConditionMode.NotEqual, i + 1, Index));
                Transit(set, set, transit =>
                {
                    transit.duration = 2f / 30f;
                    transit.AddCondition(AnimatorConditionMode.Equals, i + 1, Index);
                });
            }
        }
        {
            var localFirstTime = stateMachine.AddState("Local [First]");
            localFirstTime.writeDefaultValues = wd;
            localFirstTime.motion = blankClip;
            Transit(idle, localFirstTime, transit => transit.AddCondition(AnimatorConditionMode.If, 0, IsLocal));

            var local = stateMachine.AddState("Local");
            local.writeDefaultValues = wd;
            local.motion = blankClip;
            //Transit(idle, local, transit => transit.AddCondition(AnimatorConditionMode.If, 0, IsLocal));

            var syncStart = stateMachine.AddState("Sync Start");
            syncStart.writeDefaultValues = wd;
            syncStart.motion = blankClip;
            Transit(local, syncStart, transit =>
            {
                transit.duration = 5 / 30f;
                transit.AddCondition(AnimatorConditionMode.Equals, 0, Override);
            });

            Transit(localFirstTime, syncStart, transit => transit.AddCondition(AnimatorConditionMode.If, 0, IsLocal));

            var overrideStart = stateMachine.AddState("Override Sync Start");
            overrideStart.writeDefaultValues = wd;
            overrideStart.motion = blankClip;
            Transit(local, overrideStart, transit =>
            {
                transit.duration = 1 / 30f * 2;
                transit.AddCondition(AnimatorConditionMode.NotEqual, 0, Override);
            });

            var preState = syncStart;

            var parameters = mapa.parameters.AsSpan();
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var load = stateMachine.AddState($"Load {param.nameOrPrefix}");
                load.writeDefaultValues = wd;
                load.motion = blankClip;

                Transit(preState, load, transit =>
                {
                    transit.duration = 2 / 30f;
                    transit.AddCondition(AnimatorConditionMode.If, 0, IsLocal);
                });

                var dr = load.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                dr.parameters.Add(new() { type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy, source = param.nameOrPrefix, name = Value });
                dr.parameters.Add(new() { type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set, name = Index, value = i + 1 });

                preState = load;

                Transit(load, overrideStart, transit => transit.AddCondition(AnimatorConditionMode.NotEqual, 0, Override));
            }
            Transit(preState, local, transit => transit.AddCondition(AnimatorConditionMode.If, 0, IsLocal));


            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var @override = stateMachine.AddState($"Override {param.nameOrPrefix}");
                @override.writeDefaultValues = wd;
                @override.motion = blankClip;

                Transit(overrideStart, @override, transit => transit.AddCondition(AnimatorConditionMode.Equals, i + 1, Override));

                var dr = @override.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                dr.parameters.Add(new() { type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy, source = param.nameOrPrefix, name = Value });
                dr.parameters.Add(new() { type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set, name = Index, value = i + 1 });

                Transit(@override, @override, transit => transit.AddCondition(AnimatorConditionMode.Equals, i + 1, Override));
                Transit(@override, local, transit => transit.AddCondition(AnimatorConditionMode.NotEqual, i + 1, Override));
            }
        }

        mapa.parameters.Add(new() { syncType = ParameterSyncType.Int, defaultValue = 0, nameOrPrefix = Index, saved = false, });
        mapa.parameters.Add(new() { syncType = ParameterSyncType.Float, defaultValue = 0, nameOrPrefix = Value, saved = false, });
        mapa.parameters.Add(new() { syncType = ParameterSyncType.Int, defaultValue = 0, nameOrPrefix = Override, saved = false, localOnly = true, });

        var mama = Component.gameObject.AddComponent<MAMergeAnimator>();
        mama.matchAvatarWriteDefaults = Component.WriteDefaults == WriteDefaultsSetting.MatchAvatar;
        mama.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
        mama.animator = controller;
        mama.pathMode = MergeAnimatorPathMode.Absolute;
    }

    private void CreatePresetLoader()
    {
        var children = Component.GetComponentsInChildren<LightLimitChangerComponent>();
        foreach(var x in children.Skip(1))
        {
            if (x.TryGetComponent<MAMenuInstaller>(out var c))
                Object.DestroyImmediate(c);
        }

        var wd = Component.WriteDefaults != WriteDefaultsSetting.OFF;

        var controller = new AnimatorController() { name = $"{LightLimitChanger.Title} Presets" };
        AssetDatabase.AddObjectToAsset(controller, AssetContainer);

        const string IsLocal = "IsLocal";
        const string Preset = "LightLimitChangerPresetIndex";

        controller.AddParameter(IsLocal, AnimatorControllerParameterType.Bool);
        controller.AddParameter(Preset, AnimatorControllerParameterType.Int);

        controller.AddLayer($"{LightLimitChanger.Title} Preset");
        var layer = controller.layers[0];
        var stateMachine = layer.stateMachine;

        var blank = blankClip;
        var pos = stateMachine.entryPosition + new Vector3(200, 0);
        var idle = stateMachine.AddState("Idle", pos);
        idle.writeDefaultValues = wd;
        idle.motion = blank;

        var presetMenuRoot = menuRoot.GetOrAdd(L10n.TrStr("menu:preset"), menu => (VRCExMenuControlType.SubMenu, null));
        foreach (var (child, i) in children.Select((x, i) => (x, i + 1)))
        {
            var menuName = i == 1 ? L10n.TrStr("menu:preset/default-preset", "Default") : child.name;
            var state = stateMachine.AddState($"{i}", new Vector3((pos.x + stateMachine.exitPosition.x) / 2, pos.y + 80 * (i - 1)));
            state.motion = blank;
            state.writeDefaultValues = wd;
            Transit(idle, state, transit =>
            {
                transit.AddCondition(AnimatorConditionMode.If, 0, IsLocal);
                transit.AddCondition(AnimatorConditionMode.Equals, i, Preset);
            });
            Transit(state.AddExitTransition(), transit => transit.AddCondition(AnimatorConditionMode.NotEqual, i, Preset));

            var dr = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            if (dr == null)
                continue; // 🤔 Why?

            var parameters = animatorController.parameters;

            A(child.General.LightingControl);
            A(child.General.ColorControl);
            A(child.LilToon);
            A(child.Poiyomi);
            A(child.UnlitWF);

            presetMenuRoot.GetOrAdd(menuName, menu =>
            {
                menu.automaticValue = true;
                return (VRCExMenuControlType.Button, Preset, i);
            });

            void A<T>(T settings) where T : ISettings
            {
                var parameterBuffer = (stackalloc float[8]);
                foreach (var parameterInfo in settings.AllParameterFields().Select(x => new ParameterInfo(settings, x)))
                {
                    ReadOnlySpan<float> values = parameterBuffer[..parameterInfo.Parameter.GetValues(parameterBuffer)];

                    for (int i = 0; i < values.Length; i++)
                    {
                        var value = Utils.NormalizeInRange(values[i], parameterInfo.Range.x, parameterInfo.Range.y);
                        string postfix = values.Length == 1 ? "" : parameterInfo.ParameterType == typeof(Vector4) ? $".{"xyzw"[i]}" : $".{"rgba"[i]}";
                        var name = $"{SettingsFieldInfo<T>.ParameterPrefix}{parameterInfo.Name}{postfix}";
                        if (parameters.Any(x => x.name == name))
                            dr.parameters.Add(new() { name = name, type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set, value = value });
                    }
                }
            }
        }

        var mama = Component.gameObject.AddComponent<MAMergeAnimator>();
        mama.matchAvatarWriteDefaults = Component.WriteDefaults == WriteDefaultsSetting.MatchAvatar;
        mama.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
        mama.animator = controller;


    }

    private static void RemoveEmptySubMenus(MAMenuItem menu)
    {
        if (menu.Control.type != VRCExpressionsMenu.Control.ControlType.SubMenu)
            return;

        var children = menu.transform.Cast<Transform>().Select(x => x.GetComponent<MAMenuItem>()).Where(x => x != null);
        if (!children.Any())
        {
            Object.DestroyImmediate(menu);
            return;
        }

        foreach (var child in children)
        {
            RemoveEmptySubMenus(child);
        }
    }

    private static void Transit(AnimatorState from, AnimatorState to, Action<AnimatorStateTransition> custom = null)
        => Transit(from.AddTransition(to), custom);

    private static void Transit(AnimatorStateTransition transit, Action<AnimatorStateTransition> custom = null)
    {
        transit.hasFixedDuration = true;
        transit.duration = 0;
        transit.hasExitTime = false;
        custom?.Invoke(transit);
    }


    private DirectBlendTree GetBlendTreeGroup(string name)
    {
        return blendTree.Items
            .FirstOrDefault(x => x is DirectBlendTree d && d.Name == name) as DirectBlendTree 
            ?? blendTree.AddDirectBlendTree(name);
    }

    public LightLimitChangerProcessor AddProcessor<T>() where T : ShaderProcessor, new()
    {
        var t = new T();
        (t as ILightLimitChangerProcessorReceiver).Initialize(this);
        processors.Add(t);
        return this;
    }

    public void Dispose()
    {

    }
}
