using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch19IntegrationTests : UpstreamBatch19IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
