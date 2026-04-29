using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RvtExporter.Models;
using RvtExporter.Utils;

namespace RvtExporter.Services
{
    public class GeometryExtractor
    {
        public static List<Polyline> Extract2DBoundaries(Element element, Options opt, double elevation)
        {
            List<Polyline> allPolylines = new List<Polyline>();

            if (element is Wall w && w.WallType != null && w.WallType.Kind == WallKind.Curtain)
            {
                return allPolylines;
            }

            GeometryElement geo = element.get_Geometry(opt);
            if (geo == null)
            {
                string catName = element.Category?.Name ?? "";
                if (element is Wall || element is Floor || catName.Contains("Wall") || catName.Contains("Floor") || catName.Contains("Column")) 
                    return allPolylines;
                return GetBoundingBoxFallback(element);
            }

            List<Solid> solids = GetSolids(geo);
            foreach (Solid solid in solids)
            {
                List<Polyline> polylines = GetSilhouette(solid, elevation);
                if (polylines != null && polylines.Count > 0)
                    allPolylines.AddRange(polylines);
            }

            if (allPolylines.Count == 0)
            {
                List<Curve> standaloneCurves = GetCurves(geo);
                if (standaloneCurves.Count >= 2)
                {
                    try
                    {
                        var stitched = CurveUtils.StitchCurvesToPolylines(standaloneCurves);
                        if (stitched != null && stitched.Count > 0)
                            allPolylines.AddRange(stitched);
                    }
                    catch { }
                }
                else if (standaloneCurves.Count == 1)
                {
                    try
                    {
                        Curve c = standaloneCurves[0];
                        if (c.IsBound)
                        {
                            Polyline p = new Polyline();
                            IList<XYZ> pts = c.Tessellate();
                            foreach (var pt in pts)
                            {
                                p.Points.Add(new Point2D
                                {
                                    X = UnitConverter.FeetToMm(pt.X),
                                    Y = UnitConverter.FeetToMm(pt.Y)
                                });
                            }
                            if (p.Points.Count >= 2)
                                allPolylines.Add(p);
                        }
                    }
                    catch { }
                }
            }

            if (allPolylines.Count == 0)
            {
                string catName = element.Category?.Name ?? "";
                if (element is Wall || element is Floor || catName.Contains("Wall") || catName.Contains("Floor") || catName.Contains("Column")) 
                    return allPolylines;

                var fallback = GetBoundingBoxFallback(element);
                if (fallback.Count > 0)
                    allPolylines.AddRange(fallback);
            }

            return allPolylines;
        }

        public static List<Polyline> GetBoundingBoxFallback(Element element)
        {
            List<Polyline> result = new List<Polyline>();
            try
            {
                var bb = element.get_BoundingBox(null);
                if (bb != null)
                {
                    var poly = CurveUtils.GetBoundingBoxFootprint(bb);
                    if (poly != null && poly.Points.Count >= 3)
                        result.Add(poly);
                }
            }
            catch { }
            return result;
        }

        private static List<Solid> GetSolids(GeometryElement geomElem)
        {
            List<Solid> solids = new List<Solid>();
            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 0)
                    solids.Add(solid);
                else if (geomObj is GeometryInstance geomInst)
                {
                    GeometryElement instGeom = geomInst.GetInstanceGeometry();
                    if (instGeom != null) solids.AddRange(GetSolids(instGeom));
                }
            }
            return solids;
        }

        private static List<Curve> GetCurves(GeometryElement geomElem)
        {
            List<Curve> curves = new List<Curve>();
            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Curve curve && curve.IsBound)
                    curves.Add(curve);
                else if (geomObj is GeometryInstance geomInst)
                {
                    GeometryElement instGeom = geomInst.GetInstanceGeometry();
                    if (instGeom != null) curves.AddRange(GetCurves(instGeom));
                }
            }
            return curves;
        }

        private static List<Polyline> GetSilhouette(Solid solid, double elevation)
        {
            List<Polyline> result = new List<Polyline>();
            Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, elevation));
            try
            {
                ExtrusionAnalyzer analyzer;
                try { analyzer = ExtrusionAnalyzer.Create(solid, plane, XYZ.BasisZ); }
                catch { return result; }

                Face face = analyzer.GetExtrusionBase();
                foreach (EdgeArray ring in face.EdgeLoops)
                {
                    List<Curve> curves = new List<Curve>();
                    foreach (Edge edge in ring)
                    {
                        try
                        {
                            Curve c = edge.AsCurve();
                            if (c != null && c.IsBound)
                                curves.Add(c);
                        }
                        catch { }
                    }
                    if (curves.Count >= 2)
                    {
                        try
                        {
                            var stitched = CurveUtils.StitchCurvesToPolylines(curves);
                            if (stitched != null && stitched.Count > 0)
                                result.AddRange(stitched);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return result;
        }
    }
}
