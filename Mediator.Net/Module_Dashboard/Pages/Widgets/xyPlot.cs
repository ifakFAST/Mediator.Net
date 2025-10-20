// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets
{
    [IdentifyWidget(id: "xyPlot")]
    public class xyPlot : WidgetBaseWithConfig<XyPlotConfig>
    {
        private VariableRefUnresolved[] variablesUnresolved = [];
        private VariableRef[] variables = [];

        public override string DefaultHeight => "300px";
        public override string DefaultWidth => "100%";

        XyPlotConfig configuration => Config;

        public override Task OnActivate() {
            UpdateVariablesFromConfig();
            return Task.FromResult(true);
        }

        private void UpdateVariablesFromConfig() {
            var allVariables = new List<VariableRefUnresolved>();
            foreach (var series in configuration.DataSeries) {
                if (!string.IsNullOrEmpty(series.VariableX.Object.ToEncodedString()) && !string.IsNullOrEmpty(series.VariableX.Name)) {
                    allVariables.Add(series.VariableX);
                }
                if (!string.IsNullOrEmpty(series.VariableY.Object.ToEncodedString()) && !string.IsNullOrEmpty(series.VariableY.Name)) {
                    allVariables.Add(series.VariableY);
                }
            }
            variablesUnresolved = allVariables.ToArray();
            ResolveVariables();
        }

        private void ResolveVariables() {
            VariableRef[] newVariables = variablesUnresolved.Select(v => Context.ResolveVariableRef(v)).ToArray();
            if (!Arrays.Equals(newVariables, variables)) {
                variables = newVariables;
            }
        }

        public Task<ReqResult> UiReq_GetItemsData() {
            var allObjects = configuration.DataSeries
                .SelectMany(s => new[] { s.VariableX.Object, s.VariableY.Object })
                .Where(o => !string.IsNullOrEmpty(o.ToEncodedString()))
                .Distinct()
                .ToArray();
            return Common.GetNumericVarItemsData(Connection, allObjects);
        }

        public async Task<ReqResult> UiReq_SaveConfig(XyPlotConfig config) {
            configuration.PlotConfig = config.PlotConfig;
            configuration.DataSeries = config.DataSeries;
            await Context.SaveWidgetConfiguration(configuration);
            UpdateVariablesFromConfig();
            return ReqResult.OK();
        }

        private static RegressionInfo? CalcRegression(List<XyPoint> pts, string color) {

            int n = pts.Count;
            if (n < 2) return null; // throw new ArgumentException("Need at least two points");

            // Mittelwerte
            double sumX = 0, sumY = 0;
            for (int i = 0; i < n; i++) { sumX += pts[i].X; sumY += pts[i].Y; }
            double meanX = sumX / n, meanY = sumY / n;

            // Zentrierte Summen (numerisch stabiler)
            double sxx = 0, sxy = 0, syy = 0;
            for (int i = 0; i < n; i++) {
                double dx = pts[i].X - meanX;
                double dy = pts[i].Y - meanY;
                sxx += dx * dx;
                sxy += dx * dy;
                syy += dy * dy;
            }
            if (sxx <= 0) return null; // throw new InvalidOperationException("All X values are equal – slope undefined.");

            double slope = sxy / sxx;
            double offset = meanY - slope * meanX;

            //// R^2 (optional)
            //double sse = 0;
            //for (int i = 0; i < n; i++) {
            //    double yhat = offset + slope * pts[i].X;
            //    double e = pts[i].Y - yhat;
            //    sse += e * e;
            //}
            //double r2 = syy > 0 ? 1.0 - sse / syy : 1.0;

            return new RegressionInfo {
                Slope = slope,
                Offset = offset,
                Color = color
            };
        }

        public async Task<ReqResult> UiReq_LoadData(TimeRange timeRange, Dictionary<string, string> configVars) {

            Context.SetConfigVariables(configVars);

            Timestamp tStart = timeRange.GetStart();
            Timestamp tEnd = timeRange.GetEnd();

            if (timeRange.Type == TimeType.Last) {
                Timestamp Now = Timestamp.Now;
                tEnd = Now;
                tStart = Now - TimeRange.DurationFromTimeRange(timeRange);
            }

            ResolveVariables();

            var seriesDataList = new List<XySeriesData>();

            foreach (var seriesConfig in configuration.DataSeries) {

                VariableRef varX = Context.ResolveVariableRef(seriesConfig.VariableX);
                VariableRef varY = Context.ResolveVariableRef(seriesConfig.VariableY);

                if (string.IsNullOrEmpty(varX.Object.ToEncodedString()) || string.IsNullOrEmpty(varY.Object.ToEncodedString())) continue;

                try {
                    List<XyPoint> xyPoints = await LoadXYDataBackwards(varX, varY, tStart, tEnd, seriesConfig);

                    if (xyPoints.Count > 0) {

                        RegressionInfo? regression = null;
                        if (seriesConfig.ShowRegression == RegressionMode.Auto) {
                            regression = CalcRegression(xyPoints, seriesConfig.ColorRegression);
                        }

                        seriesDataList.Add(new XySeriesData {
                            Name = string.IsNullOrEmpty(seriesConfig.Name) ? $"{varX.Name} vs {varY.Name}" : seriesConfig.Name,
                            Color = seriesConfig.Color,
                            Size = seriesConfig.Size,
                            Checked = seriesConfig.Checked,
                            Points = xyPoints,
                            Regression = regression
                        });
                    }
                }
                catch (Exception) {
                    // Skip series if data cannot be loaded
                }
            }

            var result = new {
                Series = seriesDataList
            };

            return ReqResult.OK(result);
        }

        private async Task<List<XyPoint>> LoadXYDataBackwards(VariableRef varX, VariableRef varY, Timestamp tStart, Timestamp tEnd, XyPlotDataSeries seriesConfig) {

            const int MaxRawDataPerCall = 6000; // Maximum allowed by HistorianReadRaw
            int maxDisplayPoints = configuration.PlotConfig.MaxDataPoints;
            QualityFilter filter = configuration.PlotConfig.FilterByQuality;
            
            var resultPoints = new List<XyPoint>();
            Timestamp currentEnd = tEnd;

            // Load large ranges day by day to avoid reprocessing the entire dataset each iteration
            while (currentEnd >= tStart) {
                DateTime currentEndDate = currentEnd.ToDateTime();
                var dayStartDateTime = new DateTime(currentEndDate.Year, currentEndDate.Month, currentEndDate.Day, 0, 0, 0, DateTimeKind.Utc);
                Timestamp dayStart = Timestamp.FromDateTime(dayStartDateTime);
                if (dayStart < tStart) {
                    dayStart = tStart;
                }

                VTTQs dayDataY = await LoadRawDataRange(varY, dayStart, currentEnd, MaxRawDataPerCall, filter);
                if (dayDataY.Count > 0) {
                    VTTQs dayDataX = await LoadRawDataRange(varX, dayStart, currentEnd, MaxRawDataPerCall, filter);
                    if (dayDataX.Count > 0) {
                        List<VT> aggregatedX = ApplyAggregation(dayDataX, seriesConfig.Aggregation, seriesConfig.Resolution);
                        List<VT> aggregatedY = ApplyAggregation(dayDataY, seriesConfig.Aggregation, seriesConfig.Resolution);
                        List<XyPoint> dayPoints = PairXYData(aggregatedX, aggregatedY);
                        if (dayPoints.Count > 0) {
                            resultPoints.InsertRange(0, dayPoints);
                            if (resultPoints.Count >= maxDisplayPoints) {
                                int excess = resultPoints.Count - maxDisplayPoints;
                                resultPoints.RemoveRange(0, excess);
                                break;
                            }
                        }
                    }
                }
                currentEnd = dayStart.AddMillis(-1);
            }

            bool lastNGradient = seriesConfig.TimeHighlighting == TimeHighlightMode.LastN_Gradient;
            bool lastN = seriesConfig.TimeHighlighting == TimeHighlightMode.LastN;
            if ((lastN || lastNGradient) && resultPoints.Count > 0) {
                ColorRGB colorA = ParseColor(seriesConfig.Color);
                ColorRGB colorB = ParseColor(seriesConfig.TimeHighlightingColor);
                int highlightCount = Math.Min(seriesConfig.TimeHighlightingLastN, resultPoints.Count);
                for (int i = resultPoints.Count - highlightCount; i < resultPoints.Count; i++) {
                    string color = seriesConfig.TimeHighlightingColor;
                    if (lastNGradient && highlightCount > 1) {
                        double factor = (i - (resultPoints.Count - highlightCount)) / (double)(highlightCount - 1);
                        color = BlendColors(colorA, colorB, factor);
                    }
                    resultPoints[i].Color = color;
                }
            }

            return resultPoints;
        }

        record struct ColorRGB(int R, int G, int B);

        private static string BlendColors(ColorRGB rgb1, ColorRGB rgb2, double factor) {
            // Clamp factor between 0 and 1
            factor = Math.Max(0.0, Math.Min(1.0, factor));

            // Blend RGB components
            int r = (int)(rgb1.R + factor * (rgb2.R - rgb1.R));
            int g = (int)(rgb1.G + factor * (rgb2.G - rgb1.G));
            int b = (int)(rgb1.B + factor * (rgb2.B - rgb1.B));
            
            // Return as hex color
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static ColorRGB ParseColor(string color) {

            if (string.IsNullOrEmpty(color)) {
                return new ColorRGB(0, 0, 0);
            }
            
            // Remove # if present
            if (color.StartsWith("#")) {
                color = color[1..];
            }
            
            // Handle 3-digit hex colors (e.g., "F00" -> "FF0000")
            if (color.Length == 3) {
                color = $"{color[0]}{color[0]}{color[1]}{color[1]}{color[2]}{color[2]}";
            }
            
            // Parse 6-digit hex color
            if (color.Length == 6 && int.TryParse(color[0..2], System.Globalization.NumberStyles.HexNumber, null, out int r) &&
                int.TryParse(color[2..4], System.Globalization.NumberStyles.HexNumber, null, out int g) &&
                int.TryParse(color[4..6], System.Globalization.NumberStyles.HexNumber, null, out int b)) {
                return new ColorRGB(r, g, b);
            }
            
            // Handle common color names
            return color.ToLowerInvariant() switch {
                "red" => new ColorRGB(255, 0, 0),
                "green" => new ColorRGB(0, 128, 0),
                "blue" => new ColorRGB(0, 0, 255),
                "yellow" => new ColorRGB(255, 255, 0),
                "cyan" => new ColorRGB(0, 255, 255),
                "magenta" => new ColorRGB(255, 0, 255),
                "black" => new ColorRGB(0, 0, 0),
                "white" => new ColorRGB(255, 255, 255),
                "gray" or "grey" => new ColorRGB(128, 128, 128),
                "orange" => new ColorRGB(255, 165, 0),
                "purple" => new ColorRGB(128, 0, 128),
                "pink" => new ColorRGB(255, 192, 203),
                _ => new ColorRGB(0, 0, 0) // Default to black
            };
        }

        private async Task<VTTQs> LoadRawDataRange(VariableRef variable, Timestamp rangeStart, Timestamp rangeEnd, int maxValuesPerCall, QualityFilter filter) {
            VTTQs result = new VTTQs();
            if (rangeEnd < rangeStart) {
                return result;
            }

            Timestamp currentEnd = rangeEnd;

            while (true) {
                VTTQs chunk = await Connection.HistorianReadRaw(variable, rangeStart, currentEnd, maxValuesPerCall, BoundingMethod.TakeLastN, filter);
                if (chunk.Count == 0) {
                    break;
                }

                result.InsertRange(0, chunk);

                if (chunk.Count < maxValuesPerCall) {
                    break;
                }

                Timestamp nextEnd = chunk[0].T.AddMillis(-1);
                if (nextEnd < rangeStart) {
                    break;
                }

                currentEnd = nextEnd;
            }

            return result;
        }

        private static List<VT> ApplyAggregation(VTTQs data, AggregationMode mode, Duration resolution) {

            if (mode == AggregationMode.None || data.Count == 0 || resolution.TotalMilliseconds <= 0) {
                var res = new List<VT>(data.Count);
                for (int i = 0; i < data.Count; i++) {
                    double? doubleValue = data[i].V.AsDouble();
                    if (doubleValue.HasValue && !double.IsNaN(doubleValue.Value)) {
                        res.Add(new VT(data[i].T, doubleValue.Value));
                    }
                }
                return res;
            }

            var result = new List<VT>();
            long resMillis = resolution.TotalMilliseconds;

            Timestamp currentIntervalStart = Timestamp.FromJavaTicks((data[0].T.JavaTicks / resMillis) * resMillis);
            List<double> currentIntervalValues = [];

            foreach (var value in data) {
                double? vv = value.V.AsDouble();
                if (!vv.HasValue) continue;

                Timestamp intervalStart = Timestamp.FromJavaTicks((value.T.JavaTicks / resMillis) * resMillis);
                if (intervalStart != currentIntervalStart) {
                    if (currentIntervalValues.Count > 0) {
                        result.Add(AggregateInterval(currentIntervalStart, currentIntervalValues, mode));
                    }
                    currentIntervalStart = intervalStart;
                    currentIntervalValues.Clear();
                }
                currentIntervalValues.Add(vv.Value);
            }

            if (currentIntervalValues.Count > 0) {
                result.Add(AggregateInterval(currentIntervalStart, currentIntervalValues, mode));
            }

            return result;
        }

        private static VT AggregateInterval(Timestamp timestamp, List<double> values, AggregationMode mode) {
            double value = mode switch {
                AggregationMode.Average => values.Average(),
                AggregationMode.First => values.First(),
                AggregationMode.Last => values.Last(),
                _ => values.Average()
            };
            return new VT(timestamp, value);
        }

        record struct VT(Timestamp T, double V);

        private static List<XyPoint> PairXYData(List<VT> dataX, List<VT> dataY) {
            var points = new List<XyPoint>();

            if (dataX.Count == 0 || dataY.Count == 0) return points;

            // Create a dictionary for Y values by timestamp
            Dictionary<Timestamp, double> yByTime = dataY.ToDictionary(v => v.T, v => v.V);

            // Match X values with Y values by timestamp
            foreach (var xValue in dataX) {
                double x = xValue.V;

                if (yByTime.TryGetValue(xValue.T, out double y)) {
                    points.Add(new XyPoint {
                        X = x,
                        Y = y,
                        Time = xValue.T.JavaTicks
                    });
                }
            }
            return points;
        }

    }

    public sealed class XySeriesData {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
        public double Size { get; set; } = 3.0;
        public bool Checked { get; set; } = true;
        public List<XyPoint> Points { get; set; } = [];
        public RegressionInfo? Regression { get; set; } = null;
    }

    public sealed class RegressionInfo {
        public double Slope { get; set; } = 1.0;
        public double Offset { get; set; } = 0.0;
        public string Color { get; set; } = "black";
    }

    public sealed class XyPoint {
        public double X { get; set; }
        public double Y { get; set; }
        public long Time { get; set; }
        public string? Color { get; set; } = null;

        public bool ShouldSerializeColor() => Color != null;
    }

    public class XyPlotConfig
    {
        public XyPlotPlotConfig PlotConfig { get; set; } = new XyPlotPlotConfig();
        public XyPlotDataSeries[] DataSeries { get; set; } = [];
    }

    public sealed class XyPlotPlotConfig
    {
        public int MaxDataPoints { get; set; } = 5000;
        public QualityFilter FilterByQuality { get; set; } = QualityFilter.ExcludeBad;
        public string XAxisName { get; set; } = "";
        public string YAxisName { get; set; } = "";

        public bool XAxisStartFromZero { get; set; } = false;
        public bool YAxisStartFromZero { get; set; } = false;
        public double? XAxisLimitMin { get; set; } = null;
        public double? XAxisLimitMax { get; set; } = null;
        public double? YAxisLimitMin { get; set; } = null;
        public double? YAxisLimitMax { get; set; } = null;

        public bool ShowGrid { get; set; } = true;
        public bool ShowLegend { get; set; } = true;
        public bool Show45DegreeLine { get; set; } = true;
        public string Color45DegreeLine { get; set; } = "green";

        public bool ShouldSerializeXAxisLimitMin() => XAxisLimitMin.HasValue;
        public bool ShouldSerializeXAxisLimitMax() => XAxisLimitMax.HasValue;
        public bool ShouldSerializeYAxisLimitMin() => YAxisLimitMin.HasValue;
        public bool ShouldSerializeYAxisLimitMax() => YAxisLimitMax.HasValue;
    }

    public sealed class XyPlotDataSeries
    {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
        public double Size { get; set; } = 3.0;
        public bool Checked { get; set; } = true;

        public VariableRefUnresolved VariableX { get; set; }
        public VariableRefUnresolved VariableY { get; set; }

        public AggregationMode Aggregation { get; set; } = AggregationMode.None;
        public Duration Resolution { get; set; } = Duration.FromMinutes(10);

        public TimeHighlightMode TimeHighlighting { get; set; } = TimeHighlightMode.None;
        public int TimeHighlightingLastN { get; set; } = 1;
        public string TimeHighlightingColor { get; set; } = "black";

        public RegressionMode ShowRegression { get; set; } = RegressionMode.None;
        public string ColorRegression { get; set; } = "black";
    }

    public enum RegressionMode
    {
        None,
        Auto,
    }

    public enum AggregationMode
    {
        None,    // raw data (resolution is ignored)
        Average, // average of all points in the resolution interval
        First,   // the first point in the resolution interval
        Last,    // the last point in the resolution interval
    }

    public enum TimeHighlightMode
    {
        None, 
        LastN, // the last N points will have a different color
        LastN_Gradient // the last N points will have a gradient color from normal to the highlight color
    }
}
