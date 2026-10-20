using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_TetoArmazenamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventario_armazenamento",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    verificado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bytes_no_bucket = table.Column<long>(type: "bigint", nullable: false),
                    objetos = table.Column<int>(type: "integer", nullable: true),
                    orfaos_removidos = table.Column<int>(type: "integer", nullable: false),
                    bytes_orfaos_removidos = table.Column<long>(type: "bigint", nullable: false),
                    nivel_alerta_avisado = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_armazenamento", x => x.id);
                    table.CheckConstraint("ck_inventario_armazenamento_unico", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "reserva_envio",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chave = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reserva_envio", x => x.id);
                    table.CheckConstraint("ck_reserva_envio_tamanho", "tamanho_bytes > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_reserva_envio_chave",
                schema: "indoor",
                table: "reserva_envio",
                column: "chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reserva_envio_expira_em",
                schema: "indoor",
                table: "reserva_envio",
                column: "expira_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventario_armazenamento",
                schema: "indoor");

            migrationBuilder.DropTable(
                name: "reserva_envio",
                schema: "indoor");
        }
    }
}
