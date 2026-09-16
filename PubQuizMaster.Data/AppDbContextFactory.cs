using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PubQuizMaster.Data
{
    /// <summary>
    /// Used by the EF tools only. Reads the connection string the same way the web host does.
    /// </summary>
    public class AppDbContextFactory
        : IDesignTimeDbContextFactory<AppDbContext>
    {
        #region Private Fields

        private const string ConnectionStringName = "Default";
        private const string DefaultEnvironment = "Development";
        private const string WebProjectName = "PubQuizMaster.Web";

        #endregion Private Fields

        #region Public Methods

        public AppDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? DefaultEnvironment;

            // Same sources and precedence as the web host: files, user secrets (development only),
            // environment variables, command line (arguments after "--" of the dotnet ef call)
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(FindWebProjectDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true);

            if (environment == DefaultEnvironment)
            {
                // Shares the UserSecretsId with the web project, see PubQuizMaster.Data.csproj
                configurationBuilder.AddUserSecrets<AppDbContextFactory>(optional: true);
            }

            var configuration = configurationBuilder
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            // Fail closed: no silent fallback to a guessed database
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured for environment '{environment}'. " +
                    $"Set it in {WebProjectName}/appsettings.{environment}.json, the user secrets, " +
                    $"the environment variable ConnectionStrings__{ConnectionStringName} " +
                    $"or pass it after '--': --ConnectionStrings:{ConnectionStringName} \"...\"");
            }

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            builder.UseNpgsql(connectionString);

            return new AppDbContext(builder.Options);
        }

        #endregion Public Methods

        #region Private Methods

        private static string FindWebProjectDirectory()
        {
            var current = new DirectoryInfo(Directory.GetCurrentDirectory());

            // The tools may run from the Data project, the solution root or a bin folder
            for (var directory = current; directory != null; directory = directory.Parent)
            {
                if (directory.Name == WebProjectName) return directory.FullName;

                var candidate = Path.Combine(directory.FullName, WebProjectName);
                if (Directory.Exists(candidate)) return candidate;
            }

            return current.FullName;
        }

        #endregion Private Methods
    }
}
