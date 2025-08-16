# SecureDeleteAddin
Intercepts Revit’s native Delete. Shows a checklist; requires code when deleting ≥10 items or high-risk categories (Levels, Grids, Floors, Topography, Scope Boxes, Project Info). Optional 15-min grace.

## Build
- .NET Framework 4.8, x64
- References: RevitAPI.dll, RevitAPIUI.dll (from Revit 2025)

## Installation
- Copy `bin\x64\Release\SecureDeleteAddin.dll` to:
  C:\ProgramData\Autodesk\Revit\Addins\SecureDelete\
- Copy `install\SecureDelete.addin` to:
  C:\ProgramData\Autodesk\Revit\Addins\2025\
  (and 2024\ if needed)

## Configure
- Threshold: `ThresholdCount` in App.cs
- Grace: `Grace` in App.cs (set to `TimeSpan.Zero` to disable)
- High-risk list: `HighRiskCats` in App.cs

<h3 align="left">Languages and Tools:</h3>
<p align="left"> <a href="https://www.w3schools.com/cs/" target="_blank" rel="noreferrer"> <img src="https://raw.githubusercontent.com/devicons/devicon/master/icons/csharp/csharp-original.svg" alt="csharp" width="40" height="40"/> </a> </p>
