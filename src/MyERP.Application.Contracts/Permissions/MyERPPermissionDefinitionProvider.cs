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

        var companiesPermission = myGroup.AddPermission(MyERPPermissions.Companies.Default, L("Permission:Companies"));
        companiesPermission.AddChild(MyERPPermissions.Companies.Create, L("Permission:Companies.Create"));
        companiesPermission.AddChild(MyERPPermissions.Companies.Edit, L("Permission:Companies.Edit"));
        companiesPermission.AddChild(MyERPPermissions.Companies.Delete, L("Permission:Companies.Delete"));

        var branchesPermission = myGroup.AddPermission(MyERPPermissions.Branches.Default, L("Permission:Branches"));
        branchesPermission.AddChild(MyERPPermissions.Branches.Create, L("Permission:Branches.Create"));
        branchesPermission.AddChild(MyERPPermissions.Branches.Edit, L("Permission:Branches.Edit"));
        branchesPermission.AddChild(MyERPPermissions.Branches.Delete, L("Permission:Branches.Delete"));

        var accountsPermission = myGroup.AddPermission(MyERPPermissions.Accounts.Default, L("Permission:Accounts"));
        accountsPermission.AddChild(MyERPPermissions.Accounts.Create, L("Permission:Accounts.Create"));
        accountsPermission.AddChild(MyERPPermissions.Accounts.Edit, L("Permission:Accounts.Edit"));
        accountsPermission.AddChild(MyERPPermissions.Accounts.Delete, L("Permission:Accounts.Delete"));

        var customersPermission = myGroup.AddPermission(MyERPPermissions.Customers.Default, L("Permission:Customers"));
        customersPermission.AddChild(MyERPPermissions.Customers.Create, L("Permission:Customers.Create"));
        customersPermission.AddChild(MyERPPermissions.Customers.Edit, L("Permission:Customers.Edit"));
        customersPermission.AddChild(MyERPPermissions.Customers.Delete, L("Permission:Customers.Delete"));

        var suppliersPermission = myGroup.AddPermission(MyERPPermissions.Suppliers.Default, L("Permission:Suppliers"));
        suppliersPermission.AddChild(MyERPPermissions.Suppliers.Create, L("Permission:Suppliers.Create"));
        suppliersPermission.AddChild(MyERPPermissions.Suppliers.Edit, L("Permission:Suppliers.Edit"));
        suppliersPermission.AddChild(MyERPPermissions.Suppliers.Delete, L("Permission:Suppliers.Delete"));

        var itemsPermission = myGroup.AddPermission(MyERPPermissions.Items.Default, L("Permission:Items"));
        itemsPermission.AddChild(MyERPPermissions.Items.Create, L("Permission:Items.Create"));
        itemsPermission.AddChild(MyERPPermissions.Items.Edit, L("Permission:Items.Edit"));
        itemsPermission.AddChild(MyERPPermissions.Items.Delete, L("Permission:Items.Delete"));

        var warehousesPermission = myGroup.AddPermission(MyERPPermissions.Warehouses.Default, L("Permission:Warehouses"));
        warehousesPermission.AddChild(MyERPPermissions.Warehouses.Create, L("Permission:Warehouses.Create"));
        warehousesPermission.AddChild(MyERPPermissions.Warehouses.Edit, L("Permission:Warehouses.Edit"));
        warehousesPermission.AddChild(MyERPPermissions.Warehouses.Delete, L("Permission:Warehouses.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<MyERPResource>(name);
    }
}
