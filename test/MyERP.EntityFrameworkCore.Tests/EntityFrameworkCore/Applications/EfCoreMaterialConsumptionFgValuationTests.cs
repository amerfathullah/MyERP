using MyERP.Manufacturing;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreMaterialConsumptionFgValuationTests : MaterialConsumptionFgValuationTests<MyERPEntityFrameworkCoreTestModule>
{
}
