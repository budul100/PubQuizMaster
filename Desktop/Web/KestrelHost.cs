using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PubQuizMaster.Core.Hub;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Desktop.Web
{
    public class KestrelHost(QuizNightService quizNightService, PersistenceService persistenceService,
        ScorerSessionService scorerSessionService)
    {
        #region Public Fields

        public const int Port = 5000;

        #endregion Public Fields

        #region Private Fields

        private WebApplication? _app;
        private CancellationTokenSource? _cts;
        private Task? _runTask;

        #endregion Private Fields

        #region Public Events

        // Fired when the server is ready — carries the local URL for QR code display
        public event Action<string>? ServerReady;

        /// <summary>Fired when the tunnel drops.</summary>
        public event Action? TunnelLost;

        /// <summary>Fired when the tunnel URL is available.</summary>
        public event Action<string>? TunnelReady;

        #endregion Public Events

        #region Public Properties

        // Exposes IHubContext so Avalonia ViewModels can push events to clients
        public IHubContext<QuizHub>? HubContext { get; private set; }

        #endregion Public Properties

        #region Public Methods

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

                    // Skip APIPA (169.254.x.x) — no real network connection
                    if (address.Address.GetAddressBytes()[0] == 169 &&
                        address.Address.GetAddressBytes()[1] == 254) continue;

                    return address.Address.ToString();
                }
            }

            return "localhost";
        }

        /// <summary>
        /// Allows external code (e.g. StartupViewModel) to raise TunnelReady
        /// after a manually managed Tunnel resolves its URL.
        /// </summary>
        public void NotifyTunnelReady(string url) => TunnelReady?.Invoke(url);

        public void Start()
        {
            _cts = new CancellationTokenSource();

            var builder = WebApplication.CreateBuilder();

            // 1. NEU: Host-Filtering deaktivieren (erlaubt Anfragen von xyz.pgy.io)
            builder.Services.Configure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(options =>
            {
                options.AllowedHosts.Add("*");
            });

            // ── Kestrel binding ──────────────────────────────────────────────
            // 0.0.0.0 = all network interfaces, including hotspot adapters.
            // Avoids localhost-only binding which would be invisible to mobile clients.
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(Port);
                // options.Listen(IPAddress.Any, Port);
            });

            // ── Service registration ─────────────────────────────────────────
            // Register the shared singleton instances — the same objects
            // that the Avalonia ViewModels use. No duplication of state.
            builder.Services.AddSingleton(quizNightService);
            builder.Services.AddSingleton(persistenceService);
            builder.Services.AddSingleton(scorerSessionService);

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

            _app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                    Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
            });

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

            _app.Lifetime.ApplicationStarted.Register(() =>
            {
                var localUrl = $"http://{GetLocalIpAddress()}:{Port}";
                ServerReady?.Invoke(localUrl);
            });

            // ── Run on background thread ─────────────────────────────────────
            _runTask = Task.Run(async () =>
            {
                await _app.RunAsync(_cts.Token);
            });
        }

        public async Task StopAsync()
        {
            if (_cts == null || _app == null) return;

            _cts.Cancel();

            if (_runTask != null)
                await _runTask.ConfigureAwait(false);

            await _app.DisposeAsync();
        }

        #endregion Public Methods
    }
}