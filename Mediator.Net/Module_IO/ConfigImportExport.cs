using System.Collections.Generic;
using Ifak.Fast.Mediator.IO.Config;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using System;
using System.Globalization;

namespace Ifak.Fast.Mediator.IO;

public static class ConfigImportExport {

    public record FlatDataItem(
        string Adapter,
        string[] ParentNodes,
        string ID,
        string Name,
        string Unit,
        bool Read,
        bool Write,
        string Type,
        int Dimension, // 1 = scalar, 0 = variable len array, >1 = fixed len array
        string Address,
        string Location,
        string ConversionRead,
        string ConversionWrite,
        string Comment
    );

    public static FlatDataItem[] CreateFlatDataItemsFromExcel(byte[] excelData) {

        using var stream = new MemoryStream(excelData);
        using var workbook = new XLWorkbook(stream);

        if (!workbook.TryGetWorksheet("DataItems", out var sheet)) {
            sheet = workbook.Worksheets.First();
        }

        // Get header row and map column names to their indices
        var headerRow = sheet.Row(1);
        var headerIndices = new Dictionary<string, int>();
        foreach (var cell in headerRow.CellsUsed()) {
            headerIndices[cell.Value.ToString()] = cell.Address.ColumnNumber;
        }

        if (!headerIndices.ContainsKey("Adapter")) {
            throw new Exception("The worksheet has no column 'Adapter'");
        }

        if (!headerIndices.ContainsKey("ID")) {
            throw new Exception("The worksheet has no column 'ID'");
        }

        var items = new List<FlatDataItem>();
        IEnumerable<IXLRangeRow> rows = sheet.RangeUsed().RowsUsed().Skip(1); // Skip header row

        foreach (IXLRangeRow row in rows) {

            string str(string header) {
                if (!headerIndices.TryGetValue(header, out int column)) {
                    return "";
                }
                return row.Cell(column).Value.ToString(CultureInfo.InvariantCulture).Trim();
            }

            int integer(string header, int defaultValue) {
                if (!headerIndices.TryGetValue(header, out int column)) {
                    return defaultValue;
                }
                var val = row.Cell(column).Value;
                if (val.IsNumber) return (int)val.GetNumber();
                if (val.IsBlank) return defaultValue;
                if (val.IsText) return int.Parse(str(header));
                return defaultValue;
            }

            bool isRead(string header, bool defaultValue) {
                string s = str(header).ToLowerInvariant().Trim();
                if (s == "") return defaultValue;
                if (s.Contains("read")) return true;
                if (s.Contains("r/w")) return true;
                return false;
            }

            bool isWrite(string header, bool defaultValue) {
                string s = str(header).ToLowerInvariant().Trim();
                if (s == "") return defaultValue;
                if (s.Contains("write")) return true;
                if (s.Contains("r/w")) return true;
                return false;
            }

            var parentNodes = str("Parent Nodes").Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            string adapter = str("Adapter");
            string id = str("ID");

            if (adapter == "" || id == "") {
                continue;
            }

            items.Add(new FlatDataItem(
                Adapter: adapter,
                ParentNodes: parentNodes,
                ID: id,
                Name: str("Name"),
                Unit: str("Unit"),
                Read: isRead("Access", defaultValue: true),
                Write: isWrite("Access", defaultValue: false),
                Type: str("Type"),
                Dimension: integer("Dimension", defaultValue: 1),
                Address: str("Address"),
                Location: str("Location"),
                ConversionRead: str("Conversion Read"),
                ConversionWrite: str("Conversion Write"),
                Comment: str("Comment")
            ));
        }

        return items.ToArray();
    }

