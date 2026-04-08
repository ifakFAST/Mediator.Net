// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using NLog.Time;

namespace Ifak.Fast.Mediator;

internal sealed class AppTimeZoneTimeSource : TimeSource
{
    public override DateTime Time => AppTimeZone.ConvertToLocalTimeFromUtcDateTime(DateTime.UtcNow);

    public override DateTime FromSystemTime(DateTime systemTime)
    {
        DateTime utcTime = systemTime.Kind switch {
            DateTimeKind.Utc => systemTime,
            DateTimeKind.Local => systemTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(systemTime, DateTimeKind.Local).ToUniversalTime()
        };

        return AppTimeZone.ConvertToLocalTimeFromUtcDateTime(utcTime);
    }
}
