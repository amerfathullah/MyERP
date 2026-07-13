using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentScheduleEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_PaymentScheduleEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    InvoicePortion = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ModeOfPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_PaymentScheduleEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentScheduleEntries_ParentType_ParentId",
                table: "Acc_PaymentScheduleEntries",
                columns: new[] { "ParentType", "ParentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_PaymentScheduleEntries");
        }
    }
}
