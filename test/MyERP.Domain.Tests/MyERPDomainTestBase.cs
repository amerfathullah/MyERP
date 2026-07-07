using Volo.Abp.Modularity;

namespace MyERP;

/* Inherit from this class for your domain layer tests. */
public abstract class MyERPDomainTestBase<TStartupModule> : MyERPTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
