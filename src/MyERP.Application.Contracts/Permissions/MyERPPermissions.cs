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

    public static class Quotations
    {
        public const string Default = GroupName + ".Quotations";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class SalesOrders
    {
        public const string Default = GroupName + ".SalesOrders";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class StockEntries
    {
        public const string Default = GroupName + ".StockEntries";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Post = Default + ".Post";
        public const string Cancel = Default + ".Cancel";
    }

    public static class PurchaseOrders
    {
        public const string Default = GroupName + ".PurchaseOrders";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class PurchaseInvoices
    {
        public const string Default = GroupName + ".PurchaseInvoices";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class PaymentEntries
    {
        public const string Default = GroupName + ".PaymentEntries";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class DeliveryNotes
    {
        public const string Default = GroupName + ".DeliveryNotes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class EInvoice
    {
        public const string Default = GroupName + ".EInvoice";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class PurchaseReceipts
    {
        public const string Default = GroupName + ".PurchaseReceipts";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class ApprovalWorkflows
    {
        public const string Default = GroupName + ".ApprovalWorkflows";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ImportExport
    {
        public const string Default = GroupName + ".ImportExport";
        public const string Import = Default + ".Import";
        public const string Export = Default + ".Export";
    }

    public static class AutomationRules
    {
        public const string Default = GroupName + ".AutomationRules";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Employees
    {
        public const string Default = GroupName + ".Employees";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Leads
    {
        public const string Default = GroupName + ".Leads";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Convert = Default + ".Convert";
    }

    public static class Opportunities
    {
        public const string Default = GroupName + ".Opportunities";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Convert = Default + ".Convert";
    }

    public static class Payroll
    {
        public const string Default = GroupName + ".Payroll";
        public const string Create = Default + ".Create";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class Projects
    {
        public const string Default = GroupName + ".Projects";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Assets
    {
        public const string Default = GroupName + ".Assets";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
    }

    public static class Manufacturing
    {
        public const string Default = GroupName + ".Manufacturing";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ProductionPlans
    {
        public const string Default = GroupName + ".ProductionPlans";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class MaterialRequests
    {
        public const string Default = GroupName + ".MaterialRequests";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class Issues
    {
        public const string Default = GroupName + ".Issues";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Budgets
    {
        public const string Default = GroupName + ".Budgets";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class QualityInspections
    {
        public const string Default = GroupName + ".QualityInspections";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
    }

    public static class StockReconciliations
    {
        public const string Default = GroupName + ".StockReconciliations";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class LandedCostVouchers
    {
        public const string Default = GroupName + ".LandedCostVouchers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class LoyaltyPrograms
    {
        public const string Default = GroupName + ".LoyaltyPrograms";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SupplierScorecards
    {
        public const string Default = GroupName + ".SupplierScorecards";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ShippingRules
    {
        public const string Default = GroupName + ".ShippingRules";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SalesPersons
    {
        public const string Default = GroupName + ".SalesPersons";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }
}
