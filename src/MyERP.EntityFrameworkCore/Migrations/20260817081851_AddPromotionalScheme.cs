using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionalScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PromotionalSchemeId",
                table: "Sal_PricingRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PromotionalSchemeSlabId",
                table: "Sal_PricingRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sal_PromotionalSchemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    ApplyOn = table.Column<int>(type: "integer", nullable: false),
                    MixedConditions = table.Column<bool>(type: "boolean", nullable: false),
                    IsCumulative = table.Column<bool>(type: "boolean", nullable: false),
                    ApplyRuleOnOtherItem = table.Column<bool>(type: "boolean", nullable: false),
                    OtherApplyOn = table.Column<int>(type: "integer", nullable: true),
                    OtherTargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Selling = table.Column<bool>(type: "boolean", nullable: false),
                    Buying = table.Column<bool>(type: "boolean", nullable: false),
                    ApplicableFor = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ValidUpto = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Sal_PromotionalSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PromotionalSchemes_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PromotionalSchemeParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionalSchemeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PromotionalSchemeParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PromotionalSchemeParties_Sal_PromotionalSchemes_Promoti~",
                        column: x => x.PromotionalSchemeId,
                        principalTable: "Sal_PromotionalSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PromotionalSchemePriceDiscountSlabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionalSchemeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RateOrDiscount = table.Column<int>(type: "integer", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MinQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MaxQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PromotionalSchemePriceDiscountSlabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PromotionalSchemePriceDiscountSlabs_Sal_PromotionalSche~",
                        column: x => x.PromotionalSchemeId,
                        principalTable: "Sal_PromotionalSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PromotionalSchemeProductDiscountSlabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionalSchemeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreeItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreeQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    FreeItemRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SameItem = table.Column<bool>(type: "boolean", nullable: false),
                    MinQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MaxQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsRecursive = table.Column<bool>(type: "boolean", nullable: false),
                    RecurseFor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RoundFreeQty = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PromotionalSchemeProductDiscountSlabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PromotionalSchemeProductDiscountSlabs_Inv_Items_FreeIte~",
                        column: x => x.FreeItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_PromotionalSchemeProductDiscountSlabs_Sal_PromotionalSc~",
                        column: x => x.PromotionalSchemeId,
                        principalTable: "Sal_PromotionalSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PromotionalSchemeTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionalSchemeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PromotionalSchemeTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PromotionalSchemeTargets_Sal_PromotionalSchemes_Promoti~",
                        column: x => x.PromotionalSchemeId,
                        principalTable: "Sal_PromotionalSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PricingRules_PromotionalSchemeId",
                table: "Sal_PricingRules",
                column: "PromotionalSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PromotionalSchemeParties_PromotionalSchemeId",
                table: "Sal_PromotionalSchemeParties",
                column: "PromotionalSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PromotionalSchemePriceDiscountSlabs_PromotionalSchemeId",
                table: "Sal_PromotionalSchemePriceDiscountSlabs",
                column: "PromotionalSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PromotionalSchemeProductDiscountSlabs_FreeItemId",
                table: "Sal_PromotionalSchemeProductDiscountSlabs",
                column: "FreeItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PromotionalSchemeProductDiscountSlabs_PromotionalScheme~",
                table: "Sal_PromotionalSchemeProductDiscountSlabs",
                column: "PromotionalSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PromotionalSchemes_CompanyId",
                table: "Sal_PromotionalSchemes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PromotionalSchemes_TenantId_CompanyId_IsDisabled",
                table: "Sal_PromotionalSchemes",
                columns: new[] { "TenantId", "CompanyId", "IsDisabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PromotionalSchemeTargets_PromotionalSchemeId",
                table: "Sal_PromotionalSchemeTargets",
                column: "PromotionalSchemeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sal_PromotionalSchemeParties");

            migrationBuilder.DropTable(
                name: "Sal_PromotionalSchemePriceDiscountSlabs");

            migrationBuilder.DropTable(
                name: "Sal_PromotionalSchemeProductDiscountSlabs");

            migrationBuilder.DropTable(
                name: "Sal_PromotionalSchemeTargets");

            migrationBuilder.DropTable(
                name: "Sal_PromotionalSchemes");

            migrationBuilder.DropIndex(
                name: "IX_Sal_PricingRules_PromotionalSchemeId",
                table: "Sal_PricingRules");

            migrationBuilder.DropColumn(
                name: "PromotionalSchemeId",
                table: "Sal_PricingRules");

            migrationBuilder.DropColumn(
                name: "PromotionalSchemeSlabId",
                table: "Sal_PricingRules");
        }
    }
}
