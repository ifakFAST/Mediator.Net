using System.Collections.Generic;

namespace Ifak.Fast.Mediator.Calc
{
    public class Calculation
    {
        public string ID { get; set; } = "";

        public string Name { get; set; } = "";

        public Duration Cycle { get; set; }

        public Duration Offset { get; set; }

        public string Definition { get; set; } = ""; // e.g. C# code, SIMBA project file name

        public bool WindowVisible { get; set; } = false;

        public double RealTimeScale { get; set; } = 1;

    }
}
