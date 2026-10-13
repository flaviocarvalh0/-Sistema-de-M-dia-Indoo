using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_HashECodigoComoVarchar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "codigo_pareamento",
                schema: "indoor",
                table: "tela",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(8)",
                oldFixedLength: true,
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "hash",
                schema: "indoor",
                table: "midia",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(64)",
                oldFixedLength: true,
                oldMaxLength: 64);

            // As tabelas de histórico são da plataforma (criadas na subida da
            // API, fora do EF) e copiaram o char(n). Só existem se a API já
            // subiu neste banco.
            migrationBuilder.Sql(ColunasHistoricas("character varying"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "codigo_pareamento",
                schema: "indoor",
                table: "tela",
                type: "character(8)",
                fixedLength: true,
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "hash",
                schema: "indoor",
                table: "midia",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.Sql(ColunasHistoricas("character"));
        }

        private static string ColunasHistoricas(string tipo) => $"""
            DO $$
            BEGIN
                IF to_regclass('history.indoor_midia_history') IS NOT NULL THEN
                    ALTER TABLE history.indoor_midia_history ALTER COLUMN hash TYPE {tipo}(64);
                END IF;

                IF to_regclass('history.indoor_tela_history') IS NOT NULL THEN
                    ALTER TABLE history.indoor_tela_history ALTER COLUMN codigo_pareamento TYPE {tipo}(8);
                END IF;
            END
            $$;
            """;
    }
}
