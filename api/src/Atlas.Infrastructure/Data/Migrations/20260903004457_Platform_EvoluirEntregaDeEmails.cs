using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Platform_EvoluirEntregaDeEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "to",
                schema: "platform",
                table: "email_message");

            migrationBuilder.RenameColumn(
                name: "body",
                schema: "platform",
                table: "email_message",
                newName: "html_body");

            migrationBuilder.RenameIndex(
                name: "IX_email_message_related_entity",
                schema: "platform",
                table: "email_message",
                newName: "ix_email_message_related_entity");

            migrationBuilder.CreateTable(
                name: "email_message_attachment",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email_message_id = table.Column<long>(type: "bigint", nullable: false),
                    file_content_id = table.Column<long>(type: "bigint", nullable: false),
                    disposition = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    content_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_message_attachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_message_attachment_email_messages_email_message_id",
                        column: x => x.email_message_id,
                        principalSchema: "platform",
                        principalTable: "email_message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_email_message_attachment_file_contents_file_content_id",
                        column: x => x.file_content_id,
                        principalSchema: "platform",
                        principalTable: "file_content",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_message_recipient",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email_message_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_message_recipient", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_message_recipient_email_message_email_message_id",
                        column: x => x.email_message_id,
                        principalSchema: "platform",
                        principalTable: "email_message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_message_status_created",
                schema: "platform",
                table: "email_message",
                columns: new[] { "status", "created" });

            migrationBuilder.CreateIndex(
                name: "ix_email_message_attachment_file_content_id",
                schema: "platform",
                table: "email_message_attachment",
                column: "file_content_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_message_attachment_message_file",
                schema: "platform",
                table: "email_message_attachment",
                columns: new[] { "email_message_id", "file_content_id" });

            migrationBuilder.CreateIndex(
                name: "ux_email_message_attachment_message_content_id",
                schema: "platform",
                table: "email_message_attachment",
                columns: new[] { "email_message_id", "content_id" },
                unique: true,
                filter: "\"content_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_email_message_recipient_message_type",
                schema: "platform",
                table: "email_message_recipient",
                columns: new[] { "email_message_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_email_message_recipient_normalized_email",
                schema: "platform",
                table: "email_message_recipient",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ux_email_message_recipient_message_email",
                schema: "platform",
                table: "email_message_recipient",
                columns: new[] { "email_message_id", "normalized_email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_message_attachment",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "email_message_recipient",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "ix_email_message_status_created",
                schema: "platform",
                table: "email_message");

            migrationBuilder.RenameColumn(
                name: "html_body",
                schema: "platform",
                table: "email_message",
                newName: "body");

            migrationBuilder.RenameIndex(
                name: "ix_email_message_related_entity",
                schema: "platform",
                table: "email_message",
                newName: "IX_email_message_related_entity");

            migrationBuilder.AddColumn<string>(
                name: "to",
                schema: "platform",
                table: "email_message",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }
    }
}
