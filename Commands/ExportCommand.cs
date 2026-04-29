using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RvtExporter.Models;
using RvtExporter.Services;

namespace RvtExporter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ExportCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                var uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("Error", "No active document open.");
                    return Result.Failed;
                }

                var doc = uidoc.Document;
                Directory.CreateDirectory(@"C:\temp");

                var allLevels = LevelProcessor.ProcessAllLevels(doc);

                var summary = new List<string>();
                int totalElements = 0;
                int filesCreated = 0;
                int nonEmptyLevels = 0;

                foreach (var data in allLevels)
                {
                    string safeName = SanitizeFilename(data.Level);

                    string jsonPath = Path.Combine(@"C:\temp", safeName + ".json");
                    JsonWriter.Save(data, jsonPath);
                    filesCreated++;

                    string svgPath = Path.Combine(@"C:\temp", safeName + ".svg");
                    SvgWriter.GenerateSvg(data, svgPath);
                    filesCreated++;

                    var catGroups = data.Elements
                        .GroupBy(e => e.Category)
                        .Select(g => $"{g.Count()} {g.Key}")
                        .ToList();

                    string catSummary = catGroups.Count > 0
                        ? string.Join(", ", catGroups)
                        : "empty";

                    totalElements += data.Elements.Count;
                    if (data.Elements.Count > 0) nonEmptyLevels++;

                    summary.Add($"  {data.Level}: {catSummary}");
                }

                string summaryText = string.Join("\n", summary);

                TaskDialog.Show("Export Complete",
                    $"Views/Levels processed: {allLevels.Count}\n" +
                    $"Non-empty: {nonEmptyLevels}\n" +
                    $"Total elements: {totalElements}\n" +
                    $"Files created: {filesCreated}\n\n" +
                    $"Per view:\n{summaryText}\n\n" +
                    $"All saved to C:\\temp\\");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Crash", ex.ToString());
                return Result.Failed;
            }
        }

        private static string SanitizeFilename(string name)
        {
            string safe = Regex.Replace(name, @"[^\w\-.]", "_");
            safe = Regex.Replace(safe, @"_+", "_").Trim('_');
            return safe.ToLowerInvariant();
        }
    }
}
