using MyERP.Workflow;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreApprovalNotificationTests : ApprovalNotificationTests<MyERPEntityFrameworkCoreTestModule>
{
}
