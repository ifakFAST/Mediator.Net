using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using ClosedXML.Excel;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.IO.Config;

/*
 * Reads Model_IO.xml and DataLogging.xlsx, then writes Model_IO_Result.xml with
 * generated logging Nodes below each adapter.
 *
 * DataLogging.xlsx is expected to have the relevant data in the first worksheet:
 *   Column A: DataItem id or name from Model_IO.xml.
 *   Column B: Deadband value to write to the matching DataItem's deadband attribute.
 *   Column C: Logging interval in whole seconds.
 *
 * For each distinct interval in column C, the tool creates one Node with matching
 * History settings. History uses IntervalExactOrChanged with no offset and no deadband value. 
 * DataItems with workbook settings are moved below exactly one generated interval Node. 
 * XML DataItems without workbook settings are left in their original location and reported as
 * warnings. Workbook rows without a matching XML DataItem are also reported as warnings.
 */

const string defaultModelFile = "D:\\FAST\\Mediator.Net\\Util\\Deadband\\Model_IO.xml";
const string defaultLoggingFile = "D:\\FAST\\Mediator.Net\\Util\\Deadband\\DataLogging.xlsx";
const string defaultOutputFile = "D:\\FAST\\Mediator.Net\\Util\\Deadband\\Model_IO_Result.xml";

string modelFile = args.Length > 0 ? args[0] : defaultModelFile;
string loggingFile = args.Length > 1 ? args[1] : defaultLoggingFile;
string outputFile = args.Length > 2 ? args[2] : defaultOutputFile;

Dictionary<string, LoggingSettings> loggingSettings = ReadLoggingSettings(loggingFile);
IO_Model model = ReadModel(modelFile);

ApplyLoggingSettings(model, loggingSettings);
WriteModel(model, outputFile);

Console.WriteLine($"Created '{outputFile}'.");

static Dictionary<string, LoggingSettings> ReadLoggingSettings(string path) {
    if (!File.Exists(path)) {
        throw new FileNotFoundException($"Excel file not found: {path}", path);
    }

    using var workbookStream = OpenSharedReadStream(path);
    using var workbook = new XLWorkbook(workbookStream);
    IXLWorksheet sheet = workbook.Worksheets.First();
    var rows = sheet.RangeUsed()?.RowsUsed() ?? Enumerable.Empty<IXLRangeRow>();
    var result = new Dictionary<string, LoggingSettings>(StringComparer.OrdinalIgnoreCase);

    foreach (IXLRangeRow row in rows) {
        string id = row.Cell(1).GetString().Trim();
        string deadband = row.Cell(2).GetFormattedString().Trim();
        string interval = row.Cell(3).GetFormattedString().Trim();

        if (string.IsNullOrWhiteSpace(id) || LooksLikeHeader(id, deadband, interval)) {
            continue;
        }

        if (string.IsNullOrWhiteSpace(interval)) {
            continue;
        }

        if (!double.TryParse(interval, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) &&
            !double.TryParse(interval, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds)) {
            throw new InvalidDataException($"Invalid logging interval '{interval}' in row {row.RowNumber()} for DataItem '{id}'.");
        }

        if (seconds <= 0 || seconds % 1 != 0) {
            throw new InvalidDataException($"Logging interval must be a whole number of seconds greater than zero in row {row.RowNumber()} for DataItem '{id}'.");
        }

        string normalizedDeadband = NormalizeOptionalNumber(deadband, row.RowNumber(), id);
        long normalizedInterval = (long)seconds;

        if (result.TryGetValue(id, out LoggingSettings existing) &&
            (existing.Deadband != normalizedDeadband || existing.IntervalSeconds != normalizedInterval)) {
            throw new InvalidDataException($"DataItem '{id}' has conflicting settings in row {row.RowNumber()}.");
        }

        result[id] = new LoggingSettings(normalizedDeadband, normalizedInterval);
    }

    if (result.Count == 0) {
        throw new InvalidDataException($"No DataItem logging settings found in '{path}'.");
    }

    return result;
}

static MemoryStream OpenSharedReadStream(string path) {
    using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    var memoryStream = new MemoryStream();
    fileStream.CopyTo(memoryStream);
    memoryStream.Position = 0;
    return memoryStream;
}

static bool LooksLikeHeader(string id, string deadband, string interval) {
    return id.Equals("id", StringComparison.OrdinalIgnoreCase) ||
           id.Equals("name", StringComparison.OrdinalIgnoreCase) ||
           id.Contains("dataitem", StringComparison.OrdinalIgnoreCase) ||
           deadband.Contains("deadband", StringComparison.OrdinalIgnoreCase) ||
           interval.Contains("interval", StringComparison.OrdinalIgnoreCase);
}

