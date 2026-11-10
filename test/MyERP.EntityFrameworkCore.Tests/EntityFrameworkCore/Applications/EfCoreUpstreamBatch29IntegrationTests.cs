using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch29IntegrationTests : UpstreamBatch29IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
