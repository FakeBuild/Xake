// xake build file for activereports source code

#r @"..\..\bin\Xake.Core.dll"
open Xake

let ardll name = "out\\GrapeCity.ActiveReports." + name + ".v9.dll"

let commonSrcFiles = fileset {
  includes "CommonAssemblyInfo.cs"
  includes "VersionInfo.cs"
  includes "AssemblyNames.cs"
  includes "CommonFiles/SmartAssembly.Attributes.cs"
  }

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

printfn "Building main"

let start = System.DateTime.Now
do run (["Extensibility"; "Diagnostics"; "Testing.Tools"; "Chart" ] |> List.map ardll)

printfn "\nBuild completed in %A" (System.DateTime.Now - start)

