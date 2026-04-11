using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PubQuizMaster.Core.Services;
using PubQuizMaster.Desktop.Extensions;
using PubQuizMaster.Desktop.ViewModels;
using PubQuizMaster.Desktop.Views;
using PubQuizMaster.Desktop.Web;

namespace PubQuizMaster.Desktop
{
    public class App
        : Application
    {
        #region Public Properties

        public static KestrelHost KestrelHost { get; private set; } = null!;

        public static QuizNightService QuizNightService { get; private set; } = null!;

        public static SettingsService SettingsService { get; private set; } = null!;

        #endregion Public Properties

        #region Public Methods

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override async void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // -- 1. Services -------------------------------------------------------
            var persistence = new PersistenceService();
            var scorerSession = new ScorerSessionService();
            QuizNightService = new QuizNightService(persistence);

            SettingsService = new SettingsService();
            SettingsService.Load();

            var saved = SettingsService.Settings.ClientUrl;

            if (!string.IsNullOrWhiteSpace(saved)
                && !saved.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                SettingsService.ClearUrl();
            }

            // -- 2. Startup dialog -------------------------------------------------
            var startupVm = new StartupViewModel(persistence);
            var startupWindow = new StartupWindow(startupVm);

            // ShowDialog blockiert bis CloseRequested() die Window schließt
            desktop.MainWindow = startupWindow;
            startupWindow.Show();

            // Warten bis der User eine Entscheidung getroffen hat
            await startupWindow.HasClosed();

            // Abbruch (Fenster geschlossen ohne Auswahl)
            if (startupVm.Result == null)
            {
                desktop.Shutdown();
                return;
            }

            // -- 3. Service initialisieren -----------------------------------------
            await QuizNightService.InitializeAsync(startupVm.Result);

            // -- 4. Kestrel --------------------------------------------------------
            KestrelHost = new KestrelHost(QuizNightService, persistence, scorerSession);
            KestrelHost.ServerReady += url => Console.WriteLine($"[PubQuizMaster] Server ready → {url}");

            // -- 5. Main Window ----------------------------------------------------
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(QuizNightService, KestrelHost)
            };

            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            KestrelHost.Start();

            desktop.ShutdownRequested += async (_, _) => await KestrelHost.StopAsync();

            base.OnFrameworkInitializationCompleted();
        }

        #endregion Public Methods
    }
}