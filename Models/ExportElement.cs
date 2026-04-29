using System.Collections.Generic;

namespace RvtExporter.Models
{
    public class ExportElement
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public List<Polyline> Polylines { get; set; } = new List<Polyline>();
        public DoorData Door { get; set; }
    }

    public class DoorData
    {
        public Point2D Position { get; set; }
        public Vector2D Facing { get; set; }
        public Vector2D Hand { get; set; }
        public bool FacingFlipped { get; set; }
        public bool HandFlipped { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class MergedPolygon
    {
        public Polyline OuterLoop { get; set; } = new Polyline();
        public List<Polyline> Holes { get; set; } = new List<Polyline>();
        public List<string> SourceIds { get; set; } = new List<string>();
    }

    public class MergedCategory
    {
        public string Category { get; set; }
        public List<MergedPolygon> Polygons { get; set; } = new List<MergedPolygon>();
    }

    public class LevelExportData
    {
        public string Level { get; set; }
        public string Units { get; set; } = "mm";
        public int ElementCount { get; set; }
        public List<ExportElement> Elements { get; set; } = new List<ExportElement>();
        public List<MergedCategory> MergedCategories { get; set; } = new List<MergedCategory>();
    }
}
