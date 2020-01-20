// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ifak.Fast.Mediator
{
    public class Config
    {
        private readonly Dictionary<string, string> map;

        public Config(Dictionary<string, string> map) {
            this.map = map;
        }

        public Config(IEnumerable<NamedValue> namedValues) {
            this.map = new Dictionary<string, string>();
            foreach (NamedValue nv in namedValues)  {
                map[nv.Name] = nv.Value;
            }
        }

        public NamedValue[] ToNamedValues() => map.Keys.Select(k => new NamedValue(k, map[k])).ToArray();

        public bool GetOptionalBool(string name, bool defaultValue) {
            return map.ContainsKey(name) ? bool.Parse(map[name]) : defaultValue;
        }

        public int GetOptionalInt(string name, int defaultValue) {
            return map.ContainsKey(name) ? int.Parse(map[name], CultureInfo.InvariantCulture) : defaultValue;
        }

        public long GetOptionalLong(string name, long defaultValue) {
            return map.ContainsKey(name) ? long.Parse(map[name], CultureInfo.InvariantCulture) : defaultValue;
        }

        public double GetOptionalDouble(string name, double defaultValue) {
            return map.ContainsKey(name) ? double.Parse(map[name], CultureInfo.InvariantCulture) : defaultValue;
        }

        public string GetOptionalString(string name, string defaultValue) {
            return map.ContainsKey(name) ? map[name] : defaultValue;
        }

        public string GetString(string name) {
            if (!map.ContainsKey(name)) throw new Exception("Missing config item: " + name);
            return map[name];
        }

        public bool GetBool(string name) {
            if (!map.ContainsKey(name)) throw new Exception("Missing config item: " + name);
            return bool.Parse(map[name]);
        }

        public int GetInt(string name) {
            if (!map.ContainsKey(name)) throw new Exception("Missing config item: " + name);
            return int.Parse(map[name], CultureInfo.InvariantCulture);
        }

        public long GetLong(string name) {
            if (!map.ContainsKey(name)) throw new Exception("Missing config item: " + name);
            return long.Parse(map[name], CultureInfo.InvariantCulture);
        }

        public double GetDouble(string name) {
            if (!map.ContainsKey(name)) throw new Exception("Missing config item: " + name);
            return double.Parse(map[name], CultureInfo.InvariantCulture);
        }

        public Guid GetGuid(string name) {
            if (!map.ContainsKey(name)) throw new Exception("Missing config item: " + name);
            return new Guid(map[name]);
        }
    }
}
