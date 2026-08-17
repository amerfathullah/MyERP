using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentOrderAndUnreconcilePayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_PaymentOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PaymentOrderType = table.Column<int>(type: "integer", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyBankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AmendedFromId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Acc_PaymentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_PaymentOrders_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_UnreconcilePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherType = table.Column<int>(type: "integer", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Acc_UnreconcilePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_UnreconcilePayments_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_PaymentOrderReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModeOfPayment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_PaymentOrderReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_PaymentOrderReferences_Acc_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalTable: "Acc_PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_UnreconcilePaymentEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UnreconcilePaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgainstVoucherType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AgainstVoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Unlinked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_UnreconcilePaymentEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_UnreconcilePaymentEntries_Acc_UnreconcilePayments_Unrec~",
                        column: x => x.UnreconcilePaymentId,
                        principalTable: "Acc_UnreconcilePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentOrderReferences_PaymentOrderId",
                table: "Acc_PaymentOrderReferences",
                column: "PaymentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentOrders_CompanyId",
                table: "Acc_PaymentOrders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentOrders_TenantId_CompanyId_Status",
                table: "Acc_PaymentOrders",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_UnreconcilePaymentEntries_UnreconcilePaymentId",
                table: "Acc_UnreconcilePaymentEntries",
                column: "UnreconcilePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_UnreconcilePayments_CompanyId",
                table: "Acc_UnreconcilePayments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_UnreconcilePayments_TenantId_CompanyId_VoucherType_Vouc~",
                table: "Acc_UnreconcilePayments",
                columns: new[] { "TenantId", "CompanyId", "VoucherType", "VoucherId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_PaymentOrderReferences");

            migrationBuilder.DropTable(
                name: "Acc_UnreconcilePaymentEntries");

            migrationBuilder.DropTable(
                name: "Acc_PaymentOrders");

            migrationBuilder.DropTable(
                name: "Acc_UnreconcilePayments");
        }
    }
}
