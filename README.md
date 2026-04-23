# RvtExporter

A comprehensive, production-ready C# add-in for Autodesk Revit (2027) that automatically extracts high-fidelity 2D floor plans from Revit models. The plugin seamlessly exports architectural geometry as structured JSON data and styling-ready vector SVG files, enabling easy integration with web applications, rendering pipelines, or custom 2D visualization tools.

---

## 🌟 Key Features

- **Automated Floor Plan Extraction**: Iterates over all valid floor plan views in the active Revit document. If no floor plan views are available, the system utilizes a robust level-based fallback method to ensure no data is missed.
- **Architecturally Accurate Geometry**: Extracts native geometry for walls, floors, doors, windows, and other primary elements. Leverages advanced boolean operations (via Clipper2) to resolve skewed walls, join artifacts, and miter intersections—yielding clean, continuous boundary lines without gaps.
- **Dual Format Output**:
  - **SVG**: Automatically generates standalone, visually structured SVG graphics. Elements are rendered in the correct z-index order (e.g., floors in the background, walls and columns in the foreground), with proper coloring, SVG styling attributes, and generated door swing arcs.
  - **JSON**: Outputs highly structured spatial data containing raw coordinate polylines (`x`, `y`) mapped to unique element IDs and categories, ideal for programmatic data processing.
- **Categorization**: Dynamically filters and groups Revit objects into standard structural categories: `Walls`, `Floors`, `Columns`, `Structural Columns`, `Doors`, `Windows`, `Stairs`, `Railings`, `Furniture`, `Curtain Panels`, `Curtain Wall Mullions`, and `Generic Models`.
- **Standardized Coordinate System**: Automatically handles Revit's internal coordinate conversions, ensuring all output data is precisely scaled and normalized to Millimeters (mm), anchored around a `0,0` origin.

---

## 🚀 Installation Guide

### Prerequisites
- Autodesk Revit 2027
- .NET Framework 4.7.2 (or .NET SDK based on the `csproj` format)
- Visual Studio 2022

### Step 1: Building the Project
1. Open the `rvt_exporter.sln` solution file in Visual Studio.
2. Ensure that your Revit API libraries (`RevitAPI.dll` and `RevitAPIUI.dll`) are correctly referenced. By default, they are located in `C:\Program Files\Autodesk\Revit 2027\`. *If you see missing reference warnings, update the HintPath in the project references.*
3. Set the build configuration to **Debug** or **Release**.
4. Build the solution (`Ctrl + Shift + B`).

### Step 2: Deploying the Add-In
Revit uses an XML-based `.addin` manifest file to load external tools on startup.

1. Navigate to the Revit Add-ins directory for your user account:
   `%APPDATA%\Autodesk\Revit\Addins\2027\`
2. In this folder, create a new text file named `RvtExporter.addin`.
3. Paste the following configuration into the file, ensuring you replace the `<Assembly>` path with the actual absolute path to your compiled `rvt_exporter.dll` file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Command">
    <Name>RvtExporter</Name>
    <Assembly>C:\Path\To\Your\Project\bin\Debug\rvt_exporter.dll</Assembly>
    <AddInId>93077045-8296-48f8-aa77-c3158a1c4c5f</AddInId>
    <FullClassName>RvtExporter.Commands.ExportCommand</FullClassName>
    <VendorId>RVTX</VendorId>
    <VendorDescription>Custom Exporter Tool</VendorDescription>
  </AddIn>
</RevitAddIns>
```
4. Save the file.

---

## 🛠 Usage

1. Launch Autodesk Revit 2027.
2. Open the `.rvt` project model you wish to process.
3. On the Revit ribbon, navigate to the **Add-Ins** tab.
4. Locate the **External Tools** dropdown and click on **RvtExporter** (or the name designated in your manifest).
5. The add-in will automatically begin scanning the document, extracting the geometry, and running mathematical boolean operations to merge the structure. 
6. Once finished, a dialog box will appear detailing the result.
7. **Output Directory**: The extracted JSON and SVG files are saved automatically in your local temp directory: `C:\temp\`.

---

## 📄 Output Data Formats

### JSON Structure
The JSON output provides structured array loops mapping each room element to its respective coordinates.
```json
{
  "level": "Level 1",
  "units": "mm",
  "elementCount": 420,
  "elements": [
    {
      "id": "123456",
      "category": "Walls",
      "polylines": [
        [
          {"x": 1500.5, "y": -2000.0},
          {"x": 1500.5, "y": -5000.0}
        ]
      ]
    }
  ]
}
```

### SVG Details
The exporter generates a pristine SVG ready to be displayed in a browser or vector editing tool. 
- **ViewBox**: Automatically scales based on the bounds of the active geometry.
- **Layers**: Grouped by `<g data-category="...">` making it easy to apply global CSS styling (e.g., hiding all furniture, coloring walls solid black).
- **Paths**: Primary elements like walls are merged using `evenodd` fill rules to natively cut out shaft openings and holes.

---

## ⚙️ Technical Details

- **Geometry Processing**: Employs the `Clipper2` library to process overlapping wall shapes. Walls undergo a morphological open/close offset with strict miter limits, ensuring clean wall joints and architectural corners remain unclipped.
- **Door Swing Generation**: Reads door family parameters (Hand, Facing, Width) and mathematically calculates precise SVG arcs `<path>` for the door swing visualization.
- **Null Safety & Fallbacks**: Fully refactored to indiscriminately process components via Bounding Box and Location Curve fallbacks in case a solid geometry face cannot be generated natively by Revit.
