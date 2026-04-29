using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RvtExporter.Models;
using RvtExporter.Utils;

namespace RvtExporter.Services
{
    public class LevelProcessor
    {
        private static readonly BuiltInCategory[] TargetCategories = new[]
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_Columns,
            BuiltInCategory.OST_Stairs,
            BuiltInCategory.OST_Railings,
            BuiltInCategory.OST_Furniture,
            BuiltInCategory.OST_CurtainWallPanels,
            BuiltInCategory.OST_CurtainWallMullions,
            BuiltInCategory.OST_GenericModel
        };

        public static List<LevelExportData> ProcessAllLevels(Document doc)
        {
            var results = new List<LevelExportData>();

            var floorPlanViews = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate
                         && v.ViewType == ViewType.FloorPlan
                         && v.CanBePrinted)
                .OrderBy(v =>
                {
                    try { return v.GenLevel?.Elevation ?? 0; }
                    catch { return 0; }
                })
                .ToList();

            if (floorPlanViews.Count == 0)
            {
                return ProcessAllLevelsFallback(doc);
            }

            foreach (var view in floorPlanViews)
            {
                try
                {
                    string viewName = view.Name;
                    Level genLevel = view.GenLevel;

                    Options opt = new Options
                    {
                        View = view,
                        ComputeReferences = false,
                        DetailLevel = ViewDetailLevel.Fine
                    };

                    double cutPlaneElevation = 0;
                    try
                    {
                        if (genLevel != null)
                            cutPlaneElevation = genLevel.Elevation;
                    }
                    catch { }

                    try
                    {
                        var viewRange = view.GetViewRange();
                        if (viewRange != null)
                        {
                            double cutOffset = viewRange.GetOffset(PlanViewPlane.CutPlane);
                            cutPlaneElevation = (genLevel?.Elevation ?? 0) + cutOffset;
                        }
                    }
                    catch { }

                    var elements = new List<ExportElement>();

                    foreach (var category in TargetCategories)
                    {
                        List<Element> categoryElements;
                        try
                        {
                            categoryElements = new FilteredElementCollector(doc, view.Id)
                                .OfCategory(category)
                                .WhereElementIsNotElementType()
                                .ToList();
                        }
                        catch
                        {
                            continue;
                        }

                        if (categoryElements.Count == 0) continue;

                        string categoryName = GetCategoryDisplayName(category);

                        foreach (var e in categoryElements)
                        {
                            try
                            {
                                List<Polyline> polys = null;

                                if (category == BuiltInCategory.OST_Walls && e is Wall wall)
                                {
                                    if (wall.WallType != null && wall.WallType.Kind == WallKind.Curtain)
                                    {
                                        continue;
                                    }
                                    polys = GeometryExtractor.Extract2DBoundaries(wall, opt, cutPlaneElevation);
                                }

                                if (polys == null || polys.Count == 0)
                                {
                                    polys = GeometryExtractor.Extract2DBoundaries(e, opt, cutPlaneElevation);
                                }

                                if ((polys == null || polys.Count == 0) && category != BuiltInCategory.OST_Walls)
                                {
                                    polys = GeometryExtractor.GetBoundingBoxFallback(e);
                                }

                                if (polys != null && polys.Count > 0)
                                {
                                    var exportEl = new ExportElement
                                    {
                                        Id = e.Id.ToString(),
                                        Category = categoryName,
                                        Polylines = polys
                                    };

                                    if (category == BuiltInCategory.OST_Doors && e is FamilyInstance fi)
                                    {
                                        exportEl.Door = ExtractDoorData(fi);
                                    }

                                    elements.Add(exportEl);
                                }
                            }
                            catch { }
                        }
                    }

                    try
                    {
                        var otherElements = new FilteredElementCollector(doc, view.Id)
                            .WhereElementIsNotElementType()
                            .ToList();

                        var existingIds = new HashSet<string>(elements.Select(el => el.Id));

                        foreach (var e in otherElements)
                        {
                            try
                            {
                                string eid = e.Id.ToString();
                                if (existingIds.Contains(eid)) continue;
                                if (e.Category == null) continue;

                                string catName = e.Category.Name;
                                if (string.IsNullOrEmpty(catName)) continue;
                                if (IsNonGeometricCategory(catName)) continue;

                                List<Polyline> polys = GeometryExtractor.Extract2DBoundaries(e, opt, cutPlaneElevation);
                                if (polys == null || polys.Count == 0)
                                    polys = GeometryExtractor.GetBoundingBoxFallback(e);

                                if (polys != null && polys.Count > 0)
                                {
                                    elements.Add(new ExportElement
                                    {
                                        Id = eid,
                                        Category = catName,
                                        Polylines = polys
                                    });
                                    existingIds.Add(eid);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }

                    var levelExportData = new LevelExportData
                    {
                        Level = viewName,
                        Units = "mm",
                        ElementCount = elements.Count,
                        Elements = elements
                    };

                    TopologyProcessor.ProcessLevel(levelExportData);
                    results.Add(levelExportData);
                }
                catch { }
            }

            if (results.Count == 0)
            {
                return ProcessAllLevelsFallback(doc);
            }

            return results;
        }

        private static List<LevelExportData> ProcessAllLevelsFallback(Document doc)
        {
            var results = new List<LevelExportData>();

            Options opt = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            var allElements = new List<Element>();
            foreach (var cat in TargetCategories)
            {
                try
                {
                    var catElements = new FilteredElementCollector(doc)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToList();
                    allElements.AddRange(catElements);
                }
                catch { }
            }

            foreach (var level in levels)
            {
                double elevation = level.Elevation;
                var elements = new List<ExportElement>();

                var levelElements = allElements
                    .Where(e => MatchesLevel(e, level))
                    .ToList();

                foreach (var e in levelElements)
                {
                    try
                    {
                        List<Polyline> polys = null;
                        string categoryName = e.Category?.Name ?? "Unknown";

                        if (e is Wall wall)
                        {
                            if (wall.WallType != null && wall.WallType.Kind == WallKind.Curtain)
                            {
                                continue;
                            }
                            polys = GeometryExtractor.Extract2DBoundaries(wall, opt, elevation);
                        }

                        if (polys == null || polys.Count == 0)
                        {
                            polys = GeometryExtractor.Extract2DBoundaries(e, opt, elevation);
                        }

                        if ((polys == null || polys.Count == 0) && !(e is Wall))
                        {
                            polys = GeometryExtractor.GetBoundingBoxFallback(e);
                        }

                        if (polys != null && polys.Count > 0)
                        {
                            var exportEl = new ExportElement
                            {
                                Id = e.Id.ToString(),
                                Category = categoryName,
                                Polylines = polys
                            };

                            if (e.Category != null
                                && e.Category.Name == "Doors"
                                && e is FamilyInstance fi)
                            {
                                exportEl.Door = ExtractDoorData(fi);
                            }

                            elements.Add(exportEl);
                        }
                    }
                    catch { }
                }

                var levelExportData = new LevelExportData
                {
                    Level = level.Name,
                    Units = "mm",
                    ElementCount = elements.Count,
                    Elements = elements
                };

                TopologyProcessor.ProcessLevel(levelExportData);
                results.Add(levelExportData);
            }

            return results;
        }

        private static DoorData ExtractDoorData(FamilyInstance fi)
        {
            try
            {
                var door = new DoorData();

                var loc = fi.Location as LocationPoint;
                if (loc != null)
                {
                    door.Position = new Point2D
                    {
                        X = UnitConverter.FeetToMm(loc.Point.X),
                        Y = UnitConverter.FeetToMm(loc.Point.Y)
                    };
                }

                try
                {
                    var facing = fi.FacingOrientation;
                    door.Facing = new Vector2D { X = facing.X, Y = facing.Y };
                }
                catch { door.Facing = new Vector2D { X = 0, Y = 0 }; }

                try
                {
                    var hand = fi.HandOrientation;
                    door.Hand = new Vector2D { X = hand.X, Y = hand.Y };
                }
                catch { door.Hand = new Vector2D { X = 0, Y = 0 }; }

                try { door.FacingFlipped = fi.FacingFlipped; } catch { }
                try { door.HandFlipped = fi.HandFlipped; } catch { }

                try
                {
                    var widthParam = fi.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                    if (widthParam != null)
                        door.Width = UnitConverter.FeetToMm(widthParam.AsDouble());
                    else
                    {
                        var symWidthParam = fi.Symbol?.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                        if (symWidthParam != null)
                            door.Width = UnitConverter.FeetToMm(symWidthParam.AsDouble());
                    }
                }
                catch { }

                try
                {
                    var heightParam = fi.get_Parameter(BuiltInParameter.DOOR_HEIGHT);
                    if (heightParam != null)
                        door.Height = UnitConverter.FeetToMm(heightParam.AsDouble());
                    else
                    {
                        var symHeightParam = fi.Symbol?.get_Parameter(BuiltInParameter.DOOR_HEIGHT);
                        if (symHeightParam != null)
                            door.Height = UnitConverter.FeetToMm(symHeightParam.AsDouble());
                    }
                }
                catch { }

                if (door.Width <= 0)
                {
                    try
                    {
                        var familyWidthParam = fi.Symbol?.get_Parameter(BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM);
                        if (familyWidthParam != null)
                            door.Width = UnitConverter.FeetToMm(familyWidthParam.AsDouble());
                    }
                    catch { }
                }

                if (door.Width <= 0)
                {
                    try
                    {
                        var bb = fi.get_BoundingBox(null);
                        if (bb != null)
                        {
                            double dx = UnitConverter.FeetToMm(bb.Max.X - bb.Min.X);
                            double dy = UnitConverter.FeetToMm(bb.Max.Y - bb.Min.Y);
                            door.Width = Math.Min(dx, dy);
                            if (door.Height <= 0)
                                door.Height = UnitConverter.FeetToMm(bb.Max.Z - bb.Min.Z);
                        }
                    }
                    catch { }
                }

                return door;
            }
            catch
            {
                return null;
            }
        }

        private static bool MatchesLevel(Element e, Level level)
        {
            ElementId levelId = level.Id;

            try
            {
                var p = e.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                if (p != null && p.AsElementId() == levelId) return true;
            }
            catch { }

            try
            {
                var p = e.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                if (p != null && p.AsElementId() == levelId) return true;
            }
            catch { }

            try
            {
                var p = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (p != null && p.AsElementId() == levelId) return true;
            }
            catch { }

            try
            {
                var p = e.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM);
                if (p != null && p.AsElementId() == levelId) return true;
            }
            catch { }

            try
            {
                if (e.LevelId == levelId) return true;
            }
            catch { }

            try
            {
                var bb = e.get_BoundingBox(null);
                if (bb != null)
                {
                    double z = level.Elevation;
                    if (z >= bb.Min.Z - 0.1 && z <= bb.Max.Z + 0.1)
                        return true;
                }
            }
            catch { }

            return false;
        }

        private static string GetCategoryDisplayName(BuiltInCategory cat)
        {
            switch (cat)
            {
                case BuiltInCategory.OST_Walls: return "Walls";
                case BuiltInCategory.OST_Doors: return "Doors";
                case BuiltInCategory.OST_Windows: return "Windows";
                case BuiltInCategory.OST_Floors: return "Floors";
                case BuiltInCategory.OST_StructuralColumns: return "Structural Columns";
                case BuiltInCategory.OST_Columns: return "Columns";
                case BuiltInCategory.OST_Stairs: return "Stairs";
                case BuiltInCategory.OST_Railings: return "Railings";
                case BuiltInCategory.OST_Furniture: return "Furniture";
                case BuiltInCategory.OST_CurtainWallPanels: return "Curtain Panels";
                case BuiltInCategory.OST_GenericModel: return "Generic Models";
                default: return cat.ToString().Replace("OST_", "");
            }
        }

        private static bool IsNonGeometricCategory(string categoryName)
        {
            string[] skip = {
                "Grids", "Levels", "Views", "Sections", "Elevations",
                "Cameras", "Reference Planes", "Scope Boxes",
                "Matchline", "Revision Clouds", "Detail Items",
                "Text Notes", "Dimensions", "Spot Elevations",
                "Tags", "Keynote Tags", "Room Tags", "Area Tags",
                "Rooms", "Areas", "Sheets", "Schedules",
                "Legends", "Title Blocks", "View Titles",
                "Callouts", "Filled region", "Masking Region"
            };
            return skip.Any(s => categoryName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
