using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch24IntegrationTests : UpstreamBatch24IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
