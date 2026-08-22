using MyERP.Maintenance;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreMaintenanceScheduleGenerateScheduleTests : MaintenanceScheduleGenerateScheduleTests<MyERPEntityFrameworkCoreTestModule>
{
}
