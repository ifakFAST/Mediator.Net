// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ifak.Fast.Mediator.Calc
{
    public class Trigger_M_outof_N
    {
        private readonly long M;
        private readonly long N;
        private long counter = 0;
        private List<long> Warns;
        private bool warningActive = false;

        public Trigger_M_outof_N(int m, int n) {
            if (m > n) throw new ArgumentException("m must be smaller than n");
            Warns = new List<long>(m);
            N = n;
            M = m;
        }

        public Trigger GetTrigger(bool isOK) {

            long boundary = counter - N;

            if (isOK) {

                if (warningActive && counter >= N) {
                    Warns = Warns.Where(x => x >= boundary).ToList();
                    if (Warns.Count == 0) {
                        warningActive = false;
                        counter = 0;
                        Warns.Clear();
                        return Trigger.Off;
                    }
                }
            }
            else {

                Warns = Warns.Where(x => x >= boundary).ToList();
                Warns.Add(counter);

                if (!warningActive && Warns.Count >= M) {
                    warningActive = true;
                    counter = 0;
                    Warns.Clear();
                    return Trigger.On;
                }
            }

            counter += 1;
            return Trigger.Neutral;
        }
    }

    public enum Trigger
    {
        On,
        Off,
        Neutral
    }
}
