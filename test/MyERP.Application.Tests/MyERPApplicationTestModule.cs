using Volo.Abp.Modularity;

namespace MyERP;

[DependsOn(
    typeof(MyERPApplicationModule),
    typeof(MyERPDomainTestModule)
)]
public class MyERPApplicationTestModule : AbpModule
{

}
