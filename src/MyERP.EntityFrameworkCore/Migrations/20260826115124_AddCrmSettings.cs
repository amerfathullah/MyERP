using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CRM_Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignNamingBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AllowLeadDuplicationBasedOnEmails = table.Column<bool>(type: "boolean", nullable: false),
                    AutoCreationOfContact = table.Column<bool>(type: "boolean", nullable: false),
                    CloseOpportunityAfterDays = table.Column<int>(type: "integer", nullable: false),
                    EnableOpportunityCreationFromContactUs = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultQuotationValidityDays = table.Column<int>(type: "integer", nullable: false),
                    CarryForwardCommunicationAndComments = table.Column<bool>(type: "boolean", nullable: false),
                    UpdateTimestampOnNewCommunication = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_CRM_Settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CRM_Settings");
        }
    }
}
