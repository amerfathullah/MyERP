using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerGroupId",
                table: "Sal_Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerritoryId",
                table: "Sal_Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierGroupId",
                table: "Pur_Suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasVariants",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "VariantOfId",
                table: "Inv_Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRuleEvaluated",
                table: "Acc_BankTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MatchedTransactionRuleId",
                table: "Acc_BankTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Acc_BankTransactionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ClassifyAs = table.Column<int>(type: "integer", nullable: false),
                    BankEntryMode = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Acc_BankTransactionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_BankTransactionRules_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_ExchangeRateRevaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExchangeGainLossAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundingLossAllowance = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    TotalGainLoss = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RevaluationJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ZeroBalanceJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Acc_ExchangeRateRevaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_ExchangeRateRevaluations_Acc_Accounts_ExchangeGainLossA~",
                        column: x => x.ExchangeGainLossAccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_ExchangeRateRevaluations_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAuthorizationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BasedOn = table.Column<int>(type: "integer", nullable: false),
                    ThresholdValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SystemUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovingRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovingUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_AppAuthorizationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAutoRepeats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceDocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReferenceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceDocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NextScheduleDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyByEmail = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyRecipients = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GeneratedCount = table.Column<int>(type: "integer", nullable: false),
                    LastGeneratedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppAutoRepeats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAutoRepeats_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppCustomerGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultPaymentTermsTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultPriceListId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultCreditLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_AppCustomerGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEmailTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_AppEmailTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppNotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sender = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppNotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSupplierGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultPaymentTermsTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppSupplierGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTerritories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    TerritoryManagerId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppTerritories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hr_Loans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LoanType = table.Column<int>(type: "integer", nullable: false),
                    InterestMethod = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LoanAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AnnualInterestRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TenureMonths = table.Column<int>(type: "integer", nullable: false),
                    GracePeriodMonths = table.Column<int>(type: "integer", nullable: false),
                    DisbursementDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RepaymentStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Emi = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmountRepaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalInterestCharged = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPrincipalRepaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PenaltyRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    LoanAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    InterestIncomeAccountId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Hr_Loans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_Loans_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Hr_Loans_Hr_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Hr_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_ItemAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttributeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsNumeric = table.Column<bool>(type: "boolean", nullable: false),
                    FromRange = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ToRange = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Increment = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
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
                    table.PrimaryKey("PK_Inv_ItemAttributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_StockClosingEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalEntries = table.Column<int>(type: "integer", nullable: false),
                    TotalStockValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PreviousClosingEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScannedFromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_Inv_StockClosingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_StockClosingEntries_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prj_ActivityTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultBillingRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DefaultCostingRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Prj_ActivityTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pur_SupplierScorecards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodType = table.Column<int>(type: "integer", nullable: false),
                    WeightingFunction = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Score = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrentStanding = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_Pur_SupplierScorecards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SupplierScorecards_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pur_SupplierScorecards_Pur_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Pur_Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_LoyaltyPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExpiryDurationDays = table.Column<int>(type: "integer", nullable: false),
                    ExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Sal_LoyaltyPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_LoyaltyPrograms_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_SalesPersons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentSalesPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
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
                    table.PrimaryKey("PK_Sal_SalesPersons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_ShippingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    CalculationMode = table.Column<int>(type: "integer", nullable: false),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Sal_ShippingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_ShippingRules_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_BankTransactionRuleAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankTransactionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DebitFormula = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreditFormula = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_BankTransactionRuleAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_BankTransactionRuleAccounts_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_BankTransactionRuleAccounts_Acc_BankTransactionRules_Ba~",
                        column: x => x.BankTransactionRuleId,
                        principalTable: "Acc_BankTransactionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_BankTransactionRuleConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankTransactionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchType = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_BankTransactionRuleConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_BankTransactionRuleConditions_Acc_BankTransactionRules_~",
                        column: x => x.BankTransactionRuleId,
                        principalTable: "Acc_BankTransactionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_ExchangeRateRevaluationEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExchangeRateRevaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BalanceInAccountCurrency = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrentBalanceInCompanyCurrency = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NewExchangeRate = table.Column<decimal>(type: "numeric(18,9)", nullable: false),
                    NewBalanceInCompanyCurrency = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GainLoss = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_ExchangeRateRevaluationEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_ExchangeRateRevaluationEntries_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_ExchangeRateRevaluationEntries_Acc_ExchangeRateRevaluat~",
                        column: x => x.ExchangeRateRevaluationId,
                        principalTable: "Acc_ExchangeRateRevaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hr_LoanRepaymentSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InterestAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPayment = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OutstandingAfterPayment = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hr_LoanRepaymentSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_LoanRepaymentSchedules_Hr_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Hr_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_ItemAttributeValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemAttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_ItemAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_ItemAttributeValues_Inv_ItemAttributes_ItemAttributeId",
                        column: x => x.ItemAttributeId,
                        principalTable: "Inv_ItemAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_ItemVariantAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemAttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_ItemVariantAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_ItemVariantAttributes_Inv_ItemAttributes_ItemAttributeId",
                        column: x => x.ItemAttributeId,
                        principalTable: "Inv_ItemAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_ItemVariantAttributes_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_StockClosingBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockClosingEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StockValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ValuationRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    FifoQueue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_StockClosingBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_StockClosingBalances_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_StockClosingBalances_Inv_StockClosingEntries_StockClosi~",
                        column: x => x.StockClosingEntryId,
                        principalTable: "Inv_StockClosingEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_StockClosingBalances_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prj_ActivityCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostingRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Prj_ActivityCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prj_ActivityCosts_Prj_ActivityTypes_ActivityTypeId",
                        column: x => x.ActivityTypeId,
                        principalTable: "Prj_ActivityTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_ScorecardCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierScorecardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Formula = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_ScorecardCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_ScorecardCriteria_Pur_SupplierScorecards_SupplierScorec~",
                        column: x => x.SupplierScorecardId,
                        principalTable: "Pur_SupplierScorecards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_ScorecardPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierScorecardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalScore = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsSubmitted = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Pur_ScorecardPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_ScorecardPeriods_Pur_SupplierScorecards_SupplierScoreca~",
                        column: x => x.SupplierScorecardId,
                        principalTable: "Pur_SupplierScorecards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_ScorecardStandings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierScorecardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinGrade = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    MaxGrade = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    PreventPos = table.Column<bool>(type: "boolean", nullable: false),
                    PreventRfqs = table.Column<bool>(type: "boolean", nullable: false),
                    WarnPos = table.Column<bool>(type: "boolean", nullable: false),
                    WarnRfqs = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_ScorecardStandings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_ScorecardStandings_Pur_SupplierScorecards_SupplierScore~",
                        column: x => x.SupplierScorecardId,
                        principalTable: "Pur_SupplierScorecards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_LoyaltyPointEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoyaltyProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InvoiceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemAgainstId = table.Column<Guid>(type: "uuid", nullable: true),
                    TierName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_Sal_LoyaltyPointEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_LoyaltyPointEntries_Sal_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Sal_Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_LoyaltyPointEntries_Sal_LoyaltyPrograms_LoyaltyProgramId",
                        column: x => x.LoyaltyProgramId,
                        principalTable: "Sal_LoyaltyPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_LoyaltyProgramTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoyaltyProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    TierName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinSpent = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CollectionFactor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RedemptionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_LoyaltyProgramTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_LoyaltyProgramTiers_Sal_LoyaltyPrograms_LoyaltyProgramId",
                        column: x => x.LoyaltyProgramId,
                        principalTable: "Sal_LoyaltyPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_SalesPersonTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ItemGroupId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_SalesPersonTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_SalesPersonTargets_Sal_SalesPersons_SalesPersonId",
                        column: x => x.SalesPersonId,
                        principalTable: "Sal_SalesPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_SalesTeamEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Incentives = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ParentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_SalesTeamEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_SalesTeamEntries_Sal_SalesPersons_SalesPersonId",
                        column: x => x.SalesPersonId,
                        principalTable: "Sal_SalesPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_ShippingRuleConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ToValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_ShippingRuleConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_ShippingRuleConditions_Sal_ShippingRules_ShippingRuleId",
                        column: x => x.ShippingRuleId,
                        principalTable: "Sal_ShippingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_ShippingRuleCountries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_ShippingRuleCountries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_ShippingRuleCountries_Sal_ShippingRules_ShippingRuleId",
                        column: x => x.ShippingRuleId,
                        principalTable: "Sal_ShippingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankTransactionRuleAccounts_AccountId",
                table: "Acc_BankTransactionRuleAccounts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankTransactionRuleAccounts_BankTransactionRuleId",
                table: "Acc_BankTransactionRuleAccounts",
                column: "BankTransactionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankTransactionRuleConditions_BankTransactionRuleId",
                table: "Acc_BankTransactionRuleConditions",
                column: "BankTransactionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankTransactionRules_CompanyId",
                table: "Acc_BankTransactionRules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankTransactionRules_TenantId_CompanyId_Priority",
                table: "Acc_BankTransactionRules",
                columns: new[] { "TenantId", "CompanyId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ExchangeRateRevaluationEntries_AccountId",
                table: "Acc_ExchangeRateRevaluationEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ExchangeRateRevaluationEntries_ExchangeRateRevaluationId",
                table: "Acc_ExchangeRateRevaluationEntries",
                column: "ExchangeRateRevaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ExchangeRateRevaluations_CompanyId",
                table: "Acc_ExchangeRateRevaluations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ExchangeRateRevaluations_ExchangeGainLossAccountId",
                table: "Acc_ExchangeRateRevaluations",
                column: "ExchangeGainLossAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ExchangeRateRevaluations_TenantId_CompanyId_PostingDate",
                table: "Acc_ExchangeRateRevaluations",
                columns: new[] { "TenantId", "CompanyId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAuthorizationRules_TenantId_TransactionType_CompanyId_Ba~",
                table: "AppAuthorizationRules",
                columns: new[] { "TenantId", "TransactionType", "CompanyId", "BasedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAutoRepeats_CompanyId",
                table: "AppAutoRepeats",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAutoRepeats_TenantId_CompanyId_IsEnabled_NextScheduleDate",
                table: "AppAutoRepeats",
                columns: new[] { "TenantId", "CompanyId", "IsEnabled", "NextScheduleDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCustomerGroups_TenantId_Name",
                table: "AppCustomerGroups",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCustomerGroups_TenantId_ParentId",
                table: "AppCustomerGroups",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppEmailTemplates_TenantId_Name",
                table: "AppEmailTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppNotificationLogs_TenantId_DocumentType_DocumentId",
                table: "AppNotificationLogs",
                columns: new[] { "TenantId", "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppNotificationLogs_TenantId_Status_CreationTime",
                table: "AppNotificationLogs",
                columns: new[] { "TenantId", "Status", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSupplierGroups_TenantId_Name",
                table: "AppSupplierGroups",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSupplierGroups_TenantId_ParentId",
                table: "AppSupplierGroups",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTerritories_TenantId_Name",
                table: "AppTerritories",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTerritories_TenantId_ParentId",
                table: "AppTerritories",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Hr_LoanRepaymentSchedules_LoanId",
                table: "Hr_LoanRepaymentSchedules",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_Loans_CompanyId",
                table: "Hr_Loans",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_Loans_EmployeeId",
                table: "Hr_Loans",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_Loans_TenantId_EmployeeId_Status",
                table: "Hr_Loans",
                columns: new[] { "TenantId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Hr_Loans_TenantId_LoanNumber",
                table: "Hr_Loans",
                columns: new[] { "TenantId", "LoanNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemAttributes_TenantId_AttributeName",
                table: "Inv_ItemAttributes",
                columns: new[] { "TenantId", "AttributeName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemAttributeValues_ItemAttributeId",
                table: "Inv_ItemAttributeValues",
                column: "ItemAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemVariantAttributes_ItemAttributeId",
                table: "Inv_ItemVariantAttributes",
                column: "ItemAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemVariantAttributes_ItemId_ItemAttributeId",
                table: "Inv_ItemVariantAttributes",
                columns: new[] { "ItemId", "ItemAttributeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockClosingBalances_ItemId",
                table: "Inv_StockClosingBalances",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockClosingBalances_StockClosingEntryId_ItemId_Warehou~",
                table: "Inv_StockClosingBalances",
                columns: new[] { "StockClosingEntryId", "ItemId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockClosingBalances_WarehouseId",
                table: "Inv_StockClosingBalances",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockClosingEntries_CompanyId",
                table: "Inv_StockClosingEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockClosingEntries_TenantId_CompanyId_ToDate_Status",
                table: "Inv_StockClosingEntries",
                columns: new[] { "TenantId", "CompanyId", "ToDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Prj_ActivityCosts_ActivityTypeId",
                table: "Prj_ActivityCosts",
                column: "ActivityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Prj_ActivityCosts_TenantId_EmployeeId_ActivityTypeId",
                table: "Prj_ActivityCosts",
                columns: new[] { "TenantId", "EmployeeId", "ActivityTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prj_ActivityTypes_TenantId_Name",
                table: "Prj_ActivityTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_ScorecardCriteria_SupplierScorecardId",
                table: "Pur_ScorecardCriteria",
                column: "SupplierScorecardId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_ScorecardPeriods_SupplierScorecardId",
                table: "Pur_ScorecardPeriods",
                column: "SupplierScorecardId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_ScorecardPeriods_TenantId_SupplierId_StartDate_EndDate",
                table: "Pur_ScorecardPeriods",
                columns: new[] { "TenantId", "SupplierId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_ScorecardStandings_SupplierScorecardId",
                table: "Pur_ScorecardStandings",
                column: "SupplierScorecardId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SupplierScorecards_CompanyId",
                table: "Pur_SupplierScorecards",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SupplierScorecards_SupplierId",
                table: "Pur_SupplierScorecards",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SupplierScorecards_TenantId_SupplierId",
                table: "Pur_SupplierScorecards",
                columns: new[] { "TenantId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_LoyaltyPointEntries_CustomerId",
                table: "Sal_LoyaltyPointEntries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_LoyaltyPointEntries_LoyaltyProgramId",
                table: "Sal_LoyaltyPointEntries",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_LoyaltyPointEntries_TenantId_CustomerId_ExpiryDate",
                table: "Sal_LoyaltyPointEntries",
                columns: new[] { "TenantId", "CustomerId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_LoyaltyPointEntries_TenantId_CustomerId_LoyaltyProgramI~",
                table: "Sal_LoyaltyPointEntries",
                columns: new[] { "TenantId", "CustomerId", "LoyaltyProgramId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_LoyaltyPrograms_CompanyId",
                table: "Sal_LoyaltyPrograms",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_LoyaltyPrograms_TenantId_CompanyId_IsEnabled",
                table: "Sal_LoyaltyPrograms",
                columns: new[] { "TenantId", "CompanyId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_LoyaltyProgramTiers_LoyaltyProgramId",
                table: "Sal_LoyaltyProgramTiers",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesPersons_TenantId_EmployeeId",
                table: "Sal_SalesPersons",
                columns: new[] { "TenantId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesPersons_TenantId_Name",
                table: "Sal_SalesPersons",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesPersons_TenantId_ParentSalesPersonId",
                table: "Sal_SalesPersons",
                columns: new[] { "TenantId", "ParentSalesPersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesPersonTargets_SalesPersonId",
                table: "Sal_SalesPersonTargets",
                column: "SalesPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesTeamEntries_ParentType_ParentId",
                table: "Sal_SalesTeamEntries",
                columns: new[] { "ParentType", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesTeamEntries_SalesPersonId",
                table: "Sal_SalesTeamEntries",
                column: "SalesPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ShippingRuleConditions_ShippingRuleId",
                table: "Sal_ShippingRuleConditions",
                column: "ShippingRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ShippingRuleCountries_ShippingRuleId",
                table: "Sal_ShippingRuleCountries",
                column: "ShippingRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ShippingRules_AccountId",
                table: "Sal_ShippingRules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ShippingRules_TenantId_RuleType_IsEnabled",
                table: "Sal_ShippingRules",
                columns: new[] { "TenantId", "RuleType", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_BankTransactionRuleAccounts");

            migrationBuilder.DropTable(
                name: "Acc_BankTransactionRuleConditions");

            migrationBuilder.DropTable(
                name: "Acc_ExchangeRateRevaluationEntries");

            migrationBuilder.DropTable(
                name: "AppAuthorizationRules");

            migrationBuilder.DropTable(
                name: "AppAutoRepeats");

            migrationBuilder.DropTable(
                name: "AppCustomerGroups");

            migrationBuilder.DropTable(
                name: "AppEmailTemplates");

            migrationBuilder.DropTable(
                name: "AppNotificationLogs");

            migrationBuilder.DropTable(
                name: "AppSupplierGroups");

            migrationBuilder.DropTable(
                name: "AppTerritories");

            migrationBuilder.DropTable(
                name: "Hr_LoanRepaymentSchedules");

            migrationBuilder.DropTable(
                name: "Inv_ItemAttributeValues");

            migrationBuilder.DropTable(
                name: "Inv_ItemVariantAttributes");

            migrationBuilder.DropTable(
                name: "Inv_StockClosingBalances");

            migrationBuilder.DropTable(
                name: "Prj_ActivityCosts");

            migrationBuilder.DropTable(
                name: "Pur_ScorecardCriteria");

            migrationBuilder.DropTable(
                name: "Pur_ScorecardPeriods");

            migrationBuilder.DropTable(
                name: "Pur_ScorecardStandings");

            migrationBuilder.DropTable(
                name: "Sal_LoyaltyPointEntries");

            migrationBuilder.DropTable(
                name: "Sal_LoyaltyProgramTiers");

            migrationBuilder.DropTable(
                name: "Sal_SalesPersonTargets");

            migrationBuilder.DropTable(
                name: "Sal_SalesTeamEntries");

            migrationBuilder.DropTable(
                name: "Sal_ShippingRuleConditions");

            migrationBuilder.DropTable(
                name: "Sal_ShippingRuleCountries");

            migrationBuilder.DropTable(
                name: "Acc_BankTransactionRules");

            migrationBuilder.DropTable(
                name: "Acc_ExchangeRateRevaluations");

            migrationBuilder.DropTable(
                name: "Hr_Loans");

            migrationBuilder.DropTable(
                name: "Inv_ItemAttributes");

            migrationBuilder.DropTable(
                name: "Inv_StockClosingEntries");

            migrationBuilder.DropTable(
                name: "Prj_ActivityTypes");

            migrationBuilder.DropTable(
                name: "Pur_SupplierScorecards");

            migrationBuilder.DropTable(
                name: "Sal_LoyaltyPrograms");

            migrationBuilder.DropTable(
                name: "Sal_SalesPersons");

            migrationBuilder.DropTable(
                name: "Sal_ShippingRules");

            migrationBuilder.DropColumn(
                name: "CustomerGroupId",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "TerritoryId",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "SupplierGroupId",
                table: "Pur_Suppliers");

            migrationBuilder.DropColumn(
                name: "HasVariants",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "VariantOfId",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "IsRuleEvaluated",
                table: "Acc_BankTransactions");

            migrationBuilder.DropColumn(
                name: "MatchedTransactionRuleId",
                table: "Acc_BankTransactions");
        }
    }
}
