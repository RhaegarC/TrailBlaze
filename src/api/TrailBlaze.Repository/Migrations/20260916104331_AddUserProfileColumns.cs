using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailBlaze.Repository.Migrations
{
    /// <summary>
    /// Bounds the three profile columns that were <c>nvarchar(max)</c> by convention, and adds the
    /// four the profile slice needs.
    /// </summary>
    /// <remarks>
    /// <para><b>The three <c>AlterColumn</c> calls can fail against a populated table.</b> SQL
    /// Server refuses to narrow a column below the longest value already in it, so this migration
    /// succeeds or fails on the data it finds: an existing <c>DisplayName</c>, <c>Description</c>
    /// or <c>Role</c> longer than the new bound stops the deploy with "String or binary data would
    /// be truncated". That was worth taking — an unbounded column can reject nothing, so the
    /// application-level truncation rule these lengths exist to support had nothing to truncate
    /// to. No environment is deployed yet, so today it applies to an empty table; a database that
    /// predates it would need the over-long values shortened first.</para>
    ///
    /// <para>The two preference columns are added non-nullable with a default, so existing rows
    /// are backfilled to <c>Dark</c>/<c>en</c> by SQL Server as part of the same statement rather
    /// than by a separate update.</para>
    /// </remarks>
    public partial class AddUserProfileColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarBlobPath",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "PreferredTheme",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Dark");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarBlobPath",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredTheme",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
