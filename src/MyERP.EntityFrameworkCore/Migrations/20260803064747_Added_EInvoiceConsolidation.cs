using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class Added_EInvoiceConsolidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Acc_BankTransactions_Acc_Accounts_BankAccountId",
                table: "Acc_BankTransactions");

            migrationBuilder.AddColumn<string>(
                name: "AddDeductTax",
                table: "Tax_TransactionTaxRows",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "HoldStartedOn",
                table: "Sup_Issues",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSlaBreach",
                table: "Sup_Issues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceLevelAgreementId",
                table: "Sup_Issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Sal_Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Sal_SubscriptionPlans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryDate",
                table: "Sal_SalesOrderItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConsolidatedSalesInvoiceId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryNoteId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LhdnSubmissionId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LhdnSubmittedAt",
                table: "Sal_SalesInvoices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OpportunityId",
                table: "Sal_Quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderedQty",
                table: "Sal_QuotationItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "Sal_PosClosingEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                table: "Sal_Dunnings",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailSentTo",
                table: "Sal_Dunnings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "Sal_DeliveryNoteItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "Pur_SupplierQuotationItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Pur_SubcontractingReceipts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Pur_SubcontractingReceipts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierWarehouseId",
                table: "Pur_SubcontractingOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LandedCostVoucherAmount",
                table: "Pur_PurchaseReceiptItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Pur_PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Pur_PurchaseOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "InterCompanySalesOrderId",
                table: "Pur_PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubcontracted",
                table: "Pur_PurchaseOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Pur_PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupplierConfirmationDate",
                table: "Pur_PurchaseOrders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierConfirmationNumber",
                table: "Pur_PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupplierPromisedDate",
                table: "Pur_PurchaseOrders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierQuotationId",
                table: "Pur_PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDeliveryDate",
                table: "Pur_PurchaseOrderItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstReceiptDate",
                table: "Pur_PurchaseOrderItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSupplierConfirmed",
                table: "Pur_PurchaseOrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReceiptDate",
                table: "Pur_PurchaseOrderItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupplierPromisedDate",
                table: "Pur_PurchaseOrderItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LhdnLongId",
                table: "Pur_PurchaseInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LhdnSubmissionId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LhdnSubmittedAt",
                table: "Pur_PurchaseInvoices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderId",
                table: "Pur_MaterialRequestItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DisassembledQuantity",
                table: "Mfg_WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ScrapWarehouseId",
                table: "Mfg_WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Mfg_BOMItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Mfg_BOMItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAccountId",
                table: "Inv_Warehouses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseType",
                table: "Inv_Warehouses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ConsumedQty",
                table: "Inv_StockReservationEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferredQty",
                table: "Inv_StockReservationEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherQty",
                table: "Inv_StockReservationEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Inv_StockEntryItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Inv_StockEntryItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultBomId",
                table: "Inv_Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "Inv_Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseUom",
                table: "Inv_Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesUom",
                table: "Inv_Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerUnit",
                table: "Inv_Items",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "WeightUom",
                table: "Inv_Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Hr_Employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderId",
                table: "Ast_MaintenanceSchedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultFgWarehouseId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultScrapWarehouseId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWarehouseId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWipWarehouseId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpensesAddedToStockAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpensesAddedToStockContraAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SampleRetentionWarehouseId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "Acc_PaymentScheduleEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                table: "Acc_PaymentScheduleEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscountValidTill",
                table: "Acc_PaymentScheduleEntries",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountedAmount",
                table: "Acc_PaymentScheduleEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Acc_PaymentEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Acc_PaymentEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedAmount",
                table: "Acc_PaymentEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetExchangeRate",
                table: "Acc_PaymentEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "InterCompanyJournalEntryId",
                table: "Acc_JournalEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMultiCurrency",
                table: "Acc_JournalEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpening",
                table: "Acc_JournalEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalOfId",
                table: "Acc_JournalEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoucherType",
                table: "Acc_JournalEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Acc_BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BankAccountNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Iban = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SwiftCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BranchCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsCompanyAccount = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IntegrationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastIntegrationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsCreditCard = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_BankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_BankAccounts_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_BankAccounts_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CRM_Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContractName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SigningDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ContractTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractTerms = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    RequiresFulfilment = table.Column<bool>(type: "boolean", nullable: false),
                    IsFulfilmentSatisfied = table.Column<bool>(type: "boolean", nullable: false),
                    IsAutoRenewal = table.Column<bool>(type: "boolean", nullable: false),
                    RenewalReminderDays = table.Column<int>(type: "integer", nullable: true),
                    IpOwnership = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContractValue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRM_Contracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CRM_Prospects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProspectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Industry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Website = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Territory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnnualRevenue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    NumberOfEmployees = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConvertedCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRM_Prospects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EInv_Consolidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsolidatedInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EInv_Consolidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EInv_Consolidations_Sal_SalesInvoices_ConsolidatedInvoiceId",
                        column: x => x.ConsolidatedInvoiceId,
                        principalTable: "Sal_SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EInv_Consolidations_Sal_SalesInvoices_OriginalInvoiceId",
                        column: x => x.OriginalInvoiceId,
                        principalTable: "Sal_SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Goal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityGoals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityInspectionTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    BomId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityInspectionTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityProcedures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentQualityProcedureId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    Lft = table.Column<int>(type: "integer", nullable: false),
                    Rgt = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityProcedures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_WarehouseAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockReceivedButNotBilledAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockDeliveredButNotBilledAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockAdjustmentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_WarehouseAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_WarehouseAccounts_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_WarehouseAccounts_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mnt_WarrantyClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNoId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarrantyExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AmcExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ComplaintDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Complaint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolutionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ServiceAddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mnt_WarrantyClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mnt_WarrantyClaims_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PosProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ValidateStock = table.Column<bool>(type: "boolean", nullable: false),
                    InvoiceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    TaxTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    WriteOffAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    WriteOffCostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    WriteOffLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PostChangeGlEntries = table.Column<bool>(type: "boolean", nullable: false),
                    IncomeAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PosProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosProfiles_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_SalesPartners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PartnerType = table.Column<int>(type: "integer", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TerritoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReferralCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_SalesPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PickupFromType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PickupFromId = table.Column<Guid>(type: "uuid", nullable: true),
                    PickupFromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PickupAddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    PickupContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PickupContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeliveryToType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeliveryToId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryToName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveryAddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveryContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PickupDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeliveryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Carrier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CarrierService = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TrackingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalNetWeight = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    TotalGrossWeight = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    WeightUom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ValueOfGoods = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_Shipments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tax_ChargesTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TemplateType = table.Column<int>(type: "integer", nullable: false),
                    TaxCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tax_ChargesTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CRM_ProspectLeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRM_ProspectLeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CRM_ProspectLeads_CRM_Prospects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "CRM_Prospects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CRM_ProspectOpportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRM_ProspectOpportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CRM_ProspectOpportunities_CRM_Prospects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "CRM_Prospects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    QualityGoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ActualValue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityReviews_Inv_QualityGoals_QualityGoalId",
                        column: x => x.QualityGoalId,
                        principalTable: "Inv_QualityGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityInspectionParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityInspectionTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Specification = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpectedValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MinValue = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    IsNumeric = table.Column<bool>(type: "boolean", nullable: false),
                    FormulaBased = table.Column<bool>(type: "boolean", nullable: false),
                    Formula = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityInspectionParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityInspectionParameters_Inv_QualityInspectionTempla~",
                        column: x => x.QualityInspectionTemplateId,
                        principalTable: "Inv_QualityInspectionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityProcedureSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityProcedureId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ChildProcedureId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityProcedureSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityProcedureSteps_Inv_QualityProcedures_QualityProc~",
                        column: x => x.QualityProcedureId,
                        principalTable: "Inv_QualityProcedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PosProfilePaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PosProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeOfPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PosProfilePaymentMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosProfilePaymentMethods_Sal_PosProfiles_PosProfileId",
                        column: x => x.PosProfileId,
                        principalTable: "Sal_PosProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_ShipmentDeliveryNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryNoteNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_ShipmentDeliveryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_ShipmentDeliveryNotes_Sal_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Sal_Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tax_ChargesTemplateRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxChargesTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    ChargeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TaxCategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReferenceRowIndex = table.Column<int>(type: "integer", nullable: true),
                    IncludedInPrintRate = table.Column<bool>(type: "boolean", nullable: false),
                    IncludedInPaidAmount = table.Column<bool>(type: "boolean", nullable: false),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tax_ChargesTemplateRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tax_ChargesTemplateRows_Tax_ChargesTemplates_TaxChargesTemp~",
                        column: x => x.TaxChargesTemplateId,
                        principalTable: "Tax_ChargesTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankAccounts_AccountId",
                table: "Acc_BankAccounts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankAccounts_CompanyId",
                table: "Acc_BankAccounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankAccounts_TenantId_AccountId",
                table: "Acc_BankAccounts",
                columns: new[] { "TenantId", "AccountId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankAccounts_TenantId_CompanyId_IsDefault",
                table: "Acc_BankAccounts",
                columns: new[] { "TenantId", "CompanyId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_CRM_Contracts_TenantId_CompanyId_PartyType_PartyId_Status",
                table: "CRM_Contracts",
                columns: new[] { "TenantId", "CompanyId", "PartyType", "PartyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CRM_ProspectLeads_ProspectId_LeadId",
                table: "CRM_ProspectLeads",
                columns: new[] { "ProspectId", "LeadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRM_ProspectOpportunities_ProspectId",
                table: "CRM_ProspectOpportunities",
                column: "ProspectId");

            migrationBuilder.CreateIndex(
                name: "IX_CRM_Prospects_TenantId_CompanyId",
                table: "CRM_Prospects",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_EInv_Consolidations_ConsolidatedInvoiceId",
                table: "EInv_Consolidations",
                column: "ConsolidatedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EInv_Consolidations_OriginalInvoiceId",
                table: "EInv_Consolidations",
                column: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EInv_Consolidations_TenantId_CompanyId_OriginalInvoiceId",
                table: "EInv_Consolidations",
                columns: new[] { "TenantId", "CompanyId", "OriginalInvoiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityGoals_TenantId_IsEnabled",
                table: "Inv_QualityGoals",
                columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityInspectionParameters_QualityInspectionTemplateId",
                table: "Inv_QualityInspectionParameters",
                column: "QualityInspectionTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityInspectionTemplates_TenantId_Name",
                table: "Inv_QualityInspectionTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityProcedures_TenantId_Lft_Rgt",
                table: "Inv_QualityProcedures",
                columns: new[] { "TenantId", "Lft", "Rgt" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityProcedures_TenantId_ParentQualityProcedureId",
                table: "Inv_QualityProcedures",
                columns: new[] { "TenantId", "ParentQualityProcedureId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityProcedureSteps_QualityProcedureId",
                table: "Inv_QualityProcedureSteps",
                column: "QualityProcedureId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityReviews_QualityGoalId",
                table: "Inv_QualityReviews",
                column: "QualityGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityReviews_TenantId_QualityGoalId_ReviewDate",
                table: "Inv_QualityReviews",
                columns: new[] { "TenantId", "QualityGoalId", "ReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_WarehouseAccounts_CompanyId",
                table: "Inv_WarehouseAccounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_WarehouseAccounts_TenantId_WarehouseId_CompanyId",
                table: "Inv_WarehouseAccounts",
                columns: new[] { "TenantId", "WarehouseId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_WarehouseAccounts_WarehouseId",
                table: "Inv_WarehouseAccounts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Mnt_WarrantyClaims_CompanyId",
                table: "Mnt_WarrantyClaims",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Mnt_WarrantyClaims_TenantId_CompanyId_CustomerId_Status",
                table: "Mnt_WarrantyClaims",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfilePaymentMethods_PosProfileId",
                table: "Sal_PosProfilePaymentMethods",
                column: "PosProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfiles_CompanyId",
                table: "Sal_PosProfiles",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfiles_TenantId_CompanyId_IsDisabled",
                table: "Sal_PosProfiles",
                columns: new[] { "TenantId", "CompanyId", "IsDisabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesPartners_TenantId_Name",
                table: "Sal_SalesPartners",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ShipmentDeliveryNotes_ShipmentId",
                table: "Sal_ShipmentDeliveryNotes",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_Shipments_TenantId_CompanyId_Status",
                table: "Sal_Shipments",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tax_ChargesTemplateRows_TaxChargesTemplateId",
                table: "Tax_ChargesTemplateRows",
                column: "TaxChargesTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_ChargesTemplates_TenantId_CompanyId_TemplateType_IsDefa~",
                table: "Tax_ChargesTemplates",
                columns: new[] { "TenantId", "CompanyId", "TemplateType", "IsDefault" });

            migrationBuilder.AddForeignKey(
                name: "FK_Acc_BankTransactions_Acc_BankAccounts_BankAccountId",
                table: "Acc_BankTransactions",
                column: "BankAccountId",
                principalTable: "Acc_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Acc_BankTransactions_Acc_BankAccounts_BankAccountId",
                table: "Acc_BankTransactions");

            migrationBuilder.DropTable(
                name: "Acc_BankAccounts");

            migrationBuilder.DropTable(
                name: "CRM_Contracts");

            migrationBuilder.DropTable(
                name: "CRM_ProspectLeads");

            migrationBuilder.DropTable(
                name: "CRM_ProspectOpportunities");

            migrationBuilder.DropTable(
                name: "EInv_Consolidations");

            migrationBuilder.DropTable(
                name: "Inv_QualityInspectionParameters");

            migrationBuilder.DropTable(
                name: "Inv_QualityProcedureSteps");

            migrationBuilder.DropTable(
                name: "Inv_QualityReviews");

            migrationBuilder.DropTable(
                name: "Inv_WarehouseAccounts");

            migrationBuilder.DropTable(
                name: "Mnt_WarrantyClaims");

            migrationBuilder.DropTable(
                name: "Sal_PosProfilePaymentMethods");

            migrationBuilder.DropTable(
                name: "Sal_SalesPartners");

            migrationBuilder.DropTable(
                name: "Sal_ShipmentDeliveryNotes");

            migrationBuilder.DropTable(
                name: "Tax_ChargesTemplateRows");

            migrationBuilder.DropTable(
                name: "CRM_Prospects");

            migrationBuilder.DropTable(
                name: "Inv_QualityInspectionTemplates");

            migrationBuilder.DropTable(
                name: "Inv_QualityProcedures");

            migrationBuilder.DropTable(
                name: "Inv_QualityGoals");

            migrationBuilder.DropTable(
                name: "Sal_PosProfiles");

            migrationBuilder.DropTable(
                name: "Sal_Shipments");

            migrationBuilder.DropTable(
                name: "Tax_ChargesTemplates");

            migrationBuilder.DropColumn(
                name: "AddDeductTax",
                table: "Tax_TransactionTaxRows");

            migrationBuilder.DropColumn(
                name: "HoldStartedOn",
                table: "Sup_Issues");

            migrationBuilder.DropColumn(
                name: "IsSlaBreach",
                table: "Sup_Issues");

            migrationBuilder.DropColumn(
                name: "ServiceLevelAgreementId",
                table: "Sup_Issues");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Sal_Subscriptions");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Sal_SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryDate",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "ConsolidatedSalesInvoiceId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "LhdnSubmissionId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "LhdnSubmittedAt",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "OpportunityId",
                table: "Sal_Quotations");

            migrationBuilder.DropColumn(
                name: "OrderedQty",
                table: "Sal_QuotationItems");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "Sal_PosClosingEntries");

            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "Sal_Dunnings");

            migrationBuilder.DropColumn(
                name: "EmailSentTo",
                table: "Sal_Dunnings");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "Pur_SupplierQuotationItems");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Pur_SubcontractingReceipts");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Pur_SubcontractingReceipts");

            migrationBuilder.DropColumn(
                name: "SupplierWarehouseId",
                table: "Pur_SubcontractingOrders");

            migrationBuilder.DropColumn(
                name: "LandedCostVoucherAmount",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "InterCompanySalesOrderId",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "IsSubcontracted",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierConfirmationDate",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierConfirmationNumber",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierPromisedDate",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierQuotationId",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExpectedDeliveryDate",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "FirstReceiptDate",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "IsSupplierConfirmed",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "LastReceiptDate",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SupplierPromisedDate",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "LhdnLongId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "LhdnSubmissionId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "LhdnSubmittedAt",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "SalesOrderId",
                table: "Pur_MaterialRequestItems");

            migrationBuilder.DropColumn(
                name: "DisassembledQuantity",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "ScrapWarehouseId",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Mfg_BOMItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Mfg_BOMItems");

            migrationBuilder.DropColumn(
                name: "DefaultAccountId",
                table: "Inv_Warehouses");

            migrationBuilder.DropColumn(
                name: "WarehouseType",
                table: "Inv_Warehouses");

            migrationBuilder.DropColumn(
                name: "ConsumedQty",
                table: "Inv_StockReservationEntries");

            migrationBuilder.DropColumn(
                name: "TransferredQty",
                table: "Inv_StockReservationEntries");

            migrationBuilder.DropColumn(
                name: "VoucherQty",
                table: "Inv_StockReservationEntries");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "DefaultBomId",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "PurchaseUom",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "SalesUom",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "WeightPerUnit",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "WeightUom",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Hr_Employees");

            migrationBuilder.DropColumn(
                name: "SalesOrderId",
                table: "Ast_MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "DefaultFgWarehouseId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultScrapWarehouseId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultWarehouseId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultWipWarehouseId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ExpensesAddedToStockAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ExpensesAddedToStockContraAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "SampleRetentionWarehouseId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "Acc_PaymentScheduleEntries");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Acc_PaymentScheduleEntries");

            migrationBuilder.DropColumn(
                name: "DiscountValidTill",
                table: "Acc_PaymentScheduleEntries");

            migrationBuilder.DropColumn(
                name: "DiscountedAmount",
                table: "Acc_PaymentScheduleEntries");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "ReceivedAmount",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "TargetExchangeRate",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "InterCompanyJournalEntryId",
                table: "Acc_JournalEntries");

            migrationBuilder.DropColumn(
                name: "IsMultiCurrency",
                table: "Acc_JournalEntries");

            migrationBuilder.DropColumn(
                name: "IsOpening",
                table: "Acc_JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReversalOfId",
                table: "Acc_JournalEntries");

            migrationBuilder.DropColumn(
                name: "VoucherType",
                table: "Acc_JournalEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_Acc_BankTransactions_Acc_Accounts_BankAccountId",
                table: "Acc_BankTransactions",
                column: "BankAccountId",
                principalTable: "Acc_Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
