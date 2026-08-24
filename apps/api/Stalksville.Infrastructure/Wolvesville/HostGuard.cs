using System.Net;
using System.Net.Sockets;

namespace Stalksville.Infrastructure.Wolvesville;

/// <summary>
/// Outbound request guard: only http/https schemes, and (unless test hosts are explicitly allowed)
/// loopback, private, link-local, multicast and reserved addresses are rejected before any request
/// is sent. The production base URL is fixed to https://api.wolvesville.com, so this is
/// defense-in-depth against configuration mistakes and SSRF-style misuse.
/// </summary>
public static class HostGuard
{
    public static void Validate(Uri uri, bool allowTestHosts)
    {
        if (uri.Scheme is not ("https" or "http"))
        {
            throw new InvalidOperationException($"Blocked outbound request: scheme '{uri.Scheme}' is not allowed ({uri}).");
        }

        if (allowTestHosts)
        {
            return;
        }

        if (uri.Scheme != "https")
        {
            throw new InvalidOperationException($"Blocked outbound request: non-https upstream '{uri}' (only https is allowed).");
        }

        if (IsBlockedHost(uri.Host))
        {
            throw new InvalidOperationException($"Blocked outbound request: host '{uri.Host}' is loopback, private, link-local or reserved.");
        }
    }

    private static bool IsBlockedHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("localhost.", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false; // regular DNS name (e.g. api.wolvesville.com)
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.IsIPv6UniqueLocal || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal
            || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 0                                            // 0.0.0.0/8 "this network"
                || bytes[0] == 10                                           // 10.0.0.0/8 private
                || bytes[0] == 127                                          // 127.0.0.0/8 loopback
                || (bytes[0] == 100 && (bytes[1] & 0xC0) == 64)             // 100.64.0.0/10 CGNAT
                || (bytes[0] == 172 && (bytes[1] & 0xF0) == 16)             // 172.16.0.0/12 private
                || (bytes[0] == 192 && bytes[1] == 168)                     // 192.168.0.0/16 private
                || (bytes[0] == 169 && bytes[1] == 254)                     // 169.254.0.0/16 link-local
                || (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2)      // 192.0.2.0/24 TEST-NET-1
                || (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)   // 198.51.100.0/24 TEST-NET-2
                || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)    // 203.0.113.0/24 TEST-NET-3
                || (bytes[0] & 0xF0) == 0xE0                                // 224.0.0.0/4 multicast
                || (bytes[0] & 0xF0) == 0xF0;                               // 240.0.0.0/4 reserved (incl. broadcast)
        }

        return false;
    }
}
