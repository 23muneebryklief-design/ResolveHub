
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketReferenceSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @nextValue BIGINT;

                SELECT @nextValue = ISNULL(
                    MAX(TRY_CONVERT(BIGINT, SUBSTRING([Reference], 5, 30))),
                    0
                ) + 1
                FROM dbo.Tickets
                WHERE [Reference] LIKE 'TKT-%'
                  AND TRY_CONVERT(BIGINT, SUBSTRING([Reference], 5, 30)) IS NOT NULL;

                DECLARE @sql NVARCHAR(MAX) =
                    N'CREATE SEQUENCE dbo.TicketReferenceSequence
                      AS BIGINT
                      START WITH ' + CONVERT(NVARCHAR(30), @nextValue) +
                    N' INCREMENT BY 1;';

                EXEC sys.sp_executesql @sql;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP SEQUENCE dbo.TicketReferenceSequence;");
        }
    }
}
