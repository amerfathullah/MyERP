using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch12IntegrationTests : UpstreamBatch12IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
