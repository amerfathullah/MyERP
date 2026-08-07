using MyERP.Inventory.Tests;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCorePickListAppServiceTests : PickListAppService_Tests<MyERPEntityFrameworkCoreTestModule>
{
}
