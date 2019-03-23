// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Util
{
    public class ClientDefs
    {
        public static int strHash(string str) {
            char[] value = str.ToCharArray();
            int h = 0;
            for (int i = 0; i < value.Length; i++) {
                h = 31 * h + value[i];
            }
            return h;
        }
    }
}
