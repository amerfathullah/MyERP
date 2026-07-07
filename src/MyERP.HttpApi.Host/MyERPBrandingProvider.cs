using Microsoft.Extensions.Localization;
using MyERP.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace MyERP;

[Dependency(ReplaceServices = true)]
public class MyERPBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<MyERPResource> _localizer;

    public MyERPBrandingProvider(IStringLocalizer<MyERPResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
