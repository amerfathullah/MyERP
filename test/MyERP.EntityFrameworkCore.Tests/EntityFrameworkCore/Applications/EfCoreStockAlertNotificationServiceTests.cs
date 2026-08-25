using MyERP.Inventory;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreStockAlertNotificationServiceTests : StockAlertNotificationServiceTests<MyERPEntityFrameworkCoreTestModule>
{
}
