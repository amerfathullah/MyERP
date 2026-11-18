using MyERP.Core;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreCompanyValidationTests : CompanyValidationTests<MyERPEntityFrameworkCoreTestModule>
{
}
