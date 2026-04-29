using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using RvtExporter.Models;

namespace RvtExporter.Services
{
    public class SvgWriter
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void GenerateSvg(LevelExportData data, string outputPath)
        {
            if (data.Elements == null || data.Elements.Count == 0)
            {
                string safeLevel = XmlEscape(data.Level);
                File.WriteAllText(outputPath,
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                    "<svg viewBox=\"0 0 1000 1000\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background-color: white;\">\n" +
                    $"  <!-- Level: {safeLevel} - no geometry -->\n" +
                    "</svg>\n",
                    Utf8NoBom);
                return;
            }

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            const double PADDING = 2000.0;

            foreach (var el in data.Elements)
            {
                foreach (var loop in el.Polylines)
                {
                    foreach (var pt in loop.Points)
                    {
                        double invertedY = -pt.Y;
                        if (pt.X < minX) minX = pt.X;
                        if (pt.X > maxX) maxX = pt.X;
                        if (invertedY < minY) minY = invertedY;
                        if (invertedY > maxY) maxY = invertedY;
                    }
                }
            }

            if (minX == double.MaxValue)
            {
                string safeLevel = XmlEscape(data.Level);
                File.WriteAllText(outputPath,
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                    "<svg viewBox=\"0 0 1000 1000\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background-color: white;\">\n" +
                    $"  <!-- Level: {safeLevel} - no valid points -->\n" +
                    "</svg>\n",
                    Utf8NoBom);
                return;
            }

            double width = maxX - minX + (PADDING * 2);
            double height = maxY - minY + (PADDING * 2);

            double globalOffsetX = minX - PADDING;
            double globalOffsetY = minY - PADDING;

            string vW = width.ToString("F2", CultureInfo.InvariantCulture);
            string vH = height.ToString("F2", CultureInfo.InvariantCulture);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine($"<svg viewBox=\"0 0 {vW} {vH}\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background-color: white;\" shape-rendering=\"geometricPrecision\" stroke-linejoin=\"miter\" stroke-linecap=\"square\">");

            var categoryFills = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Floors",             "fill=\"#e8e8e8\"" },
                { "Walls",              "fill=\"#2d2d2d\"" },
                { "Columns",            "fill=\"#2d2d2d\"" },
                { "Structural Columns", "fill=\"#2d2d2d\"" },
                { "Doors",              "fill=\"none\"" },
                { "Windows",            "fill=\"none\"" },
                { "Stairs",             "fill=\"#d4c5a0\"" },
                { "Railings",           "fill=\"none\"" },
                { "Furniture",          "fill=\"none\"" },
                { "Curtain Panels",     "fill=\"#b3d9ff\"" },
                { "Curtain Wall Mullions", "fill=\"#4a4a4a\"" },
                { "Generic Models",     "fill=\"#2d2d2d\"" }
            };

            var categoryStrokes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Floors",             "stroke=\"none\"" },
                { "Walls",              "stroke=\"none\"" },
                { "Columns",            "stroke=\"none\"" },
                { "Structural Columns", "stroke=\"none\"" },
                { "Curtain Panels",     "stroke=\"none\"" },
                { "Generic Models",     "stroke=\"none\"" },

                { "Doors",              "stroke=\"#0055cc\" stroke-width=\"1.2\" vector-effect=\"non-scaling-stroke\"" },
                { "Windows",            "stroke=\"#00aacc\" stroke-width=\"1.2\" vector-effect=\"non-scaling-stroke\"" },
                { "Railings",           "stroke=\"#808080\" stroke-width=\"1.2\" vector-effect=\"non-scaling-stroke\"" },
                { "Furniture",          "stroke=\"#8899aa\" stroke-width=\"1.0\" vector-effect=\"non-scaling-stroke\"" },
                { "Curtain Wall Mullions", "stroke=\"#4a4a4a\" stroke-width=\"1.2\" vector-effect=\"non-scaling-stroke\"" }
            };

            string[] renderOrder = { "Floors", "Stairs", "Walls", "Columns", "Structural Columns",
                                     "Curtain Panels", "Curtain Wall Mullions", "Generic Models", "Furniture", "Windows", "Doors", "Railings" };

            var allCategories = data.Elements.Select(e => e.Category).Distinct().ToList();

            var orderedCategories = renderOrder
                .Where(c => allCategories.Contains(c))
                .Concat(allCategories.Where(c => !renderOrder.Contains(c)))
                .ToList();

            foreach (var catName in orderedCategories)
            {
                var catElements = data.Elements.Where(e => e.Category == catName).ToList();
                if (catElements.Count == 0) continue;

                string fillStr = categoryFills.ContainsKey(catName) ? categoryFills[catName] : "fill=\"none\"";
                string strokeStr = categoryStrokes.ContainsKey(catName) ? categoryStrokes[catName] : "stroke=\"#ff0000\" stroke-width=\"20\"";

                string safeId = XmlEscape(catName.ToLower().Replace(" ", "_"));
                string safeCat = XmlEscape(catName);
                sb.AppendLine($"  <g id=\"{safeId}\" data-category=\"{safeCat}\">");

                var categoriesToMerge = new HashSet<string> { "Walls", "Floors", "Columns", "Structural Columns", "Curtain Panels", "Generic Models" };

                if (categoriesToMerge.Contains(catName))
                {
                    var mergedCat = data.MergedCategories.FirstOrDefault(m => m.Category == catName);
                    if (mergedCat != null && mergedCat.Polygons.Count > 0)
                    {
                        var pathData = new StringBuilder();
                        foreach (var polyGroup in mergedCat.Polygons)
                        {
                            AppendSvgLoop(pathData, polyGroup.OuterLoop, globalOffsetX, globalOffsetY);
                            foreach (var hole in polyGroup.Holes)
                            {
                                AppendSvgLoop(pathData, hole, globalOffsetX, globalOffsetY);
                            }
                        }

                        string dStr = pathData.ToString().Trim();
                        sb.AppendLine($"    <path d=\"{dStr}\" fill-rule=\"evenodd\" {fillStr} stroke=\"none\" />");
                    }
                }
                else if (catName == "Doors")
                {
                    foreach (var el in catElements)
                    {
                        var uniquePaths = new HashSet<string>();
                        foreach (var loop in el.Polylines)
                        {
                            var points = loop.Points.ToList();
                            if (IsDegenerate(points)) continue;

                            EnsureClosure(points);

                            var ptsStr = string.Join(" ", points.Select(pt =>
                            {
                                double shiftedX = pt.X - globalOffsetX;
                                double shiftedY = (-pt.Y) - globalOffsetY;
                                return $"{F(shiftedX)},{F(shiftedY)}";
                            }));

                            if (!uniquePaths.Add(ptsStr)) continue;

                            sb.Append($"    <polygon data-id=\"{XmlEscape(el.Id)}\" points=\"");
                            sb.Append(ptsStr);
                            sb.AppendLine($"\" {fillStr} {strokeStr} />");
                        }

                        if (el.Door != null && el.Door.Position != null)
                        {
                            double arcWidth = el.Door.Width;

                            if (arcWidth <= 0 && el.Polylines.Count > 0)
                            {
                                double bMinX = double.MaxValue, bMinY = double.MaxValue;
                                double bMaxX = double.MinValue, bMaxY = double.MinValue;
                                foreach (var loop in el.Polylines)
                                {
                                    foreach (var pt in loop.Points)
                                    {
                                        if (pt.X < bMinX) bMinX = pt.X;
                                        if (pt.X > bMaxX) bMaxX = pt.X;
                                        if (pt.Y < bMinY) bMinY = pt.Y;
                                        if (pt.Y > bMaxY) bMaxY = pt.Y;
                                    }
                                }
                                double dx = bMaxX - bMinX;
                                double dy = bMaxY - bMinY;
                                arcWidth = Math.Max(dx, dy);
                            }

                            if (arcWidth > 0)
                            {
                                AppendDoorSwingArc(sb, el.Door, arcWidth, globalOffsetX, globalOffsetY);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var el in catElements)
                    {
                        var uniquePaths = new HashSet<string>();
                        foreach (var loop in el.Polylines)
                        {
                            var points = loop.Points.ToList();
                            if (IsDegenerate(points)) continue;

                            EnsureClosure(points);

                            var ptsStr = string.Join(" ", points.Select(pt =>
                            {
                                double shiftedX = pt.X - globalOffsetX;
                                double shiftedY = (-pt.Y) - globalOffsetY;
                                return $"{F(shiftedX)},{F(shiftedY)}";
                            }));

                            if (!uniquePaths.Add(ptsStr)) continue;

                            string tag = points.Count >= 3 ? "polygon" : "polyline";
                            sb.Append($"    <{tag} data-id=\"{XmlEscape(el.Id)}\" points=\"");
                            sb.Append(ptsStr);
                            sb.AppendLine($"\" {fillStr} {strokeStr} />");
                        }
                    }
                }

                sb.AppendLine("  </g>");
            }

            sb.AppendLine("</svg>");
            File.WriteAllText(outputPath, sb.ToString(), Utf8NoBom);
        }

        private static void AppendDoorSwingArc(StringBuilder sb, DoorData door, double arcWidth, double globalOffsetX, double globalOffsetY)
        {
            double radius = arcWidth;
            if (radius <= 0) return;

            double posX = door.Position.X;
            double posY = door.Position.Y;

            double handX = door.Hand.X;
            double handY = door.Hand.Y;
            double facingX = door.Facing.X;
            double facingY = door.Facing.Y;

            if (door.HandFlipped)
            {
                handX = -handX;
                handY = -handY;
            }
            if (door.FacingFlipped)
            {
                facingX = -facingX;
                facingY = -facingY;
            }

            double hingeX = posX - handX * (radius / 2.0);
            double hingeY = posY - handY * (radius / 2.0);

            double arcStartX = hingeX + handX * radius;
            double arcStartY = hingeY + handY * radius;

            double arcEndX = hingeX + facingX * radius;
            double arcEndY = hingeY + facingY * radius;

            double sX = arcStartX - globalOffsetX;
            double sY = (-arcStartY) - globalOffsetY;
            double eX = arcEndX - globalOffsetX;
            double eY = (-arcEndY) - globalOffsetY;
            double hX = hingeX - globalOffsetX;
            double hY = (-hingeY) - globalOffsetY;

            sb.AppendLine($"    <line x1=\"{F(hX)}\" y1=\"{F(hY)}\" x2=\"{F(sX)}\" y2=\"{F(sY)}\" stroke=\"#0055cc\" stroke-width=\"1.2\" vector-effect=\"non-scaling-stroke\" stroke-dasharray=\"60,30\" />");

            sb.AppendLine($"    <path d=\"M {F(sX)} {F(sY)} A {F(radius)} {F(radius)} 0 0 1 {F(eX)} {F(eY)}\" stroke=\"#0055cc\" stroke-width=\"1.2\" vector-effect=\"non-scaling-stroke\" fill=\"none\" stroke-dasharray=\"60,30\" />");
        }

        private static void AppendSvgLoop(StringBuilder sb, Polyline loop, double globalOffsetX, double globalOffsetY)
        {
            var points = loop.Points.ToList();
            if (IsDegenerate(points)) return;

            EnsureClosure(points);

            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                string letter = i == 0 ? "M" : "L";
                double shiftedX = pt.X - globalOffsetX;
                double shiftedY = (-pt.Y) - globalOffsetY;
                sb.Append($"{letter} {F(shiftedX)} {F(shiftedY)} ");
            }
            sb.Append("Z ");
        }

        private static void EnsureClosure(List<Point2D> points)
        {
            if (points.Count < 2) return;
            if (Math.Abs(points.First().X - points.Last().X) > 0.01 ||
                Math.Abs(points.First().Y - points.Last().Y) > 0.01)
            {
                points.Add(new Point2D { X = points.First().X, Y = points.First().Y });
            }
        }

        private static bool IsDegenerate(List<Point2D> points)
        {
            if (points == null || points.Count < 3) return true;

            var uniquePoints = new HashSet<string>();
            foreach (var p in points) uniquePoints.Add($"{p.X:F2},{p.Y:F2}");
            if (uniquePoints.Count < 3) return true;

            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                area += points[i].X * points[j].Y - points[j].X * points[i].Y;
            }
            area = Math.Abs(area / 2.0);

            if (area < 1.0) return true;

            return false;
        }

        private static string F(double val)
        {
            return val.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
        }
    }
}
