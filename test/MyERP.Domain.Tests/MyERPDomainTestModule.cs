using Volo.Abp.Modularity;

namespace MyERP;

[DependsOn(
    typeof(MyERPDomainModule),
    typeof(MyERPTestBaseModule)
)]
public class MyERPDomainTestModule : AbpModule
{

}
