using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Data;
using PubQuizMaster.Services;
using PubQuizMaster.Services.Common;
using PubQuizMaster.Services.Event;
using PubQuizMaster.Services.Import;
using PubQuizMaster.Services.Player;
using PubQuizMaster.Web.Security;
using PubQuizMaster.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database Configuration (PostgreSQL) ──────────────────────────
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── Authentication (single admin, cookie based) ──────────────────
// Fail closed: the app does not start without an admin password
if (string.IsNullOrWhiteSpace(builder.Configuration[AdminAuth.PasswordKey]))
{
    throw new InvalidOperationException(
        $"'{AdminAuth.PasswordKey}' is not configured. Set it via user secrets or environment variable 'Auth__AdminPassword'.");
}

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.Name = "PubQuizMaster.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ── Reverse proxy (client IP for the rate limiter, original scheme) ──
var reverseProxyEnabled = builder.Configuration.GetValue<bool>(ReverseProxy.EnabledKey);
if (reverseProxyEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
        ReverseProxy.Configure(options, builder.Configuration));
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(LoginRateLimit.PolicyName, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = LoginRateLimit.PermitLimit,
                Window = LoginRateLimit.Window,
                QueueLimit = 0
            }));
});

// ── Service Registrations ─────────────────────────────────────────
// Scoped = one instance per Blazor circuit, so toasts stay in the browser that triggered them
builder.Services.AddScoped<ToastService>();
builder.Services.AddSingleton<ScorerService>();
builder.Services.AddSingleton<QrCodeService>();

builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<QuizService>();
builder.Services.AddScoped<TeamService>();

builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<PresentationDownloadService>();

// ── Blazor & Razor Pages ──────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
});

var app = builder.Build();

// Must run first, everything after it relies on the real client address and scheme
if (reverseProxyEnabled)
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Login/Logout are Razor Pages; page-level authorization happens in AuthorizeRouteView
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
