using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class InitializeMyERP4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ast_CapitalizationAssets_Ast_Capitalizations_AssetCapitaliz~",
                table: "Ast_CapitalizationAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_CapitalizationItems_Ast_Capitalizations_AssetCapitaliza~",
                table: "Ast_CapitalizationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_CapitalizationItems_Ast_Capitalizations_AssetCapitaliz~1",
                table: "Ast_CapitalizationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_Capitalizations_Ast_Assets_TargetAssetId",
                table: "Ast_Capitalizations");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_DepreciationDetails_Ast_Assets_AssetId",
                table: "Ast_DepreciationDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_DepreciationSchedule_Ast_Assets_AssetId",
                table: "Ast_DepreciationSchedule");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_Repairs_Ast_Assets_AssetId",
                table: "Ast_Repairs");

            migrationBuilder.DropIndex(
                name: "IX_AbpBackgroundJobs_IsAbandoned_NextTryTime",
                table: "AbpBackgroundJobs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_Repairs",
                table: "Ast_Repairs");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Repairs_AssetId",
                table: "Ast_Repairs");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Repairs_TenantId_AssetId_Status",
                table: "Ast_Repairs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_DepreciationSchedule",
                table: "Ast_DepreciationSchedule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_DepreciationDetails",
                table: "Ast_DepreciationDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_Categories",
                table: "Ast_Categories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_Capitalizations",
                table: "Ast_Capitalizations");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Capitalizations_TargetAssetId",
                table: "Ast_Capitalizations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_CapitalizationItems",
                table: "Ast_CapitalizationItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_CapitalizationAssets",
                table: "Ast_CapitalizationAssets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppQualityActions",
                table: "AppQualityActions");

            migrationBuilder.DropColumn(
                name: "StockItemConsumedCost",
                table: "Ast_Repairs");

            migrationBuilder.RenameTable(
                name: "Ast_Repairs",
                newName: "Ast_AssetRepairs");

            migrationBuilder.RenameTable(
                name: "Ast_DepreciationSchedule",
                newName: "Ast_DepreciationScheduleEntries");

            migrationBuilder.RenameTable(
                name: "Ast_DepreciationDetails",
                newName: "Ast_AssetDepreciationDetails");

            migrationBuilder.RenameTable(
                name: "Ast_Categories",
                newName: "Ast_AssetCategories");

            migrationBuilder.RenameTable(
                name: "Ast_Capitalizations",
                newName: "Ast_AssetCapitalizations");

            migrationBuilder.RenameTable(
                name: "Ast_CapitalizationItems",
                newName: "Ast_AssetCapitalizationItems");

            migrationBuilder.RenameTable(
                name: "Ast_CapitalizationAssets",
                newName: "Ast_AssetCapitalizationAssets");

            migrationBuilder.RenameTable(
                name: "AppQualityActions",
                newName: "Inv_QualityActions");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_DepreciationSchedule_AssetId_ScheduleDate",
                table: "Ast_DepreciationScheduleEntries",
                newName: "IX_Ast_DepreciationScheduleEntries_AssetId_ScheduleDate");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_DepreciationDetails_AssetId_FinanceBookId",
                table: "Ast_AssetDepreciationDetails",
                newName: "IX_Ast_AssetDepreciationDetails_AssetId_FinanceBookId");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_Categories_TenantId_CategoryName",
                table: "Ast_AssetCategories",
                newName: "IX_Ast_AssetCategories_TenantId_CategoryName");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_Capitalizations_TenantId_CompanyId_Status",
                table: "Ast_AssetCapitalizations",
                newName: "IX_Ast_AssetCapitalizations_TenantId_CompanyId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_CapitalizationItems_AssetCapitalizationId1",
                table: "Ast_AssetCapitalizationItems",
                newName: "IX_Ast_AssetCapitalizationItems_AssetCapitalizationId1");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_CapitalizationItems_AssetCapitalizationId",
                table: "Ast_AssetCapitalizationItems",
                newName: "IX_Ast_AssetCapitalizationItems_AssetCapitalizationId");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_CapitalizationAssets_AssetCapitalizationId",
                table: "Ast_AssetCapitalizationAssets",
                newName: "IX_Ast_AssetCapitalizationAssets_AssetCapitalizationId");

            migrationBuilder.RenameIndex(
                name: "IX_AppQualityActions_TenantId_CompanyId_Status",
                table: "Inv_QualityActions",
                newName: "IX_Inv_QualityActions_TenantId_CompanyId_Status");

            migrationBuilder.AlterColumn<string>(
                name: "Resolution",
                table: "Mnt_WarrantyClaims",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Complaint",
                table: "Mnt_WarrantyClaims",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcedureId",
                table: "Inv_QualityReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessOwner",
                table: "Inv_QualityProcedures",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Frequency",
                table: "Inv_QualityGoals",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<int>(
                name: "DayOfMonth",
                table: "Inv_QualityGoals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcedureId",
                table: "Inv_QualityGoals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Weekday",
                table: "Inv_QualityGoals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowAlternativeItem",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomsTariffNumberId",
                table: "Inv_Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultManufacturerId",
                table: "Inv_Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultManufacturerPartNo",
                table: "Inv_Items",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
                ALTER TABLE ""Ast_AssetMovements"" 
                ALTER COLUMN ""Purpose"" TYPE integer 
                USING (
                    CASE 
                        WHEN ""Purpose"" = 'Issue' THEN 0
                        WHEN ""Purpose"" = 'Receipt' THEN 1
                        WHEN ""Purpose"" = 'Transfer' THEN 2
                        WHEN ""Purpose"" = 'TransferAndIssue' THEN 3
                        WHEN ""Purpose"" ~ '^[0-9]+$' THEN ""Purpose""::integer
                        ELSE 0
                    END
                );
            ");

            migrationBuilder.AlterColumn<int>(
                name: "Purpose",
                table: "Ast_AssetMovements",
                type: "integer",
                maxLength: 500,
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MovementNumber",
                table: "Ast_AssetMovements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceId",
                table: "Ast_AssetMovements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "Ast_AssetMovements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransactionDate",
                table: "Ast_AssetMovements",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletionTime",
                table: "AbpBackgroundJobs",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RepairDescription",
                table: "Ast_AssetRepairs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RepairCost",
                table: "Ast_AssetRepairs",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "ActionsPerformed",
                table: "Ast_AssetRepairs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConsumedItemsCost",
                table: "Ast_AssetRepairs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Ast_AssetRepairs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Downtime",
                table: "Ast_AssetRepairs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Ast_AssetRepairs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepairNumber",
                table: "Ast_AssetRepairs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRepairCost",
                table: "Ast_AssetRepairs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DefaultFrequencyMonths",
                table: "Ast_AssetCategories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EnableCwipAccounting",
                table: "Ast_AssetCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NonDepreciableCategory",
                table: "Ast_AssetCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCapitalizedAmount",
                table: "Ast_AssetCapitalizations",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "TargetAssetName",
                table: "Ast_AssetCapitalizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "Ast_AssetCapitalizationItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Qty",
                table: "Ast_AssetCapitalizationItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<string>(
                name: "ItemName",
                table: "Ast_AssetCapitalizationItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetCapitalizationId",
                table: "Ast_AssetCapitalizationItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Ast_AssetCapitalizationItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentValue",
                table: "Ast_AssetCapitalizationAssets",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "AssetName",
                table: "Ast_AssetCapitalizationAssets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetCapitalizationId",
                table: "Ast_AssetCapitalizationAssets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Resolution",
                table: "Inv_QualityActions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedFeedbackId",
                table: "Inv_QualityActions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedProcedureId",
                table: "Inv_QualityActions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedQualityReviewId",
                table: "Inv_QualityActions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_AssetRepairs",
                table: "Ast_AssetRepairs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_DepreciationScheduleEntries",
                table: "Ast_DepreciationScheduleEntries",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_AssetDepreciationDetails",
                table: "Ast_AssetDepreciationDetails",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_AssetCategories",
                table: "Ast_AssetCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_AssetCapitalizations",
                table: "Ast_AssetCapitalizations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_AssetCapitalizationItems",
                table: "Ast_AssetCapitalizationItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_AssetCapitalizationAssets",
                table: "Ast_AssetCapitalizationAssets",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Inv_QualityActions",
                table: "Inv_QualityActions",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Acc_BankGuarantees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BgType = table.Column<int>(type: "integer", nullable: false),
                    ReferenceDocType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReferenceDocId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceDocName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidityDays = table.Column<int>(type: "integer", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Bank = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankAccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Account = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Iban = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BranchCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SwiftNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BankGuaranteeNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NameOfBeneficiary = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MarginMoney = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Charges = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    FixedDepositNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClausesAndConditions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_Acc_BankGuarantees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityType = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetCategoryAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FixedAssetAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccumulatedDepreciationAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepreciationExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapitalWorkInProgressAccountId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetCategoryAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_AssetCategoryAccounts_Ast_AssetCategories_AssetCategory~",
                        column: x => x.AssetCategoryId,
                        principalTable: "Ast_AssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetMaintenanceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetMaintenanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetMaintenanceTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MaintenanceTask = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignToEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignTo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActionsPerformed = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CertificateNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetMaintenanceLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetMaintenances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MaintenanceManagerId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaintenanceManagerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MaintenanceTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaintenanceTeamName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetMaintenances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetMovementItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TargetLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FromEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetMovementItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_AssetMovementItems_Ast_AssetMovements_AssetMovementId",
                        column: x => x.AssetMovementId,
                        principalTable: "Ast_AssetMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetRepairConsumedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetRepairId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Qty = table.Column<decimal>(type: "numeric", nullable: false),
                    ValuationRate = table.Column<decimal>(type: "numeric", nullable: false),
                    SerialAndBatchBundleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetRepairConsumedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_AssetRepairConsumedItems_Ast_AssetRepairs_AssetRepairId",
                        column: x => x.AssetRepairId,
                        principalTable: "Ast_AssetRepairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetRepairPurchaseInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetRepairId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseInvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RepairCost = table.Column<decimal>(type: "numeric", nullable: false),
                    ExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetRepairPurchaseInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_AssetRepairPurchaseInvoices_Ast_AssetRepairs_AssetRepai~",
                        column: x => x.AssetRepairId,
                        principalTable: "Ast_AssetRepairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetValueAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdjustmentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinanceBookId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CurrentAssetValue = table.Column<decimal>(type: "numeric", nullable: false),
                    NewAssetValue = table.Column<decimal>(type: "numeric", nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    DifferenceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetValueAdjustments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_CustomsTariffNumbers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TariffNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_Inv_CustomsTariffNumbers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_DeliveryTrips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    NamingSeries = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TripNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Driver = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DriverName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DriverEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DriverAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Vehicle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DepartureTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalDistance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EmailNotificationSent = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_DeliveryTrips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_ItemAlternatives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlternativeItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TwoWay = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_ItemAlternatives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_ItemManufacturers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturerPartNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_ItemManufacturers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_Manufacturers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Website = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Inv_Manufacturers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_NonConformances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessOwner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CorrectiveAction = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PreventiveAction = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolutionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_Inv_NonConformances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityActionResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Problem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ResolutionDetails = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityActionResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityActionResolutions_Inv_QualityActions_QualityActi~",
                        column: x => x.QualityActionId,
                        principalTable: "Inv_QualityActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    DocumentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Inv_QualityFeedbacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityFeedbackTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_Inv_QualityFeedbackTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityGoalObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityGoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Objective = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Target = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityGoalObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityGoalObjectives_Inv_QualityGoals_QualityGoalId",
                        column: x => x.QualityGoalId,
                        principalTable: "Inv_QualityGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Chairperson = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Attendees = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Inv_QualityMeetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityReviewObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Objective = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Target = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Actual = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityReviewObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityReviewObjectives_Inv_QualityReviews_QualityRevie~",
                        column: x => x.QualityReviewId,
                        principalTable: "Inv_QualityReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetMaintenanceTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetMaintenanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceTask = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NextDueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastCompletionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AssignToEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignTo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CertificateNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetMaintenanceTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_AssetMaintenanceTasks_Ast_AssetMaintenances_AssetMainte~",
                        column: x => x.AssetMaintenanceId,
                        principalTable: "Ast_AssetMaintenances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_DeliveryStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryTripId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CustomerAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Locked = table.Column<bool>(type: "boolean", nullable: false),
                    Visited = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryNoteNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ContactName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmailSentTo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CustomerContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Distance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EstimatedArrival = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Inv_DeliveryStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_DeliveryStops_Inv_DeliveryTrips_DeliveryTripId",
                        column: x => x.DeliveryTripId,
                        principalTable: "Inv_DeliveryTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityFeedbackParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityFeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    Parameter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityFeedbackParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityFeedbackParameters_Inv_QualityFeedbacks_QualityF~",
                        column: x => x.QualityFeedbackId,
                        principalTable: "Inv_QualityFeedbacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityFeedbackTemplateParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityFeedbackTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Parameter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityFeedbackTemplateParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityFeedbackTemplateParameters_Inv_QualityFeedbackTe~",
                        column: x => x.QualityFeedbackTemplateId,
                        principalTable: "Inv_QualityFeedbackTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityMeetingAgendas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityMeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Agenda = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityMeetingAgendas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityMeetingAgendas_Inv_QualityMeetings_QualityMeetin~",
                        column: x => x.QualityMeetingId,
                        principalTable: "Inv_QualityMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityMeetingMinutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityMeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Discussion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ActionPlan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_QualityMeetingMinutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityMeetingMinutes_Inv_QualityMeetings_QualityMeetin~",
                        column: x => x.QualityMeetingId,
                        principalTable: "Inv_QualityMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mnt_WarrantyClaims_TenantId_CompanyId_ClaimNumber",
                table: "Mnt_WarrantyClaims",
                columns: new[] { "TenantId", "CompanyId", "ClaimNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Mnt_WarrantyClaims_TenantId_CompanyId_CustomerId",
                table: "Mnt_WarrantyClaims",
                columns: new[] { "TenantId", "CompanyId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Mnt_WarrantyClaims_TenantId_CompanyId_ItemId",
                table: "Mnt_WarrantyClaims",
                columns: new[] { "TenantId", "CompanyId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Mnt_WarrantyClaims_TenantId_CompanyId_Status",
                table: "Mnt_WarrantyClaims",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_TenantId_CompanyId_AssetCategoryId",
                table: "Ast_Assets",
                columns: new[] { "TenantId", "CompanyId", "AssetCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_TenantId_CompanyId_AssetNumber",
                table: "Ast_Assets",
                columns: new[] { "TenantId", "CompanyId", "AssetNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_TenantId_CompanyId_Status",
                table: "Ast_Assets",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovements_TenantId_CompanyId_MovementNumber",
                table: "Ast_AssetMovements",
                columns: new[] { "TenantId", "CompanyId", "MovementNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovements_TenantId_CompanyId_Status",
                table: "Ast_AssetMovements",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AbpBackgroundJobs_ApplicationName_CompletionTime_IsAbandone~",
                table: "AbpBackgroundJobs",
                columns: new[] { "ApplicationName", "CompletionTime", "IsAbandoned", "NextTryTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetRepairs_TenantId_CompanyId_AssetId",
                table: "Ast_AssetRepairs",
                columns: new[] { "TenantId", "CompanyId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetRepairs_TenantId_CompanyId_RepairNumber",
                table: "Ast_AssetRepairs",
                columns: new[] { "TenantId", "CompanyId", "RepairNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetRepairs_TenantId_CompanyId_Status",
                table: "Ast_AssetRepairs",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetCapitalizations_TenantId_CompanyId_CapitalizationN~",
                table: "Ast_AssetCapitalizations",
                columns: new[] { "TenantId", "CompanyId", "CapitalizationNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankGuarantees_TenantId_CompanyId_BankGuaranteeNumber",
                table: "Acc_BankGuarantees",
                columns: new[] { "TenantId", "CompanyId", "BankGuaranteeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankGuarantees_TenantId_CompanyId_Status",
                table: "Acc_BankGuarantees",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetActivities_TenantId_AssetId_TransactionDate",
                table: "Ast_AssetActivities",
                columns: new[] { "TenantId", "AssetId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetCategoryAccounts_AssetCategoryId",
                table: "Ast_AssetCategoryAccounts",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetCategoryAccounts_TenantId_AssetCategoryId_CompanyId",
                table: "Ast_AssetCategoryAccounts",
                columns: new[] { "TenantId", "AssetCategoryId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMaintenanceLogs_TenantId_AssetMaintenanceTaskId",
                table: "Ast_AssetMaintenanceLogs",
                columns: new[] { "TenantId", "AssetMaintenanceTaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMaintenanceLogs_TenantId_CompanyId_AssetId",
                table: "Ast_AssetMaintenanceLogs",
                columns: new[] { "TenantId", "CompanyId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMaintenanceLogs_TenantId_CompanyId_Status",
                table: "Ast_AssetMaintenanceLogs",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMaintenances_TenantId_CompanyId_AssetId",
                table: "Ast_AssetMaintenances",
                columns: new[] { "TenantId", "CompanyId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMaintenanceTasks_AssetMaintenanceId",
                table: "Ast_AssetMaintenanceTasks",
                column: "AssetMaintenanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMaintenanceTasks_TenantId_AssetMaintenanceId",
                table: "Ast_AssetMaintenanceTasks",
                columns: new[] { "TenantId", "AssetMaintenanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovementItems_AssetMovementId",
                table: "Ast_AssetMovementItems",
                column: "AssetMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovementItems_TenantId_AssetId",
                table: "Ast_AssetMovementItems",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovementItems_TenantId_AssetMovementId",
                table: "Ast_AssetMovementItems",
                columns: new[] { "TenantId", "AssetMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetRepairConsumedItems_AssetRepairId",
                table: "Ast_AssetRepairConsumedItems",
                column: "AssetRepairId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetRepairConsumedItems_TenantId_AssetRepairId",
                table: "Ast_AssetRepairConsumedItems",
                columns: new[] { "TenantId", "AssetRepairId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetRepairConsumedItems_TenantId_ItemId",
                table: "Ast_AssetRepairConsumedItems",
                columns: new[] { "TenantId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetRepairPurchaseInvoices_AssetRepairId",
                table: "Ast_AssetRepairPurchaseInvoices",
                column: "AssetRepairId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetRepairPurchaseInvoices_TenantId_AssetRepairId",
                table: "Ast_AssetRepairPurchaseInvoices",
                columns: new[] { "TenantId", "AssetRepairId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetRepairPurchaseInvoices_TenantId_PurchaseInvoiceId",
                table: "Ast_AssetRepairPurchaseInvoices",
                columns: new[] { "TenantId", "PurchaseInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetValueAdjustments_TenantId_CompanyId_AdjustmentNumb~",
                table: "Ast_AssetValueAdjustments",
                columns: new[] { "TenantId", "CompanyId", "AdjustmentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetValueAdjustments_TenantId_CompanyId_AssetId",
                table: "Ast_AssetValueAdjustments",
                columns: new[] { "TenantId", "CompanyId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetValueAdjustments_TenantId_CompanyId_Status",
                table: "Ast_AssetValueAdjustments",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_CustomsTariffNumbers_TenantId_CompanyId_TariffNumber",
                table: "Inv_CustomsTariffNumbers",
                columns: new[] { "TenantId", "CompanyId", "TariffNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_DeliveryStops_DeliveryTripId",
                table: "Inv_DeliveryStops",
                column: "DeliveryTripId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_DeliveryStops_TenantId_DeliveryTripId",
                table: "Inv_DeliveryStops",
                columns: new[] { "TenantId", "DeliveryTripId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_DeliveryTrips_TenantId_CompanyId_Status",
                table: "Inv_DeliveryTrips",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_DeliveryTrips_TenantId_CompanyId_TripNumber",
                table: "Inv_DeliveryTrips",
                columns: new[] { "TenantId", "CompanyId", "TripNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemAlternatives_TenantId_CompanyId_AlternativeItemId",
                table: "Inv_ItemAlternatives",
                columns: new[] { "TenantId", "CompanyId", "AlternativeItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemAlternatives_TenantId_CompanyId_ItemId",
                table: "Inv_ItemAlternatives",
                columns: new[] { "TenantId", "CompanyId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemAlternatives_TenantId_CompanyId_ItemId_AlternativeI~",
                table: "Inv_ItemAlternatives",
                columns: new[] { "TenantId", "CompanyId", "ItemId", "AlternativeItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemManufacturers_TenantId_CompanyId_ItemId",
                table: "Inv_ItemManufacturers",
                columns: new[] { "TenantId", "CompanyId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemManufacturers_TenantId_CompanyId_ItemId_Manufacture~",
                table: "Inv_ItemManufacturers",
                columns: new[] { "TenantId", "CompanyId", "ItemId", "ManufacturerId", "ManufacturerPartNo" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemManufacturers_TenantId_CompanyId_ManufacturerId",
                table: "Inv_ItemManufacturers",
                columns: new[] { "TenantId", "CompanyId", "ManufacturerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Manufacturers_TenantId_CompanyId_ShortName",
                table: "Inv_Manufacturers",
                columns: new[] { "TenantId", "CompanyId", "ShortName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_NonConformances_TenantId_CompanyId_Status",
                table: "Inv_NonConformances",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_NonConformances_TenantId_ProcedureId",
                table: "Inv_NonConformances",
                columns: new[] { "TenantId", "ProcedureId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityActionResolutions_QualityActionId",
                table: "Inv_QualityActionResolutions",
                column: "QualityActionId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityFeedbackParameters_QualityFeedbackId",
                table: "Inv_QualityFeedbackParameters",
                column: "QualityFeedbackId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityFeedbacks_TenantId_CompanyId_TemplateId",
                table: "Inv_QualityFeedbacks",
                columns: new[] { "TenantId", "CompanyId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityFeedbackTemplateParameters_QualityFeedbackTempla~",
                table: "Inv_QualityFeedbackTemplateParameters",
                column: "QualityFeedbackTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityFeedbackTemplates_TenantId_TemplateName",
                table: "Inv_QualityFeedbackTemplates",
                columns: new[] { "TenantId", "TemplateName" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityGoalObjectives_QualityGoalId",
                table: "Inv_QualityGoalObjectives",
                column: "QualityGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityMeetingAgendas_QualityMeetingId",
                table: "Inv_QualityMeetingAgendas",
                column: "QualityMeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityMeetingMinutes_QualityMeetingId",
                table: "Inv_QualityMeetingMinutes",
                column: "QualityMeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityMeetings_TenantId_CompanyId_MeetingDate",
                table: "Inv_QualityMeetings",
                columns: new[] { "TenantId", "CompanyId", "MeetingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityMeetings_TenantId_CompanyId_Status",
                table: "Inv_QualityMeetings",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityReviewObjectives_QualityReviewId",
                table: "Inv_QualityReviewObjectives",
                column: "QualityReviewId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_AssetCapitalizationAssets_Ast_AssetCapitalizations_Asse~",
                table: "Ast_AssetCapitalizationAssets",
                column: "AssetCapitalizationId",
                principalTable: "Ast_AssetCapitalizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_AssetCapitalizationItems_Ast_AssetCapitalizations_Asset~",
                table: "Ast_AssetCapitalizationItems",
                column: "AssetCapitalizationId",
                principalTable: "Ast_AssetCapitalizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_AssetCapitalizationItems_Ast_AssetCapitalizations_Asse~1",
                table: "Ast_AssetCapitalizationItems",
                column: "AssetCapitalizationId1",
                principalTable: "Ast_AssetCapitalizations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_AssetDepreciationDetails_Ast_Assets_AssetId",
                table: "Ast_AssetDepreciationDetails",
                column: "AssetId",
                principalTable: "Ast_Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_DepreciationScheduleEntries_Ast_Assets_AssetId",
                table: "Ast_DepreciationScheduleEntries",
                column: "AssetId",
                principalTable: "Ast_Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ast_AssetCapitalizationAssets_Ast_AssetCapitalizations_Asse~",
                table: "Ast_AssetCapitalizationAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_AssetCapitalizationItems_Ast_AssetCapitalizations_Asset~",
                table: "Ast_AssetCapitalizationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_AssetCapitalizationItems_Ast_AssetCapitalizations_Asse~1",
                table: "Ast_AssetCapitalizationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_AssetDepreciationDetails_Ast_Assets_AssetId",
                table: "Ast_AssetDepreciationDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_DepreciationScheduleEntries_Ast_Assets_AssetId",
                table: "Ast_DepreciationScheduleEntries");

            migrationBuilder.DropTable(
                name: "Acc_BankGuarantees");

            migrationBuilder.DropTable(
                name: "Ast_AssetActivities");

            migrationBuilder.DropTable(
                name: "Ast_AssetCategoryAccounts");

            migrationBuilder.DropTable(
                name: "Ast_AssetMaintenanceLogs");

            migrationBuilder.DropTable(
                name: "Ast_AssetMaintenanceTasks");

            migrationBuilder.DropTable(
                name: "Ast_AssetMovementItems");

            migrationBuilder.DropTable(
                name: "Ast_AssetRepairConsumedItems");

            migrationBuilder.DropTable(
                name: "Ast_AssetRepairPurchaseInvoices");

            migrationBuilder.DropTable(
                name: "Ast_AssetValueAdjustments");

            migrationBuilder.DropTable(
                name: "Inv_CustomsTariffNumbers");

            migrationBuilder.DropTable(
                name: "Inv_DeliveryStops");

            migrationBuilder.DropTable(
                name: "Inv_ItemAlternatives");

            migrationBuilder.DropTable(
                name: "Inv_ItemManufacturers");

            migrationBuilder.DropTable(
                name: "Inv_Manufacturers");

            migrationBuilder.DropTable(
                name: "Inv_NonConformances");

            migrationBuilder.DropTable(
                name: "Inv_QualityActionResolutions");

            migrationBuilder.DropTable(
                name: "Inv_QualityFeedbackParameters");

            migrationBuilder.DropTable(
                name: "Inv_QualityFeedbackTemplateParameters");

            migrationBuilder.DropTable(
                name: "Inv_QualityGoalObjectives");

            migrationBuilder.DropTable(
                name: "Inv_QualityMeetingAgendas");

            migrationBuilder.DropTable(
                name: "Inv_QualityMeetingMinutes");

            migrationBuilder.DropTable(
                name: "Inv_QualityReviewObjectives");

            migrationBuilder.DropTable(
                name: "Ast_AssetMaintenances");

            migrationBuilder.DropTable(
                name: "Inv_DeliveryTrips");

            migrationBuilder.DropTable(
                name: "Inv_QualityFeedbacks");

            migrationBuilder.DropTable(
                name: "Inv_QualityFeedbackTemplates");

            migrationBuilder.DropTable(
                name: "Inv_QualityMeetings");

            migrationBuilder.DropIndex(
                name: "IX_Mnt_WarrantyClaims_TenantId_CompanyId_ClaimNumber",
                table: "Mnt_WarrantyClaims");

            migrationBuilder.DropIndex(
                name: "IX_Mnt_WarrantyClaims_TenantId_CompanyId_CustomerId",
                table: "Mnt_WarrantyClaims");

            migrationBuilder.DropIndex(
                name: "IX_Mnt_WarrantyClaims_TenantId_CompanyId_ItemId",
                table: "Mnt_WarrantyClaims");

            migrationBuilder.DropIndex(
                name: "IX_Mnt_WarrantyClaims_TenantId_CompanyId_Status",
                table: "Mnt_WarrantyClaims");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Assets_TenantId_CompanyId_AssetCategoryId",
                table: "Ast_Assets");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Assets_TenantId_CompanyId_AssetNumber",
                table: "Ast_Assets");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Assets_TenantId_CompanyId_Status",
                table: "Ast_Assets");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetMovements_TenantId_CompanyId_MovementNumber",
                table: "Ast_AssetMovements");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetMovements_TenantId_CompanyId_Status",
                table: "Ast_AssetMovements");

            migrationBuilder.DropIndex(
                name: "IX_AbpBackgroundJobs_ApplicationName_CompletionTime_IsAbandone~",
                table: "AbpBackgroundJobs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Inv_QualityActions",
                table: "Inv_QualityActions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_DepreciationScheduleEntries",
                table: "Ast_DepreciationScheduleEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_AssetRepairs",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetRepairs_TenantId_CompanyId_AssetId",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetRepairs_TenantId_CompanyId_RepairNumber",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetRepairs_TenantId_CompanyId_Status",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_AssetDepreciationDetails",
                table: "Ast_AssetDepreciationDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_AssetCategories",
                table: "Ast_AssetCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_AssetCapitalizations",
                table: "Ast_AssetCapitalizations");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetCapitalizations_TenantId_CompanyId_CapitalizationN~",
                table: "Ast_AssetCapitalizations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_AssetCapitalizationItems",
                table: "Ast_AssetCapitalizationItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ast_AssetCapitalizationAssets",
                table: "Ast_AssetCapitalizationAssets");

            migrationBuilder.DropColumn(
                name: "ProcedureId",
                table: "Inv_QualityReviews");

            migrationBuilder.DropColumn(
                name: "ProcessOwner",
                table: "Inv_QualityProcedures");

            migrationBuilder.DropColumn(
                name: "DayOfMonth",
                table: "Inv_QualityGoals");

            migrationBuilder.DropColumn(
                name: "ProcedureId",
                table: "Inv_QualityGoals");

            migrationBuilder.DropColumn(
                name: "Weekday",
                table: "Inv_QualityGoals");

            migrationBuilder.DropColumn(
                name: "AllowAlternativeItem",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "CustomsTariffNumberId",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "DefaultManufacturerId",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "DefaultManufacturerPartNo",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "MovementNumber",
                table: "Ast_AssetMovements");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "Ast_AssetMovements");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "Ast_AssetMovements");

            migrationBuilder.DropColumn(
                name: "TransactionDate",
                table: "Ast_AssetMovements");

            migrationBuilder.DropColumn(
                name: "CompletionTime",
                table: "AbpBackgroundJobs");

            migrationBuilder.DropColumn(
                name: "RelatedFeedbackId",
                table: "Inv_QualityActions");

            migrationBuilder.DropColumn(
                name: "RelatedProcedureId",
                table: "Inv_QualityActions");

            migrationBuilder.DropColumn(
                name: "RelatedQualityReviewId",
                table: "Inv_QualityActions");

            migrationBuilder.DropColumn(
                name: "ActionsPerformed",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropColumn(
                name: "ConsumedItemsCost",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropColumn(
                name: "Downtime",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropColumn(
                name: "RepairNumber",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropColumn(
                name: "TotalRepairCost",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropColumn(
                name: "DefaultFrequencyMonths",
                table: "Ast_AssetCategories");

            migrationBuilder.DropColumn(
                name: "EnableCwipAccounting",
                table: "Ast_AssetCategories");

            migrationBuilder.DropColumn(
                name: "NonDepreciableCategory",
                table: "Ast_AssetCategories");

            migrationBuilder.RenameTable(
                name: "Inv_QualityActions",
                newName: "AppQualityActions");

            migrationBuilder.RenameTable(
                name: "Ast_DepreciationScheduleEntries",
                newName: "Ast_DepreciationSchedule");

            migrationBuilder.RenameTable(
                name: "Ast_AssetRepairs",
                newName: "Ast_Repairs");

            migrationBuilder.RenameTable(
                name: "Ast_AssetDepreciationDetails",
                newName: "Ast_DepreciationDetails");

            migrationBuilder.RenameTable(
                name: "Ast_AssetCategories",
                newName: "Ast_Categories");

            migrationBuilder.RenameTable(
                name: "Ast_AssetCapitalizations",
                newName: "Ast_Capitalizations");

            migrationBuilder.RenameTable(
                name: "Ast_AssetCapitalizationItems",
                newName: "Ast_CapitalizationItems");

            migrationBuilder.RenameTable(
                name: "Ast_AssetCapitalizationAssets",
                newName: "Ast_CapitalizationAssets");

            migrationBuilder.RenameIndex(
                name: "IX_Inv_QualityActions_TenantId_CompanyId_Status",
                table: "AppQualityActions",
                newName: "IX_AppQualityActions_TenantId_CompanyId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_DepreciationScheduleEntries_AssetId_ScheduleDate",
                table: "Ast_DepreciationSchedule",
                newName: "IX_Ast_DepreciationSchedule_AssetId_ScheduleDate");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_AssetDepreciationDetails_AssetId_FinanceBookId",
                table: "Ast_DepreciationDetails",
                newName: "IX_Ast_DepreciationDetails_AssetId_FinanceBookId");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_AssetCategories_TenantId_CategoryName",
                table: "Ast_Categories",
                newName: "IX_Ast_Categories_TenantId_CategoryName");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_AssetCapitalizations_TenantId_CompanyId_Status",
                table: "Ast_Capitalizations",
                newName: "IX_Ast_Capitalizations_TenantId_CompanyId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_AssetCapitalizationItems_AssetCapitalizationId1",
                table: "Ast_CapitalizationItems",
                newName: "IX_Ast_CapitalizationItems_AssetCapitalizationId1");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_AssetCapitalizationItems_AssetCapitalizationId",
                table: "Ast_CapitalizationItems",
                newName: "IX_Ast_CapitalizationItems_AssetCapitalizationId");

            migrationBuilder.RenameIndex(
                name: "IX_Ast_AssetCapitalizationAssets_AssetCapitalizationId",
                table: "Ast_CapitalizationAssets",
                newName: "IX_Ast_CapitalizationAssets_AssetCapitalizationId");

            migrationBuilder.AlterColumn<string>(
                name: "Resolution",
                table: "Mnt_WarrantyClaims",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Complaint",
                table: "Mnt_WarrantyClaims",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Frequency",
                table: "Inv_QualityGoals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Purpose",
                table: "Ast_AssetMovements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Resolution",
                table: "AppQualityActions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RepairDescription",
                table: "Ast_Repairs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RepairCost",
                table: "Ast_Repairs",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "StockItemConsumedCost",
                table: "Ast_Repairs",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCapitalizedAmount",
                table: "Ast_Capitalizations",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "TargetAssetName",
                table: "Ast_Capitalizations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "Ast_CapitalizationItems",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "Qty",
                table: "Ast_CapitalizationItems",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "ItemName",
                table: "Ast_CapitalizationItems",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetCapitalizationId",
                table: "Ast_CapitalizationItems",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Ast_CapitalizationItems",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentValue",
                table: "Ast_CapitalizationAssets",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "AssetName",
                table: "Ast_CapitalizationAssets",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetCapitalizationId",
                table: "Ast_CapitalizationAssets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppQualityActions",
                table: "AppQualityActions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_DepreciationSchedule",
                table: "Ast_DepreciationSchedule",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_Repairs",
                table: "Ast_Repairs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_DepreciationDetails",
                table: "Ast_DepreciationDetails",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_Categories",
                table: "Ast_Categories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_Capitalizations",
                table: "Ast_Capitalizations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_CapitalizationItems",
                table: "Ast_CapitalizationItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ast_CapitalizationAssets",
                table: "Ast_CapitalizationAssets",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AbpBackgroundJobs_IsAbandoned_NextTryTime",
                table: "AbpBackgroundJobs",
                columns: new[] { "IsAbandoned", "NextTryTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Repairs_AssetId",
                table: "Ast_Repairs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Repairs_TenantId_AssetId_Status",
                table: "Ast_Repairs",
                columns: new[] { "TenantId", "AssetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Capitalizations_TargetAssetId",
                table: "Ast_Capitalizations",
                column: "TargetAssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_CapitalizationAssets_Ast_Capitalizations_AssetCapitaliz~",
                table: "Ast_CapitalizationAssets",
                column: "AssetCapitalizationId",
                principalTable: "Ast_Capitalizations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_CapitalizationItems_Ast_Capitalizations_AssetCapitaliza~",
                table: "Ast_CapitalizationItems",
                column: "AssetCapitalizationId",
                principalTable: "Ast_Capitalizations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_CapitalizationItems_Ast_Capitalizations_AssetCapitaliz~1",
                table: "Ast_CapitalizationItems",
                column: "AssetCapitalizationId1",
                principalTable: "Ast_Capitalizations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_Capitalizations_Ast_Assets_TargetAssetId",
                table: "Ast_Capitalizations",
                column: "TargetAssetId",
                principalTable: "Ast_Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_DepreciationDetails_Ast_Assets_AssetId",
                table: "Ast_DepreciationDetails",
                column: "AssetId",
                principalTable: "Ast_Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_DepreciationSchedule_Ast_Assets_AssetId",
                table: "Ast_DepreciationSchedule",
                column: "AssetId",
                principalTable: "Ast_Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_Repairs_Ast_Assets_AssetId",
                table: "Ast_Repairs",
                column: "AssetId",
                principalTable: "Ast_Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
