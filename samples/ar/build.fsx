// xake build file for activereports source code

#r @"..\..\bin\Xake.Core.dll"
open Xake

let ardll name = "out\\GrapeCity.ActiveReports." + name + ".v9.dll"

let commonSrcFiles = scan (fileset {
  includes "CommonAssemblyInfo.cs"
  includes "VersionInfo.cs"
  includes "AssemblyNames.cs"
  includes "CommonFiles/SmartAssembly.Attributes.cs"
  })

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
      SrcFiles = (scan sources) @ commonSrcFiles
      References = [libs.nunit]
      }
}

ardll "Diagnostics" *> fun outname -> rule {

  do! Csc {
    CscSettings with
      Target = Library
      OutFile = outname
      SrcFiles = (ls "Diagnostics/**/*.cs") @ commonSrcFiles
      References = [libs.nunit]
      }
}

ardll "Testing.Tools" *> fun outname -> rule {

  do! Csc {
    CscSettings with
      Target = Library
      OutFile = outname
      SrcFiles = (ls "Testing/Testing.Tools/**/*.cs") @ commonSrcFiles
      References = [libs.nunit; libs.xmldiff; &ardll "Extensibility"]
      }
}

ardll "Chart" *> fun outname -> rule {

  do! Csc {
    CscSettings with
      Target = Library
      OutFile = outname
      Define = ["ARNET"]
      SrcFiles = (ls "SL/ARChart/**/*.cs") @ commonSrcFiles
      References = [libs.nunit]
      }
}

printfn "Building main"
run (["Extensibility"; "Diagnostics"; "Testing.Tools"; "Chart" ] |> List.map (ardll >> (~&))) |> ignore

