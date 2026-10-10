using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityUtmResponseTimeAndConversionRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CampaignName",
                table: "CRM_Opportunities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionRate",
                table: "CRM_Opportunities",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstRespondedOn",
                table: "CRM_Opportunities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FirstResponseTime",
                table: "CRM_Opportunities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmCampaign",
                table: "CRM_Opportunities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmMedium",
                table: "CRM_Opportunities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmSource",
                table: "CRM_Opportunities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampaignName",
                table: "CRM_Opportunities");

            migrationBuilder.DropColumn(
                name: "ConversionRate",
                table: "CRM_Opportunities");

            migrationBuilder.DropColumn(
                name: "FirstRespondedOn",
                table: "CRM_Opportunities");

            migrationBuilder.DropColumn(
                name: "FirstResponseTime",
                table: "CRM_Opportunities");

            migrationBuilder.DropColumn(
                name: "UtmCampaign",
                table: "CRM_Opportunities");

            migrationBuilder.DropColumn(
                name: "UtmMedium",
                table: "CRM_Opportunities");

            migrationBuilder.DropColumn(
                name: "UtmSource",
                table: "CRM_Opportunities");
        }
    }
}
