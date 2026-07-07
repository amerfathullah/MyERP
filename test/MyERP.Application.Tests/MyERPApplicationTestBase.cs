using Volo.Abp.Modularity;

namespace MyERP;

public abstract class MyERPApplicationTestBase<TStartupModule> : MyERPTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
