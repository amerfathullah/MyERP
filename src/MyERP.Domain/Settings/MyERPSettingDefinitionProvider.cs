using Volo.Abp.Settings;

namespace MyERP.Settings;

public class MyERPSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(MyERPSettings.MySetting1));
    }
}
