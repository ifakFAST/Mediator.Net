using System;

namespace Ifak.Fast.Mediator.Calc
{
    [AttributeUsage(AttributeTargets.Class)]
    public class Identify : Attribute
    {
        public string ID { get; set; }
        public bool Show_WindowVisible { get; set; }
        public bool Show_Definition { get; set; }
        public bool DefinitionIsCode { get; set; }
        public string DefinitionLabel { get; set; }

        public Identify(string id) {
            ID = id;
            DefinitionLabel = "";
        }

        public Identify(string id, bool showWindowVisible, bool showDefinition, string definitionLabel, bool definitionIsCode) {
            ID = id;
            Show_WindowVisible = showWindowVisible;
            Show_Definition = showDefinition;
            DefinitionLabel = definitionLabel;
            DefinitionIsCode = definitionIsCode;
        }
    }
}
