using MyERP.Books;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications.Books;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreBookAppService_Tests : BookAppService_Tests<MyERPEntityFrameworkCoreTestModule>
{

}