using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch15IntegrationTests : UpstreamBatch15IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
