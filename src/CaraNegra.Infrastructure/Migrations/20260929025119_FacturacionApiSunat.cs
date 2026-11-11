using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaraNegra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FacturacionApiSunat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ambiente",
                table: "Comprobantes",
                type: "varchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "SIMULADO")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CdrUrl",
                table: "Comprobantes",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "IntentosEnvio",
                table: "Comprobantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MensajeSunat",
                table: "Comprobantes",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PdfUrl",
                table: "Comprobantes",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoEnvioEn",
                table: "Comprobantes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XmlUrl",
                table: "Comprobantes",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_Estado",
                table: "Comprobantes",
                column: "Estado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comprobantes_Estado",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "Ambiente",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "CdrUrl",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "IntentosEnvio",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "MensajeSunat",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "PdfUrl",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "UltimoEnvioEn",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "XmlUrl",
                table: "Comprobantes");
        }
    }
}
