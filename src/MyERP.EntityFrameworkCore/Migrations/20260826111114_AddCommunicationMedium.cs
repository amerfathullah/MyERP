using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunicationMedium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comm_CommunicationMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommunicationMediumType = table.Column<int>(type: "integer", nullable: false),
                    CommunicationChannel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CatchAllEmployeeGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderSupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Comm_CommunicationMedia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Comm_CommunicationMediumTimeslots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunicationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    FromTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ToTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EmployeeGroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comm_CommunicationMediumTimeslots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comm_CommunicationMediumTimeslots_Comm_CommunicationMedia_C~",
                        column: x => x.CommunicationMediumId,
                        principalTable: "Comm_CommunicationMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comm_CommunicationMediumTimeslots_CommunicationMediumId",
                table: "Comm_CommunicationMediumTimeslots",
                column: "CommunicationMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_Comm_CommunicationMediumTimeslots_EmployeeGroupId",
                table: "Comm_CommunicationMediumTimeslots",
                column: "EmployeeGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comm_CommunicationMediumTimeslots");

            migrationBuilder.DropTable(
                name: "Comm_CommunicationMedia");
        }
    }
}
