using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderBatchAvailabilityAndCampaignEfficiency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AvailableQtyAtSourceWarehouse",
                table: "Mfg_WorkOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AvailableQtyAtWipWarehouse",
                table: "Mfg_WorkOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CRM_ProspectLeads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CampaignName",
                table: "CRM_Leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmCampaign",
                table: "CRM_Leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmMedium",
                table: "CRM_Leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmSource",
                table: "CRM_Leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableQtyAtSourceWarehouse",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "AvailableQtyAtWipWarehouse",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CRM_ProspectLeads");

            migrationBuilder.DropColumn(
                name: "CampaignName",
                table: "CRM_Leads");

            migrationBuilder.DropColumn(
                name: "UtmCampaign",
                table: "CRM_Leads");

            migrationBuilder.DropColumn(
                name: "UtmMedium",
                table: "CRM_Leads");

            migrationBuilder.DropColumn(
                name: "UtmSource",
                table: "CRM_Leads");
        }
    }
}
