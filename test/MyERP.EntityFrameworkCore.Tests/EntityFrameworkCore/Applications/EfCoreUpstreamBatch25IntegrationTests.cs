using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch25IntegrationTests : UpstreamBatch25IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
