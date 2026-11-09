using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch7IntegrationTests : UpstreamBatch7IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
