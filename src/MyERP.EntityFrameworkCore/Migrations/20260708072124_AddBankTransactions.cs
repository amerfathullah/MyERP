using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBankTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_BankTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsReconciled = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedDocumentRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReconciledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_Acc_BankTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_BankTransactions_Acc_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_BankTransactions_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankTransactions_BankAccountId",
                table: "Acc_BankTransactions",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankTransactions_CompanyId",
                table: "Acc_BankTransactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankTransactions_TenantId_BankAccountId_IsReconciled",
                table: "Acc_BankTransactions",
                columns: new[] { "TenantId", "BankAccountId", "IsReconciled" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BankTransactions_TenantId_BankAccountId_TransactionDate",
                table: "Acc_BankTransactions",
                columns: new[] { "TenantId", "BankAccountId", "TransactionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_BankTransactions");
        }
    }
}
