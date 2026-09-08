using MyERP.Inventory;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreItemStandardCostAppServiceTests : ItemStandardCostAppServiceTests<MyERPEntityFrameworkCoreTestModule>
{
}
