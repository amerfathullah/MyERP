using MyERP.Samples;
using Xunit;

namespace MyERP.EntityFrameworkCore.Domains;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<MyERPEntityFrameworkCoreTestModule>
{

}
