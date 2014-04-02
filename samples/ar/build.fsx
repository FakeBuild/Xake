// xake build file for activereports source code

#r @"..\..\bin\Xake.Core.dll"
open Xake

////// utility methods
let ardll name = "out\\GrapeCity.ActiveReports." + name + ".v9.dll"
let ardep = List.map (ardll >> (~&)) >> FileList

let commonSrcFiles = fileset {
  includes "CommonAssemblyInfo.cs"
  includes "VersionInfo.cs"
  includes "AssemblyNames.cs"
  includes "CommonFiles/SmartAssembly.Attributes.cs"
  }

// TODO consider making filesets
module libs =
  let nunit = &"Tools/NUnit/nunit.framework.dll"
  let xmldiff = &"Tools/XmlDiff/XmlDiffPatch.dll"

////// rules
ardll "Extensibility" *> fun outname -> rule {

  let sources = fileset {
    includes "Extensibility/**/*.cs"
    includes "SL/CommonFiles/SafeGraphics.cs"
  }

  do! Csc {
    CscSettings with
      Target = Library
      OutFile = outname
      SrcFiles = sources + commonSrcFiles
      References = FileList [libs.nunit]
      }
}

ardll "Diagnostics" *> fun outname -> rule {

  do! Csc {
    CscSettings with
      Target = Library
      OutFile = outname
      SrcFiles = !!"Diagnostics/**/*.cs" + commonSrcFiles
      References = FileList [libs.nunit]
      }
}

ardll "Testing.Tools" *> fun outname -> rule {

  do! Csc {
    CscSettings with
      Target = Library
      OutFile = outname
      SrcFiles = !! "Testing/Testing.Tools/**/*.cs" + commonSrcFiles
      References = FileList [libs.nunit; libs.xmldiff; &ardll "Extensibility"]
      }
}

ardll("Chart") *> fun outname -> rule {

  do! Csc {
    CscSettings with
      Target = Library
      OutFile = outname
      Define = ["ARNET"]
      SrcFiles = ls "SL/ARChart/**/*.cs" + commonSrcFiles
      References = FileList [libs.nunit]
      }
}

ardll("Document") *> fun outname -> rule {

  let src = fileset {
    includes "SL/CommonFiles/SafeGraphics.cs"
    includes "SL/DDLib.Net/Controls/**/*.cs"
    includes "SL/DDLib.Net/DDWord/kinsoku.cs"
    includes "SL/DDLib.Net/Utility/*.cs"
    includes "SL/DDLib.Net/ZLib/*.cs"
    includes "PdfExport/AR/PDFRender/BidiTable.cs"
    includes "PdfExport/AR/PDFRender/BidiReference.cs"
    includes "SL/Document/**/*.cs"
    excludes "!SL/DDLib.Net/ZLib/ZByteArray.cs"
  }

  do! Csc {
    CscSettings with
      Target = Library
      OutFile = outname
      Define = ["ARVIEWER_BUILD"]
      SrcFiles = src + commonSrcFiles
      References = FileList [libs.nunit] + (ardep ["Extensibility"; "Testing.Tools"])
      }
}

ardll("Core") *> fun outname -> rule {

  let src = fileset {
    includes "SL/CommonFiles/*.cs"
    includes "SL/AREngine/**/*.cs"
    includes "Reports/**/*.cs"
    includes "SL/CSS/*.cs"
    includes "SL/DDLib.Net/DDExpression/*.cs"
    includes "SL/DDLib.Net/DDWord/**/*.cs"
    includes "SL/DDLib.Net/Utility/XmlUtility.cs"
    includes "SL/DDLib.Net/Utility/GraphicsUtility.cs"
    includes "SL/DDLib.Net/Utility/DrawingUtility.cs"
    includes "SL/DDLib.Net/Core/DDFormat.cs"
    includes "SL/DDLib.Net/ZLib/*.cs"
    includes "SL/DDLib.Net/Drawing/MetaFileSaver.cs"
    includes "SL/DDLib.Net/Controls/Table/*.cs"
    includes "SL/Document/Document/LayoutUtils.cs"
    includes "SL/Document/ResourceStorage/HashCalculator.cs"

    excludes "Reports/OracleClient/**/*.cs"
    excludes "SL/CSS/AssemblyInfo.cs"
    excludes "SL/AREngine/AssemblyInfo.cs"

    join commonSrcFiles
  }

  do! Csc {
    CscSettings with
      Target = Library
      OutFile = outname
      Define = ["DATAMANAGER_HOST_IS_STRYKER"]
      SrcFiles = src
      References = FileList [libs.nunit] + (ardep ["Extensibility"; "Diagnostics"; "Testing.Tools"; "Document"; "Chart"])
      ReferencesGlobal = ["Microsoft.VisualBasic.dll"]
      }
}

printfn "Building main"

let start = System.DateTime.Now
do run (["Extensibility"; "Diagnostics"; "Testing.Tools"; "Chart"; "Document"; "Core" ] |> List.map ardll)

printfn "\nBuild completed in %A" (System.DateTime.Now - start)

