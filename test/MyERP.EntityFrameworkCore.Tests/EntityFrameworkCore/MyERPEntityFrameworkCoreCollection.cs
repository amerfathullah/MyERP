using Xunit;

namespace MyERP.EntityFrameworkCore;

[CollectionDefinition(MyERPTestConsts.CollectionDefinitionName)]
public class MyERPEntityFrameworkCoreCollection : ICollectionFixture<MyERPEntityFrameworkCoreFixture>
{

}
