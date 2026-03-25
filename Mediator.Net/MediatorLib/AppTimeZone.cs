// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator;

public static class AppTimeZone
{
    private static TimeZoneInfo zone = TimeZoneInfo.Local;

    public static TimeZoneInfo TimeZone => zone;

    public static string IanaId => zone.Id;

    public static void Initialize(string timeZoneId) {
        zone = string.IsNullOrWhiteSpace(timeZoneId)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public static DateTime ConvertToLocalTimeFromUtcDateTime(DateTime utcDateTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, zone);

    public static DateTime ConvertToLocalTime(Timestamp t) =>
        TimeZoneInfo.ConvertTimeFromUtc(t.ToDateTime(), zone);
}
