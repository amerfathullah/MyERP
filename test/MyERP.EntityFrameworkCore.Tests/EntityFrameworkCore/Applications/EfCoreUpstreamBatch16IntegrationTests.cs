using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch16IntegrationTests : UpstreamBatch16IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
