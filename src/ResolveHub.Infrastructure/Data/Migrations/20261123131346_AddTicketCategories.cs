
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Infrastructure.Data.Migrations
{
    public partial class AddTicketCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false),
                    Code = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: false),
                    Name = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false),
                    IsActive = table.Column<bool>(
                        type: "bit",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCategories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TicketCategories",
                columns: new[] { "Id", "Code", "Name", "IsActive" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), "ACCOUNT_ACCESS", "Account & Access", true },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), "BILLING_PAYMENTS", "Billing & Payments", true },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), "TECHNICAL_SUPPORT", "Technical Support", true },
                    { new Guid("a1000000-0000-0000-0000-000000000004"), "BUG_REPORT", "Bug Report", true },
                    { new Guid("a1000000-0000-0000-0000-000000000005"), "FEATURE_REQUEST", "Feature Request", true },
                    { new Guid("a1000000-0000-0000-0000-000000000006"), "GENERAL_ENQUIRY", "General Enquiry", true }
                });

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Tickets
                SET CategoryId = CASE
                    WHEN UPPER(LTRIM(RTRIM(Category))) IN ('ACCOUNT & ACCESS', 'ACCOUNT_ACCESS')
                        THEN 'a1000000-0000-0000-0000-000000000001'
                    WHEN UPPER(LTRIM(RTRIM(Category))) IN ('BILLING & PAYMENTS', 'BILLING_PAYMENTS')
                        THEN 'a1000000-0000-0000-0000-000000000002'
                    WHEN UPPER(LTRIM(RTRIM(Category))) IN ('TECHNICAL SUPPORT', 'TECHNICAL_SUPPORT')
                        THEN 'a1000000-0000-0000-0000-000000000003'
                    WHEN UPPER(LTRIM(RTRIM(Category))) IN ('BUG REPORT', 'BUG_REPORT')
                        THEN 'a1000000-0000-0000-0000-000000000004'
                    WHEN UPPER(LTRIM(RTRIM(Category))) IN ('FEATURE REQUEST', 'FEATURE_REQUEST')
                        THEN 'a1000000-0000-0000-0000-000000000005'
                    ELSE 'a1000000-0000-0000-0000-000000000006'
                END;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Tickets");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CategoryId",
                table: "Tickets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_Code",
                table: "TicketCategories",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketCategories_CategoryId",
                table: "Tickets",
                column: "CategoryId",
                principalTable: "TicketCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketCategories_CategoryId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CategoryId",
                table: "Tickets");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Tickets
                SET Category = TicketCategories.Name
                FROM Tickets
                INNER JOIN TicketCategories
                    ON Tickets.CategoryId = TicketCategories.Id;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketCategories");
        }
    }
}
