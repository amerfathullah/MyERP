using MyERP.EntityFrameworkCore;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreUpstreamBatch18IntegrationTests : UpstreamBatch18IntegrationTests<MyERPEntityFrameworkCoreTestModule>
{
}
