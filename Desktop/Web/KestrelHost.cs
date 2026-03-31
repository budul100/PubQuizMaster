using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PubQuizMaster.Core.Services;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// ─────────────────────────────────────────────
// KESTREL BOOTSTRAP
// Starts an embedded ASP.NET Core / Kestrel web server
// inside the Avalonia process. Runs on a background thread.
// No separate process, no installation required.
//
// Scorer clients connect via: http://[LocalIP]:5000
// SignalR hub endpoint:        http://[LocalIP]:5000/quizhub
// Static web client:           http://[LocalIP]:5000/index.html
// ─────────────────────────────────────────────

namespace PubQuizMaster.Desktop.Web
{
    /// <summary>
    /// Manages the embedded Kestrel web server lifecycle.
    /// Call Start() on app launch, Stop() on app shutdown.
    /// </summary>
    public class KestrelHost
    {
        private WebApplication? _app;
        private CancellationTokenSource? _cts;
        private Task? _runTask;

        public const int Port = 5000;

        // Fired when the server is ready — carries the local URL for QR code display
        public event Action<string>? ServerReady;

        // Exposes IHubContext so Avalonia ViewModels can push events to clients
        public IHubContext<QuizHub>? HubContext { get; private set; }

        // ── Shared services injected from outside ──────────────────────────────
        // These are created once in App.axaml.cs and shared between
        // the Avalonia UI layer and the Kestrel/SignalR layer.
        private readonly QuizNightService _quizNightService;
        private readonly PersistenceService _persistenceService;
        private readonly ScorerSessionService _scorerSessionService;

        public KestrelHost(
            QuizNightService quizNightService,
            PersistenceService persistenceService,
            ScorerSessionService scorerSessionService)
        {
            _quizNightService = quizNightService;
            _persistenceService = persistenceService;
            _scorerSessionService = scorerSessionService;
        }

        // ── Start ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds and starts the Kestrel server on a background thread.
        /// Non-blocking — returns immediately after the server is ready.
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();

            var builder = WebApplication.CreateBuilder();

            // ── Kestrel binding ──────────────────────────────────────────────
            // 0.0.0.0 = all network interfaces, including hotspot adapters.
            // Avoids localhost-only binding which would be invisible to mobile clients.
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Any, Port);
            });

            // ── Service registration ─────────────────────────────────────────
            // Register the shared singleton instances — the same objects
            // that the Avalonia ViewModels use. No duplication of state.
            builder.Services.AddSingleton(_quizNightService);
            builder.Services.AddSingleton(_persistenceService);
            builder.Services.AddSingleton(_scorerSessionService);

            // SignalR with JSON polymorphism support for AnswerBase
            builder.Services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    // Required so AnswerBool / AnswerPoint survive serialization
                    options.PayloadSerializerOptions.Converters.Add(new AnswerBaseJsonConverter());
                });

            // CORS — open for local network access (no credentials, no external risk)
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            _app = builder.Build();

            // ── Middleware pipeline ──────────────────────────────────────────
            _app.UseCors();

            // Serve static files from wwwroot (index.html + signalr.min.js)
            _app.UseStaticFiles();

            // Map SignalR hub
            _app.MapHub<QuizHub>("/quizhub");

            // Fallback: any unknown route returns index.html (SPA behavior)
            _app.MapFallbackToFile("index.html");

            // Grab IHubContext so Avalonia can push events to clients
            HubContext = _app.Services.GetRequiredService<IHubContext<QuizHub>>();

            // ── Run on background thread ─────────────────────────────────────
            _runTask = Task.Run(async () =>
            {
                // Fire event so Avalonia can display the URL / QR code
                var localUrl = $"http://{GetLocalIpAddress()}:{Port}";
                ServerReady?.Invoke(localUrl);

                await _app.RunAsync(_cts.Token);
            });
        }

        // ── Stop ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Gracefully shuts down the Kestrel server.
        /// Call from App.OnFrameworkInitializationCompleted or window close handler.
        /// </summary>
        public async Task StopAsync()
        {
            if (_cts == null || _app == null) return;

            _cts.Cancel();

            if (_runTask != null)
                await _runTask.ConfigureAwait(false);

            await _app.DisposeAsync();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the first non-loopback IPv4 address of this machine.
        /// Works across Wi-Fi, Ethernet, and mobile hotspot adapters.
        /// Falls back to localhost if no suitable address is found.
        /// </summary>
        public static string GetLocalIpAddress()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(address.Address)) continue;

                    return address.Address.ToString();
                }
            }

            return "localhost";
        }
    }



}