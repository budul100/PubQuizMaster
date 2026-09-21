using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace PubQuizMaster.Web.Helpers
{
    /// <summary>
    /// Forwarded headers behind nginx/Traefik. Without them the rate limiter only sees the proxy address
    /// and every client shares one login limit.
    /// </summary>
    public static class ReverseProxy
    {
        #region Public Fields

        public const string EnabledKey = "ReverseProxy:Enabled";
        public const string KnownNetworksKey = "ReverseProxy:KnownNetworks";
        public const string KnownProxiesKey = "ReverseProxy:KnownProxies";

        #endregion Public Fields

        #region Public Methods

        public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Defaults only trust loopback, the proxy runs in its own container
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            var networks = configuration.GetSection(KnownNetworksKey).Get<string[]>() ?? [];
            var proxies = configuration.GetSection(KnownProxiesKey).Get<string[]>() ?? [];

            // Fail closed: trusting every sender would let clients spoof X-Forwarded-For and bypass the rate limiter
            if (networks.Length == 0 && proxies.Length == 0)
            {
                throw new InvalidOperationException(
                    $"'{EnabledKey}' is set, but neither '{KnownNetworksKey}' nor '{KnownProxiesKey}' is configured.");
            }

            foreach (var cidr in networks)
            {
                var network = System.Net.IPNetwork.Parse(cidr);
                options.KnownNetworks.Add(
                    new Microsoft.AspNetCore.HttpOverrides.IPNetwork(network.BaseAddress, network.PrefixLength));
            }

            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        }

        #endregion Public Methods
    }
}