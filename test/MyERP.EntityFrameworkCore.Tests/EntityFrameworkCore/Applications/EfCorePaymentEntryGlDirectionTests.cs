using MyERP.Accounting;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCorePaymentEntryGlDirectionTests : PaymentEntryGlDirectionTests<MyERPEntityFrameworkCoreTestModule>
{
}
