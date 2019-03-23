// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;

namespace Ifak.Fast.Mediator.Util
{
    public class Xml
    {
        public static string ToXml<T>(T model) {
            var x = new System.Xml.Serialization.XmlSerializer(model.GetType());
            var writer = new Utf8StringWriter();
            x.Serialize(writer, model);
            return writer.ToString();
        }

        public static T FromXmlString<T>(string xml) {
            var x = new System.Xml.Serialization.XmlSerializer(typeof(T));
            var source = new StringReader(xml);
            return (T)x.Deserialize(source);
        }

        public static T FromXmlStream<T>(Stream source) {
            var x = new System.Xml.Serialization.XmlSerializer(typeof(T));
            return (T)x.Deserialize(source);
        }

        public static T FromXmlFile<T>(string fileName) {
            using (var fstream = new FileStream(fileName, FileMode.Open)) {
                return FromXmlStream<T>(fstream);
            }
        }
    }

    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }
    }
}
