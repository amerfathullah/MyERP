using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch11IntegrationTests : UpstreamBatch11IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
