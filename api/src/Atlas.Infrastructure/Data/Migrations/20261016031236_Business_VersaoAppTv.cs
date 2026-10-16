using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_VersaoAppTv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "tv_de_teste",
                schema: "indoor",
                table: "tela",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "versao_codigo_app",
                schema: "indoor",
                table: "estado_tela",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "versao_app",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    chave_armazenamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    distribuicao = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versao_app", x => x.id);
                    table.CheckConstraint("ck_versao_app_codigo", "codigo > 0");
                    table.CheckConstraint("ck_versao_app_hash", "hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_versao_app_tamanho", "tamanho_bytes BETWEEN 1 AND 52428800");
                });

            migrationBuilder.CreateIndex(
                name: "ix_versao_app_chave_armazenamento",
                schema: "indoor",
                table: "versao_app",
                column: "chave_armazenamento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_versao_app_codigo",
                schema: "indoor",
                table: "versao_app",
                column: "codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "versao_app",
                schema: "indoor");

            migrationBuilder.DropColumn(
                name: "tv_de_teste",
                schema: "indoor",
                table: "tela");

            migrationBuilder.DropColumn(
                name: "versao_codigo_app",
                schema: "indoor",
                table: "estado_tela");
        }
    }
}
