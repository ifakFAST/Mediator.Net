// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Ifak.Fast.Mediator.IO
{
    public class LinearFunctionParser
    {

        public static Func<double, double> MakeConversion(string conversion) {

            if (string.IsNullOrWhiteSpace(conversion)) {
                return x => x;
            }

            if (TryParseNumber(conversion, out double factor)) {
                return (x) => factor * x;
            }

            if (conversion.Contains('/') && !conversion.Contains('x')) {
                double? frac = ParseFraction(conversion);
                if (frac.HasValue) {
                    double m = frac.Value;
                    return (x) => m * x;
                }
            }

            if (conversion.Contains('x')) {
                string[] array = conversion.Split('x');
                if (array.Length == 2) {
                    string leftX = array[0].Trim();
                    string rightX = array[1].Trim();
                    if (leftX.EndsWith('*') && (rightX.Length == 0 || rightX.StartsWith('+') || rightX.StartsWith('-'))) {
                        // e.g.: 9/5 * x + 32
                        string strM = leftX[0..^1];
                        string strN = rightX.Length == 0 ? "" : rightX[1..];
                        bool offsetPlus = rightX.Length == 0 ? true : rightX[0] == '+';
                        double? fracM = ParseNumberOrFraction(strM);
                        double? offset = strN == "" ? 0 : ParseNumberOrFraction(strN);
                        if (fracM.HasValue && offset.HasValue) {
                            double m = fracM.Value;
                            double n = offsetPlus ? offset.Value : -1.0 * offset.Value;
                            return (x) => m * x + n;
                        }
                    }
                    else if (leftX.EndsWith('(') && rightX.EndsWith(')') && (rightX.StartsWith('+') || rightX.StartsWith('-'))) {
                        // e.g.: 5/9 * (x - 32)
                        string strM = leftX[0..^1].Trim();
                        if (strM.EndsWith('*')) {
                            strM = strM[0..^1];
                            double? fracM = ParseNumberOrFraction(strM);
                            string strN = rightX[1..^1];
                            bool offsetPlus = rightX[0] == '+';
                            double? offset = ParseNumberOrFraction(strN);
                            if (fracM.HasValue && offset.HasValue) {
                                double m = fracM.Value;
                                double n = offsetPlus ? (m * offset.Value) : (-1.0 * m * offset.Value);
                                return (x) => m * x + n;
                            }
                        }
                    }
                }
            }

            throw new Exception($"Failed to analyze conversion: {conversion}");
        }

        static double? ParseNumberOrFraction(string str) {
            if (TryParseNumber(str, out double num)) {
                return num;
            }
            return ParseFraction(str);
        }

        static double? ParseFraction(string str) {
            string[] array = RemoveOuterParanthesis(str).Split('/');
            if (array.Length == 2 && TryParseNumber(array[0], out double left) && TryParseNumber(array[1], out double right)) {
                return left / right;
            }
            return null;
        }

        static bool TryParseNumber(string str, out double value) {
            return double.TryParse(RemoveOuterParanthesis(str), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }

        static string RemoveOuterParanthesis(string str) {
            str = str.Trim();
            while (str.Length > 2 && str[0] == '(' && str[^1] == ')') {
                str = str[1..^1];
            }
            return str;
        }
    }
}
