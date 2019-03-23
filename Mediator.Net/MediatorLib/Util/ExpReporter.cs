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
    public static class ExpExtension
    {
        public static Exception InnerMost(this Exception exp) {
            Exception result = exp;
            while (result.InnerException != null)
                result = result.InnerException;
            return result;
        }
    }
}
