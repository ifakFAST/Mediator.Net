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
