using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch22IntegrationTests : UpstreamBatch22IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
