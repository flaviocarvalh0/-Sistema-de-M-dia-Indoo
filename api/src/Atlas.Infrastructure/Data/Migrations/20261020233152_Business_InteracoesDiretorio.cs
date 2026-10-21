using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_InteracoesDiretorio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "interacao_diretorio",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tela_id = table.Column<long>(type: "bigint", nullable: false),
                    parceiro_id = table.Column<long>(type: "bigint", nullable: false),
                    evento = table.Column<int>(type: "integer", nullable: false),
                    cliente_id = table.Column<long>(type: "bigint", nullable: true),
                    contato = table.Column<int>(type: "integer", nullable: true),
                    momento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    visitante = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interacao_diretorio", x => x.id);
                    table.CheckConstraint("ck_interacao_diretorio_contato", "(evento = 2 AND cliente_id IS NOT NULL AND contato IS NOT NULL) OR (evento <> 2 AND cliente_id IS NULL AND contato IS NULL)");
                    table.CheckConstraint("ck_interacao_diretorio_evento", "evento IN (1, 2, 3)");
                    table.CheckConstraint("ck_interacao_diretorio_visitante", "visitante ~ '^[0-9a-f]{16}$'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_interacao_diretorio_cliente_id_momento",
                schema: "indoor",
                table: "interacao_diretorio",
                columns: new[] { "cliente_id", "momento" });

            migrationBuilder.CreateIndex(
                name: "ix_interacao_diretorio_tela_id_momento",
                schema: "indoor",
                table: "interacao_diretorio",
                columns: new[] { "tela_id", "momento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interacao_diretorio",
                schema: "indoor");
        }
    }
}
