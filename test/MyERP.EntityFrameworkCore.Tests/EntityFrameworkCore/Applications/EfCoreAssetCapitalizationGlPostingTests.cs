using MyERP.Assets;
using Xunit;

namespace MyERP.EntityFrameworkCore.Applications;

[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EfCoreAssetCapitalizationGlPostingTests : AssetCapitalizationGlPostingTests<MyERPEntityFrameworkCoreTestModule>
{
}
