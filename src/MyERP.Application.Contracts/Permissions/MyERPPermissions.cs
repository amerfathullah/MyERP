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

    public static class TaxTemplates
    {
        public const string Default = GroupName + ".TaxTemplates";
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

    public static class RepostAccountingLedger
    {
        public const string Default = GroupName + ".RepostAccountingLedger";
        public const string Create = Default + ".Create";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class ProcessPaymentReconciliation
    {
        public const string Default = GroupName + ".ProcessPaymentReconciliation";
        public const string Create = Default + ".Create";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
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

    public static class PaymentOrders
    {
        public const string Default = GroupName + ".PaymentOrders";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class UnreconcilePayments
    {
        public const string Default = GroupName + ".UnreconcilePayments";
        public const string Create = Default + ".Create";
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

    public static class PackingSlips
    {
        public const string Default = GroupName + ".PackingSlips";
        public const string Create = Default + ".Create";
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

    public static class MasterProductionSchedules
    {
        public const string Default = GroupName + ".MasterProductionSchedules";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class SalesForecasts
    {
        public const string Default = GroupName + ".SalesForecasts";
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

    public static class ServiceLevelAgreements
    {
        public const string Default = GroupName + ".ServiceLevelAgreements";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class IssuePriorities
    {
        public const string Default = GroupName + ".IssuePriorities";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class IssueTypes
    {
        public const string Default = GroupName + ".IssueTypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SupportSettings
    {
        public const string Default = GroupName + ".SupportSettings";
        public const string Edit = Default + ".Edit";
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

    public static class ShareManagement
    {
        public const string Default = GroupName + ".ShareManagement";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class PromotionalSchemes
    {
        public const string Default = GroupName + ".PromotionalSchemes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
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

    /// <summary>
    /// Manager-level permission for modifying company restriction settings on master data.
    /// Per ERPNext PR #57383: only master-manager roles can view/edit restrict_to_companies and allowed_companies.
    /// Maps to ERPNext permlevel 1 on Item (Item Manager), Customer (Sales Master Manager), Supplier (Purchase Master Manager).
    /// </summary>
    public static class CompanyRestrictions
    {
        public const string Default = GroupName + ".CompanyRestrictions";
        public const string Manage = Default + ".Manage";
    }

    public static class SalesPartners
    {
        public const string Default = GroupName + ".SalesPartners";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class WarrantyClaims
    {
        public const string Default = GroupName + ".WarrantyClaims";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string StartWork = Default + ".StartWork";
        public const string Close = Default + ".Close";
        public const string Reopen = Default + ".Reopen";
        public const string Cancel = Default + ".Cancel";
    }

    public static class MaintenanceSchedules
    {
        public const string Default = GroupName + ".MaintenanceSchedules";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
    }

    public static class MaintenanceVisits
    {
        public const string Default = GroupName + ".MaintenanceVisits";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
    }

    public static class WarehouseAccounts
    {
        public const string Default = GroupName + ".WarehouseAccounts";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class BankGuarantees
    {
        public const string Default = GroupName + ".BankGuarantees";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Submit = Default + ".Submit";
        public const string Cancel = Default + ".Cancel";
    }

    public static class CustomsTariffNumbers
    {
        public const string Default = GroupName + ".CustomsTariffNumbers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Manufacturers
    {
        public const string Default = GroupName + ".Manufacturers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ItemManufacturers
    {
        public const string Default = GroupName + ".ItemManufacturers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ItemAlternatives
    {
        public const string Default = GroupName + ".ItemAlternatives";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class PartySpecificItems
    {
        public const string Default = GroupName + ".PartySpecificItems";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class DeliveryTrips
    {
        public const string Default = GroupName + ".DeliveryTrips";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Schedule = Default + ".Schedule";
        public const string Transit = Default + ".Transit";
        public const string Complete = Default + ".Complete";
        public const string Cancel = Default + ".Cancel";
    }


    public static class AssetMaintenances
    {
        public const string Default = GroupName + ".AssetMaintenances";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class AssetMaintenanceLogs
    {
        public const string Default = GroupName + ".AssetMaintenanceLogs";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Complete = Default + ".Complete";
        public const string Cancel = Default + ".Cancel";
    }

    public static class QualityGoals
    {
        public const string Default = GroupName + ".QualityGoals";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class QualityReviews
    {
        public const string Default = GroupName + ".QualityReviews";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class QualityProcedures
    {
        public const string Default = GroupName + ".QualityProcedures";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class QualityActions
    {
        public const string Default = GroupName + ".QualityActions";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class NonConformances
    {
        public const string Default = GroupName + ".NonConformances";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class QualityMeetings
    {
        public const string Default = GroupName + ".QualityMeetings";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class QualityFeedbacks
    {
        public const string Default = GroupName + ".QualityFeedbacks";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class AssetCategories
    {
        public const string Default = GroupName + ".AssetCategories";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Locations
    {
        public const string Default = GroupName + ".Locations";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Vehicles
    {
        public const string Default = GroupName + ".Vehicles";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Drivers
    {
        public const string Default = GroupName + ".Drivers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class DrivingLicenseCategories
    {
        public const string Default = GroupName + ".DrivingLicenseCategories";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class AssetMovements
    {
        public const string Default = GroupName + ".AssetMovements";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class AssetRepairs
    {
        public const string Default = GroupName + ".AssetRepairs";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class AssetCapitalizations
    {
        public const string Default = GroupName + ".AssetCapitalizations";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class AssetValueAdjustments
    {
        public const string Default = GroupName + ".AssetValueAdjustments";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class TermsAndConditions
    {
        public const string Default = GroupName + ".TermsAndConditions";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class OpportunityLostReasons
    {
        public const string Default = GroupName + ".OpportunityLostReasons";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class EmployeeGroups
    {
        public const string Default = GroupName + ".EmployeeGroups";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class CustomerGroups
    {
        public const string Default = GroupName + ".CustomerGroups";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SupplierGroups
    {
        public const string Default = GroupName + ".SupplierGroups";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Territories
    {
        public const string Default = GroupName + ".Territories";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ActivityCosts
    {
        public const string Default = GroupName + ".ActivityCosts";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class TaskTypes
    {
        public const string Default = GroupName + ".TaskTypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class PlantFloors
    {
        public const string Default = GroupName + ".PlantFloors";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class QuotationLostReasons
    {
        public const string Default = GroupName + ".QuotationLostReasons";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class QualityInspectionParameterGroups
    {
        public const string Default = GroupName + ".QualityInspectionParameterGroups";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ShipmentParcelTemplates
    {
        public const string Default = GroupName + ".ShipmentParcelTemplates";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ItemLeadTimes
    {
        public const string Default = GroupName + ".ItemLeadTimes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ProjectUpdates
    {
        public const string Default = GroupName + ".ProjectUpdates";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class BankAccountTypes
    {
        public const string Default = GroupName + ".BankAccountTypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class BankAccountSubtypes
    {
        public const string Default = GroupName + ".BankAccountSubtypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SalesPartnerTypes
    {
        public const string Default = GroupName + ".SalesPartnerTypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class BankAccountBalances
    {
        public const string Default = GroupName + ".BankAccountBalances";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SupplierScorecardVariables
    {
        public const string Default = GroupName + ".SupplierScorecardVariables";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class PartyTypes
    {
        public const string Default = GroupName + ".PartyTypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Banks
    {
        public const string Default = GroupName + ".Banks";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ChequePrintTemplates
    {
        public const string Default = GroupName + ".ChequePrintTemplates";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class TaxWithholdingGroups
    {
        public const string Default = GroupName + ".TaxWithholdingGroups";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class OpportunityTypes
    {
        public const string Default = GroupName + ".OpportunityTypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class CommunicationMedia
    {
        public const string Default = GroupName + ".CommunicationMedia";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class TelephonyCallTypes
    {
        public const string Default = GroupName + ".TelephonyCallTypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class CallLogs
    {
        public const string Default = GroupName + ".CallLogs";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class IncomingCallSettings
    {
        public const string Default = GroupName + ".IncomingCallSettings";
        public const string Edit = Default + ".Edit";
    }

    public static class Settings
    {
        public const string Default = GroupName + ".Settings";
        public const string Edit = Default + ".Edit";
    }
}

