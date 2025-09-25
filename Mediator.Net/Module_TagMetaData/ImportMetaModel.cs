// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace Ifak.Fast.Mediator.TagMetaData;

public static class MetaModelExcel
{
    private enum Column
    {
        A = 1,
        B = 2,
        C = 3,
        D = 4,
        E = 5,
        F = 6,
        G = 7,
        H = 8
    }

    public static MetaModel ImportFromExcel(Stream stream) {

        var model = new MetaModel();

        using var workbook = new XLWorkbook(stream);

        // Import Categories
        ImportCategories(workbook, model);

        // Import UnitGroups
        ImportUnitGroups(workbook, model);

        // Import Units
        ImportUnits(workbook, model);

        // Import Whats
        ImportWhats(workbook, model);

        return model;
    }

    private static void ImportCategories(XLWorkbook workbook, MetaModel model) {
        var sheet = workbook.Worksheet("Category");

        for (int row = 2; row <= 100; ++row) {
            var categoryId = GetCellText(sheet, row, Column.A);

            if (IsValidIdentifier(categoryId)) {
                model.Categories.Add(new Category {
                    ID = categoryId!.Trim()
                });
            }
            else if (string.IsNullOrWhiteSpace(categoryId)) {
                // Stop when we hit empty rows
                break;
            }
        }
    }

    private static void ImportUnitGroups(XLWorkbook workbook, MetaModel model) {
        var sheet = workbook.Worksheet("UnitGroup");

        for (int row = 2; row <= 100; ++row) {
            var unitGroupId = GetCellText(sheet, row, Column.A);

            if (IsValidIdentifier(unitGroupId)) {
                model.UnitGroups.Add(new UnitGroup {
                    ID = unitGroupId!.Trim()
                });
            }
            else if (string.IsNullOrWhiteSpace(unitGroupId)) {
                // Stop when we hit empty rows
                break;
            }
        }
    }

    private static void ImportUnits(XLWorkbook workbook, MetaModel model) {
        var sheet = workbook.Worksheet("Unit");

        for (int row = 2; row <= 100; ++row) {
            var unitId = GetCellText(sheet, row, Column.A);

            if (IsValidIdentifier(unitId)) {
                var unitGroup = GetCellText(sheet, row, Column.B) ?? "";
                var isSI = GetCellText(sheet, row, Column.C) == "X";
                var factor = GetCellNumber(sheet, row, Column.D) ?? 1.0;
                var offset = GetCellNumber(sheet, row, Column.E) ?? 0.0;

                model.Units.Add(new Unit {
                    ID = unitId!.Trim(),
                    UnitGroup = unitGroup,
                    IsSI = isSI,
                    Factor = factor,
                    Offset = offset
                });
            }
            else if (string.IsNullOrWhiteSpace(unitId)) {
                // Stop when we hit empty rows
                break;
            }
        }
    }

    private static void ImportWhats(XLWorkbook workbook, MetaModel model) {
        var sheet = workbook.Worksheet("What");

        for (int row = 2; row <= 100; ++row) {
            var whatId = GetCellText(sheet, row, Column.A);

            if (IsValidIdentifier(whatId)) {
                var unitGroup = GetCellText(sheet, row, Column.B) ?? "";
                var name = GetCellText(sheet, row, Column.C) ?? "";
                var shortName = GetCellText(sheet, row, Column.D) ?? "";
                var category = GetCellText(sheet, row, Column.E) ?? "";
                var refUnit = GetCellText(sheet, row, Column.G) ?? ""; // Column G for Ref Unit

                model.Whats.Add(new What {
                    ID = whatId!.Trim(),
                    UnitGroup = unitGroup,
                    Name = name,
                    ShortName = shortName,
                    Category = category,
                    RefUnit = refUnit
                });
            }
            else if (string.IsNullOrWhiteSpace(whatId)) {
                // Stop when we hit empty rows
                break;
            }
        }
    }

