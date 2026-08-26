using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyExchangeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_CurrencyExchangeSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApiEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AccessKey = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UseHttp = table.Column<bool>(type: "boolean", nullable: false),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Acc_CurrencyExchangeSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Acc_CurrencyExchangeSettingsDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_CurrencyExchangeSettingsDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_CurrencyExchangeSettingsDetails_Acc_CurrencyExchangeSet~",
                        column: x => x.SettingsId,
                        principalTable: "Acc_CurrencyExchangeSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_CurrencyExchangeSettingsResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_CurrencyExchangeSettingsResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_CurrencyExchangeSettingsResults_Acc_CurrencyExchangeSet~",
                        column: x => x.SettingsId,
                        principalTable: "Acc_CurrencyExchangeSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_CurrencyExchangeSettingsDetails_SettingsId",
                table: "Acc_CurrencyExchangeSettingsDetails",
                column: "SettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_CurrencyExchangeSettingsResults_SettingsId",
                table: "Acc_CurrencyExchangeSettingsResults",
                column: "SettingsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_CurrencyExchangeSettingsDetails");

            migrationBuilder.DropTable(
                name: "Acc_CurrencyExchangeSettingsResults");

            migrationBuilder.DropTable(
                name: "Acc_CurrencyExchangeSettings");
        }
    }
}
