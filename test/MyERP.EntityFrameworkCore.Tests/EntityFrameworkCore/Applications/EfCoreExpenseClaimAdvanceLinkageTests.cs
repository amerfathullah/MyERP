using MyERP.HumanResources;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreExpenseClaimAdvanceLinkageTests : ExpenseClaimAdvanceLinkageTests<MyERPEntityFrameworkCoreTestModule>
{
}
