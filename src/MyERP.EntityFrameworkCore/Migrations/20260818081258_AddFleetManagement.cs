using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ast_Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransporterId = table.Column<Guid>(type: "uuid", nullable: true),
                    CellNumber = table.Column<string>(type: "text", nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LicenseExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_Ast_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_DrivingLicenseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_Ast_DrivingLicenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicensePlate = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Make = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    ChassisNumber = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: true),
                    FuelType = table.Column<int>(type: "integer", nullable: false),
                    FuelUom = table.Column<string>(type: "text", nullable: true),
                    LastOdometer = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CarryingCapacity = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Wheels = table.Column<int>(type: "integer", nullable: true),
                    Doors = table.Column<int>(type: "integer", nullable: true),
                    VehicleValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AcquisitionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InsuranceCompany = table.Column<string>(type: "text", nullable: true),
                    PolicyNumber = table.Column<string>(type: "text", nullable: true),
                    InsuranceStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InsuranceEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RoadTaxExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FitnessCertificateExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_Ast_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_DriverLicenseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ast_DriverLicenseCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_DriverLicenseCategories_Ast_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Ast_Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_DriverLicenseCategories_DriverId_CategoryId",
                table: "Ast_DriverLicenseCategories",
                columns: new[] { "DriverId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Drivers_TenantId_CompanyId",
                table: "Ast_Drivers",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Drivers_TenantId_LicenseNumber",
                table: "Ast_Drivers",
                columns: new[] { "TenantId", "LicenseNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_DrivingLicenseCategories_TenantId_CategoryName",
                table: "Ast_DrivingLicenseCategories",
                columns: new[] { "TenantId", "CategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Vehicles_TenantId_CompanyId",
                table: "Ast_Vehicles",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Vehicles_TenantId_LicensePlate",
                table: "Ast_Vehicles",
                columns: new[] { "TenantId", "LicensePlate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ast_DriverLicenseCategories");

            migrationBuilder.DropTable(
                name: "Ast_DrivingLicenseCategories");

            migrationBuilder.DropTable(
                name: "Ast_Vehicles");

            migrationBuilder.DropTable(
                name: "Ast_Drivers");
        }
    }
}
