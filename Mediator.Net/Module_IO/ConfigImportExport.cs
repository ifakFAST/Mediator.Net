using System.Collections.Generic;
using Ifak.Fast.Mediator.IO.Config;
using ClosedXML.Excel;
using System.IO;

namespace Ifak.Fast.Mediator.IO;

public static class ConfigImportExport {

    public record FlatDataItem(
        string AdapterID,
        string[] ParentNodeIDs,
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

    public static byte[] CreateExcelFromFlatDataItems(FlatDataItem[] items) {

        string ReadWrite(FlatDataItem it) => it.Read ? (it.Write ? "R/W" : "Read") : (it.Write ? "Write" : "");

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("DataItems");

        // Set up header row
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Value = "Adapter ID";
        sheet.Cell(1, 2).Value = "Parent Node IDs";
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

        sheet.Range("A1:M1").Style.Border.SetBottomBorder(XLBorderStyleValues.Thin);
        sheet.Range("A1:M1").Style.Font.SetBold(true);
        sheet.Range("A1:M1").Style.Fill.SetBackgroundColor(XLColor.LightGray);
        sheet.SheetView.FreezeRows(1);

        // Fill in data
        for (int i = 0; i < items.Length; i++) {
            var item = items[i];
            int rowIdx = i + 2;  // +2 because Excel is 1-indexed and the first row is the header

            sheet.Cell(rowIdx, 1).Value = item.AdapterID;
            sheet.Cell(rowIdx, 2).Value = string.Join(" / ", item.ParentNodeIDs);
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
                System.Array.Empty<string>(), // No parent nodes
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

}
