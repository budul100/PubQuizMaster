using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;
using PubQuizMaster.Core.Services;
using PubQuizMaster.Desktop.ViewModels;
using PubQuizMaster.Desktop.Views;
using PubQuizMaster.Desktop.Web;

namespace PubQuizMaster.Desktop;

public class App : Application
{
    #region Public Properties

    public static KestrelHost KestrelHost { get; private set; } = null!;
    public static QuizNightService QuizNightService { get; private set; } = null!;

    #endregion Public Properties

    #region Public Methods

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        // ── 1. Build hardcoded test session ──────────────────────────────────
        var quizNight = new QuizNight();

        // ── 2. Services ───────────────────────────────────────────────────────
        var persistence = new PersistenceService();
        var scorerSession = new ScorerSessionService();
        QuizNightService = new QuizNightService(persistence);
        await QuizNightService.InitializeAsync(quizNight);

        // ── 3. Kestrel ────────────────────────────────────────────────────────
        KestrelHost = new KestrelHost(QuizNightService, persistence, scorerSession);

        KestrelHost.ServerReady += url =>
        {
            Console.WriteLine($"[PubQuizMaster] Server ready → {url}");
            Console.WriteLine($"[PubQuizMaster] Open on mobile: {url}?scorerId=scorer-a");
        };

        KestrelHost.Start(); // non-blocking, runs Kestrel on a background thread

        // ── 4. Avalonia window ────────────────────────────────────────────────
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(QuizNightService, KestrelHost)
            };

            desktop.ShutdownRequested += async (_, _) => await KestrelHost.StopAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    #endregion Public Methods
}