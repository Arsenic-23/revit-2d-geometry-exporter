using System.Collections.Generic;

namespace RvtExporter.Models
{
    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class Vector2D
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class Polyline
    {
        public bool IsHole { get; set; } = false;
        public List<Point2D> Points { get; set; } = new List<Point2D>();
    }
}
