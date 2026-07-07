namespace MyERP.Permissions;

public static class MyERPPermissions
{
    public const string GroupName = "MyERP";

    public static class Companies
    {
        public const string Default = GroupName + ".Companies";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Branches
    {
        public const string Default = GroupName + ".Branches";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Accounts
    {
        public const string Default = GroupName + ".Accounts";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Customers
    {
        public const string Default = GroupName + ".Customers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Suppliers
    {
        public const string Default = GroupName + ".Suppliers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Items
    {
        public const string Default = GroupName + ".Items";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Warehouses
    {
        public const string Default = GroupName + ".Warehouses";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class TaxCategories
    {
        public const string Default = GroupName + ".TaxCategories";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SalesInvoices
    {
        public const string Default = GroupName + ".SalesInvoices";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class JournalEntries
    {
        public const string Default = GroupName + ".JournalEntries";
        public const string Create = Default + ".Create";
        public const string Post = Default + ".Post";
    }
}
