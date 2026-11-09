using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch27IntegrationTests : UpstreamBatch27IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
