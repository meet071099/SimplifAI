using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentVerificationAPI.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Forms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniqueUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    RecruiterEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    VerificationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    IsBlurred = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsCorrectType = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    VerificationDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusColor = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonalInfo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalInfo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalInfo_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Forms",
                columns: new[] { "Id", "CreatedAt", "RecruiterEmail", "Status", "SubmittedAt", "UniqueUrl" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(7943), "recruiter1@test.com", "Pending", null, "test-form-1" });

            migrationBuilder.InsertData(
                table: "Forms",
                columns: new[] { "Id", "CreatedAt", "RecruiterEmail", "Status", "SubmittedAt", "UniqueUrl" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(7951), "recruiter2@test.com", "Pending", null, "test-form-2" });

            migrationBuilder.InsertData(
                table: "Documents",
                columns: new[] { "Id", "ConfidenceScore", "ContentType", "DocumentType", "FileName", "FilePath", "FileSize", "FormId", "IsCorrectType", "StatusColor", "UploadedAt", "VerificationDetails", "VerificationStatus" },
                values: new object[,]
                {
                    { new Guid("44444444-4444-4444-4444-444444444444"), 95.5m, "image/jpeg", "Passport", "passport_sample.jpg", "/uploads/test/passport_sample.jpg", 1024000L, new Guid("11111111-1111-1111-1111-111111111111"), true, "Green", new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(8315), null, "Verified" },
                    { new Guid("55555555-5555-5555-5555-555555555555"), 72.3m, "image/jpeg", "DriverLicense", "license_sample.jpg", "/uploads/test/license_sample.jpg", 856000L, new Guid("11111111-1111-1111-1111-111111111111"), true, "Yellow", new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(8326), null, "Verified" }
                });

            migrationBuilder.InsertData(
                table: "Documents",
                columns: new[] { "Id", "ConfidenceScore", "ContentType", "DocumentType", "FileName", "FilePath", "FileSize", "FormId", "IsBlurred", "StatusColor", "UploadedAt", "VerificationDetails", "VerificationStatus" },
                values: new object[] { new Guid("66666666-6666-6666-6666-666666666666"), 25.8m, "image/jpeg", "Passport", "blurred_passport.jpg", "/uploads/test/blurred_passport.jpg", 512000L, new Guid("22222222-2222-2222-2222-222222222222"), true, "Red", new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(8333), null, "Failed" });

            migrationBuilder.InsertData(
                table: "PersonalInfo",
                columns: new[] { "Id", "Address", "CreatedAt", "DateOfBirth", "Email", "FirstName", "FormId", "LastName", "Phone" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), "123 Test Street, Test City, TC 12345", new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(8262), new DateTime(1990, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "john.doe@test.com", "John", new Guid("11111111-1111-1111-1111-111111111111"), "Doe", "+1234567890" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FormId",
                table: "Documents",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_Forms_UniqueUrl",
                table: "Forms",
                column: "UniqueUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalInfo_FormId",
                table: "PersonalInfo",
                column: "FormId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "PersonalInfo");

            migrationBuilder.DropTable(
                name: "Forms");
        }
    }
}
