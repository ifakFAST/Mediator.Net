using System;

namespace Ifak.Fast.Mediator.Calc
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
