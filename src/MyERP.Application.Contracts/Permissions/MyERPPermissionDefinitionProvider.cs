using MyERP.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace MyERP.Permissions;

public class MyERPPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(MyERPPermissions.GroupName);

        var booksPermission = myGroup.AddPermission(MyERPPermissions.Books.Default, L("Permission:Books"));
        booksPermission.AddChild(MyERPPermissions.Books.Create, L("Permission:Books.Create"));
        booksPermission.AddChild(MyERPPermissions.Books.Edit, L("Permission:Books.Edit"));
        booksPermission.AddChild(MyERPPermissions.Books.Delete, L("Permission:Books.Delete"));

        var authorsPermission = myGroup.AddPermission(MyERPPermissions.Authors.Default, L("Permission:Authors"));
        authorsPermission.AddChild(MyERPPermissions.Authors.Create, L("Permission:Authors.Create"));
        authorsPermission.AddChild(MyERPPermissions.Authors.Edit, L("Permission:Authors.Edit"));
        authorsPermission.AddChild(MyERPPermissions.Authors.Delete, L("Permission:Authors.Delete"));
        //Define your own permissions here. Example:
        //myGroup.AddPermission(MyERPPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<MyERPResource>(name);
    }
}
