using Microsoft.Extensions.DependencyInjection;
using MyERP.Accounting.DomainServices;
using NSubstitute;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.PermissionManagement;

namespace MyERP;

[DependsOn(
    typeof(MyERPApplicationModule),
    typeof(MyERPDomainTestModule)
)]
public class MyERPApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<FeatureManagementOptions>(options =>
        {
            options.SaveStaticFeaturesToDatabase = false;
            options.IsDynamicFeatureStoreEnabled = false;
        });
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
        });

        context.Services.AddSingleton(Substitute.For<IOpenIddictApplicationRepository>());
        context.Services.AddTransient<ICurrencyExchangeProvider, TestCurrencyExchangeProvider>();
    }
}