    private static string? GetCellText(IXLWorksheet sheet, int row, Column col) {
        var cell = sheet.Cell(row, (int)col).Value;
        return cell.IsText ? cell.GetText() : null;
    }

    private static double? GetCellNumber(IXLWorksheet sheet, int row, Column col) {
        var cell = sheet.Cell(row, (int)col).Value;
        return cell.IsNumber ? cell.GetNumber() : null;
    }

    private static bool IsValidIdentifier(string? str) {
        return !string.IsNullOrEmpty(str) && str.ToCharArray().Any(c => char.IsLetter(c) || char.IsDigit(c));
    }

    public static void ExportToExcel(MetaModel model, Stream output) {
        using var workbook = new XLWorkbook();

        CreateWhatsSheet(workbook, model);
        CreateCategoriesSheet(workbook, model);
        CreateUnitGroupsSheet(workbook, model);
        CreateUnitsSheet(workbook, model);

        workbook.SaveAs(output);
    }

    private static void CreateWhatsSheet(XLWorkbook workbook, MetaModel model) {
        var sheet = workbook.Worksheets.Add("What");

        // Headers
        sheet.Cell(1, 1).Value = "Identifier";
        sheet.Cell(1, 2).Value = "UnitGroup";
        sheet.Cell(1, 3).Value = "Name";
        sheet.Cell(1, 4).Value = "Short Name";
        sheet.Cell(1, 5).Value = "Category";
        sheet.Cell(1, 6).Value = "Atom/Molecule";
        sheet.Cell(1, 7).Value = "Ref Unit";

        // Style headers
        var headerRange = sheet.Range("A1:G1");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Freeze header row
        sheet.SheetView.FreezeRows(1);

        // Data
        int row = 2;
        foreach (var what in model.Whats) {
            sheet.Cell(row, 1).Value = what.ID;
            sheet.Cell(row, 2).Value = what.UnitGroup;
            sheet.Cell(row, 3).Value = what.Name;
            sheet.Cell(row, 4).Value = what.ShortName;
            sheet.Cell(row, 5).Value = what.Category;
            sheet.Cell(row, 7).Value = what.RefUnit;
            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void CreateCategoriesSheet(XLWorkbook workbook, MetaModel model) {
        var sheet = workbook.Worksheets.Add("Category");

        sheet.Cell(1, 1).Value = "Identifier";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Range("A1:A1").Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.SheetView.FreezeRows(1);

        int row = 2;
        foreach (var category in model.Categories) {
            sheet.Cell(row, 1).Value = category.ID;
            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void CreateUnitGroupsSheet(XLWorkbook workbook, MetaModel model) {
        var sheet = workbook.Worksheets.Add("UnitGroup");

        sheet.Cell(1, 1).Value = "Identifier";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Range("A1:A1").Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.SheetView.FreezeRows(1);

        int row = 2;
        foreach (var unitGroup in model.UnitGroups) {
            sheet.Cell(row, 1).Value = unitGroup.ID;
            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void CreateUnitsSheet(XLWorkbook workbook, MetaModel model) {
        var sheet = workbook.Worksheets.Add("Unit");

        // Headers
        sheet.Cell(1, 1).Value = "Identifier";
        sheet.Cell(1, 2).Value = "UnitGroup";
        sheet.Cell(1, 3).Value = "Is SI-Unit";
        sheet.Cell(1, 4).Value = "Factor";
        sheet.Cell(1, 5).Value = "Offset";

        // Style headers
        var headerRange = sheet.Range("A1:E1");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.SheetView.FreezeRows(1);

        // Data
        int row = 2;
        foreach (var unit in model.Units) {
            sheet.Cell(row, 1).Value = unit.ID;
            sheet.Cell(row, 2).Value = unit.UnitGroup;
            sheet.Cell(row, 3).Value = unit.IsSI ? "X" : "";
            sheet.Cell(row, 4).Value = unit.Factor;
            sheet.Cell(row, 5).Value = unit.Offset;
            row++;
        }

        sheet.Columns().AdjustToContents();
    }
}
