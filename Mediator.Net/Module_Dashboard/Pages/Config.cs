// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Json.Linq;
using Ifak.Fast.Mediator.Util;
using System.Collections.Generic;
using System.Linq;

namespace Ifak.Fast.Mediator.Dashboard.Pages
{
    public class Config : ModelObject
    {
        protected override string GetID(IReadOnlyCollection<IModelObject> parents) {
            var parentView = (Dashboard.View)parents.First();
            return parentView.ID + ".Config";
        }

        protected override string GetDisplayName(IReadOnlyCollection<IModelObject> parents) {
            var parentView = (Dashboard.View)parents.First();
            return parentView.Name + ".Config";
        }

        public bool HideHeader { get; set; } = false;

        public ConfigVariable[] ConfigVariables { get; set; } = [];

        public Page[] Pages { get; set; } = [];
    }

    public class ConfigVariable 
    {
        public string ID { get; set; } = "";
        public string DefaultValue { get; set; } = "";
    }


    public class Page : ModelObject 
    {
        protected override string GetID(IReadOnlyCollection<IModelObject> parents) {
            var parentView = (Dashboard.View)parents.Skip(1).First();
            return parentView.ID + ".Page." + ID;
        }

        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
        public Row[] Rows { get; set; } = [];

        protected override Variable[] GetVariablesOrNull(IReadOnlyCollection<IModelObject> parents) {

            var variable = new Variable(
                    name: "ActionLog",
                    type: DataType.Struct,
                    defaultValue: DataValue.FromDataType(DataType.Struct, dimension: 1),
                    unit: "",
                    typeConstraints: "",
                    dimension: 1,
                    dimensionNames: [],
                    remember: false,
                    history: History.None,
                    writable: false,
                    syncReadable: false);

            return new Variable[] { variable };
        }
    }

    public class Row
    {
        public Column[] Columns { get; set; } = [];
    }

    public class Column
    {
        public ColumnWidth Width { get; set; } = ColumnWidth.Fill;
        public Widget[] Widgets { get; set; } = [];
    }

    public class Widget
    {
        public string ID { get; set; } = "";   // id of the instance (unique per page)
        public string Type { get; set; } = ""; // id of the widget type

        public string Title { get; set; } = "";
        public string Width { get; set; } = "";
        public string Height { get; set; } = "";

        public JObject Config { get; set; } = new JObject();

        public bool ShouldSerializeTitle() => Title != "";

        public Widget() { }

        public Widget(string id, string type, string width = "", string height = "", JObject? config = null) {
            ID = id;
            Type = type;
            Width = width;
            Height = height;
            Config = config ?? new JObject();
        }
    }

    public enum ColumnWidth
    {
        Fill = 0,
        Auto = -1, // as width as required by widget
        OneOfTwelve = 1,
        TwoOfTwelve = 2,
        ThreeOfTwelve = 3,
        FourOfTwelve = 4,
        FiveOfTwelve = 5,
        SixOfTwelve = 6,
        SevenOfTwelve = 7,
        EightOfTwelve = 8,
        NineOfTwelve = 9,
        TenOfTwelve = 10,
        ElevenOfTwelve = 11,
        TwelveOfTwelve = 12,
    }

    public static class ConfigFromHistoryPlot
    {
        public static Config Convert(View_HistoryPlots.ViewConfig? c) {
            if (c == null) return new Config();
            return new Config() {
                Pages = c.Tabs.Select(tab => PageFromTab(tab, c.DataExport)).ToArray()
            };
        }

        private static Page PageFromTab(View_HistoryPlots.TabConfig tab, View_HistoryPlots.DataExport dataExport) {

            var hc = new Widgets.HistoryPlotConfig() {
                PlotConfig = MapPlotConfig(tab.PlotConfig),
                Items = tab.Items.Select(MapItemConfig).ToArray(),
                DataExport = MapDataExport(dataExport)
            };

            JObject config = StdJson.ObjectToJObject(hc, indented: true);

            return new Page() {
                ID = tab.Name,
                Name = tab.Name,
                Rows = new Row[] {
                    new Row() {
                        Columns = new Column[] {
                            new Column() {
                                Width = ColumnWidth.Fill,
                                Widgets = new Widget[] {
                                    new Widget("w1", "HistoryPlot", width: "100%", height: "600px", config: config)
                                }
                            }
                        }
                    }
                }
            };
        }

