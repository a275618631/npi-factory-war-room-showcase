using Volo.Abp.Features;
using Volo.Abp.Localization;
using Volo.Abp.Validation.StringValues;

namespace Core.Features;

public static class NpiFeatures
{
    public const string GroupName = "Modules";

    public static class Module
    {
        public const string N4 = "Module.N4";
    }
}

public class NpiFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        var moduleGroup = context.AddGroup(
            NpiFeatures.GroupName,
            displayName: new FixedLocalizableString("Modules")
        );

        moduleGroup.AddFeature(
            NpiFeatures.Module.N4,
            defaultValue: "false",
            displayName: new FixedLocalizableString("N4 Blocker List"),
            valueType: new ToggleStringValueType()
        );
    }
}


