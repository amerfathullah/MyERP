using MyERP.Projects;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreTaskDependencyCycleTests : TaskDependencyCycleTests<MyERPEntityFrameworkCoreTestModule>
{
}
