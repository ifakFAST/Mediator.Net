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
/// Day numbers are signed counts of UTC days relative to January 1, 1970: zero identifies that
/// date and negative values identify dates before it. Supported day numbers range from
/// <see cref="MinDayNumber"/> through <see cref="MaxDayNumber"/>, corresponding to the range of
/// dates supported by <see cref="DateTime"/>.
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
    /// <summary>The day number for January 1, 0001 UTC, the earliest supported UTC day.</summary>
    public const int MinDayNumber = -719162;

    /// <summary>The day number for December 31, 9999 UTC, the latest supported UTC day.</summary>
    public const int MaxDayNumber = 2932896;

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
    /// The signed UTC day number used by <see cref="ArchiveChannel"/>, expressed as days relative
    /// to the Unix epoch. Negative values identify days before the epoch.
    /// </param>
    /// <param name="data">The complete serialized and compressed day block. The caller must not modify or reuse the array after calling this method.</param>
    /// <remarks>
    /// If a block already exists for the same channel and day, it is replaced.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dayNumber"/> is outside the inclusive range from
    /// <see cref="MinDayNumber"/> through <see cref="MaxDayNumber"/>.
    /// </exception>
    public abstract void WriteDayData(ChannelRef channel, int dayNumber, byte[] data);

    /// <summary>
    /// Opens an opaque data block for a channel and UTC day.
    /// </summary>
    /// <param name="channel">The channel whose block is requested.</param>
    /// <param name="dayNumber">
    /// The signed UTC day number used by <see cref="ArchiveChannel"/>, expressed as days relative
    /// to the Unix epoch. Negative values identify days before the epoch.
    /// </param>
    /// <returns>
    /// A readable stream positioned at the beginning of the stored block, or
    /// <see langword="null"/> when no block exists. The caller owns and must dispose a returned
    /// stream.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dayNumber"/> is outside the inclusive range from
    /// <see cref="MinDayNumber"/> through <see cref="MaxDayNumber"/>.
    /// </exception>
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// Either range endpoint is outside the inclusive range from <see cref="MinDayNumber"/>
    /// through <see cref="MaxDayNumber"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="startDayNumberInclusive"/> is greater than
    /// <paramref name="endDayNumberInclusive"/>.
    /// </exception>
    public abstract void DeleteDayData(ChannelRef channel, int startDayNumberInclusive, int endDayNumberInclusive);

    protected static void ValidateDayNumber(int dayNumber, string paramName) {
        if (dayNumber < MinDayNumber || dayNumber > MaxDayNumber) {
            throw new ArgumentOutOfRangeException(
                paramName,
                dayNumber,
                $"Day number must be between {MinDayNumber} and {MaxDayNumber}, inclusive.");
        }
    }

    protected static void ValidateDayNumberRange(
        int startDayNumberInclusive,
        int endDayNumberInclusive,
        string startParamName,
        string endParamName) {

        ValidateDayNumber(startDayNumberInclusive, startParamName);
        ValidateDayNumber(endDayNumberInclusive, endParamName);

        if (startDayNumberInclusive > endDayNumberInclusive) {
            throw new ArgumentException(
                "The start day number must be less than or equal to the end day number.",
                startParamName);
        }
    }

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