static string NormalizeOptionalNumber(string value, int rowNumber, string id) {
    if (string.IsNullOrWhiteSpace(value)) {
        return "";
    }

    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
        !double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number)) {
        throw new InvalidDataException($"Invalid deadband '{value}' in row {rowNumber} for DataItem '{id}'.");
    }

    return number.ToString("G17", CultureInfo.InvariantCulture);
}

static IO_Model ReadModel(string path) {

    if (!File.Exists(path)) {
        throw new FileNotFoundException($"XML model file not found: {path}", path);
    }

    var serializer = new XmlSerializer(typeof(IO_Model));
    using var stream = File.OpenRead(path);
    return (IO_Model?)serializer.Deserialize(stream) ?? throw new InvalidDataException($"Could not deserialize '{path}'.");
}

static void ApplyLoggingSettings(IO_Model model, IReadOnlyDictionary<string, LoggingSettings> loggingSettings) {

    var unmatchedSettings = new HashSet<string>(loggingSettings.Keys, StringComparer.OrdinalIgnoreCase);
    var missingModelItems = new List<string>();

    foreach (Adapter adapter in model.GetAllAdapters()) {

        List<DataItem> allAdapterItems = adapter.GetAllDataItems();
        List<DataItem> movedItems = allAdapterItems
            .Where(item => TryGetSettings(item, loggingSettings, out _))
            .ToList();

        missingModelItems.AddRange(
            allAdapterItems
                .Where(item => !TryGetSettings(item, loggingSettings, out _))
                .Select(item => string.IsNullOrWhiteSpace(item.Name) || item.Name == item.ID ? item.ID : $"{item.ID} ({item.Name})"));

        RemoveMovedItems(adapter, movedItems.ToHashSet());

        adapter.Nodes.AddRange(
            movedItems
                .GroupBy(item => GetSettings(item, loggingSettings).IntervalSeconds)
                .OrderBy(group => group.Key)
                .Select(group => CreateNode(group.Key, group.OrderBy(item => item.ID, StringComparer.OrdinalIgnoreCase))));

        foreach (DataItem item in movedItems) {
            LoggingSettings settings = GetSettings(item, loggingSettings);
            item.Deadband = settings.Deadband;
            item.Scheduling = null;
            item.History = null;
            unmatchedSettings.Remove(item.ID);
            unmatchedSettings.Remove(item.Name);
        }
    }

    if (missingModelItems.Count > 0) {
        Console.WriteLine("Warning: Ignored XML DataItems without workbook logging settings: " +
                          string.Join(", ", missingModelItems.OrderBy(x => x)));
    }

    if (unmatchedSettings.Count > 0) {
        Console.WriteLine("Warning: No matching XML DataItem found for workbook rows: " + string.Join(", ", unmatchedSettings.OrderBy(x => x)));
    }
}

static bool TryGetSettings(DataItem item, IReadOnlyDictionary<string, LoggingSettings> loggingSettings, out LoggingSettings settings) {
    return loggingSettings.TryGetValue(item.ID, out settings!) ||
           (!string.IsNullOrWhiteSpace(item.Name) && loggingSettings.TryGetValue(item.Name, out settings!));
}

static LoggingSettings GetSettings(DataItem item, IReadOnlyDictionary<string, LoggingSettings> loggingSettings) {
    return TryGetSettings(item, loggingSettings, out LoggingSettings settings)
        ? settings
        : throw new InvalidOperationException($"Missing settings for DataItem '{item.ID}'.");
}

static void RemoveMovedItems(Adapter adapter, HashSet<DataItem> movedItems) {
    adapter.DataItems.RemoveAll(movedItems.Contains);
    RemoveMovedItemsFromNodes(adapter.Nodes, movedItems);
}

static void RemoveMovedItemsFromNodes(List<Node> nodes, HashSet<DataItem> movedItems) {
    foreach (Node node in nodes) {
        node.DataItems.RemoveAll(movedItems.Contains);
        RemoveMovedItemsFromNodes(node.Nodes, movedItems);
    }
}

static Node CreateNode(long intervalSeconds, IEnumerable<DataItem> dataItems) {
    string formattedInterval = intervalSeconds.ToString(CultureInfo.InvariantCulture);

    return new Node {
        ID = $"Logging_{formattedInterval}s",
        Name = $"Logging {formattedInterval} s",
        History = History.IntervalExactOrChanged(Duration.FromSeconds(intervalSeconds)),
        DataItems = dataItems.ToList()
    };
}

static void WriteModel(IO_Model model, string path) {
    var serializer = new XmlSerializer(typeof(IO_Model));
    var settings = new XmlWriterSettings {
        Indent = true,
        Encoding = new UTF8Encoding(false)
    };

    using var writer = XmlWriter.Create(path, settings);
    serializer.Serialize(writer, model);
}

readonly record struct LoggingSettings(string Deadband, long IntervalSeconds);
