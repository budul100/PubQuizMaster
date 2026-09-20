using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Models.Content;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        #region Public Properties

        public DbSet<Answer> Answers => Set<Answer>();

        public DbSet<Participant> Participants => Set<Participant>();

        public DbSet<Quiz> Quizzes => Set<Quiz>();

        public DbSet<Round> Rounds => Set<Round>();

        public DbSet<Scorer> Scorers => Set<Scorer>();

        public DbSet<Result> Scores => Set<Result>();

        public DbSet<Team> Teams => Set<Team>();

        #endregion Public Properties

        #region Protected Methods

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // All keys are set client-side. With generated keys EF treats a new entity that is
            // discovered via a navigation and already has a key as existing (UPDATE instead of INSERT).
            modelBuilder.Entity<Answer>().Property(a => a.Id).ValueGeneratedNever();
            modelBuilder.Entity<Quiz>().Property(q => q.Id).ValueGeneratedNever();
            modelBuilder.Entity<Result>().Property(r => r.Id).ValueGeneratedNever();
            modelBuilder.Entity<Round>().Property(r => r.Id).ValueGeneratedNever();
            modelBuilder.Entity<Scorer>().Property(s => s.Id).ValueGeneratedNever();
            modelBuilder.Entity<Team>().Property(t => t.Id).ValueGeneratedNever();

            // At most one live quiz night. All rows matching the filter share the same
            // column values, so the unique index admits a single row only.
            modelBuilder.Entity<Quiz>()
                .HasIndex(q => new { q.IsCompleted, q.IsLegacyImport })
                .IsUnique()
                .HasFilter("\"IsCompleted\" = false AND \"IsLegacyImport\" = false")
                .HasDatabaseName(Constants.SingleActiveQuiz);

            modelBuilder.Entity<Round>()
                .HasMany(r => r.Answers)
                .WithOne()
                .HasForeignKey(a => a.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            // Team names are matched by their normalized form
            modelBuilder.Entity<Team>()
                .HasIndex(t => t.Normalized)
                .IsUnique()
                .HasDatabaseName(Constants.TeamName);

            // One answer per cell
            modelBuilder.Entity<Answer>()
                .HasIndex(a => new { a.RoundId, a.TeamId, a.QuestionIndex })
                .IsUnique()
                .HasDatabaseName(Constants.AnswerCell);

            modelBuilder.Entity<Participant>()
                .HasKey(qt => new { qt.QuizId, qt.TeamId });

            modelBuilder.Entity<Participant>()
                .HasOne(qt => qt.Quiz)
                .WithMany(q => q.ParticipatingTeams)
                .HasForeignKey(qt => qt.QuizId);

            modelBuilder.Entity<Participant>()
                .HasOne(qt => qt.Team)
                .WithMany()
                .HasForeignKey(qt => qt.TeamId);

            modelBuilder.Entity<Result>()
                .HasOne(ls => ls.Quiz)
                .WithMany(q => q.Results)
                .HasForeignKey(ls => ls.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Result>()
                .HasOne(ls => ls.Team)
                .WithMany(t => t.Results)
                .HasForeignKey(ls => ls.TeamId);

            // One legacy result per team and quiz night, keeps the import idempotent
            modelBuilder.Entity<Result>()
                .HasIndex(r => new { r.QuizId, r.TeamId })
                .IsUnique()
                .HasDatabaseName(Constants.ResultPerTeam);

            var jsonOptions = new JsonSerializerOptions();

            modelBuilder.Entity<Answer>()
                .Property(a => a.Value)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<AnswerBase>(v, jsonOptions)!);
        }

        #endregion Protected Methods
    }
}