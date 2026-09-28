using Microsoft.EntityFrameworkCore.Migrations;

namespace PubQuizMaster.Data.Migrations
{
    public partial class QuizStatusAndStations : Migration
    {
        #region Protected Methods

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScorerStations");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_SingleActive",
                table: "Quizzes");

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "Quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Planned nights have no counterpart, they fall back to completed so at most one stays open
            migrationBuilder.Sql(
                "UPDATE \"Quizzes\" SET \"IsCompleted\" = (\"Status\" <> 1);");

            migrationBuilder.AddColumn<int>(
                name: "SheetOrder",
                table: "Participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Quizzes");

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_SingleActive",
                table: "Quizzes",
                columns: new[] { "IsCompleted", "IsLegacyImport" },
                unique: true,
                filter: "\"IsCompleted\" = false AND \"IsLegacyImport\" = false");
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quizzes_SingleActive",
                table: "Quizzes");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Quizzes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Open nights become live, everything else (including legacy imports) completed
            migrationBuilder.Sql(
                "UPDATE \"Quizzes\" SET \"Status\" = CASE WHEN \"IsCompleted\" THEN 2 ELSE 1 END;");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "SheetOrder",
                table: "Participants");

            migrationBuilder.CreateTable(
                name: "ScorerStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    QuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScorerId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScorerStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScorerStations_Quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScorerStations_QuizId_ScorerId",
                table: "ScorerStations",
                columns: new[] { "QuizId", "ScorerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_SingleActive",
                table: "Quizzes",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 1");
        }

        #endregion Protected Methods
    }
}