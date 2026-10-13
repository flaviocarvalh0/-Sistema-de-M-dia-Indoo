using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_CriarEstruturaIndoor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "indoor");

            migrationBuilder.EnsureSchema(
                name: "business");

            migrationBuilder.CreateTable(
                name: "parceiro",
                schema: "business",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    nome_responsavel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fuso_horario = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "America/Sao_Paulo"),
                    cota_armazenamento_mb = table.Column<int>(type: "integer", nullable: false, defaultValue: 200),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    motivo_bloqueio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    observacao_interna = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parceiro", x => x.id);
                    table.CheckConstraint("ck_parceiro_cota", "cota_armazenamento_mb BETWEEN 1 AND 1048576");
                    table.CheckConstraint("ck_parceiro_documento", "documento IS NULL OR documento ~ '^[0-9]{11}$' OR documento ~ '^[0-9A-Z]{12}[0-9]{2}$'");
                    table.CheckConstraint("ck_parceiro_motivo_bloqueio", "situacao <> 2 OR motivo_bloqueio IS NOT NULL");
                    table.CheckConstraint("ck_parceiro_telefone", "telefone IS NULL OR telefone ~ '^[0-9]{10,11}$'");
                });

            migrationBuilder.CreateTable(
                name: "midia",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parceiro_id = table.Column<long>(type: "bigint", nullable: false),
                    titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    chave_armazenamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo_conteudo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    duracao_segundos = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                    inicio_exibicao = table.Column<DateOnly>(type: "date", nullable: true),
                    fim_exibicao = table.Column<DateOnly>(type: "date", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_midia", x => x.id);
                    table.CheckConstraint("ck_midia_duracao", "duracao_segundos BETWEEN 1 AND 3600");
                    table.CheckConstraint("ck_midia_hash", "hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_midia_periodo", "inicio_exibicao IS NULL OR fim_exibicao IS NULL OR fim_exibicao >= inicio_exibicao");
                    table.CheckConstraint("ck_midia_tamanho", "tamanho_bytes BETWEEN 1 AND 52428800");
                    table.ForeignKey(
                        name: "fk_midia_parceiros_parceiro_id",
                        column: x => x.parceiro_id,
                        principalSchema: "business",
                        principalTable: "parceiro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "playlist",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parceiro_id = table.Column<long>(type: "bigint", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist", x => x.id);
                    table.ForeignKey(
                        name: "fk_playlist_parceiros_parceiro_id",
                        column: x => x.parceiro_id,
                        principalSchema: "business",
                        principalTable: "parceiro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario_parceiro",
                schema: "business",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    parceiro_id = table.Column<long>(type: "bigint", nullable: false),
                    papel = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_parceiro", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuario_parceiro_parceiro_parceiro_id",
                        column: x => x.parceiro_id,
                        principalSchema: "business",
                        principalTable: "parceiro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuario_parceiro_user_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "platform",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_playlist",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    playlist_id = table.Column<long>(type: "bigint", nullable: false),
                    midia_id = table.Column<long>(type: "bigint", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    dias_semana = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_playlist", x => x.id);
                    table.CheckConstraint("ck_item_playlist_dias_semana", "dias_semana BETWEEN 1 AND 127");
                    table.CheckConstraint("ck_item_playlist_ordem", "ordem >= 0");
                    table.ForeignKey(
                        name: "fk_item_playlist_midias_midia_id",
                        column: x => x.midia_id,
                        principalSchema: "indoor",
                        principalTable: "midia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_item_playlist_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalSchema: "indoor",
                        principalTable: "playlist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tela",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parceiro_id = table.Column<long>(type: "bigint", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    localizacao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    playlist_id = table.Column<long>(type: "bigint", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    codigo_pareamento = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: true),
                    codigo_pareamento_expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dispositivo_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pareada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    versao_credencial = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tela", x => x.id);
                    table.CheckConstraint("ck_tela_codigo_pareamento", "codigo_pareamento IS NULL OR (codigo_pareamento ~ '^[A-Z0-9]{8}$' AND codigo_pareamento_expira_em IS NOT NULL)");
                    table.CheckConstraint("ck_tela_pareada_em", "(dispositivo_id IS NULL) = (pareada_em IS NULL)");
                    table.ForeignKey(
                        name: "fk_tela_parceiros_parceiro_id",
                        column: x => x.parceiro_id,
                        principalSchema: "business",
                        principalTable: "parceiro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tela_playlist_playlist_id",
                        column: x => x.playlist_id,
                        principalSchema: "indoor",
                        principalTable: "playlist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estado_tela",
                schema: "indoor",
                columns: table => new
                {
                    tela_id = table.Column<long>(type: "bigint", nullable: false),
                    ultima_sincronizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    versao_app = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    versao_grade = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    itens_prontos = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estado_tela", x => x.tela_id);
                    table.ForeignKey(
                        name: "fk_estado_tela_telas_tela_id",
                        column: x => x.tela_id,
                        principalSchema: "indoor",
                        principalTable: "tela",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_estado_tela_ultima_sincronizacao",
                schema: "indoor",
                table: "estado_tela",
                column: "ultima_sincronizacao");

            migrationBuilder.CreateIndex(
                name: "ix_item_playlist_midia_id",
                schema: "indoor",
                table: "item_playlist",
                column: "midia_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_playlist_playlist_id_ordem",
                schema: "indoor",
                table: "item_playlist",
                columns: new[] { "playlist_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "ix_midia_chave_armazenamento",
                schema: "indoor",
                table: "midia",
                column: "chave_armazenamento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_midia_parceiro_id_ativo",
                schema: "indoor",
                table: "midia",
                columns: new[] { "parceiro_id", "ativo" });

            migrationBuilder.CreateIndex(
                name: "ix_parceiro_situacao",
                schema: "business",
                table: "parceiro",
                column: "situacao");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_parceiro_id_nome",
                schema: "indoor",
                table: "playlist",
                columns: new[] { "parceiro_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tela_codigo_pareamento",
                schema: "indoor",
                table: "tela",
                column: "codigo_pareamento",
                unique: true,
                filter: "codigo_pareamento IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tela_parceiro_id_nome",
                schema: "indoor",
                table: "tela",
                columns: new[] { "parceiro_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tela_playlist_id",
                schema: "indoor",
                table: "tela",
                column: "playlist_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_parceiro_parceiro_id",
                schema: "business",
                table: "usuario_parceiro",
                column: "parceiro_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_parceiro_usuario_id",
                schema: "business",
                table: "usuario_parceiro",
                column: "usuario_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estado_tela",
                schema: "indoor");

            migrationBuilder.DropTable(
                name: "item_playlist",
                schema: "indoor");

            migrationBuilder.DropTable(
                name: "usuario_parceiro",
                schema: "business");

            migrationBuilder.DropTable(
                name: "tela",
                schema: "indoor");

            migrationBuilder.DropTable(
                name: "midia",
                schema: "indoor");

            migrationBuilder.DropTable(
                name: "playlist",
                schema: "indoor");

            migrationBuilder.DropTable(
                name: "parceiro",
                schema: "business");
        }
    }
}