    public static byte[] CreateExcelFromFlatDataItems(FlatDataItem[] items) {

        static string ReadWrite(FlatDataItem it) => it.Read ? (it.Write ? "R/W" : "Read") : (it.Write ? "Write" : "");

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("DataItems");

        // Set up header row
        sheet.Cell(1, 1).Value = "Adapter";
        sheet.Cell(1, 2).Value = "Parent Nodes";
        sheet.Cell(1, 3).Value = "ID";
        sheet.Cell(1, 4).Value = "Name";
        sheet.Cell(1, 5).Value = "Unit";
        sheet.Cell(1, 6).Value = "Access";
        sheet.Cell(1, 7).Value = "Type";
        sheet.Cell(1, 8).Value = "Dimension";
        sheet.Cell(1, 9).Value = "Address";
        sheet.Cell(1, 10).Value = "Location";
        sheet.Cell(1, 11).Value = "Conversion Read";
        sheet.Cell(1, 12).Value = "Conversion Write";
        sheet.Cell(1, 13).Value = "Comment";

        sheet.Range("A1:M1").Style.Border.SetBottomBorder(XLBorderStyleValues.Medium);
        sheet.Range("A1:M1").Style.Font.SetBold(true);
        sheet.Range("A1:M1").Style.Fill.SetBackgroundColor(XLColor.LightGray);
        sheet.SheetView.FreezeRows(1);

        // Fill in data
        for (int i = 0; i < items.Length; i++) {
            var item = items[i];
            int rowIdx = i + 2;  // +2 because Excel is 1-indexed and the first row is the header

            sheet.Cell(rowIdx, 1).Value = item.Adapter;
            sheet.Cell(rowIdx, 2).Value = string.Join(" / ", item.ParentNodes);
            sheet.Cell(rowIdx, 3).Value = item.ID;
            sheet.Cell(rowIdx, 4).Value = item.Name;
            sheet.Cell(rowIdx, 5).Value = item.Unit;
            sheet.Cell(rowIdx, 6).Value = ReadWrite(item);
            sheet.Cell(rowIdx, 7).Value = item.Type;
            sheet.Cell(rowIdx, 8).Value = item.Dimension;
            sheet.Cell(rowIdx, 9).Value = item.Address;
            sheet.Cell(rowIdx, 10).Value = item.Location;
            sheet.Cell(rowIdx, 11).Value = item.ConversionRead;
            sheet.Cell(rowIdx, 12).Value = item.ConversionWrite;
            sheet.Cell(rowIdx, 13).Value = item.Comment;
        }

        // Optionally, adjust columns to fit the content
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static FlatDataItem[] Model2FlatDataItems(IO_Model model) {
        var items = new List<FlatDataItem>();
        foreach (var adapter in model.GetAllAdapters()) {
            FlattenAdapter(adapter, items);
        }
        return items.ToArray();
    }

    public static FlatDataItem[] Adapter2FlatDataItems(Config.Adapter adapter) {
        var items = new List<FlatDataItem>();
        FlattenAdapter(adapter, items);
        return items.ToArray();
    }

    private static void FlattenAdapter(Config.Adapter adapter, List<FlatDataItem> flatDataItems) {        

        foreach (var dataItem in adapter.DataItems) {
            var flatItem = new FlatDataItem(
                adapter.ID,
                Array.Empty<string>(), // No parent nodes
                dataItem.ID,
                dataItem.Name,
                dataItem.Unit,
                dataItem.Read,
                dataItem.Write,
                dataItem.Type.ToString(),
                dataItem.Dimension,
                dataItem.Address,
                dataItem.LocationStringForXml,
                dataItem.ConversionRead,
                dataItem.ConversionWrite,
                dataItem.Comment
            );
            flatDataItems.Add(flatItem);
        }

        FlattenNodes(adapter.ID, adapter.Nodes, flatDataItems);
    }

    private static void FlattenNodes(string adapterID, List<Config.Node> nodes, List<FlatDataItem> flatDataItems, List<string>? parentIDs = null) {
        
        parentIDs ??= new List<string>();

        foreach (var node in nodes) {

            List<string> currentParentIDs = new(parentIDs) {
                node.ID
            };

            foreach (var dataItem in node.DataItems) {
                flatDataItems.Add(new FlatDataItem(
                    adapterID,
                    currentParentIDs.ToArray(),
                    dataItem.ID,
                    dataItem.Name,
                    dataItem.Unit,
                    dataItem.Read,
                    dataItem.Write,
                    dataItem.Type.ToString(),
                    dataItem.Dimension,
                    dataItem.Address,
                    dataItem.LocationStringForXml,
                    dataItem.ConversionRead,
                    dataItem.ConversionWrite,
                    dataItem.Comment
                ));
            }

            // Recurse into child nodes
            FlattenNodes(adapterID, node.Nodes, flatDataItems, currentParentIDs);
        }
    }

    public static void UpdateModelWithFlatDataItems(IO_Model model, FlatDataItem[] items) {

        var set = new HashSet<string>();
        foreach (var item in items) {
            if (!set.Add(item.ID)) {
                throw new Exception($"Duplicate ID: {item.ID}");
            }
        }

        List<Config.Adapter> adapters = model.GetAllAdapters();
        Dictionary<string, List<Config.DataItem>> mapDataItemsList = new();

        void AddNodeDataItemsList(List<Config.Node> nodes) {
            foreach (var node in nodes) {
                foreach (var dataItem in node.DataItems) {
                    mapDataItemsList.Add(dataItem.ID, node.DataItems);
                }
                AddNodeDataItemsList(node.Nodes);
            }
        }

        foreach (var adapter in adapters) {
            foreach (var dataItem in adapter.DataItems) {
                mapDataItemsList.Add(dataItem.ID, adapter.DataItems);
            }
            AddNodeDataItemsList(adapter.Nodes);
        }

        Dictionary<string, Config.DataItem> mapDataItems = model.GetAllDataItems().ToDictionary(x => x.ID);

        FlatDataItem? previousItem = null;

        foreach (FlatDataItem item in items) {

            var matchingAdapter = FindAdapter(adapters, item.Adapter);
            if (matchingAdapter == null) {
                matchingAdapter = new Config.Adapter { 
                    ID = item.Adapter, 
                    Name = item.Adapter 
                };
                model.Adapters.Add(matchingAdapter);
            }

            Config.Node? parentNode = null;
            foreach (var node in item.ParentNodes) {
                if (parentNode == null) {
                    // Look in the adapter's nodes
                    var matchingNode = FindNode(matchingAdapter.Nodes, node);
                    if (matchingNode == null) {
                        matchingNode = new Config.Node { ID = node, Name = node };
                        matchingAdapter.Nodes.Add(matchingNode);
                    }
                    parentNode = matchingNode;
                }
                else {
                    // Look in the current parent node's child nodes
                    var matchingNode = FindNode(parentNode.Nodes, node);
                    if (matchingNode == null) {
                        matchingNode = new Config.Node { ID = node, Name = node };
                        parentNode.Nodes.Add(matchingNode);
                    }
                    parentNode = matchingNode;
                }
            }

            Config.DataItem? existingDataItem = mapDataItems.GetValueOrDefault(item.ID);
            List<Config.DataItem>? existingDataItemsList = mapDataItemsList.GetValueOrDefault(item.ID);

            if (parentNode == null) {
                // Data item is directly under the adapter
                UpdateOrAddDataItem(matchingAdapter.DataItems, item, previousItem, existingDataItem, existingDataItemsList);
            }
            else {
                // Data item is under a node
                UpdateOrAddDataItem(parentNode.DataItems, item, previousItem, existingDataItem, existingDataItemsList);
            }

            previousItem = item;
        }
    }

    private static Config.Adapter? FindAdapter(List<Config.Adapter> adapters, string adapter) {
        var adapterByID = adapters.FirstOrDefault(n => n.ID == adapter);
        if (adapterByID is not null) {
            return adapterByID;
        }
        int nameCount = adapters.Count(n => n.Name == adapter);
        if (nameCount == 1) {
            return adapters.First(n => n.Name == adapter);
        }
        return null;
    }

    private static Config.Node? FindNode(List<Config.Node> nodes, string node) {
        var nodeByID = nodes.FirstOrDefault(n => n.ID == node);
        if (nodeByID is not null) {
            return nodeByID;
        }
        int nameCount = nodes.Count(n => n.Name == node);
        if (nameCount == 1) {
            return nodes.First(n => n.Name == node);
        }
        return null;
    }

    private static void UpdateOrAddDataItem(List<Config.DataItem> dataItems, FlatDataItem item, FlatDataItem? previousItem, Config.DataItem? existingDataItem, List<Config.DataItem>? existingDataItemList) {

        void AddDataItem(Config.DataItem dataItem) {
            int idx = dataItems.FindIndex(di => di.ID == previousItem?.ID);
            if (idx == -1) {
                dataItems.Add(dataItem);
            }
            else {
                dataItems.Insert(idx + 1, dataItem);
            }
        }

        bool containsID = dataItems.Any(di => di.ID == item.ID);

        if (existingDataItem == null) {
            var newDataItem = new Config.DataItem {
                ID = item.ID,
                Name = item.Name,
                Unit = item.Unit,
                Type = ParseDataTypeEnum(item.Type),
                Dimension = item.Dimension,
                Read = item.Read,
                Write = item.Write,
                Address = item.Address,
                ConversionRead = item.ConversionRead,
                ConversionWrite = item.ConversionWrite,
                LocationStringForXml = item.Location,
                Comment = item.Comment
            };

            AddDataItem(newDataItem);
        }
        else {

            bool needToMove = !containsID;

            if (needToMove) {
                AddDataItem(existingDataItem);
                existingDataItemList?.Remove(existingDataItem);
            }

            existingDataItem.Name = item.Name;
            existingDataItem.Unit = item.Unit;
            existingDataItem.Type = ParseDataTypeEnum(item.Type);
            existingDataItem.Dimension = item.Dimension;
            existingDataItem.Read = item.Read;
            existingDataItem.Write = item.Write;
            existingDataItem.Address = item.Address;
            existingDataItem.ConversionRead = item.ConversionRead;
            existingDataItem.ConversionWrite = item.ConversionWrite;
            existingDataItem.LocationStringForXml = item.Location;
            existingDataItem.Comment = item.Comment;
        }
    }

    private static DataType ParseDataTypeEnum(string dataType) {
        if (Enum.TryParse(dataType, ignoreCase: true, out DataType result)) {
            return result;
        }
        return DataType.Float64;
    }

}
