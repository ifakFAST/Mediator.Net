// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Ifak.Fast.Mediator.Timeseries.Archive;

/// <summary>
/// Provides storage for opaque, UTC-day-sized archive blocks keyed by channel and day number.
/// </summary>
/// <remarks>
/// <para>
/// This class abstracts only the physical persistence of archive blocks. It does not interpret
/// their contents. <see cref="ArchiveChannel"/> is responsible for grouping time-series values by
/// UTC day, serializing and compressing them.
/// </para>
/// <para>
/// A single storage instance is normally shared by all archive channels owned by one history
/// worker. The owner of the storage instance is responsible for disposing it; individual
/// <see cref="ArchiveChannel"/> instances do not own or dispose the storage.
/// </para>
/// <para>
/// Operations are synchronous. The abstraction does not provide transactions spanning multiple
/// days, guarantee durability across a process or machine failure, or require implementations to
/// support concurrent calls. Callers must serialize access unless an implementation explicitly
/// documents stronger guarantees.
/// </para>
/// </remarks>
public abstract class StorageBase: IDisposable
{
    /// <summary>
    /// Gets the lowest and highest day numbers for which the channel has stored data.
    /// </summary>
    /// <param name="channel">The channel whose stored day range is requested.</param>
    /// <returns>
    /// The inclusive bounding range of stored day numbers, or <see langword="null"/> when no data
    /// is stored for the channel. Days between the returned bounds are not necessarily present.
    /// </returns>
    public abstract (int dayStart, int dayEnd)? GetStoredDayNumberRange(ChannelRef channel);

    /// <summary>
    /// Stores a complete opaque data block for a channel and UTC day.
    /// </summary>
    /// <param name="channel">The channel to which the block belongs.</param>
    /// <param name="dayNumber">
    /// The UTC day number used by <see cref="ArchiveChannel"/>, expressed as days since the Unix
    /// epoch.
    /// </param>
    /// <param name="data">The complete serialized and compressed day block. The caller must not modify or reuse the array after calling this method.</param>
    /// <remarks>
    /// If a block already exists for the same channel and day, it is replaced.
    /// </remarks>
    public abstract void WriteDayData(ChannelRef channel, int dayNumber, byte[] data);

    /// <summary>
    /// Opens an opaque data block for a channel and UTC day.
    /// </summary>
    /// <param name="channel">The channel whose block is requested.</param>
    /// <param name="dayNumber">
    /// The UTC day number used by <see cref="ArchiveChannel"/>, expressed as days since the Unix
    /// epoch.
    /// </param>
    /// <returns>
    /// A readable stream positioned at the beginning of the stored block, or
    /// <see langword="null"/> when no block exists. The caller owns and must dispose a returned
    /// stream.
    /// </returns>
    public abstract Stream? ReadDayData(ChannelRef channel, int dayNumber);

    /// <summary>
    /// Deletes stored blocks for a channel over an inclusive range of UTC day numbers.
    /// </summary>
    /// <param name="channel">The channel whose blocks are deleted.</param>
    /// <param name="startDayNumberInclusive">The first day number to delete.</param>
    /// <param name="endDayNumberInclusive">The last day number to delete.</param>
    /// <remarks>
    /// Missing blocks are ignored. The abstraction does not make deletion of more than one block
    /// transactional.
    /// </remarks>
    public abstract void DeleteDayData(ChannelRef channel, int startDayNumberInclusive, int endDayNumberInclusive);

    /// <summary>
    /// Determines whether <see cref="Compact"/> is expected to reclaim enough storage space to be
    /// worth running.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when compaction is recommended; otherwise,
    /// <see langword="false"/>. The default implementation returns <see langword="false"/>.
    /// </returns>
    public virtual bool CanCompact() { return false; }

    /// <summary>
    /// Reclaims unused physical storage when supported by the implementation.
    /// </summary>
    /// <remarks>The default implementation performs no work.</remarks>
    public virtual void Compact() { }

    /// <summary>
    /// Releases connections, streams, and other resources owned by the storage implementation.
    /// </summary>
    public abstract void Dispose();
}
