using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using RvtExporter.Models;

namespace RvtExporter.Services
{
    public static class TopologyProcessor
    {
        private const double Scale = 100.0;

        public static void ProcessLevel(LevelExportData levelData)
        {
            var wallCategories = new[] { "Walls" };
            var columnCategories = new[] { "Columns", "Structural Columns" };
            var genericCategories = new[] { "Generic Models" };
            var curtainCategories = new[] { "Curtain Panels", "Curtain Wall Mullions" };
            var floorCategories = new[] { "Floors" };

            double snap = 0.1;

            ProcessCategoryGroup(levelData, "Walls",          wallCategories,    snap, 5.0);
            ProcessCategoryGroup(levelData, "Columns",        columnCategories,  snap, 5.0);
            ProcessCategoryGroup(levelData, "Floors",         floorCategories,   snap, 5.0);
            ProcessCategoryGroup(levelData, "Curtain Panels", curtainCategories, snap, 5.0);
            ProcessCategoryGroup(levelData, "Generic Models", genericCategories, snap, 5.0);
        }

        private static void ProcessCategoryGroup(LevelExportData levelData, string outCategory, string[] inCategories, double snapMm, double wallOffsetMm = 10.0)
        {
            var elements = levelData.Elements
                .Where(e => inCategories.Contains(e.Category, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (elements.Count == 0) return;

            var paths = new Paths64();
            var sourceIds = elements.Select(e => e.Id).Distinct().ToList();

            foreach (var el in elements)
            {
                foreach (var poly in el.Polylines)
                {
                    var path = new Path64();
                    foreach (var pt in poly.Points)
                    {
                        double x = Math.Round(pt.X / snapMm) * snapMm;
                        double y = Math.Round(pt.Y / snapMm) * snapMm;
                        path.Add(new Point64((long)(x * Scale), (long)(y * Scale)));
                    }
                    if (path.Count >= 3)
                    {
                        var first = path[0];
                        var last = path[path.Count - 1];
                        if (first.X != last.X || first.Y != last.Y)
                            path.Add(new Point64(first.X, first.Y));
                        paths.Add(path);
                    }
                }
            }
            if (paths.Count == 0) return;

            Paths64 union1 = Clipper.Union(paths, FillRule.NonZero);

            double offset = wallOffsetMm * Scale; 

            var offsetter = new ClipperOffset();
            offsetter.MiterLimit = 100.0;

            var expanded = new Paths64();
            offsetter.AddPaths(union1, JoinType.Miter, EndType.Polygon);
            offsetter.Execute(offset, expanded);
            offsetter.Clear();

            var contracted = new Paths64();
            offsetter.MiterLimit = 100.0;
            offsetter.AddPaths(expanded, JoinType.Miter, EndType.Polygon);
            
            double contractOffset = (outCategory == "Curtain Panels") ? (offset - 5.0 * Scale) : offset;
            offsetter.Execute(-contractOffset, contracted);
            offsetter.Clear();

            var tree = new PolyTree64();
            Clipper.BooleanOp(ClipType.Union, contracted, new Paths64(), tree, FillRule.NonZero);

            var mergedCat = new MergedCategory { Category = outCategory };
            foreach (PolyPath64 outerNode in tree)
            {
                ExtractPolygons(outerNode, mergedCat, sourceIds);
            }

            if (mergedCat.Polygons.Count > 0)
            {
                levelData.MergedCategories.Add(mergedCat);
            }
        }

        private static void ExtractPolygons(PolyPath64 node, MergedCategory category, List<string> sourceIds)
        {
            if (node.Polygon != null && node.Polygon.Count >= 3)
            {
                var mergedPoly = new MergedPolygon();
                mergedPoly.SourceIds = sourceIds;
                mergedPoly.OuterLoop = ConvertToPolyline(node.Polygon, isHole: false);

                foreach (PolyPath64 childHole in node)
                {
                    if (childHole.Polygon != null && childHole.Polygon.Count >= 3)
                    {
                        var holePoly = ConvertToPolyline(childHole.Polygon, isHole: true);
                        if (holePoly.Points.Count >= 3)
                        {
                            mergedPoly.Holes.Add(holePoly);
                        }
                    }

                    foreach (PolyPath64 island in childHole)
                    {
                        ExtractPolygons(island, category, sourceIds);
                    }
                }

                if (mergedPoly.OuterLoop.Points.Count >= 3)
                {
                    category.Polygons.Add(mergedPoly);
                }
            }
        }

        private static Polyline ConvertToPolyline(Path64 path, bool isHole)
        {
            var poly = new Polyline { IsHole = isHole };
            if (path.Count < 3) return poly;

            double area = Clipper.Area(path);
            if (isHole && area > 0) path.Reverse();
            if (!isHole && area < 0) path.Reverse();

            var cleanPath = new Path64();
            for (int i = 0; i < path.Count; i++)
            {
                var prev = cleanPath.Count > 0 ? cleanPath[cleanPath.Count - 1] : path[path.Count - 1];
                var curr = path[i];
                var next = path[(i + 1) % path.Count];

                if (curr.X == prev.X && curr.Y == prev.Y) continue;

                long dx1 = curr.X - prev.X;
                long dy1 = curr.Y - prev.Y;
                long dx2 = next.X - curr.X;
                long dy2 = next.Y - curr.Y;

                long cross = dx1 * dy2 - dy1 * dx2;
                if (cross == 0)
                {
                    long dot = dx1 * dx2 + dy1 * dy2;
                    if (dot > 0) continue;
                }
                cleanPath.Add(curr);
            }

            if (cleanPath.Count < 3) cleanPath = path;

            foreach (var pt in cleanPath)
            {
                poly.Points.Add(new Point2D { X = pt.X / Scale, Y = pt.Y / Scale });
            }

            return poly;
        }
    }
}
