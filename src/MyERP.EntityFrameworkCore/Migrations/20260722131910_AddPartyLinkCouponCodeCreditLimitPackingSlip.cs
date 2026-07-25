using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyLinkCouponCodeCreditLimitPackingSlip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountCategoryId",
                table: "Acc_Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Acc_AccountCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    RootType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Acc_AccountCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPartyLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryPartyType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PrimaryPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecondaryPartyType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SecondaryPartyId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppPartyLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_CouponCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CouponName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CouponType = table.Column<int>(type: "integer", nullable: false),
                    PricingRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaximumUse = table.Column<int>(type: "integer", nullable: false),
                    Used = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ValidUpto = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaximumUsePerCustomer = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Sal_CouponCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_CustomerCreditLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BypassCreditLimitCheck = table.Column<bool>(type: "boolean", nullable: false),
                    OverdueBillingThreshold = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_CustomerCreditLimits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PackingSlips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromCaseNo = table.Column<int>(type: "integer", nullable: false),
                    ToCaseNo = table.Column<int>(type: "integer", nullable: false),
                    NetWeight = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GrossWeight = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WeightUom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_Sal_PackingSlips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PackingSlipItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackingSlipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NetWeight = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeliveryNoteItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseInvoiceItemId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PackingSlipItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PackingSlipItems_Sal_PackingSlips_PackingSlipId",
                        column: x => x.PackingSlipId,
                        principalTable: "Sal_PackingSlips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_AccountCategories_TenantId_Name",
                table: "Acc_AccountCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPartyLinks_TenantId_PrimaryPartyType_PrimaryPartyId",
                table: "AppPartyLinks",
                columns: new[] { "TenantId", "PrimaryPartyType", "PrimaryPartyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPartyLinks_TenantId_SecondaryPartyType_SecondaryPartyId",
                table: "AppPartyLinks",
                columns: new[] { "TenantId", "SecondaryPartyType", "SecondaryPartyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_CouponCodes_TenantId_Code",
                table: "Sal_CouponCodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_CustomerCreditLimits_TenantId_CustomerId_CompanyId",
                table: "Sal_CustomerCreditLimits",
                columns: new[] { "TenantId", "CustomerId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PackingSlipItems_PackingSlipId",
                table: "Sal_PackingSlipItems",
                column: "PackingSlipId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PackingSlips_TenantId_DeliveryNoteId",
                table: "Sal_PackingSlips",
                columns: new[] { "TenantId", "DeliveryNoteId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_AccountCategories");

            migrationBuilder.DropTable(
                name: "AppPartyLinks");

            migrationBuilder.DropTable(
                name: "Sal_CouponCodes");

            migrationBuilder.DropTable(
                name: "Sal_CustomerCreditLimits");

            migrationBuilder.DropTable(
                name: "Sal_PackingSlipItems");

            migrationBuilder.DropTable(
                name: "Sal_PackingSlips");

            migrationBuilder.DropColumn(
                name: "AccountCategoryId",
                table: "Acc_Accounts");
        }
    }
}
