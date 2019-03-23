// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Ifak.Fast.Mediator.EventLog
{
    [XmlRoot(Namespace = "Module_EventLog", ElementName = "EventLog_Model")]
    public class EventLogConfig : ModelObject
    {
        [XmlIgnore]
        public string ID { get; set; } = "Root";

        [XmlIgnore]
        public string Name { get; set; } = "EventLog_Model";

        protected override Variable[] GetVariablesOrNull(IEnumerable<IModelObject> parents) {

            var variable = new Variable(
                name: "LastEvent",
                type: DataType.Struct,
                dimension: 1,
                defaultValue: DataValue.FromJSON("{}"),
                remember: false,
                history: History.None
            );

            return new Variable[] {
               variable
            };
        }
    }
}
