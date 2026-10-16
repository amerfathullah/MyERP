using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationLostReasonDetailAndOrderLostReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderLostReason",
                table: "Sal_Quotations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sal_QuotationLostReasonDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotationLostReasonId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_QuotationLostReasonDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_QuotationLostReasonDetails_Sal_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Sal_Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_QuotationLostReasonDetails_Sls_QuotationLostReasons_Quo~",
                        column: x => x.QuotationLostReasonId,
                        principalTable: "Sls_QuotationLostReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_QuotationLostReasonDetails_QuotationId",
                table: "Sal_QuotationLostReasonDetails",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_QuotationLostReasonDetails_QuotationLostReasonId",
                table: "Sal_QuotationLostReasonDetails",
                column: "QuotationLostReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_QuotationLostReasonDetails_TenantId_QuotationId_Quotati~",
                table: "Sal_QuotationLostReasonDetails",
                columns: new[] { "TenantId", "QuotationId", "QuotationLostReasonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sal_QuotationLostReasonDetails");

            migrationBuilder.DropColumn(
                name: "OrderLostReason",
                table: "Sal_Quotations");
        }
    }
}
