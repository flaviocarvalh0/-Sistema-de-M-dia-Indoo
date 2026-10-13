using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Platform_AdicionaControleConcorrenciaXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "platform",
                table: "user_session",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "platform",
                table: "system_parameter",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "platform",
                table: "system_module",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "platform",
                table: "system_menu_item",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "platform",
                table: "system_icon",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "platform",
                table: "smtp_configuration",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "platform",
                table: "entity_metadata",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "platform",
                table: "email_message",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "platform",
                table: "user_session");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "platform",
                table: "system_parameter");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "platform",
                table: "system_module");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "platform",
                table: "system_menu_item");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "platform",
                table: "system_icon");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "platform",
                table: "smtp_configuration");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "platform",
                table: "entity_metadata");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "platform",
                table: "email_message");
        }
    }
}
