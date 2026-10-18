using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_ClientesEParceiros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Separação entre parceiro (local da TV, sem acesso) e cliente
            // (anunciante, com login). Decisão: começar limpo — os cadastros
            // antigos misturavam os dois papéis. Apaga parceiros, telas, mídias
            // (e itens de playlist), estados e clima, e os logins dos antigos
            // parceiros. Playlists, fontes de notícias e versões do app ficam.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE _logins_parceiros ON COMMIT DROP AS
                    SELECT usuario_id FROM business.usuario_parceiro;
                TRUNCATE business.parceiro CASCADE;
                DELETE FROM platform."user" WHERE id IN (SELECT usuario_id FROM _logins_parceiros);
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_midia_parceiros_parceiro_id",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropTable(
                name: "usuario_parceiro",
                schema: "business");

            migrationBuilder.DropIndex(
                name: "ix_parceiro_situacao",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_cota",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_diretorio_aceite",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_instagram_publico",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_limite_midias_ativas",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_motivo_bloqueio",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_site_publico",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_whatsapp_publico",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "categoria",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "cota_armazenamento_mb",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "descricao_publica",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "diretorio_aceito_em",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "instagram_publico",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "limite_midias_ativas",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "motivo_bloqueio",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "site_publico",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "situacao",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "whatsapp_publico",
                schema: "business",
                table: "parceiro");

            migrationBuilder.RenameColumn(
                name: "observacao_interna",
                schema: "business",
                table: "parceiro",
                newName: "observacao");

            migrationBuilder.RenameColumn(
                name: "exibir_no_diretorio_publico",
                schema: "business",
                table: "parceiro",
                newName: "ativo");

            migrationBuilder.RenameColumn(
                name: "parceiro_id",
                schema: "indoor",
                table: "midia",
                newName: "cliente_id");

            migrationBuilder.RenameIndex(
                name: "ix_midia_parceiro_id_ativo",
                schema: "indoor",
                table: "midia",
                newName: "ix_midia_cliente_id_ativo");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "business",
                table: "parceiro",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AddColumn<long>(
                name: "cliente_id",
                schema: "business",
                table: "parceiro",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco",
                schema: "business",
                table: "parceiro",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cliente",
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
                    limite_midias_ativas = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    motivo_bloqueio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    observacao_interna = table.Column<string>(type: "text", nullable: true),
                    exibir_no_diretorio_publico = table.Column<bool>(type: "boolean", nullable: false),
                    diretorio_aceito_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    categoria = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    descricao_publica = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    whatsapp_publico = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    instagram_publico = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    site_publico = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cliente", x => x.id);
                    table.CheckConstraint("ck_cliente_cota", "cota_armazenamento_mb BETWEEN 1 AND 1048576");
                    table.CheckConstraint("ck_cliente_diretorio_aceite", "(exibir_no_diretorio_publico) = (diretorio_aceito_em IS NOT NULL)");
                    table.CheckConstraint("ck_cliente_documento", "documento IS NULL OR documento ~ '^[0-9]{11}$' OR documento ~ '^[0-9A-Z]{12}[0-9]{2}$'");
                    table.CheckConstraint("ck_cliente_instagram_publico", "instagram_publico IS NULL OR instagram_publico ~ '^[A-Za-z0-9._]{1,30}$'");
                    table.CheckConstraint("ck_cliente_limite_midias_ativas", "limite_midias_ativas BETWEEN 1 AND 1000");
                    table.CheckConstraint("ck_cliente_motivo_bloqueio", "situacao <> 2 OR motivo_bloqueio IS NOT NULL");
                    table.CheckConstraint("ck_cliente_site_publico", "site_publico IS NULL OR site_publico ~ '^https?://'");
                    table.CheckConstraint("ck_cliente_telefone", "telefone IS NULL OR telefone ~ '^[0-9]{10,11}$'");
                    table.CheckConstraint("ck_cliente_whatsapp_publico", "whatsapp_publico IS NULL OR whatsapp_publico ~ '^[0-9]{12,15}$'");
                });

            migrationBuilder.CreateTable(
                name: "usuario_cliente",
                schema: "business",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    cliente_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_usuario_cliente", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuario_cliente_cliente_cliente_id",
                        column: x => x.cliente_id,
                        principalSchema: "business",
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuario_cliente_user_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "platform",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_parceiro_cliente_id",
                schema: "business",
                table: "parceiro",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_parceiro_nome",
                schema: "business",
                table: "parceiro",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_cliente_situacao",
                schema: "business",
                table: "cliente",
                column: "situacao");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_cliente_cliente_id",
                schema: "business",
                table: "usuario_cliente",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_cliente_usuario_id",
                schema: "business",
                table: "usuario_cliente",
                column: "usuario_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_midia_cliente_cliente_id",
                schema: "indoor",
                table: "midia",
                column: "cliente_id",
                principalSchema: "business",
                principalTable: "cliente",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_parceiro_cliente_cliente_id",
                schema: "business",
                table: "parceiro",
                column: "cliente_id",
                principalSchema: "business",
                principalTable: "cliente",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_midia_cliente_cliente_id",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropForeignKey(
                name: "fk_parceiro_cliente_cliente_id",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropTable(
                name: "usuario_cliente",
                schema: "business");

            migrationBuilder.DropTable(
                name: "cliente",
                schema: "business");

            migrationBuilder.DropIndex(
                name: "ix_parceiro_cliente_id",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropIndex(
                name: "ix_parceiro_nome",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "cliente_id",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "endereco",
                schema: "business",
                table: "parceiro");

            migrationBuilder.RenameColumn(
                name: "observacao",
                schema: "business",
                table: "parceiro",
                newName: "observacao_interna");

            migrationBuilder.RenameColumn(
                name: "ativo",
                schema: "business",
                table: "parceiro",
                newName: "exibir_no_diretorio_publico");

            migrationBuilder.RenameColumn(
                name: "cliente_id",
                schema: "indoor",
                table: "midia",
                newName: "parceiro_id");

            migrationBuilder.RenameIndex(
                name: "ix_midia_cliente_id_ativo",
                schema: "indoor",
                table: "midia",
                newName: "ix_midia_parceiro_id_ativo");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "business",
                table: "parceiro",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "categoria",
                schema: "business",
                table: "parceiro",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cota_armazenamento_mb",
                schema: "business",
                table: "parceiro",
                type: "integer",
                nullable: false,
                defaultValue: 200);

            migrationBuilder.AddColumn<string>(
                name: "descricao_publica",
                schema: "business",
                table: "parceiro",
                type: "character varying(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "diretorio_aceito_em",
                schema: "business",
                table: "parceiro",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instagram_publico",
                schema: "business",
                table: "parceiro",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "limite_midias_ativas",
                schema: "business",
                table: "parceiro",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "motivo_bloqueio",
                schema: "business",
                table: "parceiro",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "site_publico",
                schema: "business",
                table: "parceiro",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "situacao",
                schema: "business",
                table: "parceiro",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "whatsapp_publico",
                schema: "business",
                table: "parceiro",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "usuario_parceiro",
                schema: "business",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parceiro_id = table.Column<long>(type: "bigint", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<long>(type: "bigint", nullable: true),
                    papel = table.Column<int>(type: "integer", nullable: false),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "ix_parceiro_situacao",
                schema: "business",
                table: "parceiro",
                column: "situacao");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parceiro_cota",
                schema: "business",
                table: "parceiro",
                sql: "cota_armazenamento_mb BETWEEN 1 AND 1048576");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parceiro_diretorio_aceite",
                schema: "business",
                table: "parceiro",
                sql: "(exibir_no_diretorio_publico) = (diretorio_aceito_em IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parceiro_instagram_publico",
                schema: "business",
                table: "parceiro",
                sql: "instagram_publico IS NULL OR instagram_publico ~ '^[A-Za-z0-9._]{1,30}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parceiro_limite_midias_ativas",
                schema: "business",
                table: "parceiro",
                sql: "limite_midias_ativas BETWEEN 1 AND 1000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parceiro_motivo_bloqueio",
                schema: "business",
                table: "parceiro",
                sql: "situacao <> 2 OR motivo_bloqueio IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parceiro_site_publico",
                schema: "business",
                table: "parceiro",
                sql: "site_publico IS NULL OR site_publico ~ '^https?://'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parceiro_whatsapp_publico",
                schema: "business",
                table: "parceiro",
                sql: "whatsapp_publico IS NULL OR whatsapp_publico ~ '^[0-9]{12,15}$'");

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

            migrationBuilder.AddForeignKey(
                name: "fk_midia_parceiros_parceiro_id",
                schema: "indoor",
                table: "midia",
                column: "parceiro_id",
                principalSchema: "business",
                principalTable: "parceiro",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
