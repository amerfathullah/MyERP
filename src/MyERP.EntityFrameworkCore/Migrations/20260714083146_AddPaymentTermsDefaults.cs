using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTermsDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPaymentTermsTemplateId",
                table: "Sal_Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPaymentTermsTemplateId",
                table: "Pur_Suppliers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultPaymentTermsTemplateId",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "DefaultPaymentTermsTemplateId",
                table: "Pur_Suppliers");
        }
    }
}
