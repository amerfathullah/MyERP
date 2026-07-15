using MyERP.Sales;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreSalesInvoiceAppServiceTests : SalesInvoiceAppService_Tests<MyERPEntityFrameworkCoreTestModule>
{
}
