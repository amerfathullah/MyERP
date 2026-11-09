using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch10IntegrationTests : UpstreamBatch10IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
