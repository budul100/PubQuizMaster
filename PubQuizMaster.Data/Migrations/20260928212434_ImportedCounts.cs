using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubQuizMaster.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportedCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImportedQuestionCount",
                table: "Quizzes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportedRoundCount",
                table: "Quizzes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportedQuestionCount",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "ImportedRoundCount",
                table: "Quizzes");
        }
    }
}
