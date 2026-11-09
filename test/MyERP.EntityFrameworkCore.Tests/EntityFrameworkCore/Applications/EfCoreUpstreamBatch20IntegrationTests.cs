using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch20IntegrationTests : UpstreamBatch20IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
