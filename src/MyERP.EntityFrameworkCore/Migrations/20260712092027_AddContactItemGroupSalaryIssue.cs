using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddContactItemGroupSalaryIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ItemGroupId",
                table: "Inv_Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Salutation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MobileNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimaryContact = table.Column<bool>(type: "boolean", nullable: false),
                    IsBillingContact = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppContacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hr_SalaryComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ComponentType = table.Column<int>(type: "integer", nullable: false),
                    IsStatutory = table.Column<bool>(type: "boolean", nullable: false),
                    IsTaxApplicable = table.Column<bool>(type: "boolean", nullable: false),
                    DependsOnPaymentDays = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Hr_SalaryComponents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hr_SalaryStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsHourlyBased = table.Column<bool>(type: "boolean", nullable: false),
                    PayrollFrequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Hr_SalaryStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_SalaryStructures_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_ItemGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultIncomeAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_ItemGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sup_Issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssueType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uuid", nullable: true),
                    RaisedVia = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OpeningDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ResolutionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FirstResponseTime = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ResolutionTime = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    FirstRespondedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TotalHoldTime = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_Sup_Issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sup_Issues_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hr_SalaryStructureDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalaryStructureId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalaryComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Formula = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsFormulaBasedAmount = table.Column<bool>(type: "boolean", nullable: false),
                    ComponentType = table.Column<int>(type: "integer", nullable: false),
                    IsStatisticalComponent = table.Column<bool>(type: "boolean", nullable: false),
                    Condition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hr_SalaryStructureDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_SalaryStructureDetails_Hr_SalaryStructures_SalaryStructu~",
                        column: x => x.SalaryStructureId,
                        principalTable: "Hr_SalaryStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppContacts_TenantId_PartyType_PartyId",
                table: "AppContacts",
                columns: new[] { "TenantId", "PartyType", "PartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppContacts_TenantId_PartyType_PartyId_IsPrimaryContact",
                table: "AppContacts",
                columns: new[] { "TenantId", "PartyType", "PartyId", "IsPrimaryContact" });

            migrationBuilder.CreateIndex(
                name: "IX_Hr_SalaryComponents_TenantId_Name",
                table: "Hr_SalaryComponents",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hr_SalaryStructureDetails_SalaryStructureId",
                table: "Hr_SalaryStructureDetails",
                column: "SalaryStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_SalaryStructures_CompanyId",
                table: "Hr_SalaryStructures",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_SalaryStructures_TenantId_CompanyId_Name",
                table: "Hr_SalaryStructures",
                columns: new[] { "TenantId", "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemGroups_TenantId_Name",
                table: "Inv_ItemGroups",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sup_Issues_CompanyId",
                table: "Sup_Issues",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sup_Issues_TenantId_CustomerId_Status",
                table: "Sup_Issues",
                columns: new[] { "TenantId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sup_Issues_TenantId_Status",
                table: "Sup_Issues",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppContacts");

            migrationBuilder.DropTable(
                name: "Hr_SalaryComponents");

            migrationBuilder.DropTable(
                name: "Hr_SalaryStructureDetails");

            migrationBuilder.DropTable(
                name: "Inv_ItemGroups");

            migrationBuilder.DropTable(
                name: "Sup_Issues");

            migrationBuilder.DropTable(
                name: "Hr_SalaryStructures");

            migrationBuilder.DropColumn(
                name: "ItemGroupId",
                table: "Inv_Items");
        }
    }
}
