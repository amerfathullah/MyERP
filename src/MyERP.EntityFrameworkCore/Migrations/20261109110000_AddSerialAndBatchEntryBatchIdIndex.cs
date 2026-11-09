using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSerialAndBatchEntryBatchIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialAndBatchEntries_TenantId_BatchId",
                table: "Inv_SerialAndBatchEntries",
                columns: new[] { "TenantId", "BatchId" },
                filter: "\"BatchId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inv_SerialAndBatchEntries_TenantId_BatchId",
                table: "Inv_SerialAndBatchEntries");
        }
    }
}
