using Autodesk.Revit.DB;

namespace RvtExporter.Utils
{
    public static class UnitConverter
    {
        public static double FeetToMm(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
        }

        public static double FeetToMeters(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Meters);
        }
    }
}
