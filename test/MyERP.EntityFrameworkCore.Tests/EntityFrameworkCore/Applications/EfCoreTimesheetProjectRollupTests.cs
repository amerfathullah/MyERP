using MyERP.Projects;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreTimesheetProjectRollupTests : TimesheetProjectRollupTests<MyERPEntityFrameworkCoreTestModule>
{
}
