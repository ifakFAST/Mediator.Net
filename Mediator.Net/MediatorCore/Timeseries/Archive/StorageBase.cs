// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Ifak.Fast.Mediator.Timeseries.Archive;

public abstract class StorageBase: IDisposable
{
    public abstract (int dayStart, int dayEnd)? GetStoredDayNumberRange(ChannelRef channel);

    public abstract void WriteDayData(ChannelRef channel, int dayNumber, byte[] data);

    public abstract Stream? ReadDayData(ChannelRef channel, int dayNumber);

    public abstract void DeleteDayData(ChannelRef channel, int startDayNumberInclusive, int endDayNumberInclusive);

    /// <summary>
    /// Determines whether calling Compact would reclaim sufficient space to be worth calling it.
    /// </summary>
    public virtual bool CanCompact() { return false; }

    public virtual void Compact() { }

    public abstract void Dispose();
}
