using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RvtExporter.Models;

namespace RvtExporter.Utils
{
    public class CurveUtils
    {
        public static List<Polyline> StitchCurvesToPolylines(List<Curve> rawCurves)
        {
            List<Polyline> stitchedPolylines = new List<Polyline>();
            if (rawCurves == null || rawCurves.Count == 0) return stitchedPolylines;

            List<Curve> curves = rawCurves.Where(c => c != null && c.Length > 0.001).ToList();

            while (curves.Count > 0)
            {
                Polyline loop = new Polyline();
                Curve currentCurve = curves[0];
                curves.RemoveAt(0);

                AddCurveToPolyline(loop, currentCurve, false);
                XYZ endPoint = currentCurve.GetEndPoint(1);

                bool foundNext = true;
                while (foundNext && curves.Count > 0)
                {
                    foundNext = false;
                    for (int i = 0; i < curves.Count; i++)
                    {
                        Curve candidate = curves[i];
                        if (candidate.GetEndPoint(0).DistanceTo(endPoint) < 0.02)
                        {
                            AddCurveToPolyline(loop, candidate, false);
                            endPoint = candidate.GetEndPoint(1);
                            curves.RemoveAt(i);
                            foundNext = true;
                            break;
                        }
                        else if (candidate.GetEndPoint(1).DistanceTo(endPoint) < 0.02)
                        {
                            AddCurveToPolyline(loop, candidate, true);
                            endPoint = candidate.GetEndPoint(0);
                            curves.RemoveAt(i);
                            foundNext = true;
                            break;
                        }
                    }
                }

                RemoveDuplicatePoints(loop);
                EnsureClosed(loop);

                if (loop.Points.Count >= 3)
                {
                    stitchedPolylines.Add(loop);
                }
            }

            return stitchedPolylines;
        }

        private static void AddCurveToPolyline(Polyline polyline, Curve curve, bool reversed)
        {
            IList<XYZ> pts = curve.Tessellate();
            if (reversed)
            {
                pts = pts.Reverse().ToList();
            }

            for (int i = 0; i < pts.Count; i++)
            {
                polyline.Points.Add(new Point2D
                {
                    X = UnitConverter.FeetToMm(pts[i].X),
                    Y = UnitConverter.FeetToMm(pts[i].Y)
                });
            }
        }

        private static void RemoveDuplicatePoints(Polyline polyline)
        {
            if (polyline.Points.Count < 2) return;
            List<Point2D> cleaned = new List<Point2D>();
            cleaned.Add(polyline.Points[0]);

            for (int i = 1; i < polyline.Points.Count; i++)
            {
                var prev = cleaned[cleaned.Count - 1];
                var curr = polyline.Points[i];

                double diffX = Math.Abs(prev.X - curr.X);
                double diffY = Math.Abs(prev.Y - curr.Y);

                if (diffX > 0.01 || diffY > 0.01)
                {
                    cleaned.Add(curr);
                }
            }
            polyline.Points = cleaned;
        }

        private static void EnsureClosed(Polyline polyline)
        {
            if (polyline.Points.Count > 0)
            {
                var first = polyline.Points[0];
                var last = polyline.Points[polyline.Points.Count - 1];
                if (Math.Abs(first.X - last.X) > 0.01 || Math.Abs(first.Y - last.Y) > 0.01)
                {
                    polyline.Points.Add(new Point2D { X = first.X, Y = first.Y });
                }
            }
        }

        public static Polyline GetBoundingBoxFootprint(BoundingBoxXYZ bb)
        {
            Polyline polyline = new Polyline();
            double minX = UnitConverter.FeetToMm(bb.Min.X);
            double minY = UnitConverter.FeetToMm(bb.Min.Y);
            double maxX = UnitConverter.FeetToMm(bb.Max.X);
            double maxY = UnitConverter.FeetToMm(bb.Max.Y);

            polyline.Points.Add(new Point2D { X = minX, Y = minY });
            polyline.Points.Add(new Point2D { X = maxX, Y = minY });
            polyline.Points.Add(new Point2D { X = maxX, Y = maxY });
            polyline.Points.Add(new Point2D { X = minX, Y = maxY });
            polyline.Points.Add(new Point2D { X = minX, Y = minY });

            return polyline;
        }
    }
}
