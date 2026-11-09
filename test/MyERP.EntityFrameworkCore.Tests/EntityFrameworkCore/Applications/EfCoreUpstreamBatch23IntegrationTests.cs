using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch23IntegrationTests : UpstreamBatch23IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
