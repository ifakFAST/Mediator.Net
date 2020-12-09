using System;
using System.Linq;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Calc.Adapter_CSharp; // for State

namespace Std {

    public class PI {

        public bool Invert = false;
        public double K;    // controller gain
        public Duration Tn; // integral time constant
        public double OutMin;
        public double OutMax;
       
        private readonly State integral = new State(name: "Integral", unit: "", defaultValue: 0.0);
        public double Integral => integral.Value;

        public PI(bool invert, double K, Duration Tn, double outMin, double outMax) {
            this.Invert = invert;
            this.K = K;
            this.Tn = Tn;
            this.OutMin = outMin;
            this.OutMax = outMax;
        }

        public double Step(double setpoint, double measurement, Duration dt) {
            
            double error = Invert ? (measurement - setpoint) : (setpoint - measurement);
            double proportional = K * error;
            integral.Value = integral + K / Tn.TotalMinutes * error * dt.TotalMinutes;

            double integralLimitMin = Math.Min(0, OutMin - proportional);
            double integralLimitMax = Math.Max(0, OutMax - proportional);
            integral.Value = Limit(integral, min: integralLimitMin, max: integralLimitMax);

            double output = proportional + integral;
            output = Limit(output, min: OutMin, max: OutMax);

            return output;
        }

        double Limit(double x, double min, double max) { return Math.Min(max, Math.Max(min, x)); }
    }
    
    public class PT1 {
        
        public double K;
        public Duration T;
        
        private readonly State yLast;
        
        public PT1(double K, Duration T, double y0 = 0.0) {
            this.K = K;
            this.T = T;
            this.yLast = new State(name: "yLast", unit: "", defaultValue: y0);
        }
        
        public double Step(double u, Duration dt) {
            double Tstar = 1.0 / ((T.TotalMinutes / dt.TotalMinutes) + 1.0);
            yLast.Value = Tstar * (K * u - yLast) + yLast;
            return yLast;
        }
        
        public double Step(double? u, Duration dt) {
            if (u.HasValue) {
                return Step(u.Value, dt: dt);
            }
            else {
                return yLast;
            }
        }
    }
    
    public class Util {
        
        public static VTQ Average(params VTQ[] vtqs) => Average(0, vtqs);

        public static VTQ Average(double defaultValue, params VTQ[] vtqs) {

            if (vtqs.Length == 0) throw new ArgumentException("Average requires at least one parameter");

            if (vtqs.Length == 1) {
                return vtqs[0];
            }

            double sum = 0;
            double count = 0;
            Quality q = Quality.Good;
            Timestamp t = Timestamp.Empty;

            foreach (VTQ vtq in vtqs) {
                double? v = vtq.V.AsDouble();
                if (vtq.Q != Quality.Bad && v.HasValue) {

                    sum += v.Value;
                    count += 1;

                    if (vtq.Q == Quality.Uncertain) {
                        q = Quality.Uncertain;
                    }

                    if (vtq.T > t) {
                        t = vtq.T;
                    }
                }
            }

            if (count == 0) {
                Timestamp tMax = vtqs.Select(v => v.T).Max();
                return VTQ.Make(defaultValue, tMax, Quality.Bad);
            }
            else {
                double avg = sum / count;
                return VTQ.Make(avg, t, q);
            }
        }        
    }
}
