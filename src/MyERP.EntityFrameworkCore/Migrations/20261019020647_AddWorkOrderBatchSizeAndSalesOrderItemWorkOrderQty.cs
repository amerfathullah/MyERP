using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderBatchSizeAndSalesOrderItemWorkOrderQty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ProducedQty",
                table: "Sal_SalesOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkOrderQty",
                table: "Sal_SalesOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BatchSize",
                table: "Mfg_WorkOrders",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProducedQty",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "WorkOrderQty",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "BatchSize",
                table: "Mfg_WorkOrders");
        }
    }
}
