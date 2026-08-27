using MyERP.HumanResources;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreLeaveApproverEnforcementTests : LeaveApproverEnforcementTests<MyERPEntityFrameworkCoreTestModule>
{
}
