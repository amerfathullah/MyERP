using MyERP.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace MyERP.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(MyERPEntityFrameworkCoreModule),
    typeof(MyERPApplicationContractsModule)
)]
public class MyERPDbMigratorModule : AbpModule
{
}
