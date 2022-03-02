using System;
using System.Linq;
using System.Collections.Generic;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Calc.Adapter_CSharp; // for State

namespace Std {

    public class DelayValue {

        public Duration Resolution;
        public Duration Delay;
        public DataValue DefaultValue;

        private readonly StateStructArray<VTQ> stateBuffer = new StateStructArray<VTQ>(name: "buffer", defaultValue: new VTQ[0]);

        public DelayValue(Duration resolution, Duration delay, DataValue defaultValue) {
            this.Resolution = resolution;
            this.Delay = delay;
            this.DefaultValue = defaultValue;
        }

        public VTQ Step(Timestamp t, VTQ x) {

            VTQ[] buffer = stateBuffer.Value ?? new VTQ[0];

            var (idx1, dist1) = MinDistIndex(buffer, x.T);

            if (dist1 >= Resolution) {
                List<VTQ> buff = buffer.ToList();
                buff.Add(x);
                buffer = buff.OrderBy(v => v.T).ToArray();
                stateBuffer.Value = buffer;
            }

            var (idx, dist) = MinDistIndex(buffer, t - Delay);

            if (idx < 0) {
                return VTQ.Make(DefaultValue, t, Quality.Bad);
            }

            VTQ res = buffer[idx];
            if (idx > 0) {
                stateBuffer.Value = buffer.Skip(idx).ToArray();
            }

            if (dist < Resolution)
                return res;
            else
                return VTQ.Make(DefaultValue, t, Quality.Bad);
        }

        private static (int idx, Duration dist) MinDistIndex(IList<VTQ> buffer, Timestamp t) {

            Duration minDist = Duration.FromDays(1000);
            int minIdx = -1;

            if (buffer != null) {
                for (int i = 0; i < buffer.Count; ++i) {
                    VTQ vtq = buffer[i];
                    Duration dist = (vtq.T - t).Abs();
                    if (dist < minDist) {
                        minDist = dist;
                        minIdx = i;
                    }
                }
            }
            return (minIdx, minDist);
        }
    }
    
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