        private static Widgets.PlotConfig MapPlotConfig(View_HistoryPlots.PlotConfig plot) {
            return new Widgets.PlotConfig() {
                MaxDataPoints = plot.MaxDataPoints,
                FilterByQuality = plot.FilterByQuality,
                LeftAxisName = plot.LeftAxisName,
                LeftAxisStartFromZero = plot.LeftAxisStartFromZero,
                RightAxisName = plot.RightAxisName,
                RightAxisStartFromZero = plot.RightAxisStartFromZero,
                LeftAxisLimitY = plot.LeftAxisLimitY,
                RightAxisLimitY = plot.RightAxisLimitY,
            };
        }

        private static Widgets.ItemConfig MapItemConfig(View_HistoryPlots.ItemConfig it) {
            return new Widgets.ItemConfig() {
                Name = it.Name,
                Color = it.Color,
                Size = it.Size,
                SeriesType = MapSeriesType(it.SeriesType),
                Axis = MapAxis(it.Axis),
                Checked = it.Checked,
                Variable = new VariableRefUnresolved(it.Variable.Object, it.Variable.Name),
            };
        }

        private static Widgets.SeriesType MapSeriesType(View_HistoryPlots.SeriesType type) {
            switch (type) {
                case View_HistoryPlots.SeriesType.Scatter: return Widgets.SeriesType.Scatter;
                case View_HistoryPlots.SeriesType.Line: return Widgets.SeriesType.Line;
                default: return Widgets.SeriesType.Line;
            }
        }

        private static Widgets.Axis MapAxis(View_HistoryPlots.Axis type) {
            switch (type) {
                case View_HistoryPlots.Axis.Left: return Widgets.Axis.Left;
                case View_HistoryPlots.Axis.Right: return Widgets.Axis.Right;
                default: return Widgets.Axis.Left;
            }
        }

        private static Widgets.DataExport MapDataExport(View_HistoryPlots.DataExport de) {
            return new Widgets.DataExport() {
                CSV = new Widgets.CsvDataExport() {
                    ColumnSeparator = de.CSV.ColumnSeparator,
                    TimestampFormat = de.CSV.TimestampFormat,
                },
                Spreadsheet = new Widgets.SpreadsheetDataExport() {
                    TimestampFormat = de.Spreadsheet.TimestampFormat,
                }
            };
        }
    }

    public static class Example
    {
        public static Config Get() {
            return new Config() {
                Pages = new Page[] {

                    new Page() {
                        ID = "p1",
                        Name = "Overview",
                        Rows = new Row[] {

                            new Row() {
                                Columns = new Column[] {
                                    new Column() {
                                        Width = ColumnWidth.Fill,
                                        Widgets = new Widget[] {
                                            new Widget("w1", "VarTable")
                                        }
                                    },
                                    new Column() {
                                        Width = ColumnWidth.Fill,
                                        Widgets = new Widget[] {
                                            new Widget("w2", "HistoryPlot")
                                        }
                                    }
                                }
                            },

                            new Row() {
                                Columns = new Column[] {
                                    new Column() {
                                        Width = ColumnWidth.Fill,
                                        Widgets = new Widget[] {
                                            new Widget("w3", "HistoryPlot")
                                        }
                                    }
                                }
                            }

                        }
                    },

                    new Page() {
                        ID = "p2",
                        Name = "Details Pump 1",
                        Rows = new Row[] {

                            new Row() {
                                Columns = new Column[] {
                                    new Column() {
                                        Width = ColumnWidth.Fill,
                                        Widgets = new Widget[] {
                                            new Widget("w1", "VarTable")
                                        }
                                    }
                                }
                            },

                        }
                    },


                    new Page() {
                        ID = "p3",
                        Name = "Details Pump 2",
                        Rows = new Row[] {

                            new Row() {
                                Columns = new Column[] {
                                    new Column() {
                                        Width = ColumnWidth.Fill,
                                        Widgets = new Widget[] {
                                            new Widget("w1", "VarTable")
                                        }
                                    }
                                }
                            },

                        }
                    }

                }
            };
        }
    }
}
