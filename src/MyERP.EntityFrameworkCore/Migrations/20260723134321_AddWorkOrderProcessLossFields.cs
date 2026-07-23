using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderProcessLossFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ProcessLossPercentage",
                table: "Mfg_WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessLossQty",
                table: "Mfg_WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "FinishedGoodItemId",
                table: "Mfg_JobCards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrective",
                table: "Mfg_JobCards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SemiFgBomId",
                table: "Mfg_JobCards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Inv_PickLists",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBatchNo",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSerialNo",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessLossPercentage",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "ProcessLossQty",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "FinishedGoodItemId",
                table: "Mfg_JobCards");

            migrationBuilder.DropColumn(
                name: "IsCorrective",
                table: "Mfg_JobCards");

            migrationBuilder.DropColumn(
                name: "SemiFgBomId",
                table: "Mfg_JobCards");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Inv_PickLists");

            migrationBuilder.DropColumn(
                name: "HasBatchNo",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "HasSerialNo",
                table: "Inv_Items");
        }
    }
}
