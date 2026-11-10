using System.Net;

namespace Crawl.Infrastructure.Egress;

/// <summary>
/// <see cref="IWebProxy"/> that reads the active endpoint from <see cref="IEgressProxyProvider"/>.
/// When no proxy is selected, returns null (direct connection).
/// </summary>
public sealed class EgressWebProxy(IEgressProxyProvider provider) : IWebProxy
{
    public ICredentials? Credentials { get; set; }

    public Uri? GetProxy(Uri destination) => provider.CurrentProxy;

    public bool IsBypassed(Uri host) => provider.CurrentProxy is null;
}
