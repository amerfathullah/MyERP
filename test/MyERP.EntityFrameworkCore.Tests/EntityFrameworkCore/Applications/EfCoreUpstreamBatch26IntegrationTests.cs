using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch26IntegrationTests : UpstreamBatch26IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
