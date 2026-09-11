using MyERP.CRM;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreOpportunityCompanyGuardTests : OpportunityCompanyGuardTests<MyERPEntityFrameworkCoreTestModule>
{
}
