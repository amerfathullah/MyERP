using MyERP.Purchasing;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreSupplierQuotationCompanyGuardTests : SupplierQuotationCompanyGuardTests<MyERPEntityFrameworkCoreTestModule>
{
}
