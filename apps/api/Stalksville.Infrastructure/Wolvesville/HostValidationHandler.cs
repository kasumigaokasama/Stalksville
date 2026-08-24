using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Stalksville.Infrastructure.Wolvesville;

/// <summary>DelegatingHandler that runs <see cref="HostGuard"/> before any request leaves the process.</summary>
public sealed class HostValidationHandler(
    IOptions<WolvesvilleOptions> options,
    IHostEnvironment environment) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri)
        {
            var allowTestHosts = options.Value.AllowTestHost && environment.IsDevelopment();
            HostGuard.Validate(uri, allowTestHosts);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
