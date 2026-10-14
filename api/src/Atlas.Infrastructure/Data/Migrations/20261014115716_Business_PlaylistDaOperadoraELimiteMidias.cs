using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_PlaylistDaOperadoraELimiteMidias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_playlist_parceiros_parceiro_id",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropIndex(
                name: "ix_playlist_parceiro_id_nome",
                schema: "indoor",
                table: "playlist");

            // A playlist passa a ser da rede, com nome único. Dois parceiros
            // podiam ter playlists com o mesmo nome: a repetida ganha o nome do
            // parceiro e, se ainda repetir, o id.
            migrationBuilder.Sql("""
                UPDATE indoor.playlist p
                   SET nome = left(p.nome || ' (' || par.nome || ')', 100)
                  FROM business.parceiro par
                 WHERE par.id = p.parceiro_id
                   AND EXISTS (SELECT 1 FROM indoor.playlist o WHERE o.nome = p.nome AND o.id <> p.id);

                UPDATE indoor.playlist p
                   SET nome = left(p.nome, 90) || ' #' || p.id
                 WHERE EXISTS (SELECT 1 FROM indoor.playlist o WHERE o.nome = p.nome AND o.id <> p.id);
                """);

            migrationBuilder.DropColumn(
                name: "parceiro_id",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.AddColumn<int>(
                name: "limite_midias_ativas",
                schema: "business",
                table: "parceiro",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_playlist_nome",
                schema: "indoor",
                table: "playlist",
                column: "nome",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_parceiro_limite_midias_ativas",
                schema: "business",
                table: "parceiro",
                sql: "limite_midias_ativas BETWEEN 1 AND 1000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_playlist_nome",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_limite_midias_ativas",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "limite_midias_ativas",
                schema: "business",
                table: "parceiro");

            migrationBuilder.AddColumn<long>(
                name: "parceiro_id",
                schema: "indoor",
                table: "playlist",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Volta como do primeiro parceiro: a informação de dono não existe mais.
            migrationBuilder.Sql("UPDATE indoor.playlist SET parceiro_id = (SELECT min(id) FROM business.parceiro);");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_parceiro_id_nome",
                schema: "indoor",
                table: "playlist",
                columns: new[] { "parceiro_id", "nome" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_playlist_parceiros_parceiro_id",
                schema: "indoor",
                table: "playlist",
                column: "parceiro_id",
                principalSchema: "business",
                principalTable: "parceiro",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
