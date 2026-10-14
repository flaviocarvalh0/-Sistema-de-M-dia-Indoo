using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_NoticiasClimaEComposicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duracao_noticia_segundos",
                schema: "indoor",
                table: "playlist",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<bool>(
                name: "embaralhar_anuncios",
                schema: "indoor",
                table: "playlist",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "exibir_clima",
                schema: "indoor",
                table: "playlist",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "exibir_relogio",
                schema: "indoor",
                table: "playlist",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "noticia_a_cada_anuncios",
                schema: "indoor",
                table: "playlist",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "posicao_fixa",
                schema: "indoor",
                table: "item_playlist",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "fonte_noticia",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    url_feed = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    credito = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    ultima_coleta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fonte_noticia", x => x.id);
                    table.CheckConstraint("ck_fonte_noticia_url", "url_feed ~ '^https?://'");
                });

            migrationBuilder.CreateTable(
                name: "previsao_clima",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parceiro_id = table.Column<long>(type: "bigint", nullable: false),
                    cidade_consultada = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    local = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    temperatura_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    minima_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    maxima_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    codigo_tempo = table.Column<int>(type: "integer", nullable: true),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_previsao_clima", x => x.id);
                    table.ForeignKey(
                        name: "fk_previsao_clima_parceiros_parceiro_id",
                        column: x => x.parceiro_id,
                        principalSchema: "business",
                        principalTable: "parceiro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "noticia",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fonte_noticia_id = table.Column<long>(type: "bigint", nullable: false),
                    identificador = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    publicada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    coletada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    chave_imagem = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tipo_conteudo_imagem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tamanho_imagem_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_imagem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_noticia", x => x.id);
                    table.CheckConstraint("ck_noticia_hash_imagem", "hash_imagem IS NULL OR hash_imagem ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_noticia_imagem_completa", "(chave_imagem IS NULL) = (hash_imagem IS NULL) AND (chave_imagem IS NULL) = (tamanho_imagem_bytes IS NULL) AND (chave_imagem IS NULL) = (tipo_conteudo_imagem IS NULL)");
                    table.ForeignKey(
                        name: "fk_noticia_fonte_noticia_fonte_noticia_id",
                        column: x => x.fonte_noticia_id,
                        principalSchema: "indoor",
                        principalTable: "fonte_noticia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_playlist_duracao_noticia",
                schema: "indoor",
                table: "playlist",
                sql: "duracao_noticia_segundos BETWEEN 5 AND 60");

            migrationBuilder.AddCheckConstraint(
                name: "ck_playlist_noticia_a_cada",
                schema: "indoor",
                table: "playlist",
                sql: "noticia_a_cada_anuncios BETWEEN 0 AND 20");

            migrationBuilder.CreateIndex(
                name: "ix_fonte_noticia_nome",
                schema: "indoor",
                table: "fonte_noticia",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fonte_noticia_url_feed",
                schema: "indoor",
                table: "fonte_noticia",
                column: "url_feed",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_noticia_fonte_noticia_id_identificador",
                schema: "indoor",
                table: "noticia",
                columns: new[] { "fonte_noticia_id", "identificador" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_noticia_publicada_em",
                schema: "indoor",
                table: "noticia",
                column: "publicada_em");

            migrationBuilder.CreateIndex(
                name: "ix_previsao_clima_parceiro_id",
                schema: "indoor",
                table: "previsao_clima",
                column: "parceiro_id",
                unique: true);

            // Fonte padrão: Agência Brasil (EBC), conteúdo Creative Commons BY,
            // livre para uso comercial com o crédito na tela.
            migrationBuilder.Sql("""
                INSERT INTO indoor.fonte_noticia (nome, url_feed, credito, ativo, created, last_modified)
                SELECT 'Agência Brasil', 'https://agenciabrasil.ebc.com.br/rss/ultimasnoticias/feed.xml',
                       'Fonte: Agência Brasil', true, now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM indoor.fonte_noticia);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "noticia",
                schema: "indoor");

            migrationBuilder.DropTable(
                name: "previsao_clima",
                schema: "indoor");

            migrationBuilder.DropTable(
                name: "fonte_noticia",
                schema: "indoor");

            migrationBuilder.DropCheckConstraint(
                name: "ck_playlist_duracao_noticia",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropCheckConstraint(
                name: "ck_playlist_noticia_a_cada",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropColumn(
                name: "duracao_noticia_segundos",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropColumn(
                name: "embaralhar_anuncios",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropColumn(
                name: "exibir_clima",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropColumn(
                name: "exibir_relogio",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropColumn(
                name: "noticia_a_cada_anuncios",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropColumn(
                name: "posicao_fixa",
                schema: "indoor",
                table: "item_playlist");
        }
    }
}
