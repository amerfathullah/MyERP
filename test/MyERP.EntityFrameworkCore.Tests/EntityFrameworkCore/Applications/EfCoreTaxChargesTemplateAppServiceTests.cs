using MyERP.Tax;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreTaxChargesTemplateAppServiceTests : TaxChargesTemplateAppServiceTests<MyERPEntityFrameworkCoreTestModule>
{
}
