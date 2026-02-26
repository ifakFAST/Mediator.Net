// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ganss.Xss;

namespace Ifak.Fast.Mediator.Dashboard.Security;

internal static class HtmlContentSanitizer
{
    public static string Sanitize(string? html) {
        var sanitizer = new HtmlSanitizer();
        return sanitizer.Sanitize(html ?? "");
    }
}
