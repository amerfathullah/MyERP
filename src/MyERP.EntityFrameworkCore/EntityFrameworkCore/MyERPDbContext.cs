using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Tax;
using MyERP.Tax.Entities;
using MyERP.EInvoice;
using MyERP.EInvoice.Entities;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;
using MyERP.Workflow.Entities;
using MyERP.Notification.Entities;
using MyERP.ImportExport.Entities;
using MyERP.Automation;
using MyERP.Automation.Entities;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Projects;
using MyERP.Projects.Entities;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Support.Entities;
using MyERP.Workflow;
using MyERP.Notification;
using MyERP.ImportExport;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace MyERP.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class MyERPDbContext :
    AbpDbContext<MyERPDbContext>,
    ITenantManagementDbContext,
    IIdentityDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */

    // Core
    public DbSet<Company> Companies { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<DocumentSeries> DocumentSeries { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<AutoRepeat> AutoRepeats { get; set; }
    public DbSet<AuthorizationRule> AuthorizationRules { get; set; }
    public DbSet<Territory> Territories { get; set; }
    public DbSet<CustomerGroup> CustomerGroups { get; set; }
    public DbSet<SupplierGroup> SupplierGroups { get; set; }
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<NotificationLog> NotificationLogs { get; set; }

    // Accounting
    public DbSet<Account> Accounts { get; set; }
    public DbSet<FiscalYear> FiscalYears { get; set; }
    public DbSet<AccountingRule> AccountingRules { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }
    public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
    public DbSet<PaymentEntry> PaymentEntries { get; set; }
    public DbSet<PaymentEntryReference> PaymentEntryReferences { get; set; }
    public DbSet<BankTransaction> BankTransactions { get; set; }
    public DbSet<BankTransactionRule> BankTransactionRules { get; set; }
    public DbSet<BankTransactionRuleCondition> BankTransactionRuleConditions { get; set; }
    public DbSet<BankTransactionRuleAccount> BankTransactionRuleAccounts { get; set; }
    public DbSet<CurrencyExchange> CurrencyExchanges { get; set; }
    public DbSet<PaymentLedgerEntry> PaymentLedgerEntries { get; set; }
    public DbSet<CostCenter> CostCenters { get; set; }
    public DbSet<AccountingPeriod> AccountingPeriods { get; set; }
    public DbSet<ModeOfPayment> ModesOfPayment { get; set; }
    public DbSet<PaymentTermsTemplate> PaymentTermsTemplates { get; set; }
    public DbSet<PaymentTerm> PaymentTerms { get; set; }
    public DbSet<Budget> Budgets { get; set; }
    public DbSet<BudgetAccount> BudgetAccounts { get; set; }
    public DbSet<PeriodClosingVoucher> PeriodClosingVouchers { get; set; }
    public DbSet<PeriodClosingEntry> PeriodClosingEntries { get; set; }
    public DbSet<PaymentRequest> PaymentRequests { get; set; }
    public DbSet<PaymentScheduleEntry> PaymentScheduleEntries { get; set; }
    public DbSet<ExchangeRateRevaluation> ExchangeRateRevaluations { get; set; }
    public DbSet<ExchangeRateRevaluationEntry> ExchangeRateRevaluationEntries { get; set; }
    public DbSet<AccountingDimension> AccountingDimensions { get; set; }
    public DbSet<AccountingDimensionFilter> AccountingDimensionFilters { get; set; }
    public DbSet<GlDimensionValue> GlDimensionValues { get; set; }
    public DbSet<FinanceBook> FinanceBooks { get; set; }
    public DbSet<AccountClosingBalance> AccountClosingBalances { get; set; }

    // Sales
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Quotation> Quotations { get; set; }
    public DbSet<QuotationItem> QuotationItems { get; set; }
    public DbSet<SalesOrder> SalesOrders { get; set; }
    public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
    public DbSet<SalesInvoice> SalesInvoices { get; set; }
    public DbSet<SalesInvoiceItem> SalesInvoiceItems { get; set; }
    public DbSet<DeliveryNote> DeliveryNotes { get; set; }
    public DbSet<DeliveryNoteItem> DeliveryNoteItems { get; set; }
    public DbSet<BlanketOrder> BlanketOrders { get; set; }
    public DbSet<BlanketOrderItem> BlanketOrderItems { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<Dunning> Dunnings { get; set; }
    public DbSet<DunningOverduePayment> DunningOverduePayments { get; set; }
    public DbSet<ProductBundle> ProductBundles { get; set; }
    public DbSet<ProductBundleItem> ProductBundleItems { get; set; }
    public DbSet<LoyaltyProgram> LoyaltyPrograms { get; set; }
    public DbSet<LoyaltyProgramTier> LoyaltyProgramTiers { get; set; }
    public DbSet<LoyaltyPointEntry> LoyaltyPointEntries { get; set; }
    public DbSet<PosClosingEntry> PosClosingEntries { get; set; }
    public DbSet<PosClosingPayment> PosClosingPayments { get; set; }
    public DbSet<PosClosingInvoice> PosClosingInvoices { get; set; }

    // Purchasing
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }
    public DbSet<PurchaseReceipt> PurchaseReceipts { get; set; }
    public DbSet<PurchaseReceiptItem> PurchaseReceiptItems { get; set; }
    public DbSet<MaterialRequest> MaterialRequests { get; set; }
    public DbSet<MaterialRequestItem> MaterialRequestItems { get; set; }
    public DbSet<SubcontractingOrder> SubcontractingOrders { get; set; }
    public DbSet<SubcontractingOrderItem> SubcontractingOrderItems { get; set; }
    public DbSet<SubcontractingOrderSuppliedItem> SubcontractingOrderSuppliedItems { get; set; }
    public DbSet<SubcontractingReceipt> SubcontractingReceipts { get; set; }
    public DbSet<SubcontractingReceiptItem> SubcontractingReceiptItems { get; set; }
    public DbSet<SupplierQuotation> SupplierQuotations { get; set; }
    public DbSet<SupplierQuotationItem> SupplierQuotationItems { get; set; }
    public DbSet<SupplierScorecard> SupplierScorecards { get; set; }
    public DbSet<ScorecardStanding> ScorecardStandings { get; set; }
    public DbSet<ScorecardCriterion> ScorecardCriteria { get; set; }
    public DbSet<ScorecardPeriod> ScorecardPeriods { get; set; }
    public DbSet<RequestForQuotation> RequestForQuotations { get; set; }
    public DbSet<RfqItem> RfqItems { get; set; }
    public DbSet<RfqSupplier> RfqSuppliers { get; set; }

    // Inventory
    public DbSet<Item> Items { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<StockEntry> StockEntries { get; set; }
    public DbSet<StockEntryItem> StockEntryItems { get; set; }
    public DbSet<StockLedgerEntry> StockLedgerEntries { get; set; }
    public DbSet<Bin> Bins { get; set; }
    public DbSet<PriceList> PriceLists { get; set; }
    public DbSet<ItemPrice> ItemPrices { get; set; }
    public DbSet<Batch> Batches { get; set; }
    public DbSet<UomConversion> UomConversions { get; set; }
    public DbSet<ItemGroup> ItemGroups { get; set; }
    public DbSet<ItemAttribute> ItemAttributes { get; set; }
    public DbSet<ItemAttributeValue> ItemAttributeValues { get; set; }
    public DbSet<ItemVariantAttribute> ItemVariantAttributes { get; set; }
    public DbSet<SerialNo> SerialNos { get; set; }
    public DbSet<QualityInspection> QualityInspections { get; set; }
    public DbSet<QualityInspectionReading> QualityInspectionReadings { get; set; }
    public DbSet<StockReconciliation> StockReconciliations { get; set; }
    public DbSet<StockReconciliationItem> StockReconciliationItems { get; set; }
    public DbSet<LandedCostVoucher> LandedCostVouchers { get; set; }
    public DbSet<LandedCostItem> LandedCostItems { get; set; }
    public DbSet<LandedCostCharge> LandedCostCharges { get; set; }
    public DbSet<StockReservationEntry> StockReservationEntries { get; set; }
    public DbSet<PickList> PickLists { get; set; }
    public DbSet<PickListItem> PickListItems { get; set; }
    public DbSet<StockClosingEntry> StockClosingEntries { get; set; }
    public DbSet<StockClosingBalance> StockClosingBalances { get; set; }

    // Tax
    public DbSet<TaxCategory> TaxCategories { get; set; }
    public DbSet<TaxRule> TaxRules { get; set; }
    public DbSet<TransactionTaxRow> TransactionTaxRows { get; set; }
    public DbSet<ItemTaxTemplate> ItemTaxTemplates { get; set; }
    public DbSet<ItemTaxTemplateDetail> ItemTaxTemplateDetails { get; set; }
    public DbSet<TaxWithholdingEntry> TaxWithholdingEntries { get; set; }

    // E-Invoice
    public DbSet<EInvoiceSubmission> EInvoiceSubmissions { get; set; }
    public DbSet<LhdnSuccessLog> LhdnSuccessLogs { get; set; }

    // Human Resources
    public DbSet<Employee> Employees { get; set; }
    public DbSet<ContributionRule> ContributionRules { get; set; }
    public DbSet<PayrollEntry> PayrollEntries { get; set; }
    public DbSet<PayrollEntryLine> PayrollEntryLines { get; set; }
    public DbSet<SalaryComponent> SalaryComponents { get; set; }
    public DbSet<SalaryStructure> SalaryStructures { get; set; }
    public DbSet<SalaryStructureDetail> SalaryStructureDetails { get; set; }
    public DbSet<LeaveType> LeaveTypes { get; set; }
    public DbSet<LeaveApplication> LeaveApplications { get; set; }
    public DbSet<LeaveAllocation> LeaveAllocations { get; set; }
    public DbSet<HolidayList> HolidayLists { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<SalarySlip> SalarySlips { get; set; }
    public DbSet<SalarySlipComponent> SalarySlipComponents { get; set; }
    public DbSet<ExpenseClaim> ExpenseClaims { get; set; }
    public DbSet<ExpenseClaimDetail> ExpenseClaimDetails { get; set; }
    public DbSet<Loan> Loans { get; set; }
    public DbSet<LoanRepaymentSchedule> LoanRepaymentSchedules { get; set; }

    // Support
    public DbSet<Issue> Issues { get; set; }

    // Workflow
    public DbSet<ApprovalRule> ApprovalRules { get; set; }
    public DbSet<ApprovalRequest> ApprovalRequests { get; set; }

    // Notifications
    public DbSet<AppNotification> AppNotifications { get; set; }

    // Activity Log
    public DbSet<DocumentActivityLog> DocumentActivityLogs { get; set; }

    // Import/Export
    public DbSet<ImportJob> ImportJobs { get; set; }

    // Automation
    public DbSet<AutomationRule> AutomationRules { get; set; }
    public DbSet<AutomationExecutionLog> AutomationExecutionLogs { get; set; }

    // CRM
    public DbSet<Lead> Leads { get; set; }
    public DbSet<Opportunity> Opportunities { get; set; }
    public DbSet<OpportunityItem> OpportunityItems { get; set; }

    // Projects
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectTask> ProjectTasks { get; set; }
    public DbSet<TaskDependency> TaskDependencies { get; set; }
    public DbSet<Timesheet> Timesheets { get; set; }
    public DbSet<TimesheetDetail> TimesheetDetails { get; set; }
    public DbSet<ActivityType> ActivityTypes { get; set; }
    public DbSet<ActivityCost> ActivityCosts { get; set; }

    // Assets
    public DbSet<Asset> Assets { get; set; }
    public DbSet<AssetCategory> AssetCategories { get; set; }
    public DbSet<DepreciationScheduleEntry> DepreciationScheduleEntries { get; set; }
    public DbSet<MaintenanceSchedule> MaintenanceSchedules { get; set; }
    public DbSet<MaintenanceScheduleDetail> MaintenanceScheduleDetails { get; set; }
    public DbSet<MaintenanceVisit> MaintenanceVisits { get; set; }
    public DbSet<MaintenanceVisitPurpose> MaintenanceVisitPurposes { get; set; }
    public DbSet<AssetMovement> AssetMovements { get; set; }

    // Manufacturing
    public DbSet<BillOfMaterials> BillOfMaterials { get; set; }
    public DbSet<BomItem> BomItems { get; set; }
    public DbSet<WorkOrder> WorkOrders { get; set; }
    public DbSet<WorkOrderItem> WorkOrderItems { get; set; }
    public DbSet<ProductionPlan> ProductionPlans { get; set; }
    public DbSet<ProductionPlanItem> ProductionPlanItems { get; set; }
    public DbSet<ProductionPlanMrItem> ProductionPlanMrItems { get; set; }
    public DbSet<Workstation> Workstations { get; set; }
    public DbSet<WorkstationCost> WorkstationCosts { get; set; }
    public DbSet<WorkstationWorkingHour> WorkstationWorkingHours { get; set; }
    public DbSet<Operation> Operations { get; set; }
    public DbSet<Routing> Routings { get; set; }
    public DbSet<RoutingOperation> RoutingOperations { get; set; }
    public DbSet<JobCard> JobCards { get; set; }
    public DbSet<JobCardTimeLog> JobCardTimeLogs { get; set; }
    public DbSet<ManufacturingSettings> ManufacturingSettings { get; set; }

    // Sales (additional)
    public DbSet<PricingRule> PricingRules { get; set; }
    public DbSet<ShippingRule> ShippingRules { get; set; }
    public DbSet<ShippingRuleCondition> ShippingRuleConditions { get; set; }
    public DbSet<ShippingRuleCountry> ShippingRuleCountries { get; set; }
    public DbSet<SalesPerson> SalesPersons { get; set; }
    public DbSet<SalesPersonTarget> SalesPersonTargets { get; set; }
    public DbSet<SalesTeamEntry> SalesTeamEntries { get; set; }

    #region Entities from the modules

    /* Notice: We only implemented IIdentityProDbContext and ISaasDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityProDbContext and ISaasDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion

    public MyERPDbContext(DbContextOptions<MyERPDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureTenantManagement();
        builder.ConfigureBlobStoring();

        /* Configure your own tables/entities inside here */

        builder.Entity<Company>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "Companies", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(CompanyConsts.MaxNameLength);
            b.Property(x => x.ShortName).HasMaxLength(CompanyConsts.MaxShortNameLength);
            b.Property(x => x.TaxId).HasMaxLength(CompanyConsts.MaxTaxIdLength);
            b.Property(x => x.RegistrationNumber).HasMaxLength(CompanyConsts.MaxRegistrationNumberLength);
            b.Property(x => x.SstRegistrationNumber).HasMaxLength(CompanyConsts.MaxSstRegistrationLength);
            b.Property(x => x.MsicCode).HasMaxLength(CompanyConsts.MaxMsicCodeLength);
            b.Property(x => x.Phone).HasMaxLength(CompanyConsts.MaxPhoneLength);
            b.Property(x => x.Email).HasMaxLength(CompanyConsts.MaxEmailLength);
            b.Property(x => x.Website).HasMaxLength(CompanyConsts.MaxWebsiteLength);
            b.Property(x => x.Address).HasMaxLength(CompanyConsts.MaxAddressLength);
            b.Property(x => x.City).HasMaxLength(CompanyConsts.MaxCityLength);
            b.Property(x => x.State).HasMaxLength(CompanyConsts.MaxStateLength);
            b.Property(x => x.PostalCode).HasMaxLength(CompanyConsts.MaxPostalCodeLength);
            b.Property(x => x.Country).HasMaxLength(CompanyConsts.MaxCountryLength);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(CompanyConsts.MaxCurrencyCodeLength);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<Branch>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "Branches", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(BranchConsts.MaxNameLength);
            b.Property(x => x.Code).HasMaxLength(BranchConsts.MaxCodeLength);
            b.Property(x => x.Phone).HasMaxLength(BranchConsts.MaxPhoneLength);
            b.Property(x => x.Email).HasMaxLength(BranchConsts.MaxEmailLength);
            b.Property(x => x.Address).HasMaxLength(BranchConsts.MaxAddressLength);
            b.Property(x => x.City).HasMaxLength(BranchConsts.MaxCityLength);
            b.Property(x => x.State).HasMaxLength(BranchConsts.MaxStateLength);
            b.Property(x => x.PostalCode).HasMaxLength(BranchConsts.MaxPostalCodeLength);
            b.Property(x => x.Country).HasMaxLength(BranchConsts.MaxCountryLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique().HasFilter("\"Code\" IS NOT NULL");
        });

        builder.Entity<DocumentSeries>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "DocumentSeries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(DocumentSeriesConsts.MaxNameLength);
            b.Property(x => x.DocumentType).IsRequired().HasMaxLength(DocumentSeriesConsts.MaxDocumentTypeLength);
            b.Property(x => x.Prefix).IsRequired().HasMaxLength(DocumentSeriesConsts.MaxPrefixLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DocumentType }).IsUnique();
        });

        builder.Entity<Address>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "Addresses", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.AddressType).IsRequired().HasMaxLength(50);
            b.Property(x => x.AddressLine1).IsRequired().HasMaxLength(300);
            b.Property(x => x.AddressLine2).HasMaxLength(300);
            b.Property(x => x.City).HasMaxLength(100);
            b.Property(x => x.State).HasMaxLength(100);
            b.Property(x => x.PostalCode).HasMaxLength(20);
            b.Property(x => x.Country).IsRequired().HasMaxLength(100);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.Fax).HasMaxLength(50);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.PartyType).IsRequired().HasMaxLength(50);
            b.HasIndex(x => new { x.TenantId, x.PartyType, x.PartyId });
            b.HasIndex(x => new { x.TenantId, x.PartyType, x.PartyId, x.IsPrimaryAddress });
        });

        builder.Entity<Contact>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "Contacts", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
            b.Property(x => x.LastName).HasMaxLength(100);
            b.Property(x => x.Salutation).HasMaxLength(20);
            b.Property(x => x.Designation).HasMaxLength(100);
            b.Property(x => x.Department).HasMaxLength(100);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.MobileNo).HasMaxLength(50);
            b.Property(x => x.PartyType).IsRequired().HasMaxLength(50);
            b.HasIndex(x => new { x.TenantId, x.PartyType, x.PartyId });
            b.HasIndex(x => new { x.TenantId, x.PartyType, x.PartyId, x.IsPrimaryContact });
        });

        builder.Entity<AutoRepeat>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "AutoRepeats", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ReferenceDocumentType).IsRequired().HasMaxLength(100);
            b.Property(x => x.ReferenceDocumentNumber).HasMaxLength(100);
            b.Property(x => x.NotifyRecipients).HasMaxLength(500);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.IsEnabled, x.NextScheduleDate });
        });

        builder.Entity<AuthorizationRule>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "AuthorizationRules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TransactionType).IsRequired().HasMaxLength(100);
            b.Property(x => x.ThresholdValue).HasColumnType("decimal(18,4)");
            b.Property(x => x.SystemRole).HasMaxLength(100);
            b.Property(x => x.ApprovingRole).HasMaxLength(100);
            b.Property(x => x.Designation).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.TransactionType, x.CompanyId, x.BasedOn });
        });

        builder.Entity<Territory>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "Territories", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ParentId });
        });

        builder.Entity<CustomerGroup>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "CustomerGroups", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.DefaultCreditLimit).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ParentId });
        });

        builder.Entity<SupplierGroup>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "SupplierGroups", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ParentId });
        });

        builder.Entity<EmailTemplate>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "EmailTemplates", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Subject).IsRequired().HasMaxLength(500);
            b.Property(x => x.Body).IsRequired();
            b.Property(x => x.DocumentType).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<NotificationLog>(b =>
        {
            b.ToTable(MyERPConsts.DbTablePrefix + "NotificationLogs", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Subject).IsRequired().HasMaxLength(500);
            b.Property(x => x.Body).HasMaxLength(4000);
            b.Property(x => x.Recipient).IsRequired().HasMaxLength(200);
            b.Property(x => x.Sender).HasMaxLength(200);
            b.Property(x => x.DocumentType).HasMaxLength(100);
            b.Property(x => x.ErrorMessage).HasMaxLength(2000);
            b.HasIndex(x => new { x.TenantId, x.DocumentType, x.DocumentId });
            b.HasIndex(x => new { x.TenantId, x.Status, x.CreationTime });
        });

        // Accounting
        builder.Entity<Account>(b =>
        {
            b.ToTable("Acc_Accounts", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.AccountCode).IsRequired().HasMaxLength(AccountConsts.MaxAccountCodeLength);
            b.Property(x => x.AccountName).IsRequired().HasMaxLength(AccountConsts.MaxAccountNameLength);
            b.Property(x => x.Currency).HasMaxLength(AccountConsts.MaxCurrencyLength);
            b.Property(x => x.Description).HasMaxLength(AccountConsts.MaxDescriptionLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.AccountCode }).IsUnique();
        });

        builder.Entity<FiscalYear>(b =>
        {
            b.ToTable("Acc_FiscalYears", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(FiscalYearConsts.MaxNameLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Name }).IsUnique();
        });

        // Sales
        builder.Entity<Customer>(b =>
        {
            b.ToTable("Sal_Customers", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(CustomerConsts.MaxNameLength);
            b.Property(x => x.CustomerCode).HasMaxLength(CustomerConsts.MaxCodeLength);
            b.Property(x => x.Tin).HasMaxLength(CustomerConsts.MaxTinLength);
            b.Property(x => x.RegistrationNumber).HasMaxLength(CustomerConsts.MaxRegistrationNumberLength);
            b.Property(x => x.SstRegistrationNumber).HasMaxLength(CustomerConsts.MaxSstRegistrationLength);
            b.Property(x => x.IdType).HasMaxLength(CustomerConsts.MaxIdTypeLength);
            b.Property(x => x.IdValue).HasMaxLength(CustomerConsts.MaxIdValueLength);
            b.Property(x => x.ContactPerson).HasMaxLength(CustomerConsts.MaxContactPersonLength);
            b.Property(x => x.Phone).HasMaxLength(CustomerConsts.MaxPhoneLength);
            b.Property(x => x.Email).HasMaxLength(CustomerConsts.MaxEmailLength);
            b.Property(x => x.Website).HasMaxLength(CustomerConsts.MaxWebsiteLength);
            b.Property(x => x.Address).HasMaxLength(CustomerConsts.MaxAddressLength);
            b.Property(x => x.City).HasMaxLength(CustomerConsts.MaxCityLength);
            b.Property(x => x.State).HasMaxLength(CustomerConsts.MaxStateLength);
            b.Property(x => x.PostalCode).HasMaxLength(CustomerConsts.MaxPostalCodeLength);
            b.Property(x => x.Country).HasMaxLength(CustomerConsts.MaxCountryLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CustomerCode }).IsUnique().HasFilter("\"CustomerCode\" IS NOT NULL");
        });

        // Purchasing
        builder.Entity<Supplier>(b =>
        {
            b.ToTable("Pur_Suppliers", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(SupplierConsts.MaxNameLength);
            b.Property(x => x.SupplierCode).HasMaxLength(SupplierConsts.MaxCodeLength);
            b.Property(x => x.Tin).HasMaxLength(SupplierConsts.MaxTinLength);
            b.Property(x => x.RegistrationNumber).HasMaxLength(SupplierConsts.MaxRegistrationNumberLength);
            b.Property(x => x.SstRegistrationNumber).HasMaxLength(SupplierConsts.MaxSstRegistrationLength);
            b.Property(x => x.IdType).HasMaxLength(SupplierConsts.MaxIdTypeLength);
            b.Property(x => x.IdValue).HasMaxLength(SupplierConsts.MaxIdValueLength);
            b.Property(x => x.ContactPerson).HasMaxLength(SupplierConsts.MaxContactPersonLength);
            b.Property(x => x.Phone).HasMaxLength(SupplierConsts.MaxPhoneLength);
            b.Property(x => x.Email).HasMaxLength(SupplierConsts.MaxEmailLength);
            b.Property(x => x.Website).HasMaxLength(SupplierConsts.MaxWebsiteLength);
            b.Property(x => x.Address).HasMaxLength(SupplierConsts.MaxAddressLength);
            b.Property(x => x.City).HasMaxLength(SupplierConsts.MaxCityLength);
            b.Property(x => x.State).HasMaxLength(SupplierConsts.MaxStateLength);
            b.Property(x => x.PostalCode).HasMaxLength(SupplierConsts.MaxPostalCodeLength);
            b.Property(x => x.Country).HasMaxLength(SupplierConsts.MaxCountryLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SupplierCode }).IsUnique().HasFilter("\"SupplierCode\" IS NOT NULL");
        });

        // Inventory
        builder.Entity<Item>(b =>
        {
            b.ToTable("Inv_Items", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemCode).IsRequired().HasMaxLength(ItemConsts.MaxCodeLength);
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(ItemConsts.MaxNameLength);
            b.Property(x => x.Barcode).HasMaxLength(ItemConsts.MaxBarcodeLength);
            b.Property(x => x.Description).HasMaxLength(ItemConsts.MaxDescriptionLength);
            b.Property(x => x.ItemGroup).HasMaxLength(ItemConsts.MaxGroupLength);
            b.Property(x => x.Brand).HasMaxLength(ItemConsts.MaxBrandLength);
            b.Property(x => x.Uom).IsRequired().HasMaxLength(ItemConsts.MaxUomLength);
            b.Property(x => x.StandardSellingPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.StandardBuyingPrice).HasColumnType("decimal(18,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ItemCode }).IsUnique();
        });

        builder.Entity<Warehouse>(b =>
        {
            b.ToTable("Inv_Warehouses", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(WarehouseConsts.MaxNameLength);
            b.Property(x => x.WarehouseCode).HasMaxLength(WarehouseConsts.MaxCodeLength);
            b.Property(x => x.Address).HasMaxLength(WarehouseConsts.MaxAddressLength);
            b.Property(x => x.City).HasMaxLength(WarehouseConsts.MaxCityLength);
            b.Property(x => x.State).HasMaxLength(WarehouseConsts.MaxStateLength);
            b.Property(x => x.PostalCode).HasMaxLength(WarehouseConsts.MaxPostalCodeLength);
            b.Property(x => x.Country).HasMaxLength(WarehouseConsts.MaxCountryLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WarehouseCode }).IsUnique().HasFilter("\"WarehouseCode\" IS NOT NULL");
        });

        // Tax
        builder.Entity<TaxCategory>(b =>
        {
            b.ToTable("Tax_Categories", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Code).IsRequired().HasMaxLength(TaxCategoryConsts.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(TaxCategoryConsts.MaxNameLength);
            b.Property(x => x.Description).HasMaxLength(TaxCategoryConsts.MaxDescriptionLength);
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        builder.Entity<TaxRule>(b =>
        {
            b.ToTable("Tax_Rules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Rate).HasColumnType("decimal(8,4)");
            b.Property(x => x.ItemGroupFilter).HasMaxLength(TaxRuleConsts.MaxItemGroupFilterLength);
            b.Property(x => x.RegionFilter).HasMaxLength(TaxRuleConsts.MaxRegionFilterLength);
            b.Property(x => x.Description).HasMaxLength(TaxRuleConsts.MaxDescriptionLength);
            b.HasOne<TaxCategory>().WithMany().HasForeignKey(x => x.TaxCategoryId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.TaxCategoryId, x.EffectiveFrom });
        });

        builder.Entity<TransactionTaxRow>(b =>
        {
            b.ToTable("Tax_TransactionTaxRows", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ParentType).IsRequired().HasMaxLength(50);
            b.Property(x => x.Description).IsRequired().HasMaxLength(200);
            b.Property(x => x.ChargeType).IsRequired().HasMaxLength(30);
            b.Property(x => x.TaxCategory).IsRequired().HasMaxLength(30);
            b.Property(x => x.Rate).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmountAfterDiscount).HasColumnType("decimal(18,4)");
            b.Property(x => x.Total).HasColumnType("decimal(18,4)");
            b.Property(x => x.BaseTaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.BaseTotal).HasColumnType("decimal(18,4)");
            b.HasIndex(x => new { x.ParentType, x.ParentId });
        });

        // Accounting Rules & Journal Entries
        builder.Entity<AccountingRule>(b =>
        {
            b.ToTable("Acc_AccountingRules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(AccountingRuleConsts.MaxNameLength);
            b.Property(x => x.DocumentType).IsRequired().HasMaxLength(AccountingRuleConsts.MaxDocumentTypeLength);
            b.Property(x => x.Description).HasMaxLength(AccountingRuleConsts.MaxDescriptionLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DocumentType });
        });

        builder.Entity<JournalEntry>(b =>
        {
            b.ToTable("Acc_JournalEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.EntryNumber).HasMaxLength(JournalEntryConsts.MaxReferenceNumberLength);
            b.Property(x => x.ReferenceType).HasMaxLength(JournalEntryConsts.MaxReferenceTypeLength);
            b.Property(x => x.ReferenceNumber).HasMaxLength(JournalEntryConsts.MaxReferenceNumberLength);
            b.Property(x => x.Narration).HasMaxLength(JournalEntryConsts.MaxNarrationLength);
            b.Property(x => x.TotalDebit).HasColumnType("decimal(18,4)");
            b.Property(x => x.TotalCredit).HasColumnType("decimal(18,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<FiscalYear>().WithMany().HasForeignKey(x => x.FiscalYearId).IsRequired();
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.JournalEntryId).IsRequired();
            b.Navigation(x => x.Lines).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.EntryNumber }).IsUnique().HasFilter("\"EntryNumber\" IS NOT NULL");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PostingDate });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Status });
        });

        builder.Entity<JournalEntryLine>(b =>
        {
            b.ToTable("Acc_JournalEntryLines", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            b.Property(x => x.AmountInAccountCurrency).HasColumnType("decimal(18,4)");
            b.Property(x => x.ExchangeRate).HasColumnType("decimal(18,6)");
            b.Property(x => x.AccountCurrency).HasMaxLength(3);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.PartyType).HasMaxLength(50);
            b.Property(x => x.AgainstVoucherType).HasMaxLength(64);
            b.Property(x => x.FinanceBook).HasMaxLength(100);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
        });

        builder.Entity<AccountingDimension>(b =>
        {
            b.ToTable("Acc_Dimensions", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DocumentType).IsRequired().HasMaxLength(100);
            b.Property(x => x.Label).IsRequired().HasMaxLength(200);
            b.Property(x => x.FieldName).IsRequired().HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.DocumentType }).IsUnique();
        });

        builder.Entity<AccountingDimensionFilter>(b =>
        {
            b.ToTable("Acc_DimensionFilters", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DimensionValueIds).HasMaxLength(4000);
            b.HasIndex(x => new { x.TenantId, x.AccountingDimensionId, x.AccountId, x.CompanyId }).IsUnique();
        });

        builder.Entity<GlDimensionValue>(b =>
        {
            b.ToTable("Acc_GlDimensionValues", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DimensionFieldName).IsRequired().HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.JournalEntryLineId, x.AccountingDimensionId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.DimensionFieldName, x.DimensionValueId });
        });

        builder.Entity<FinanceBook>(b =>
        {
            b.ToTable("Acc_FinanceBooks", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(140);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Name }).IsUnique();
        });

        builder.Entity<AccountClosingBalance>(b =>
        {
            b.ToTable("Acc_ClosingBalances", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Period).IsRequired().HasMaxLength(20);
            b.Property(x => x.Debit).HasColumnType("decimal(18,4)");
            b.Property(x => x.Credit).HasColumnType("decimal(18,4)");
            b.Property(x => x.FinanceBook).HasMaxLength(140);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Period, x.AccountId, x.CostCenterId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ClosingDate });
        });

        // Quotations
        builder.Entity<Quotation>(b =>
        {
            b.ToTable("Sal_Quotations", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.QuotationNumber).IsRequired().HasMaxLength(QuotationConsts.MaxQuotationNumberLength);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(QuotationConsts.MaxCurrencyCodeLength);
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.Terms).HasMaxLength(QuotationConsts.MaxTermsLength);
            b.Property(x => x.Notes).HasMaxLength(QuotationConsts.MaxNoteLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.QuotationId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.QuotationNumber }).IsUnique();
        });

        builder.Entity<QuotationItem>(b =>
        {
            b.ToTable("Sal_QuotationItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Uom).IsRequired().HasMaxLength(20);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
        });

        // Sales Orders
        builder.Entity<SalesOrder>(b =>
        {
            b.ToTable("Sal_SalesOrders", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.OrderNumber).IsRequired().HasMaxLength(SalesOrderConsts.MaxOrderNumberLength);
            b.Property(x => x.CustomerPoNumber).HasMaxLength(SalesOrderConsts.MaxCustomerPoLength);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(SalesOrderConsts.MaxCurrencyCodeLength);
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.Terms).HasMaxLength(SalesOrderConsts.MaxTermsLength);
            b.Property(x => x.Notes).HasMaxLength(SalesOrderConsts.MaxNoteLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SalesOrderId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.OrderNumber }).IsUnique();
        });

        builder.Entity<SalesOrderItem>(b =>
        {
            b.ToTable("Sal_SalesOrderItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Uom).IsRequired().HasMaxLength(20);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.DeliveredQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.BilledQty).HasColumnType("decimal(18,4)");
        });

        // Sales Invoices
        builder.Entity<SalesInvoice>(b =>
        {
            b.ToTable("Sal_SalesInvoices", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(SalesInvoiceConsts.MaxInvoiceNumberLength);
            b.Property(x => x.SupplierTin).HasMaxLength(SalesInvoiceConsts.MaxTinLength);
            b.Property(x => x.BuyerTin).HasMaxLength(SalesInvoiceConsts.MaxTinLength);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(SalesInvoiceConsts.MaxCurrencyCodeLength);
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.AmountPaid).HasColumnType("decimal(18,4)");
            b.Property(x => x.Notes).HasMaxLength(SalesInvoiceConsts.MaxNoteLength);
            b.Property(x => x.LhdnUuid).HasMaxLength(50);
            b.Property(x => x.LhdnLongId).HasMaxLength(100);
            b.Property(x => x.QrCodeUrl).HasMaxLength(512);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SalesInvoiceId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CustomerId, x.Status });
        });

        builder.Entity<SalesInvoiceItem>(b =>
        {
            b.ToTable("Sal_SalesInvoiceItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(SalesInvoiceItemConsts.MaxDescriptionLength);
            b.Property(x => x.Uom).IsRequired().HasMaxLength(SalesInvoiceItemConsts.MaxUomLength);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        // Delivery Notes
        builder.Entity<DeliveryNote>(b =>
        {
            b.ToTable("Sal_DeliveryNotes", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DeliveryNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(3);
            b.Property(x => x.ShippingAddress).HasMaxLength(500);
            b.Property(x => x.Transporter).HasMaxLength(200);
            b.Property(x => x.TrackingNumber).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).IsRequired();
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.DeliveryNoteId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DeliveryNumber }).IsUnique();
        });

        builder.Entity<DeliveryNoteItem>(b =>
        {
            b.ToTable("Sal_DeliveryNoteItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Uom).IsRequired().HasMaxLength(50);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        // Purchase Orders
        builder.Entity<PurchaseOrder>(b =>
        {
            b.ToTable("Pur_PurchaseOrders", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.OrderNumber).IsRequired().HasMaxLength(PurchaseOrderConsts.MaxOrderNumberLength);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(PurchaseOrderConsts.MaxCurrencyCodeLength);
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.Terms).HasMaxLength(PurchaseOrderConsts.MaxTermsLength);
            b.Property(x => x.Notes).HasMaxLength(PurchaseOrderConsts.MaxNoteLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PurchaseOrderId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.OrderNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SupplierId, x.Status });
        });

        builder.Entity<PurchaseOrderItem>(b =>
        {
            b.ToTable("Pur_PurchaseOrderItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Uom).IsRequired().HasMaxLength(20);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.ReceivedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.BilledQty).HasColumnType("decimal(18,4)");
        });

        // Purchase Invoices
        builder.Entity<PurchaseInvoice>(b =>
        {
            b.ToTable("Pur_PurchaseInvoices", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(PurchaseInvoiceConsts.MaxInvoiceNumberLength);
            b.Property(x => x.SupplierInvoiceNumber).HasMaxLength(PurchaseInvoiceConsts.MaxSupplierInvoiceNumberLength);
            b.Property(x => x.SupplierTin).HasMaxLength(PurchaseInvoiceConsts.MaxTinLength);
            b.Property(x => x.BuyerTin).HasMaxLength(PurchaseInvoiceConsts.MaxTinLength);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(PurchaseInvoiceConsts.MaxCurrencyCodeLength);
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.AmountPaid).HasColumnType("decimal(18,4)");
            b.Property(x => x.Notes).HasMaxLength(PurchaseInvoiceConsts.MaxNoteLength);
            b.Property(x => x.LhdnUuid).HasMaxLength(50);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PurchaseInvoiceId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceNumber }).IsUnique();
        });

        builder.Entity<PurchaseInvoiceItem>(b =>
        {
            b.ToTable("Pur_PurchaseInvoiceItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(PurchaseInvoiceItemConsts.MaxDescriptionLength);
            b.Property(x => x.Uom).IsRequired().HasMaxLength(PurchaseInvoiceItemConsts.MaxUomLength);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        // Purchase Receipts
        builder.Entity<PurchaseReceipt>(b =>
        {
            b.ToTable("Pur_PurchaseReceipts", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ReceiptNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(3);
            b.Property(x => x.SupplierDeliveryNote).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).IsRequired();
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PurchaseReceiptId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReceiptNumber }).IsUnique();
        });

        builder.Entity<PurchaseReceiptItem>(b =>
        {
            b.ToTable("Pur_PurchaseReceiptItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Uom).IsRequired().HasMaxLength(50);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        // Material Requests
        builder.Entity<MaterialRequest>(b =>
        {
            b.ToTable("Pur_MaterialRequests", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.RequestNumber).IsRequired().HasMaxLength(MaterialRequestConsts.MaxRequestNumberLength);
            b.Property(x => x.Notes).HasMaxLength(MaterialRequestConsts.MaxNotesLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.MaterialRequestId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.RequestNumber }).IsUnique();
        });

        builder.Entity<MaterialRequestItem>(b =>
        {
            b.ToTable("Pur_MaterialRequestItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(128);
            b.Property(x => x.Uom).IsRequired().HasMaxLength(20);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.OrderedQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.ReceivedQuantity).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        // Subcontracting
        builder.Entity<SubcontractingOrder>(b =>
        {
            b.ToTable("Pur_SubcontractingOrders", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.OrderNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(10);
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.PerReceived).HasColumnType("decimal(5,2)");
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SubcontractingOrderId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasMany(x => x.SuppliedItems).WithOne().HasForeignKey(x => x.SubcontractingOrderId).IsRequired();
            b.Navigation(x => x.SuppliedItems).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
        });

        builder.Entity<SubcontractingOrderItem>(b =>
        {
            b.ToTable("Pur_SubcontractingOrderItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
            b.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            b.Property(x => x.Rate).HasColumnType("decimal(18,4)");
            b.Property(x => x.ReceivedQty).HasColumnType("decimal(18,4)");
        });

        builder.Entity<SubcontractingOrderSuppliedItem>(b =>
        {
            b.ToTable("Pur_SubcontractingOrderSuppliedItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
            b.Property(x => x.RequiredQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.TransferredQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.ConsumedQty).HasColumnType("decimal(18,4)");
        });

        builder.Entity<SubcontractingReceipt>(b =>
        {
            b.ToTable("Pur_SubcontractingReceipts", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ReceiptNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SubcontractingReceiptId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.ReceiptNumber }).IsUnique();
        });

        builder.Entity<SubcontractingReceiptItem>(b =>
        {
            b.ToTable("Pur_SubcontractingReceiptItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
            b.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            b.Property(x => x.Rate).HasColumnType("decimal(18,4)");
        });

        // Payment Entry
        builder.Entity<PaymentEntry>(b =>
        {
            b.ToTable("Acc_PaymentEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PaymentNumber).HasMaxLength(PaymentEntryConsts.MaxPaymentNumberLength);
            b.Property(x => x.ModeOfPayment).HasMaxLength(PaymentEntryConsts.MaxModeOfPaymentLength);
            b.Property(x => x.PartyType).HasMaxLength(50);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(PaymentEntryConsts.MaxCurrencyCodeLength);
            b.Property(x => x.PaidAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.ReferenceNumber).HasMaxLength(PaymentEntryConsts.MaxReferenceNumberLength);
            b.Property(x => x.Notes).HasMaxLength(PaymentEntryConsts.MaxNoteLength);
            b.Property(x => x.AgainstInvoiceType).HasMaxLength(50);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.PaidFromAccountId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.PaidToAccountId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.References).WithOne().HasForeignKey(x => x.PaymentEntryId).IsRequired();
            b.Navigation(x => x.References).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PaymentNumber }).IsUnique().HasFilter("\"PaymentNumber\" IS NOT NULL");
        });

        builder.Entity<PaymentEntryReference>(b =>
        {
            b.ToTable("Acc_PaymentEntryReferences", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ReferenceType).IsRequired().HasMaxLength(50);
            b.Property(x => x.ReferenceNumber).HasMaxLength(100);
            b.Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.OutstandingAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.AllocatedAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.ExchangeRate).HasColumnType("decimal(18,6)");
            b.HasIndex(x => x.PaymentEntryId);
        });

        builder.Entity<BankTransaction>(b =>
        {
            b.ToTable("Acc_BankTransactions", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            b.Property(x => x.ReferenceNumber).HasMaxLength(100);
            b.Property(x => x.MatchedDocumentRef).HasMaxLength(100);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.BankAccountId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.BankAccountId, x.IsReconciled });
            b.HasIndex(x => new { x.TenantId, x.BankAccountId, x.TransactionDate });
        });

        builder.Entity<BankTransactionRule>(b =>
        {
            b.ToTable("Acc_BankTransactionRules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.RuleName).IsRequired().HasMaxLength(200);
            b.Property(x => x.PartyType).HasMaxLength(50);
            b.Property(x => x.MinAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.MaxAmount).HasColumnType("decimal(18,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Conditions).WithOne().HasForeignKey(x => x.BankTransactionRuleId).IsRequired();
            b.Navigation(x => x.Conditions).AutoInclude();
            b.HasMany(x => x.Accounts).WithOne().HasForeignKey(x => x.BankTransactionRuleId).IsRequired();
            b.Navigation(x => x.Accounts).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Priority });
        });

        builder.Entity<BankTransactionRuleCondition>(b =>
        {
            b.ToTable("Acc_BankTransactionRuleConditions", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Value).IsRequired().HasMaxLength(500);
        });

        builder.Entity<BankTransactionRuleAccount>(b =>
        {
            b.ToTable("Acc_BankTransactionRuleAccounts", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DebitFormula).HasMaxLength(200);
            b.Property(x => x.CreditFormula).HasMaxLength(200);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
        });

        builder.Entity<CurrencyExchange>(b =>
        {
            b.ToTable("Acc_CurrencyExchanges", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.FromCurrency).IsRequired().HasMaxLength(10);
            b.Property(x => x.ToCurrency).IsRequired().HasMaxLength(10);
            b.Property(x => x.ExchangeRate).HasColumnType("decimal(18,9)");
            b.Property(x => x.ForBuying).HasMaxLength(20);
            b.HasIndex(x => new { x.TenantId, x.FromCurrency, x.ToCurrency, x.Date });
        });

        builder.Entity<PaymentLedgerEntry>(b =>
        {
            b.ToTable("Acc_PaymentLedgerEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PartyType).IsRequired().HasMaxLength(50);
            b.Property(x => x.VoucherType).IsRequired().HasMaxLength(50);
            b.Property(x => x.AgainstVoucherType).IsRequired().HasMaxLength(50);
            b.Property(x => x.AccountCurrency).IsRequired().HasMaxLength(10);
            b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            b.Property(x => x.AmountInAccountCurrency).HasColumnType("decimal(18,4)");
            b.Property(x => x.Remarks).HasMaxLength(500);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.AgainstVoucherType, x.AgainstVoucherId, x.Delinked });
            b.HasIndex(x => new { x.TenantId, x.PartyType, x.PartyId, x.PostingDate });
            b.HasIndex(x => new { x.TenantId, x.VoucherType, x.VoucherId });
        });

        builder.Entity<CostCenter>(b =>
        {
            b.ToTable("Acc_CostCenters", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.CostCenterNumber).HasMaxLength(50);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Name }).IsUnique();
        });

        builder.Entity<AccountingPeriod>(b =>
        {
            b.ToTable("Acc_AccountingPeriods", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PeriodName).IsRequired().HasMaxLength(100);
            b.Property(x => x.ExemptedRole).HasMaxLength(100);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.StartDate, x.EndDate });
        });

        builder.Entity<ModeOfPayment>(b =>
        {
            b.ToTable("Acc_ModesOfPayment", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.Type).IsRequired().HasMaxLength(20);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<PaymentTermsTemplate>(b =>
        {
            b.ToTable("Acc_PaymentTermsTemplates", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.HasMany(x => x.Terms).WithOne().HasForeignKey(x => x.PaymentTermsTemplateId).IsRequired();
            b.Navigation(x => x.Terms).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<PaymentTerm>(b =>
        {
            b.ToTable("Acc_PaymentTerms", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.InvoicePortion).HasColumnType("decimal(18,4)");
            b.Property(x => x.Description).HasMaxLength(200);
        });

        builder.Entity<PaymentScheduleEntry>(b =>
        {
            b.ToTable("Acc_PaymentScheduleEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ParentType).IsRequired().HasMaxLength(50);
            b.Property(x => x.Description).HasMaxLength(200);
            b.Property(x => x.PaymentAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.PaidAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.InvoicePortion).HasColumnType("decimal(18,4)");
            b.HasIndex(x => new { x.ParentType, x.ParentId });
        });

        // Budget
        builder.Entity<Budget>(b =>
        {
            b.ToTable("Acc_Budgets", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.BudgetAgainst).IsRequired().HasMaxLength(50);
            b.Property(x => x.BudgetAgainstName).HasMaxLength(200);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<FiscalYear>().WithMany().HasForeignKey(x => x.FiscalYearId).IsRequired();
            b.HasMany(x => x.Accounts).WithOne().HasForeignKey(x => x.BudgetId).IsRequired();
            b.Navigation(x => x.Accounts).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.FiscalYearId, x.BudgetAgainst, x.BudgetAgainstId }).IsUnique();
        });

        builder.Entity<BudgetAccount>(b =>
        {
            b.ToTable("Acc_BudgetAccounts", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.AccountName).HasMaxLength(200);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
        });

        // E-Invoice Submissions
        builder.Entity<EInvoiceSubmission>(b =>
        {
            b.ToTable("EInv_Submissions", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.SubmissionUid).HasMaxLength(EInvoiceConsts.MaxSubmissionUidLength);
            b.Property(x => x.DocumentUuid).HasMaxLength(EInvoiceConsts.MaxUuidLength);
            b.Property(x => x.LongId).HasMaxLength(EInvoiceConsts.MaxLongIdLength);
            b.Property(x => x.SourceDocumentType).IsRequired().HasMaxLength(50);
            b.Property(x => x.DocumentTypeCode).IsRequired().HasMaxLength(EInvoiceConsts.MaxDocumentTypeLength);
            b.Property(x => x.Status).IsRequired().HasMaxLength(EInvoiceConsts.MaxStatusLength);
            b.Property(x => x.Reason).HasMaxLength(EInvoiceConsts.MaxReasonLength);
            b.Property(x => x.QrCodeUrl).HasMaxLength(EInvoiceConsts.MaxQrCodeUrlLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.DocumentUuid }).IsUnique().HasFilter("\"DocumentUuid\" IS NOT NULL");
            b.HasIndex(x => new { x.TenantId, x.SourceDocumentType, x.SourceDocumentId });
        });

        builder.Entity<LhdnSuccessLog>(b =>
        {
            b.ToTable("EInv_SuccessLogs", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DocumentUuid).IsRequired().HasMaxLength(EInvoiceConsts.MaxUuidLength);
            b.Property(x => x.LongId).HasMaxLength(EInvoiceConsts.MaxLongIdLength);
            b.Property(x => x.SourceDocumentType).IsRequired().HasMaxLength(50);
            b.Property(x => x.SourceDocumentNumber).HasMaxLength(64);
            b.Property(x => x.DocumentTypeCode).IsRequired().HasMaxLength(EInvoiceConsts.MaxDocumentTypeLength);
            b.Property(x => x.QrCodeUrl).HasMaxLength(EInvoiceConsts.MaxQrCodeUrlLength);
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,2)");
            b.HasOne<EInvoiceSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SubmittedAt });
            b.HasIndex(x => new { x.TenantId, x.DocumentUuid }).IsUnique();
        });

        // Stock Entries & Ledger
        builder.Entity<StockEntry>(b =>
        {
            b.ToTable("Inv_StockEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.EntryNumber).HasMaxLength(StockEntryConsts.MaxEntryNumberLength);
            b.Property(x => x.ReferenceType).HasMaxLength(StockEntryConsts.MaxReferenceNumberLength);
            b.Property(x => x.Notes).HasMaxLength(StockEntryConsts.MaxNoteLength);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.StockEntryId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.EntryNumber }).IsUnique().HasFilter("\"EntryNumber\" IS NOT NULL");
        });

        builder.Entity<StockEntryItem>(b =>
        {
            b.ToTable("Inv_StockEntryItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.ValuationRate).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        builder.Entity<StockLedgerEntry>(b =>
        {
            b.ToTable("Inv_StockLedgerEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.QuantityChange).HasColumnType("decimal(18,4)");
            b.Property(x => x.ValuationRate).HasColumnType("decimal(18,4)");
            b.Property(x => x.StockValue).HasColumnType("decimal(18,4)");
            b.Property(x => x.BalanceQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.BalanceValue).HasColumnType("decimal(18,4)");
            b.Property(x => x.VoucherType).HasMaxLength(50);
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).IsRequired();
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.ItemId, x.WarehouseId, x.PostingDate });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PostingDate });
            b.HasIndex(x => new { x.TenantId, x.VoucherType, x.VoucherId });
        });

        builder.Entity<Bin>(b =>
        {
            b.ToTable("Inv_Bins", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ActualQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.OrderedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.PlannedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.ReservedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.IndentedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.ReservedQtyForProduction).HasColumnType("decimal(18,4)");
            b.Property(x => x.ReservedQtyForSubContract).HasColumnType("decimal(18,4)");
            b.Property(x => x.ReservedQtyForProductionPlan).HasColumnType("decimal(18,4)");
            b.Property(x => x.StockValue).HasColumnType("decimal(18,4)");
            b.Property(x => x.ValuationRate).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.ItemId, x.WarehouseId }).IsUnique();
            // Optimistic concurrency via ConcurrencyStamp (prevents lost updates on concurrent bin modifications)
            b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(40);
        });

        builder.Entity<PriceList>(b =>
        {
            b.ToTable("Inv_PriceLists", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(PriceListConsts.MaxNameLength);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(PriceListConsts.MaxCurrencyCodeLength);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<ItemPrice>(b =>
        {
            b.ToTable("Inv_ItemPrices", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PriceListRate).HasColumnType("decimal(18,4)");
            b.Property(x => x.MinQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.Uom).IsRequired().HasMaxLength(ItemPriceConsts.MaxUomLength);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(ItemPriceConsts.MaxCurrencyCodeLength);
            b.Property(x => x.BatchNo).HasMaxLength(ItemPriceConsts.MaxBatchNoLength);
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).IsRequired();
            // Per DO-NOT: "Allow duplicate Item Prices for same composite key"
            b.HasIndex(x => new { x.TenantId, x.ItemId, x.PriceListId, x.Uom, x.ValidFrom, x.CustomerId, x.SupplierId, x.BatchNo }).IsUnique();
        });

        builder.Entity<Batch>(b =>
        {
            b.ToTable("Inv_Batches", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.BatchNo).IsRequired().HasMaxLength(BatchConsts.MaxBatchNoLength);
            b.Property(x => x.SupplierBatchNo).HasMaxLength(BatchConsts.MaxSupplierBatchNoLength);
            b.Property(x => x.Description).HasMaxLength(BatchConsts.MaxDescriptionLength);
            b.Property(x => x.ReferenceDocType).HasMaxLength(BatchConsts.MaxRefDocTypeLength);
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.ItemId, x.BatchNo }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ExpiryDate });
        });

        builder.Entity<UomConversion>(b =>
        {
            b.ToTable("Inv_UomConversions", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.FromUom).IsRequired().HasMaxLength(20);
            b.Property(x => x.ToUom).IsRequired().HasMaxLength(20);
            b.Property(x => x.ConversionFactor).HasColumnType("decimal(18,9)");
            b.HasIndex(x => new { x.TenantId, x.FromUom, x.ToUom, x.ItemId });
        });

        builder.Entity<ItemGroup>(b =>
        {
            b.ToTable("Inv_ItemGroups", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<ItemAttribute>(b =>
        {
            b.ToTable("Inv_ItemAttributes", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.AttributeName).IsRequired().HasMaxLength(200);
            b.Property(x => x.FromRange).HasColumnType("decimal(18,4)");
            b.Property(x => x.ToRange).HasColumnType("decimal(18,4)");
            b.Property(x => x.Increment).HasColumnType("decimal(18,4)");
            b.HasMany(x => x.Values).WithOne().HasForeignKey(x => x.ItemAttributeId).IsRequired();
            b.Navigation(x => x.Values).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.AttributeName }).IsUnique();
        });

        builder.Entity<ItemAttributeValue>(b =>
        {
            b.ToTable("Inv_ItemAttributeValues", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.AttributeValue).IsRequired().HasMaxLength(200);
            b.Property(x => x.Abbreviation).IsRequired().HasMaxLength(50);
        });

        builder.Entity<ItemVariantAttribute>(b =>
        {
            b.ToTable("Inv_ItemVariantAttributes", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.AttributeValue).IsRequired().HasMaxLength(200);
            b.HasOne<Item>().WithMany(x => x.VariantAttributes).HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<ItemAttribute>().WithMany().HasForeignKey(x => x.ItemAttributeId).IsRequired();
            b.HasIndex(x => new { x.ItemId, x.ItemAttributeId }).IsUnique();
        });

        builder.Entity<SerialNo>(b =>
        {
            b.ToTable("Inv_SerialNos", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.SerialNumber).IsRequired().HasMaxLength(100);
            b.Property(x => x.PurchaseDocumentType).HasMaxLength(50);
            b.Property(x => x.DeliveryDocumentType).HasMaxLength(50);
            b.Property(x => x.MaintenanceStatus).IsRequired().HasMaxLength(30);
            b.Property(x => x.PurchaseRate).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.SerialNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ItemId, x.WarehouseId, x.Status });
        });

        // Quality Inspection
        builder.Entity<QualityInspection>(b =>
        {
            b.ToTable("Inv_QualityInspections", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).HasMaxLength(200);
            b.Property(x => x.ReferenceType).HasMaxLength(50);
            b.Property(x => x.BatchNo).HasMaxLength(100);
            b.Property(x => x.Remarks).HasMaxLength(2000);
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Readings).WithOne().HasForeignKey(x => x.QualityInspectionId).IsRequired();
            b.Navigation(x => x.Readings).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.ReferenceType, x.ReferenceId });
        });

        builder.Entity<QualityInspectionReading>(b =>
        {
            b.ToTable("Inv_QualityInspectionReadings", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Specification).IsRequired().HasMaxLength(200);
            b.Property(x => x.ExpectedValue).HasMaxLength(200);
            b.Property(x => x.ReadingValue).HasMaxLength(200);
            b.Property(x => x.Formula).HasMaxLength(500);
            b.Property(x => x.MinValue).HasColumnType("decimal(18,6)");
            b.Property(x => x.MaxValue).HasColumnType("decimal(18,6)");
        });

        // Stock Reconciliation
        builder.Entity<StockReconciliation>(b =>
        {
            b.ToTable("Inv_StockReconciliations", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ReconciliationNumber).HasMaxLength(50);
            b.Property(x => x.Purpose).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.DifferenceAmount).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.StockReconciliationId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PostingDate });
        });

        builder.Entity<StockReconciliationItem>(b =>
        {
            b.ToTable("Inv_StockReconciliationItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.CurrentQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.CurrentValuationRate).HasColumnType("decimal(18,4)");
            b.Property(x => x.NewQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.NewValuationRate).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).IsRequired();
        });

        // Landed Cost Voucher
        builder.Entity<LandedCostVoucher>(b =>
        {
            b.ToTable("Inv_LandedCostVouchers", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.VoucherNumber).HasMaxLength(50);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.LandedCostVoucherId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasMany(x => x.Charges).WithOne().HasForeignKey(x => x.LandedCostVoucherId).IsRequired();
            b.Navigation(x => x.Charges).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.VoucherNumber }).IsUnique().HasFilter("\"VoucherNumber\" IS NOT NULL");
        });

        builder.Entity<LandedCostItem>(b =>
        {
            b.ToTable("Inv_LandedCostItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ReceiptType).IsRequired().HasMaxLength(50);
            b.Property(x => x.Description).HasMaxLength(200);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.ApplicableCharges).HasColumnType("decimal(18,2)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        builder.Entity<LandedCostCharge>(b =>
        {
            b.ToTable("Inv_LandedCostCharges", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(200);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.ExpenseAccountId).IsRequired();
        });

        // Human Resources
        builder.Entity<Employee>(b =>
        {
            b.ToTable("Hr_Employees", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.EmployeeId).IsRequired().HasMaxLength(EmployeeConsts.MaxEmployeeIdLength);
            b.Property(x => x.FirstName).IsRequired().HasMaxLength(EmployeeConsts.MaxNameLength);
            b.Property(x => x.LastName).HasMaxLength(EmployeeConsts.MaxNameLength);
            b.Property(x => x.IcNumber).HasMaxLength(EmployeeConsts.MaxIcNumberLength);
            b.Property(x => x.PassportNumber).HasMaxLength(EmployeeConsts.MaxPassportLength);
            b.Property(x => x.Phone).HasMaxLength(EmployeeConsts.MaxPhoneLength);
            b.Property(x => x.Email).HasMaxLength(EmployeeConsts.MaxEmailLength);
            b.Property(x => x.Address).HasMaxLength(EmployeeConsts.MaxAddressLength);
            b.Property(x => x.Designation).HasMaxLength(EmployeeConsts.MaxDesignationLength);
            b.Property(x => x.Department).HasMaxLength(EmployeeConsts.MaxDepartmentLength);
            b.Property(x => x.BankName).HasMaxLength(EmployeeConsts.MaxBankNameLength);
            b.Property(x => x.BankAccountNumber).HasMaxLength(EmployeeConsts.MaxBankAccountLength);
            b.Property(x => x.EpfNumber).HasMaxLength(EmployeeConsts.MaxEpfNumberLength);
            b.Property(x => x.SocsoNumber).HasMaxLength(EmployeeConsts.MaxSocsoNumberLength);
            b.Property(x => x.TaxNumber).HasMaxLength(EmployeeConsts.MaxTaxNumberLength);
            b.Property(x => x.BasicSalary).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.EmployeeId }).IsUnique();
        });

        builder.Entity<ContributionRule>(b =>
        {
            b.ToTable("Hr_ContributionRules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.EmployeeRate).HasColumnType("decimal(8,4)");
            b.Property(x => x.EmployerRate).HasColumnType("decimal(8,4)");
            b.Property(x => x.SalaryCeiling).HasColumnType("decimal(18,2)");
            b.Property(x => x.MinimumSalary).HasColumnType("decimal(18,2)");
            b.Property(x => x.MaximumSalary).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.TenantId, x.Type, x.EffectiveFrom });
        });

        builder.Entity<PayrollEntry>(b =>
        {
            b.ToTable("Hr_PayrollEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PayrollNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.CurrencyCode).HasMaxLength(10);
            b.Property(x => x.TotalGrossSalary).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalDeductions).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalNetSalary).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalEmployerContributions).HasColumnType("decimal(18,2)");
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PayrollEntryId).IsRequired();
            b.Navigation(x => x.Lines).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Year, x.Month }).IsUnique();
        });

        builder.Entity<PayrollEntryLine>(b =>
        {
            b.ToTable("Hr_PayrollEntryLines", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.EmployeeName).IsRequired().HasMaxLength(256);
            b.Property(x => x.GrossSalary).HasColumnType("decimal(18,2)");
            b.Property(x => x.EpfEmployee).HasColumnType("decimal(18,2)");
            b.Property(x => x.EpfEmployer).HasColumnType("decimal(18,2)");
            b.Property(x => x.SocsoEmployee).HasColumnType("decimal(18,2)");
            b.Property(x => x.SocsoEmployer).HasColumnType("decimal(18,2)");
            b.Property(x => x.EisEmployee).HasColumnType("decimal(18,2)");
            b.Property(x => x.EisEmployer).HasColumnType("decimal(18,2)");
            b.Property(x => x.Pcb).HasColumnType("decimal(18,2)");
        });

        builder.Entity<SalaryComponent>(b =>
        {
            b.ToTable("Hr_SalaryComponents", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Abbreviation).HasMaxLength(20);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<SalaryStructure>(b =>
        {
            b.ToTable("Hr_SalaryStructures", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.PayrollFrequency).IsRequired().HasMaxLength(20);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Details).WithOne().HasForeignKey(x => x.SalaryStructureId).IsRequired();
            b.Navigation(x => x.Details).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Name }).IsUnique();
        });

        builder.Entity<SalaryStructureDetail>(b =>
        {
            b.ToTable("Hr_SalaryStructureDetails", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ComponentName).IsRequired().HasMaxLength(200);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Formula).HasMaxLength(500);
            b.Property(x => x.Condition).HasMaxLength(500);
        });

        builder.Entity<LeaveType>(b =>
        {
            b.ToTable("Hr_LeaveTypes", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.MaxDaysAllowed).HasColumnType("decimal(5,1)");
            b.Property(x => x.MaxCarryForwardDays).HasColumnType("decimal(5,1)");
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<LeaveApplication>(b =>
        {
            b.ToTable("Hr_LeaveApplications", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.EmployeeName).HasMaxLength(200);
            b.Property(x => x.LeaveTypeName).HasMaxLength(100);
            b.Property(x => x.TotalLeaveDays).HasColumnType("decimal(5,1)");
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.FromDate, x.ToDate });
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        builder.Entity<LeaveAllocation>(b =>
        {
            b.ToTable("Hr_LeaveAllocations", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TotalLeavesAllocated).HasColumnType("decimal(5,1)");
            b.Property(x => x.CarryForwardDays).HasColumnType("decimal(5,1)");
            b.Property(x => x.LeavesUsed).HasColumnType("decimal(5,1)");
            b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.LeaveTypeId, x.FromDate, x.ToDate }).IsUnique();
        });

        builder.Entity<HolidayList>(b =>
        {
            b.ToTable("Hr_HolidayLists", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.WeeklyOff).HasMaxLength(100);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Holidays).WithOne().HasForeignKey(x => x.HolidayListId).IsRequired();
            b.Navigation(x => x.Holidays).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Year });
        });

        builder.Entity<Holiday>(b =>
        {
            b.ToTable("Hr_Holidays", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(200);
            b.HasIndex(x => new { x.HolidayListId, x.HolidayDate });
        });

        // Support
        builder.Entity<Issue>(b =>
        {
            b.ToTable("Sup_Issues", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Subject).IsRequired().HasMaxLength(500);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.Priority).IsRequired().HasMaxLength(20);
            b.Property(x => x.IssueType).HasMaxLength(100);
            b.Property(x => x.RaisedVia).HasMaxLength(50);
            b.Property(x => x.Resolution).HasMaxLength(4000);
            b.Property(x => x.FirstResponseTime).HasColumnType("decimal(18,2)");
            b.Property(x => x.ResolutionTime).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalHoldTime).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.Status });
        });

        // Workflow
        builder.Entity<ApprovalRule>(b =>
        {
            b.ToTable("Wf_ApprovalRules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DocumentType).IsRequired().HasMaxLength(ApprovalRuleConsts.MaxDocumentTypeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(ApprovalRuleConsts.MaxNameLength);
            b.Property(x => x.ConditionExpression).HasMaxLength(ApprovalRuleConsts.MaxConditionExpressionLength);
            b.Property(x => x.Description).HasMaxLength(ApprovalRuleConsts.MaxDescriptionLength);
            b.Property(x => x.MinimumAmount).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.TenantId, x.DocumentType, x.IsActive });
        });

        builder.Entity<ApprovalRequest>(b =>
        {
            b.ToTable("Wf_ApprovalRequests", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DocumentType).IsRequired().HasMaxLength(ApprovalRequestConsts.MaxDocumentTypeLength);
            b.Property(x => x.Remarks).HasMaxLength(ApprovalRequestConsts.MaxRemarksLength);
            b.HasOne<ApprovalRule>().WithMany().HasForeignKey(x => x.ApprovalRuleId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.DocumentType, x.DocumentId, x.Status });
            b.HasIndex(x => new { x.TenantId, x.Status, x.Level });
        });

        // Notifications
        builder.Entity<AppNotification>(b =>
        {
            b.ToTable("App_Notifications", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Subject).IsRequired().HasMaxLength(NotificationConsts.MaxSubjectLength);
            b.Property(x => x.Body).HasMaxLength(NotificationConsts.MaxBodyLength);
            b.HasIndex(x => new { x.TenantId, x.UserId, x.IsRead });
        });

        // Document Activity Log
        builder.Entity<DocumentActivityLog>(b =>
        {
            b.ToTable("App_ActivityLogs", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DocumentType).IsRequired().HasMaxLength(64);
            b.Property(x => x.ActivityType).IsRequired().HasMaxLength(64);
            b.Property(x => x.DocumentNumber).HasMaxLength(128);
            b.Property(x => x.PreviousStatus).HasMaxLength(64);
            b.Property(x => x.NewStatus).HasMaxLength(64);
            b.Property(x => x.Details).HasMaxLength(2000);
            b.HasIndex(x => new { x.TenantId, x.DocumentType, x.DocumentId });
            b.HasIndex(x => new { x.CompanyId, x.CreationTime });
        });

        // Import/Export
        builder.Entity<ImportJob>(b =>
        {
            b.ToTable("App_ImportJobs", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.FileName).IsRequired().HasMaxLength(ImportJobConsts.MaxFileNameLength);
            b.Property(x => x.EntityType).IsRequired().HasMaxLength(ImportJobConsts.MaxEntityTypeLength);
            b.Property(x => x.ErrorDetails).HasMaxLength(ImportJobConsts.MaxErrorMessageLength);
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        // Automation
        builder.Entity<AutomationRule>(b =>
        {
            b.ToTable("Auto_Rules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(AutomationRuleConsts.MaxNameLength);
            b.Property(x => x.Description).HasMaxLength(AutomationRuleConsts.MaxDescriptionLength);
            b.Property(x => x.DocumentType).HasMaxLength(AutomationRuleConsts.MaxDocumentTypeLength);
            b.Property(x => x.ConditionExpression).HasMaxLength(AutomationRuleConsts.MaxConditionExpressionLength);
            b.Property(x => x.ActionConfig).HasMaxLength(AutomationRuleConsts.MaxActionConfigLength);
            b.HasIndex(x => new { x.TenantId, x.Trigger, x.IsActive });
        });

        builder.Entity<AutomationExecutionLog>(b =>
        {
            b.ToTable("Auto_ExecutionLogs", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.SourceDocumentType).HasMaxLength(AutomationRuleConsts.MaxDocumentTypeLength);
            b.Property(x => x.ErrorMessage).HasMaxLength(1024);
            b.HasOne<AutomationRule>().WithMany().HasForeignKey(x => x.AutomationRuleId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.AutomationRuleId, x.CreationTime });
        });

        // CRM
        builder.Entity<Lead>(b =>
        {
            b.ToTable("CRM_Leads", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.LeadNumber).IsRequired().HasMaxLength(LeadConsts.MaxLeadNumberLength);
            b.Property(x => x.FirstName).IsRequired().HasMaxLength(LeadConsts.MaxFirstNameLength);
            b.Property(x => x.LastName).HasMaxLength(LeadConsts.MaxLastNameLength);
            b.Property(x => x.CompanyName).HasMaxLength(LeadConsts.MaxCompanyNameLength);
            b.Property(x => x.Email).HasMaxLength(LeadConsts.MaxEmailLength);
            b.Property(x => x.Phone).HasMaxLength(LeadConsts.MaxPhoneLength);
            b.Property(x => x.MobileNo).HasMaxLength(LeadConsts.MaxPhoneLength);
            b.Property(x => x.JobTitle).HasMaxLength(LeadConsts.MaxJobTitleLength);
            b.Property(x => x.Website).HasMaxLength(LeadConsts.MaxWebsiteLength);
            b.Property(x => x.City).HasMaxLength(LeadConsts.MaxCityLength);
            b.Property(x => x.State).HasMaxLength(LeadConsts.MaxStateLength);
            b.Property(x => x.Country).HasMaxLength(LeadConsts.MaxCountryLength);
            b.Property(x => x.Industry).HasMaxLength(LeadConsts.MaxIndustryLength);
            b.Property(x => x.Notes).HasMaxLength(LeadConsts.MaxNoteLength);
            b.Property(x => x.AnnualRevenue).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.TenantId, x.LeadNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.HasIndex(x => new { x.TenantId, x.Email });
        });

        builder.Entity<Opportunity>(b =>
        {
            b.ToTable("CRM_Opportunities", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.OpportunityNumber).IsRequired().HasMaxLength(OpportunityConsts.MaxOpportunityNumberLength);
            b.Property(x => x.Title).IsRequired().HasMaxLength(OpportunityConsts.MaxTitleLength);
            b.Property(x => x.SalesStage).HasMaxLength(OpportunityConsts.MaxSalesStageLength);
            b.Property(x => x.CurrencyCode).HasMaxLength(OpportunityConsts.MaxCurrencyCodeLength);
            b.Property(x => x.LostReason).HasMaxLength(OpportunityConsts.MaxLostReasonLength);
            b.Property(x => x.Notes).HasMaxLength(OpportunityConsts.MaxNoteLength);
            b.Property(x => x.ContactName).HasMaxLength(200);
            b.Property(x => x.ContactEmail).HasMaxLength(256);
            b.Property(x => x.ContactPhone).HasMaxLength(30);
            b.Property(x => x.Territory).HasMaxLength(100);
            b.Property(x => x.OpportunityAmount).HasColumnType("decimal(18,2)");
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OpportunityId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.OpportunityNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        builder.Entity<OpportunityItem>(b =>
        {
            b.ToTable("CRM_OpportunityItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(200);
            b.Property(x => x.Uom).HasMaxLength(20);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });

        // Projects
        builder.Entity<Project>(b =>
        {
            b.ToTable("Prj_Projects", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ProjectNumber).IsRequired().HasMaxLength(ProjectConsts.MaxProjectNumberLength);
            b.Property(x => x.ProjectName).IsRequired().HasMaxLength(ProjectConsts.MaxProjectNameLength);
            b.Property(x => x.Notes).HasMaxLength(ProjectConsts.MaxNoteLength);
            b.Property(x => x.CostCenter).HasMaxLength(ProjectConsts.MaxCostCenterLength);
            b.Property(x => x.EstimatedCost).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalCostingAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalBillingAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalBilledAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.PercentComplete).HasColumnType("decimal(5,1)");
            b.HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.ProjectId).IsRequired();
            b.Navigation(x => x.Tasks).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.ProjectNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        builder.Entity<ProjectTask>(b =>
        {
            b.ToTable("Prj_Tasks", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TaskNumber).IsRequired().HasMaxLength(ProjectTaskConsts.MaxTaskNumberLength);
            b.Property(x => x.Subject).IsRequired().HasMaxLength(ProjectTaskConsts.MaxSubjectLength);
            b.Property(x => x.Description).HasMaxLength(ProjectTaskConsts.MaxDescriptionLength);
            b.Property(x => x.TaskWeight).HasColumnType("decimal(5,2)");
            b.Property(x => x.Progress).HasColumnType("decimal(5,1)");
            b.Property(x => x.ExpectedHours).HasColumnType("decimal(8,2)");
            b.Property(x => x.ActualHours).HasColumnType("decimal(8,2)");
            b.HasMany(x => x.Dependencies).WithOne().HasForeignKey(x => x.TaskId).IsRequired();
            b.Navigation(x => x.Dependencies).AutoInclude();
            b.HasIndex(x => new { x.ProjectId, x.Status });
        });

        builder.Entity<TaskDependency>(b =>
        {
            b.ToTable("Prj_TaskDependencies", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.HasIndex(x => new { x.TaskId, x.DependsOnTaskId }).IsUnique();
        });

        builder.Entity<Timesheet>(b =>
        {
            b.ToTable("Prj_Timesheets", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.EmployeeName).HasMaxLength(200);
            b.Property(x => x.TotalHours).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalBillableHours).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalBillingAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalCostingAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Note).HasMaxLength(2000);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Details).WithOne().HasForeignKey(x => x.TimesheetId).IsRequired();
            b.Navigation(x => x.Details).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.StartDate });
        });

        builder.Entity<TimesheetDetail>(b =>
        {
            b.ToTable("Prj_TimesheetDetails", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ActivityType).IsRequired().HasMaxLength(100);
            b.Property(x => x.Hours).HasColumnType("decimal(18,2)");
            b.Property(x => x.BillingRate).HasColumnType("decimal(18,2)");
            b.Property(x => x.CostingRate).HasColumnType("decimal(18,2)");
            b.Property(x => x.Description).HasMaxLength(500);
        });

        builder.Entity<ActivityType>(b =>
        {
            b.ToTable("Prj_ActivityTypes", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.DefaultBillingRate).HasColumnType("decimal(18,2)");
            b.Property(x => x.DefaultCostingRate).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<ActivityCost>(b =>
        {
            b.ToTable("Prj_ActivityCosts", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.BillingRate).HasColumnType("decimal(18,2)");
            b.Property(x => x.CostingRate).HasColumnType("decimal(18,2)");
            b.HasOne<ActivityType>().WithMany().HasForeignKey(x => x.ActivityTypeId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.ActivityTypeId }).IsUnique();
        });

        // Assets
        builder.Entity<AssetCategory>(b =>
        {
            b.ToTable("Ast_Categories", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.CategoryName).IsRequired().HasMaxLength(AssetCategoryConsts.MaxCategoryNameLength);
            b.Property(x => x.DefaultDepreciationRate).HasColumnType("decimal(5,2)");
            b.HasIndex(x => new { x.TenantId, x.CategoryName }).IsUnique();
        });

        builder.Entity<Asset>(b =>
        {
            b.ToTable("Ast_Assets", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.AssetNumber).IsRequired().HasMaxLength(AssetConsts.MaxAssetNumberLength);
            b.Property(x => x.AssetName).IsRequired().HasMaxLength(AssetConsts.MaxAssetNameLength);
            b.Property(x => x.Location).HasMaxLength(AssetConsts.MaxLocationLength);
            b.Property(x => x.Notes).HasMaxLength(AssetConsts.MaxNoteLength);
            b.Property(x => x.PurchaseAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.AdditionalCost).HasColumnType("decimal(18,2)");
            b.Property(x => x.OpeningAccumulatedDepreciation).HasColumnType("decimal(18,2)");
            b.Property(x => x.ValueAfterDepreciation).HasColumnType("decimal(18,2)");
            b.Property(x => x.DisposalAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.DepreciationRate).HasColumnType("decimal(5,2)");
            b.HasMany(x => x.DepreciationSchedule).WithOne().HasForeignKey(x => x.AssetId).IsRequired();
            b.Navigation(x => x.DepreciationSchedule).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.AssetNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        builder.Entity<DepreciationScheduleEntry>(b =>
        {
            b.ToTable("Ast_DepreciationSchedule", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DepreciationAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.AccumulatedDepreciation).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.AssetId, x.ScheduleDate });
        });

        builder.Entity<MaintenanceSchedule>(b =>
        {
            b.ToTable("Ast_MaintenanceSchedules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Periodicity).IsRequired().HasMaxLength(30);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Details).WithOne().HasForeignKey(x => x.MaintenanceScheduleId).IsRequired();
            b.Navigation(x => x.Details).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        builder.Entity<MaintenanceScheduleDetail>(b =>
        {
            b.ToTable("Ast_MaintenanceScheduleDetails", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Notes).HasMaxLength(500);
        });

        builder.Entity<MaintenanceVisit>(b =>
        {
            b.ToTable("Ast_MaintenanceVisits", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.MaintenanceType).IsRequired().HasMaxLength(30);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Purposes).WithOne().HasForeignKey(x => x.MaintenanceVisitId).IsRequired();
            b.Navigation(x => x.Purposes).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompletionStatus });
        });

        builder.Entity<MaintenanceVisitPurpose>(b =>
        {
            b.ToTable("Ast_MaintenanceVisitPurposes", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).HasMaxLength(200);
            b.Property(x => x.WorkDone).IsRequired().HasMaxLength(200);
            b.Property(x => x.WorkDetails).HasMaxLength(2000);
        });

        // Manufacturing
        builder.Entity<BillOfMaterials>(b =>
        {
            b.ToTable("Mfg_BOM", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.BomNumber).IsRequired().HasMaxLength(BomConsts.MaxBomNumberLength);
            b.Property(x => x.Uom).HasMaxLength(20);
            b.Property(x => x.Notes).HasMaxLength(BomConsts.MaxNoteLength);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.TotalMaterialCost).HasColumnType("decimal(18,2)");
            b.Property(x => x.OperatingCost).HasColumnType("decimal(18,2)");
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.BomId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.BomNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ItemId, x.IsDefault });
        });

        builder.Entity<BomItem>(b =>
        {
            b.ToTable("Mfg_BOMItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
            b.Property(x => x.Uom).HasMaxLength(20);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.Rate).HasColumnType("decimal(18,2)");
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.IsPhantom).HasDefaultValue(false);
        });

        builder.Entity<WorkOrder>(b =>
        {
            b.ToTable("Mfg_WorkOrders", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.WorkOrderNumber).IsRequired().HasMaxLength(WorkOrderConsts.MaxWorkOrderNumberLength);
            b.Property(x => x.Notes).HasMaxLength(WorkOrderConsts.MaxNoteLength);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.ProducedQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.MaterialTransferred).HasColumnType("decimal(18,4)");
            b.HasMany(x => x.RequiredItems).WithOne().HasForeignKey(x => x.WorkOrderId).IsRequired();
            b.Navigation(x => x.RequiredItems).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.WorkOrderNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        builder.Entity<WorkOrderItem>(b =>
        {
            b.ToTable("Mfg_WorkOrderItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
            b.Property(x => x.RequiredQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.TransferredQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.ConsumedQuantity).HasColumnType("decimal(18,4)");
        });

        builder.Entity<ProductionPlan>(b =>
        {
            b.ToTable("Mfg_ProductionPlans", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PlanNumber).IsRequired().HasMaxLength(ProductionPlanConsts.MaxPlanNumberLength);
            b.Property(x => x.Notes).HasMaxLength(ProductionPlanConsts.MaxNoteLength);
            b.HasMany(x => x.PlannedItems).WithOne().HasForeignKey(x => x.ProductionPlanId).IsRequired();
            b.Navigation(x => x.PlannedItems).AutoInclude();
            b.HasMany(x => x.MaterialRequirements).WithOne().HasForeignKey(x => x.ProductionPlanId).IsRequired();
            b.Navigation(x => x.MaterialRequirements).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.PlanNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        builder.Entity<ProductionPlanItem>(b =>
        {
            b.ToTable("Mfg_ProductionPlanItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(ProductionPlanConsts.MaxItemNameLength);
            b.Property(x => x.PlannedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.ProducedQty).HasColumnType("decimal(18,4)");
        });

        builder.Entity<ProductionPlanMrItem>(b =>
        {
            b.ToTable("Mfg_ProductionPlanMrItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(ProductionPlanConsts.MaxItemNameLength);
            b.Property(x => x.RequiredQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.OrderedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.AvailableQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.PlannedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.MinOrderQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.SafetyStock).HasColumnType("decimal(18,4)");
            b.Property(x => x.Uom).HasMaxLength(ProductionPlanConsts.MaxUomLength);
        });

        // Workstation
        builder.Entity<Workstation>(b =>
        {
            b.ToTable("Mfg_Workstations", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.WorkstationType).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.HourRate).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Costs).WithOne().HasForeignKey(x => x.WorkstationId).IsRequired();
            b.Navigation(x => x.Costs).AutoInclude();
            b.HasMany(x => x.WorkingHours).WithOne().HasForeignKey(x => x.WorkstationId).IsRequired();
            b.Navigation(x => x.WorkingHours).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Name }).IsUnique();
        });

        builder.Entity<WorkstationCost>(b =>
        {
            b.ToTable("Mfg_WorkstationCosts", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.OperatingComponent).IsRequired().HasMaxLength(100);
            b.Property(x => x.OperatingCost).HasColumnType("decimal(18,2)");
        });

        builder.Entity<WorkstationWorkingHour>(b =>
        {
            b.ToTable("Mfg_WorkstationWorkingHours", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Day).IsRequired().HasMaxLength(20);
        });

        // Operation
        builder.Entity<Operation>(b =>
        {
            b.ToTable("Mfg_Operations", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.WorkstationType).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        // Routing
        builder.Entity<Routing>(b =>
        {
            b.ToTable("Mfg_Routings", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.HasMany(x => x.Operations).WithOne().HasForeignKey(x => x.RoutingId).IsRequired();
            b.Navigation(x => x.Operations).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<RoutingOperation>(b =>
        {
            b.ToTable("Mfg_RoutingOperations", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.TimeInMins).HasColumnType("decimal(18,2)");
            b.Property(x => x.OperatingCost).HasColumnType("decimal(18,2)");
            b.Property(x => x.HourRate).HasColumnType("decimal(18,2)");
            b.HasOne<Operation>().WithMany().HasForeignKey(x => x.OperationId).IsRequired();
        });

        // Pricing Rule
        builder.Entity<PricingRule>(b =>
        {
            b.ToTable("Sal_PricingRules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.ApplicableFor).IsRequired().HasMaxLength(20);
            b.Property(x => x.ApplyOnName).HasMaxLength(200);
            b.Property(x => x.PartyType).HasMaxLength(50);
            b.Property(x => x.DiscountPercentage).HasColumnType("decimal(18,4)");
            b.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Rate).HasColumnType("decimal(18,2)");
            b.Property(x => x.FreeItemQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.MinQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.MaxQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.MinAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.MaxAmount).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.TenantId, x.ApplyOn, x.ApplyOnId, x.Priority });
        });

        builder.Entity<ShippingRule>(b =>
        {
            b.ToTable("Sal_ShippingRules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Label).IsRequired().HasMaxLength(200);
            b.Property(x => x.FixedAmount).HasColumnType("decimal(18,2)");
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
            b.HasMany(x => x.Conditions).WithOne().HasForeignKey(x => x.ShippingRuleId).IsRequired();
            b.Navigation(x => x.Conditions).AutoInclude();
            b.HasMany(x => x.Countries).WithOne().HasForeignKey(x => x.ShippingRuleId).IsRequired();
            b.Navigation(x => x.Countries).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.RuleType, x.IsEnabled });
        });

        builder.Entity<ShippingRuleCondition>(b =>
        {
            b.ToTable("Sal_ShippingRuleConditions", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.FromValue).HasColumnType("decimal(18,4)");
            b.Property(x => x.ToValue).HasColumnType("decimal(18,4)");
            b.Property(x => x.ShippingAmount).HasColumnType("decimal(18,2)");
        });

        builder.Entity<ShippingRuleCountry>(b =>
        {
            b.ToTable("Sal_ShippingRuleCountries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.CountryCode).IsRequired().HasMaxLength(10);
        });

        builder.Entity<SalesPerson>(b =>
        {
            b.ToTable("Sal_SalesPersons", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.CommissionRate).HasColumnType("decimal(5,2)");
            b.HasMany(x => x.Targets).WithOne().HasForeignKey(x => x.SalesPersonId).IsRequired();
            b.Navigation(x => x.Targets).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique().HasFilter(null);
            b.HasIndex(x => new { x.TenantId, x.ParentSalesPersonId });
        });

        builder.Entity<SalesPersonTarget>(b =>
        {
            b.ToTable("Sal_SalesPersonTargets", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TargetQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.TargetAmount).HasColumnType("decimal(18,2)");
        });

        builder.Entity<SalesTeamEntry>(b =>
        {
            b.ToTable("Sal_SalesTeamEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ParentType).IsRequired().HasMaxLength(50);
            b.Property(x => x.AllocatedPercentage).HasColumnType("decimal(5,2)");
            b.Property(x => x.AllocatedAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.CommissionRate).HasColumnType("decimal(5,2)");
            b.Property(x => x.Incentives).HasColumnType("decimal(18,2)");
            b.HasOne<SalesPerson>().WithMany().HasForeignKey(x => x.SalesPersonId).IsRequired();
            b.HasIndex(x => new { x.ParentType, x.ParentId });
        });

        // Stock Reservation Entry
        builder.Entity<StockReservationEntry>(b =>
        {
            b.ToTable("Inv_StockReservationEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.VoucherType).IsRequired().HasMaxLength(50);
            b.Property(x => x.ReservedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.DeliveredQty).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).IsRequired();
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.VoucherType, x.VoucherId });
            b.HasIndex(x => new { x.TenantId, x.ItemId, x.WarehouseId, x.Status });
        });

        // Job Card
        builder.Entity<JobCard>(b =>
        {
            b.ToTable("Mfg_JobCards", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.WorkstationType).HasMaxLength(100);
            b.Property(x => x.ForQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.CompletedQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.ProcessLossQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.TotalTimeInMins).HasColumnType("decimal(18,2)");
            b.Property(x => x.PlannedTimeInMins).HasColumnType("decimal(18,2)");
            b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).IsRequired();
            b.HasOne<Operation>().WithMany().HasForeignKey(x => x.OperationId).IsRequired();
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.TimeLogs).WithOne().HasForeignKey(x => x.JobCardId).IsRequired();
            b.Navigation(x => x.TimeLogs).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.OperationId });
        });

        builder.Entity<JobCardTimeLog>(b =>
        {
            b.ToTable("Mfg_JobCardTimeLogs", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TimeInMins).HasColumnType("decimal(18,2)");
            b.Property(x => x.CompletedQty).HasColumnType("decimal(18,4)");
        });

        builder.Entity<ManufacturingSettings>(b =>
        {
            b.ToTable("Mfg_Settings", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.OverproductionPercentage).HasColumnType("decimal(5,2)");
            b.Property(x => x.TransferExtraMaterialsPercentage).HasColumnType("decimal(5,2)");
            b.Property(x => x.BackflushRawMaterialsBasedOn).IsRequired().HasMaxLength(50);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId }).IsUnique();
        });

        // Item Tax Template
        builder.Entity<ItemTaxTemplate>(b =>
        {
            b.ToTable("Tax_ItemTaxTemplates", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Details).WithOne().HasForeignKey(x => x.ItemTaxTemplateId).IsRequired();
            b.Navigation(x => x.Details).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Title }).IsUnique();
        });

        builder.Entity<ItemTaxTemplateDetail>(b =>
        {
            b.ToTable("Tax_ItemTaxTemplateDetails", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TaxRate).HasColumnType("decimal(18,4)");
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.TaxAccountId).IsRequired();
        });

        // Tax Withholding Entry
        builder.Entity<TaxWithholdingEntry>(b =>
        {
            b.ToTable("Tax_WithholdingEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PartyType).IsRequired().HasMaxLength(30);
            b.Property(x => x.VoucherType).IsRequired().HasMaxLength(50);
            b.Property(x => x.TaxCategory).HasMaxLength(100);
            b.Property(x => x.CertificateNumber).HasMaxLength(100);
            b.Property(x => x.WithholdingRate).HasColumnType("decimal(18,4)");
            b.Property(x => x.TaxableAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.WithheldAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.LdcRate).HasColumnType("decimal(18,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.PartyId, x.PostingDate });
            b.HasIndex(x => new { x.TenantId, x.VoucherType, x.VoucherId });
        });

        // Blanket Order
        builder.Entity<BlanketOrder>(b =>
        {
            b.ToTable("Sal_BlanketOrders", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.OrderNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.OrderType).IsRequired().HasMaxLength(20);
            b.Property(x => x.PartyName).HasMaxLength(200);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.BlanketOrderId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.OrderNumber }).IsUnique();
        });

        builder.Entity<BlanketOrderItem>(b =>
        {
            b.ToTable("Sal_BlanketOrderItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).HasMaxLength(200);
            b.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            b.Property(x => x.Rate).HasColumnType("decimal(18,2)");
            b.Property(x => x.OrderedQty).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        // Period Closing Voucher
        builder.Entity<PeriodClosingVoucher>(b =>
        {
            b.ToTable("Acc_PeriodClosingVouchers", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.VoucherNumber).HasMaxLength(50);
            b.Property(x => x.Remarks).HasMaxLength(2000);
            b.Property(x => x.TotalClosingAmount).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<FiscalYear>().WithMany().HasForeignKey(x => x.FiscalYearId).IsRequired();
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.ClosingAccountId).IsRequired();
            b.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.PeriodClosingVoucherId).IsRequired();
            b.Navigation(x => x.Entries).AutoInclude();
        });

        builder.Entity<PeriodClosingEntry>(b =>
        {
            b.ToTable("Acc_PeriodClosingEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
        });

        builder.Entity<ExchangeRateRevaluation>(b =>
        {
            b.ToTable("Acc_ExchangeRateRevaluations", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TotalGainLoss).HasColumnType("decimal(18,4)");
            b.Property(x => x.RoundingLossAllowance).HasColumnType("decimal(5,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.ExchangeGainLossAccountId).IsRequired();
            b.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.ExchangeRateRevaluationId).IsRequired();
            b.Navigation(x => x.Entries).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PostingDate });
        });

        builder.Entity<ExchangeRateRevaluationEntry>(b =>
        {
            b.ToTable("Acc_ExchangeRateRevaluationEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.AccountCurrency).IsRequired().HasMaxLength(10);
            b.Property(x => x.BalanceInAccountCurrency).HasColumnType("decimal(18,4)");
            b.Property(x => x.CurrentBalanceInCompanyCurrency).HasColumnType("decimal(18,4)");
            b.Property(x => x.NewExchangeRate).HasColumnType("decimal(18,9)");
            b.Property(x => x.NewBalanceInCompanyCurrency).HasColumnType("decimal(18,4)");
            b.Property(x => x.GainLoss).HasColumnType("decimal(18,4)");
            b.Property(x => x.PartyType).HasMaxLength(50);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
        });

        // Supplier Quotation
        builder.Entity<SupplierQuotation>(b =>
        {
            b.ToTable("Pur_SupplierQuotations", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.QuotationNumber).HasMaxLength(50);
            b.Property(x => x.SupplierName).HasMaxLength(200);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(10);
            b.Property(x => x.ExchangeRate).HasColumnType("decimal(18,6)");
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,2)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,2)");
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SupplierQuotationId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.QuotationNumber }).IsUnique().HasFilter("\"QuotationNumber\" IS NOT NULL");
        });

        builder.Entity<SupplierQuotationItem>(b =>
        {
            b.ToTable("Pur_SupplierQuotationItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).HasMaxLength(200);
            b.Property(x => x.Uom).HasMaxLength(50);
            b.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            b.Property(x => x.Rate).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        builder.Entity<SupplierScorecard>(b =>
        {
            b.ToTable("Pur_SupplierScorecards", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.WeightingFunction).HasMaxLength(500);
            b.Property(x => x.Score).HasColumnType("decimal(18,4)");
            b.Property(x => x.CurrentStanding).HasMaxLength(100);
            b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).IsRequired();
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Standings).WithOne().HasForeignKey(x => x.SupplierScorecardId).IsRequired();
            b.Navigation(x => x.Standings).AutoInclude();
            b.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.SupplierScorecardId).IsRequired();
            b.Navigation(x => x.Criteria).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.SupplierId }).IsUnique();
        });

        builder.Entity<ScorecardStanding>(b =>
        {
            b.ToTable("Pur_ScorecardStandings", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.MinGrade).HasColumnType("decimal(5,2)");
            b.Property(x => x.MaxGrade).HasColumnType("decimal(5,2)");
        });

        builder.Entity<ScorecardCriterion>(b =>
        {
            b.ToTable("Pur_ScorecardCriteria", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Weight).HasColumnType("decimal(5,2)");
            b.Property(x => x.MaxScore).HasColumnType("decimal(5,2)");
            b.Property(x => x.Formula).HasMaxLength(2000);
        });

        builder.Entity<ScorecardPeriod>(b =>
        {
            b.ToTable("Pur_ScorecardPeriods", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TotalScore).HasColumnType("decimal(18,4)");
            b.HasOne<SupplierScorecard>().WithMany().HasForeignKey(x => x.SupplierScorecardId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.SupplierId, x.StartDate, x.EndDate });
        });

        // Request for Quotation
        builder.Entity<RequestForQuotation>(b =>
        {
            b.ToTable("Pur_RequestForQuotations", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.RfqNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(10);
            b.Property(x => x.MessageForSupplier).HasMaxLength(2000);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.RequestForQuotationId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasMany(x => x.Suppliers).WithOne().HasForeignKey(x => x.RequestForQuotationId).IsRequired();
            b.Navigation(x => x.Suppliers).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.RfqNumber }).IsUnique();
        });

        builder.Entity<RfqItem>(b =>
        {
            b.ToTable("Pur_RfqItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            b.Property(x => x.Uom).HasMaxLength(50);
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        builder.Entity<RfqSupplier>(b =>
        {
            b.ToTable("Pur_RfqSuppliers", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.SupplierName).IsRequired().HasMaxLength(200);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.QuoteStatus).HasMaxLength(30);
            b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).IsRequired();
        });

        // Subscription
        builder.Entity<Subscription>(b =>
        {
            b.ToTable("Sal_Subscriptions", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.SubscriptionNumber).HasMaxLength(50);
            b.Property(x => x.PartyType).IsRequired().HasMaxLength(50);
            b.Property(x => x.PartyName).HasMaxLength(200);
            b.Property(x => x.GenerateDocumentType).IsRequired().HasMaxLength(50);
            b.Property(x => x.BillingInterval).IsRequired().HasMaxLength(30);
            b.Property(x => x.TotalPerInterval).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Plans).WithOne().HasForeignKey(x => x.SubscriptionId).IsRequired();
            b.Navigation(x => x.Plans).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SubscriptionNumber }).IsUnique().HasFilter("\"SubscriptionNumber\" IS NOT NULL");
        });

        builder.Entity<SubscriptionPlan>(b =>
        {
            b.ToTable("Sal_SubscriptionPlans", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).HasMaxLength(200);
            b.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            b.Property(x => x.Rate).HasColumnType("decimal(18,2)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
        });

        // Dunning
        builder.Entity<Dunning>(b =>
        {
            b.ToTable("Sal_Dunnings", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.CustomerName).HasMaxLength(200);
            b.Property(x => x.TotalOutstanding).HasColumnType("decimal(18,2)");
            b.Property(x => x.DunningFee).HasColumnType("decimal(18,2)");
            b.Property(x => x.InterestAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).IsRequired();
            b.HasMany(x => x.OverduePayments).WithOne().HasForeignKey(x => x.DunningId).IsRequired();
            b.Navigation(x => x.OverduePayments).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.DunningLevel });
        });

        builder.Entity<DunningOverduePayment>(b =>
        {
            b.ToTable("Sal_DunningOverduePayments", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.OutstandingAmount).HasColumnType("decimal(18,2)");
            b.HasOne<SalesInvoice>().WithMany().HasForeignKey(x => x.SalesInvoiceId).IsRequired();
        });

        // Pick List
        builder.Entity<PickList>(b =>
        {
            b.ToTable("Inv_PickLists", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PickListNumber).HasMaxLength(50);
            b.Property(x => x.Purpose).IsRequired().HasMaxLength(50);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PickListId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Status });
        });

        builder.Entity<PickListItem>(b =>
        {
            b.ToTable("Inv_PickListItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).HasMaxLength(200);
            b.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            b.Property(x => x.StockQty).HasColumnType("decimal(18,4)");
            b.Property(x => x.TransferredQty).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).IsRequired();
        });

        builder.Entity<StockClosingEntry>(b =>
        {
            b.ToTable("Inv_StockClosingEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TotalStockValue).HasColumnType("decimal(18,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Balances).WithOne().HasForeignKey(x => x.StockClosingEntryId).IsRequired();
            b.Navigation(x => x.Balances).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ToDate, x.Status });
        });

        builder.Entity<StockClosingBalance>(b =>
        {
            b.ToTable("Inv_StockClosingBalances", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            b.Property(x => x.StockValue).HasColumnType("decimal(18,4)");
            b.Property(x => x.ValuationRate).HasColumnType("decimal(18,6)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).IsRequired();
            b.HasIndex(x => new { x.StockClosingEntryId, x.ItemId, x.WarehouseId }).IsUnique();
        });

        // Asset Movement
        builder.Entity<AssetMovement>(b =>
        {
            b.ToTable("Ast_AssetMovements", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.MovementType).IsRequired().HasMaxLength(30);
            b.Property(x => x.SourceLocation).HasMaxLength(200);
            b.Property(x => x.TargetLocation).HasMaxLength(200);
            b.Property(x => x.Purpose).HasMaxLength(500);
            b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired();
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.AssetId, x.MovementDate });
        });

        // Payment Request
        builder.Entity<PaymentRequest>(b =>
        {
            b.ToTable("Acc_PaymentRequests", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PaymentRequestType).IsRequired().HasMaxLength(20);
            b.Property(x => x.ReferenceDoctype).IsRequired().HasMaxLength(50);
            b.Property(x => x.ReferenceNumber).HasMaxLength(100);
            b.Property(x => x.PartyType).IsRequired().HasMaxLength(50);
            b.Property(x => x.PartyName).HasMaxLength(200);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(10);
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,2)");
            b.Property(x => x.OutstandingAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.PaymentGateway).HasMaxLength(100);
            b.Property(x => x.PaymentUrl).HasMaxLength(500);
            b.Property(x => x.EmailTo).HasMaxLength(200);
            b.Property(x => x.Subject).HasMaxLength(500);
            b.Property(x => x.Message).HasMaxLength(2000);
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.ReferenceDoctype, x.ReferenceId });
            b.HasIndex(x => new { x.TenantId, x.PartyType, x.PartyId, x.Status });
        });

        // Salary Slip
        builder.Entity<SalarySlip>(b =>
        {
            b.ToTable("Hr_SalarySlips", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.EmployeeName).HasMaxLength(200);
            b.Property(x => x.GrossAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalDeductions).HasColumnType("decimal(18,2)");
            b.Property(x => x.NetAmount).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).IsRequired();
            b.HasMany(x => x.Earnings).WithOne().HasForeignKey(x => x.SalarySlipId).IsRequired();
            b.Navigation(x => x.Earnings).AutoInclude();
            b.HasMany(x => x.Deductions).WithOne().HasForeignKey(x => x.SalarySlipId).IsRequired();
            b.Navigation(x => x.Deductions).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.StartDate, x.EndDate });
        });

        builder.Entity<SalarySlipComponent>(b =>
        {
            b.ToTable("Hr_SalarySlipComponents", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ComponentName).IsRequired().HasMaxLength(200);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });

        // Expense Claim
        builder.Entity<ExpenseClaim>(b =>
        {
            b.ToTable("Hr_ExpenseClaims", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.EmployeeName).HasMaxLength(200);
            b.Property(x => x.ExpenseType).HasMaxLength(100);
            b.Property(x => x.TotalClaimedAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalSanctionedAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalAmountReimbursed).HasColumnType("decimal(18,2)");
            b.Property(x => x.AdvanceAmount).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).IsRequired();
            b.HasMany(x => x.Expenses).WithOne().HasForeignKey(x => x.ExpenseClaimId).IsRequired();
            b.Navigation(x => x.Expenses).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Status });
        });

        builder.Entity<ExpenseClaimDetail>(b =>
        {
            b.ToTable("Hr_ExpenseClaimDetails", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });

        builder.Entity<Loan>(b =>
        {
            b.ToTable("Hr_Loans", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.LoanNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.LoanAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.AnnualInterestRate).HasColumnType("decimal(5,2)");
            b.Property(x => x.PenaltyRate).HasColumnType("decimal(5,2)");
            b.Property(x => x.Emi).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalAmountRepaid).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalInterestCharged).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalPrincipalRepaid).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).IsRequired();
            b.HasMany(x => x.RepaymentSchedule).WithOne().HasForeignKey(x => x.LoanId).IsRequired();
            b.Navigation(x => x.RepaymentSchedule).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Status });
            b.HasIndex(x => new { x.TenantId, x.LoanNumber }).IsUnique();
        });

        builder.Entity<LoanRepaymentSchedule>(b =>
        {
            b.ToTable("Hr_LoanRepaymentSchedules", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PrincipalAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.InterestAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalPayment).HasColumnType("decimal(18,2)");
            b.Property(x => x.OutstandingAfterPayment).HasColumnType("decimal(18,2)");
        });

        // Product Bundle
        builder.Entity<ProductBundle>(b =>
        {
            b.ToTable("Sal_ProductBundles", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ProductBundleId).IsRequired();
            b.Navigation(x => x.Items).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.ItemId, x.IsActive });
        });

        builder.Entity<ProductBundleItem>(b =>
        {
            b.ToTable("Sal_ProductBundleItems", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).HasMaxLength(200);
            b.Property(x => x.Uom).HasMaxLength(50);
            b.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            b.HasOne<Item>().WithMany().HasForeignKey(x => x.ComponentItemId).IsRequired();
        });

        builder.Entity<LoyaltyProgram>(b =>
        {
            b.ToTable("Sal_LoyaltyPrograms", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.ConversionFactor).HasColumnType("decimal(18,4)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Tiers).WithOne().HasForeignKey(x => x.LoyaltyProgramId).IsRequired();
            b.Navigation(x => x.Tiers).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.IsEnabled });
        });

        builder.Entity<LoyaltyProgramTier>(b =>
        {
            b.ToTable("Sal_LoyaltyProgramTiers", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TierName).IsRequired().HasMaxLength(100);
            b.Property(x => x.MinSpent).HasColumnType("decimal(18,2)");
            b.Property(x => x.CollectionFactor).HasColumnType("decimal(18,4)");
            b.Property(x => x.RedemptionFactor).HasColumnType("decimal(18,6)");
        });

        builder.Entity<LoyaltyPointEntry>(b =>
        {
            b.ToTable("Sal_LoyaltyPointEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.InvoiceType).HasMaxLength(50);
            b.Property(x => x.TierName).HasMaxLength(100);
            b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).IsRequired();
            b.HasOne<LoyaltyProgram>().WithMany().HasForeignKey(x => x.LoyaltyProgramId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.LoyaltyProgramId, x.PostingDate });
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.ExpiryDate });
        });

        // POS Closing Entry
        builder.Entity<PosClosingEntry>(b =>
        {
            b.ToTable("Sal_PosClosingEntries", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,2)");
            b.Property(x => x.NetTotal).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalTaxes).HasColumnType("decimal(18,2)");
            b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).IsRequired();
            b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.PosClosingEntryId).IsRequired();
            b.Navigation(x => x.Payments).AutoInclude();
            b.HasMany(x => x.Invoices).WithOne().HasForeignKey(x => x.PosClosingEntryId).IsRequired();
            b.Navigation(x => x.Invoices).AutoInclude();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PosProfileId, x.Status });
        });

        builder.Entity<PosClosingPayment>(b =>
        {
            b.ToTable("Sal_PosClosingPayments", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ModeName).IsRequired().HasMaxLength(100);
            b.Property(x => x.ExpectedAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.ClosingAmount).HasColumnType("decimal(18,2)");
        });

        builder.Entity<PosClosingInvoice>(b =>
        {
            b.ToTable("Sal_PosClosingInvoices", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,2)");
        });
    }
}
