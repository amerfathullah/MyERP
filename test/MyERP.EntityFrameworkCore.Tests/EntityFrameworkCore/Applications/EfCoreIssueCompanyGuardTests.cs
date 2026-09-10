using MyERP.Support;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreIssueCompanyGuardTests : IssueCompanyGuardTests<MyERPEntityFrameworkCoreTestModule>
{
}
