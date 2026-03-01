using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAgents.BeeHiveAgent.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeModelVersionToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelVersionId",
                table: "Predictions");

            migrationBuilder.AddColumn<string>(
                name: "ModelVersion",
                table: "Predictions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelVersion",
                table: "Predictions");

            migrationBuilder.AddColumn<Guid>(
                name: "ModelVersionId",
                table: "Predictions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
