using MyERP.Sales;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCorePosCompleteSaleGlPostingTests : PosCompleteSaleGlPostingTests<MyERPEntityFrameworkCoreTestModule>
{
}
