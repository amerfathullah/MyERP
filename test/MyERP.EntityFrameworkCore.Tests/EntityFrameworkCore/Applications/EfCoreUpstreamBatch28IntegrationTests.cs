using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch28IntegrationTests : UpstreamBatch28IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
