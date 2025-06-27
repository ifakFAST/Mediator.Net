// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Calc;

// [Identify("Test")]
public class TestCalc : CalculationBase
{

    public override Task<InitResult> Initialize(InitParameter parameter, AdapterCallback callback) {
        return Task.FromResult(new InitResult());
    }

    public override Task Shutdown() {
        return Task.FromResult(true);
    }

    public override Task<StepResult> Step(Timestamp t, Duration dt, InputValue[] inputValues) {

        VTQ a = inputValues[0].Value;
        VTQ b = inputValues[1].Value;

        float res = (float)(a.V.AsDouble()! + b.V.AsDouble()!);
        Thread.Sleep(100);

        VTQ r = VTQ.Make(res, t, GetWorstOf(a.Q, b.Q));
        var result = new StepResult() {
            Output = new OutputValue[] {
                new OutputValue() {
                    OutputID = "Y",
                    Value = r
                }
            }
        };
        return Task.FromResult(result);
    }

    private static Quality GetWorstOf(params Quality[] qualities) {
        Quality res = Quality.Good;
        foreach (Quality q in qualities) {
            if (q == Quality.Bad) {
                return Quality.Bad;
            }
            else if (q == Quality.Uncertain) {
                res = Quality.Uncertain;
            }
        }
        return res;
    }
}
