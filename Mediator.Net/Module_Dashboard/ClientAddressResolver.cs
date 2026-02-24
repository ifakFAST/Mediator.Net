// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Sockets;

namespace Ifak.Fast.Mediator.Dashboard;

internal static class ClientAddressResolver {

    public static string GetClientAddress(HttpRequest request) {

        var remoteIP = request.HttpContext.Connection.RemoteIpAddress;

        if (remoteIP == null) {
            return "unknown";
        }

        if (remoteIP.IsIPv4MappedToIPv6) {
            remoteIP = remoteIP.MapToIPv4();
        }

        if (IsPrivateOrLoopbackAddress(remoteIP) && TryGetForwardedClientAddress(request, out IPAddress forwardedIP)) {
            if (forwardedIP.IsIPv4MappedToIPv6) {
                forwardedIP = forwardedIP.MapToIPv4();
            }
            return forwardedIP.ToString();
        }

        return remoteIP.ToString();
    }

    private static bool TryGetForwardedClientAddress(HttpRequest request, out IPAddress forwardedIP) {

        forwardedIP = IPAddress.None;

        string forwardedFor = request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor)) {
            string firstIP = forwardedFor.Split(',')[0].Trim();
            if (IPAddress.TryParse(firstIP, out IPAddress? parsedIP)) {
                forwardedIP = parsedIP;
                return true;
            }
        }

        string realIP = request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(realIP) && IPAddress.TryParse(realIP.Trim(), out IPAddress? parsedRealIP)) {
            forwardedIP = parsedRealIP;
            return true;
        }

        return false;
    }

    private static bool IsPrivateOrLoopbackAddress(IPAddress address) {

        if (IPAddress.IsLoopback(address)) {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork) {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6) {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) {
                return true;
            }

            byte[] bytes = address.GetAddressBytes();
            return (bytes[0] & 0xFE) == 0xFC; // fc00::/7 unique local address range
        }

        return false;
    }
}
