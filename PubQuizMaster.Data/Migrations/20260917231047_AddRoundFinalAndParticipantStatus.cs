using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubQuizMaster.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundFinalAndParticipantStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFinal",
                table: "Rounds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Participants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNonCompetitive",
                table: "Participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFinal",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "IsNonCompetitive",
                table: "Participants");
        }
    }
}