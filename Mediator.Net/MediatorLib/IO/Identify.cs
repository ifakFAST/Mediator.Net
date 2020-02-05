// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.IO
{
    [AttributeUsage(AttributeTargets.Class)]
    public class Identify : Attribute
    {
        public string ID { get; set; }

        public Identify(string id) {
            ID = id;
        }
    }
}
