using System.IO;
using System.Text;
using System.Globalization;
using RvtExporter.Models;

namespace RvtExporter.Services
{
    public class JsonWriter
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void Save(LevelExportData data, string outputPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"level\": \"{EscapeJson(data.Level)}\",");
            sb.AppendLine($"  \"units\": \"{data.Units}\",");
            sb.AppendLine($"  \"elementCount\": {data.Elements.Count},");
            sb.AppendLine("  \"elements\": [");

            for (int i = 0; i < data.Elements.Count; i++)
            {
                var el = data.Elements[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"id\": \"{EscapeJson(el.Id)}\",");
                sb.AppendLine($"      \"category\": \"{EscapeJson(el.Category)}\",");
                sb.AppendLine("      \"polylines\": [");

                for (int j = 0; j < el.Polylines.Count; j++)
                {
                    var loop = el.Polylines[j];
                    sb.AppendLine("        [");
                    for (int k = 0; k < loop.Points.Count; k++)
                    {
                        var pt = loop.Points[k];
                        string comma = k < loop.Points.Count - 1 ? "," : "";
                        string xStr = pt.X.ToString("F2", CultureInfo.InvariantCulture);
                        string yStr = pt.Y.ToString("F2", CultureInfo.InvariantCulture);
                        sb.AppendLine($"          {{\"x\": {xStr}, \"y\": {yStr}}}{comma}");
                    }
                    string loopComma = j < el.Polylines.Count - 1 ? "," : "";
                    sb.AppendLine($"        ]{loopComma}");
                }

                sb.Append("      ]");

                if (el.Door != null)
                {
                    sb.AppendLine(",");
                    WriteDoorData(sb, el.Door);
                }
                else
                {
                    sb.AppendLine();
                }

                string elComma = i < data.Elements.Count - 1 ? "," : "";
                sb.AppendLine($"    }}{elComma}");
            }

            sb.AppendLine("  ]");

            if (data.MergedCategories != null && data.MergedCategories.Count > 0)
            {
                var validMergedCats = System.Linq.Enumerable.ToList(
                    System.Linq.Enumerable.Where(data.MergedCategories, mc => mc.Polygons.Count > 0));

                if (validMergedCats.Count > 0)
                {
                    sb.AppendLine("  ,\"mergedCategories\": [");
                    for (int i = 0; i < validMergedCats.Count; i++)
                    {
                        var mc = validMergedCats[i];
                        sb.AppendLine("    {");
                        sb.AppendLine($"      \"category\": \"{EscapeJson(mc.Category)}\",");
                        sb.AppendLine("      \"polygons\": [");

                        for (int j = 0; j < mc.Polygons.Count; j++)
                        {
                            var polyGroup = mc.Polygons[j];
                            sb.AppendLine("        {");

                            sb.AppendLine("          \"outer\": [");
                            WritePointArray(sb, polyGroup.OuterLoop, "            ");
                            sb.AppendLine("          ],");

                            sb.AppendLine("          \"holes\": [");
                            for (int h = 0; h < polyGroup.Holes.Count; h++)
                            {
                                var hole = polyGroup.Holes[h];
                                sb.AppendLine("            [");
                                WritePointArray(sb, hole, "              ");
                                string holeComma = h < polyGroup.Holes.Count - 1 ? "," : "";
                                sb.AppendLine($"            ]{holeComma}");
                            }
                            sb.AppendLine("          ],");

                            sb.AppendLine("          \"sourceIds\": [");
                            for (int s = 0; s < polyGroup.SourceIds.Count; s++)
                            {
                                string sComma = s < polyGroup.SourceIds.Count - 1 ? "," : "";
                                sb.AppendLine($"            \"{EscapeJson(polyGroup.SourceIds[s])}\"{sComma}");
                            }
                            sb.AppendLine("          ]");

                            string groupComma = j < mc.Polygons.Count - 1 ? "," : "";
                            sb.AppendLine($"        }}{groupComma}");
                        }

                        sb.AppendLine("      ]");
                        string mcComma = i < validMergedCats.Count - 1 ? "," : "";
                        sb.AppendLine($"    }}{mcComma}");
                    }
                    sb.AppendLine("  ]");
                }
            }
            sb.AppendLine("}");
            File.WriteAllText(outputPath, sb.ToString(), Utf8NoBom);
        }

        private static void WritePointArray(StringBuilder sb, Polyline loop, string indent)
        {
            for (int k = 0; k < loop.Points.Count; k++)
            {
                var pt = loop.Points[k];
                string comma = k < loop.Points.Count - 1 ? "," : "";
                string xStr = pt.X.ToString("F2", CultureInfo.InvariantCulture);
                string yStr = pt.Y.ToString("F2", CultureInfo.InvariantCulture);
                sb.AppendLine($"{indent}{{\"x\": {xStr}, \"y\": {yStr}}}{comma}");
            }
        }

        private static void WriteDoorData(StringBuilder sb, DoorData door)
        {
            sb.AppendLine("      \"door\": {");

            if (door.Position != null)
            {
                sb.AppendLine($"        \"position\": {{\"x\": {F(door.Position.X)}, \"y\": {F(door.Position.Y)}}},");
            }
            if (door.Facing != null)
            {
                sb.AppendLine($"        \"facing\": {{\"x\": {F(door.Facing.X)}, \"y\": {F(door.Facing.Y)}}},");
            }
            if (door.Hand != null)
            {
                sb.AppendLine($"        \"hand\": {{\"x\": {F(door.Hand.X)}, \"y\": {F(door.Hand.Y)}}},");
            }

            sb.AppendLine($"        \"facingFlipped\": {(door.FacingFlipped ? "true" : "false")},");
            sb.AppendLine($"        \"handFlipped\": {(door.HandFlipped ? "true" : "false")},");
            sb.AppendLine($"        \"width\": {F(door.Width)},");
            sb.AppendLine($"        \"height\": {F(door.Height)}");
            sb.AppendLine("      }");
        }

        private static string F(double val)
        {
            return val.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
