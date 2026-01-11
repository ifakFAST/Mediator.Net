// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.Timeseries
{
    public abstract class TimeSeriesDB
    {

        public enum Mode
        {
            ReadWrite,
            ReadOnly
        }

        public record OpenParams(
            string Name, 
            string ConnectionString,
            Mode ReadWriteMode,
            string[]? Settings = null,
            Duration? RetentionTime = null,
            Duration? RetentionCheckInterval = null);

        public abstract void Open(OpenParams parameter);

        public abstract bool IsOpen { get; }

        public abstract void Close();

        public abstract ChannelInfo[] GetAllChannels();

        public abstract bool ExistsChannel(string objectID, string variable);

        public abstract Channel GetChannel(string objectID, string variable);

        public abstract bool RemoveChannel(string objectID, string variable);

        protected (string name, string value) SplitSetting(string setting) {
            int i = setting.IndexOf("=");
            if (i < 0) throw new Exception("Missing = in setting: " + setting);
            string name = setting[0..i];
            string value = setting[(i + 1)..];
            return (name, value);
        }

        public virtual void ClearDatabase(OpenParams parameter) {
            if (IsOpen) {
                RemoveAllChannels();
            }
            else {
                Open(parameter);
                try {
                    RemoveAllChannels();
                }
                finally {
                    Close();
                }
            }
        }

        public virtual void RemoveAllChannels() {
            ChannelInfo[] channels = GetAllChannels();
            foreach (var c in channels) {
                RemoveChannel(c.Object, c.Variable);
            }
        }

        public virtual Channel CreateChannel(ChannelInfo channel) {
            return CreateChannels(new ChannelInfo[] { channel })[0];
        }

        public abstract Channel[] CreateChannels(ChannelInfo[] channels);

       /**
        * Execute prepared update actions in one batch. The individual updateActions
        * must not throw an exception but return non-null string in case of error.
        * The sequence of all error messages is returned (empty list when no error).
        * */
        public abstract string[] BatchExecute(Func<PrepareContext, string?>[] updateActions);

        /// <summary>
        /// Retrieves the amount of free/unused space within the database in percent (0.0 - 100.0).
        /// The higher this value, the more disk space will be reclaimed when running Vacuum().
        /// </summary>
        public virtual double? FreeSpacePercent() {
            return null;
        }

        public virtual void Vacuum() {
            
        }
    }

    public record ChannelInfo(
        string Object, 
        string Variable, 
        DataType Type) {

    }

    public readonly struct ChannelRef(string objectID, string variableName) : IEquatable<ChannelRef>
    {
        public static ChannelRef Make(string objectID, string variableName) {
            return new ChannelRef(objectID, variableName);
        }

        public string ObjectID => objectID ?? "";
        public string VariableName => variableName ?? "";

        public bool Equals(ChannelRef other) => objectID == other.ObjectID && variableName == other.VariableName;

        public override bool Equals(object? obj) {
            return obj is ChannelRef cRef && Equals(cRef);
        }

        public static bool operator ==(ChannelRef lhs, ChannelRef rhs) => lhs.Equals(rhs);

        public static bool operator !=(ChannelRef lhs, ChannelRef rhs) => !(lhs.Equals(rhs));

        public override string ToString() => ObjectID + "." + VariableName;

        public override int GetHashCode() => HashCode.Combine(objectID, variableName);
    }
}
