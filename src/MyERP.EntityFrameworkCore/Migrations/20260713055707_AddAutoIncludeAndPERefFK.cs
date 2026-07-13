using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoIncludeAndPERefFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Acc_PaymentEntryReferences_Acc_PaymentEntries_PaymentEntryId",
                table: "Acc_PaymentEntryReferences",
                column: "PaymentEntryId",
                principalTable: "Acc_PaymentEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Acc_PaymentEntryReferences_Acc_PaymentEntries_PaymentEntryId",
                table: "Acc_PaymentEntryReferences");
        }
    }
}
