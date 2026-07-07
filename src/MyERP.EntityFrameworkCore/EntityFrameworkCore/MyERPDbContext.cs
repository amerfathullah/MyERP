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

    // Accounting
    public DbSet<Account> Accounts { get; set; }
    public DbSet<FiscalYear> FiscalYears { get; set; }
    public DbSet<AccountingRule> AccountingRules { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }
    public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
    public DbSet<PaymentEntry> PaymentEntries { get; set; }

    // Sales
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Quotation> Quotations { get; set; }
    public DbSet<QuotationItem> QuotationItems { get; set; }
    public DbSet<SalesOrder> SalesOrders { get; set; }
    public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
    public DbSet<SalesInvoice> SalesInvoices { get; set; }
    public DbSet<SalesInvoiceItem> SalesInvoiceItems { get; set; }

    // Purchasing
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }

    // Inventory
    public DbSet<Item> Items { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<StockEntry> StockEntries { get; set; }
    public DbSet<StockEntryItem> StockEntryItems { get; set; }
    public DbSet<StockLedgerEntry> StockLedgerEntries { get; set; }

    // Tax
    public DbSet<TaxCategory> TaxCategories { get; set; }
    public DbSet<TaxRule> TaxRules { get; set; }

    // E-Invoice
    public DbSet<EInvoiceSubmission> EInvoiceSubmissions { get; set; }

    // Human Resources
    public DbSet<Employee> Employees { get; set; }
    public DbSet<ContributionRule> ContributionRules { get; set; }

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
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.EntryNumber }).IsUnique().HasFilter("\"EntryNumber\" IS NOT NULL");
        });

        builder.Entity<JournalEntryLine>(b =>
        {
            b.ToTable("Acc_JournalEntryLines", MyERPConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.PartyType).HasMaxLength(50);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
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
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceNumber }).IsUnique();
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
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.OrderNumber }).IsUnique();
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
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PaymentNumber }).IsUnique().HasFilter("\"PaymentNumber\" IS NOT NULL");
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
    }
}
