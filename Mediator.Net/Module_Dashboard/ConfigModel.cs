// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Ifak.Fast.Mediator.Dashboard
{
    [XmlRoot(Namespace = "Module_Dashboard", ElementName = "Dashboard_Model")]
    public class DashboardModel : ModelObject
    {
        [XmlIgnore]
        public string ID { get; set; } = "Root";

        [XmlIgnore]
        public string Name { get; set; } = "Root";

        public List<View> Views { get; set; } = new List<View>();
    }

    public class View : ModelObject
    {
        [XmlAttribute("id")]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        [XmlAttribute("name")]
        public string Name { get; set; } = "View Name";

        [XmlAttribute("type")]
        public string Type { get; set; } = "";

        [XmlAttribute("group")]
        public string Group { get; set; } = "";

        [ContainsNestedModel]
        public DataValue Config { get; set; }

    }
}
