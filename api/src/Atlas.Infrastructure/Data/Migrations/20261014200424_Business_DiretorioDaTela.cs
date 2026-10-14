using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_DiretorioDaTela : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_diretorio",
                schema: "indoor",
                table: "tela",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "diretorio_ativo",
                schema: "indoor",
                table: "tela",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "categoria",
                schema: "business",
                table: "parceiro",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

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

            migrationBuilder.AddColumn<bool>(
                name: "exibir_no_diretorio_publico",
                schema: "business",
                table: "parceiro",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "instagram_publico",
                schema: "business",
                table: "parceiro",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "site_publico",
                schema: "business",
                table: "parceiro",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whatsapp_publico",
                schema: "business",
                table: "parceiro",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tela_codigo_diretorio",
                schema: "indoor",
                table: "tela",
                column: "codigo_diretorio",
                unique: true,
                filter: "codigo_diretorio IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tela_codigo_diretorio",
                schema: "indoor",
                table: "tela",
                sql: "codigo_diretorio IS NULL OR codigo_diretorio ~ '^[A-Z0-9]{10}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tela_diretorio",
                schema: "indoor",
                table: "tela",
                sql: "NOT diretorio_ativo OR codigo_diretorio IS NOT NULL");

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
                name: "ck_parceiro_site_publico",
                schema: "business",
                table: "parceiro",
                sql: "site_publico IS NULL OR site_publico ~ '^https?://'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_parceiro_whatsapp_publico",
                schema: "business",
                table: "parceiro",
                sql: "whatsapp_publico IS NULL OR whatsapp_publico ~ '^[0-9]{12,15}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tela_codigo_diretorio",
                schema: "indoor",
                table: "tela");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tela_codigo_diretorio",
                schema: "indoor",
                table: "tela");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tela_diretorio",
                schema: "indoor",
                table: "tela");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_diretorio_aceite",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropCheckConstraint(
                name: "ck_parceiro_instagram_publico",
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
                name: "codigo_diretorio",
                schema: "indoor",
                table: "tela");

            migrationBuilder.DropColumn(
                name: "diretorio_ativo",
                schema: "indoor",
                table: "tela");

            migrationBuilder.DropColumn(
                name: "categoria",
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
                name: "exibir_no_diretorio_publico",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "instagram_publico",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "site_publico",
                schema: "business",
                table: "parceiro");

            migrationBuilder.DropColumn(
                name: "whatsapp_publico",
                schema: "business",
                table: "parceiro");
        }
    }
}
