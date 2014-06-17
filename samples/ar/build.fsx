// xake build file for activereports source code

//#r @"..\..\bin\Xake.Core.dll"
#r @"\projects\Mine\xake\bin\Xake.Core.dll"
open Xake

let DEBUG = false

////// utility methods
let ardll name = "out\\GrapeCity.ActiveReports." + name + ".v9.dll"
let arexe name = "out\\GrapeCity.ActiveReports." + name + ".v9.exe"
let ardep = List.fold (fun fs f -> fs + (ardll f)) Fileset.Empty

let commonSrcFiles = fileset {
    includes "CommonAssemblyInfo.cs"
    includes "VersionInfo.cs"
    includes "AssemblyNames.cs"
    includes "CommonFiles/SmartAssembly.Attributes.cs"
    }

// "External" libraries
module libs =
    let nunit      = !! "Tools/NUnit/nunit.framework.dll"
    let xmldiff    = !! "Tools/XmlDiff/XmlDiffPatch.dll"
    let moq        = !! "Tools/Moq.3.1/moq.dll"
    let moqseq     = !! "Tools/Moq.3.1/moq.sequences.dll"
    let iTextSharp = !! "ExternalLibs/iTextSharp/build/iTextSharp.dll"
    let OpenXml    = !! "ExternalLibs/OpenXMLSDKV2.0/DocumentFormat.OpenXml.dll"
    let qwhale     = !! "ExternalLibs\QwhaleEditor\Qwhale.All.dll"

let dlls = List.map ardll <| ["Extensibility"; "Diagnostics"; "Testing.Tools"; "Chart"; "Document"; "Core"; "OracleClient"; "RdfExport"; "XmlExport"; "Image.Unsafe"; "ImageExport"; "Viewer.Win" ]

// do xake {XakeOptions with FileLog = "build.log"; FileLogLevel = Verbosity.Diag; Threads = 4 } {
do xakeArgs fsi.CommandLineArgs {
    XakeOptions with FileLog = "build.log"; FileLogLevel = Verbosity.Diag; Threads = 4; Vars = [("NETFX", "3.5")] } {

    want (["build"])

    phony "all" (action {
        do! need ["clean"]
        do! need ["build"]
    })

    rule ("clean" => action {
        do! rm ["out" </> "*.*"]
    })

    phony "build" (action {
        do! need ([arexe "Viewer"] @ dlls)
    })

    rules [
        ardll "Extensibility" *> fun outname -> action {

            let sources = fileset {
                includes "Extensibility/**/*.cs"
                includes "SL/CommonFiles/SafeGraphics.cs"
                join commonSrcFiles
            }

            do! Csc {
                CscSettings with
                    Out = outname
                    Src = sources
                    Ref = libs.nunit

                    Resources =
                    [
                        // TODO basedir "." redundant
                        resourceset {
                            prefix "GrapeCity.ActiveReports"
                            dynamic true
                            basedir "."
                            includes "Extensibility/**/*.resx"
                        }
                    ]
                }
            }
    
        ardll "Diagnostics" *> fun outname -> action {

            do! Csc {
                CscSettings with
                    Out = outname
                    Src = !!"Diagnostics/**/*.cs" + commonSrcFiles
                    Ref = libs.nunit
                    Resources =
                    [
                        resourceset {
                            prefix "GrapeCity.ActiveReports"
                            dynamic true
                            basedir "."
                            includes "Diagnostics/**/*.resx"
                        }
                    ]
                }
            }

        ardll "Testing.Tools" *> fun outname -> action {

            do! Csc {
                CscSettings with
                    Out = outname
                    Src = !! "Testing/Testing.Tools/**/*.cs" + commonSrcFiles
                    Ref = libs.nunit + libs.xmldiff + !! (ardll "Extensibility")
                    }
            }

        ardll("Chart") *> fun outname -> action {
            (* example of simplified syntax *)
            do! (csc {
                out outname
                define ["ARNET"]
                src (!! "SL/ARChart/**/*.cs" + commonSrcFiles)
                refs libs.nunit
                resourceslist [
                    resourceset {
                        prefix "GrapeCity.ActiveReports.Chart.Wizard.Pictures"
                        dynamic true
                        basedir "SL/ARChart/ActiveReports.Chart/Wizard/Pictures"
                        includes "DataMemberTreeItems.bmp"
                    }
                    resourceset {
                        prefix "GrapeCity.ActiveReports.Chart"
                        dynamic true
                        basedir "SL/ARChart/ActiveReports.Chart"
                        includes "**/*.resx"
                    }
                ]
            })
            }

        ardll("OracleClient") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Target = Library
                    Out = outname
                    Src = ls "Reports/OracleClient/**/*.cs" + commonSrcFiles
                    Ref = libs.nunit + libs.moq + ardep ["Extensibility"; "Core"]
                    RefGlobal = ["System.Data.OracleClient.dll"]
                    }
            }

        ardll("RdfExport") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Target = Library
                    Out = outname
                    Src = ls "RDFExport/**/*.cs"
                        + "Reports/ReportsCore/Rendering/CumulativeTotalsHelper.cs"
                        + commonSrcFiles
                    Ref = libs.nunit + libs.moq + ardep ["Extensibility"; "Core"; "Diagnostics"; "Testing.Tools"; "Document"]
                    }
            }

        ardll("XmlExport") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Target = Library
                    Out = outname
                    Src = fileset {
                        includes "XmlExport/**/*.cs"
                        includes "SL/CommonFiles/SafeGraphics.cs"
                        includes "SL/Exports/*.cs"
                        includes "SL/DDLib.Net/Shared/*.cs"
                        includes "SL/Document/Document/LayoutUtils.cs"
                        join commonSrcFiles
                        }
                    Ref = libs.nunit + libs.moq + libs.moqseq
                        + ardep ["Extensibility"; "Core"; "Diagnostics"; "Testing.Tools"; "Document"; "RdfExport"]
                    }
            }

        ardll("Image.Unsafe") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Target = Library
                    Unsafe = true
                    Out = outname
                    Src = ls "SL/DDLib.Net/Drawing/MonochromeBitmapTool.cs" + commonSrcFiles
                    }
            }

        ardll("ImageExport") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = fileset {
                        includes "ImageExport/**/*.cs"
                        includes "Reports/ReportsCore/Rendering/Tools/Text/FontDescriptor.cs"
                        includes "Reports/ReportsCore/Rendering/Tools/Cache/Services.cs"
                        //includes "SL/Exports/PageRangeParser.cs"
                        join commonSrcFiles
                        }
                    Ref = libs.nunit + libs.moq
                        + ardep ["Extensibility"; "Core"; "Diagnostics"; "Testing.Tools"; "Document"; "Image.Unsafe"; "RdfExport"]
                    }
            }

        ardll("Viewer.Win") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = fileset {
                        includes "UnifiedViewer/Base/Common/**/*.cs"
                        includes "UnifiedViewer/Base/Properties/BaseResources.Designer.cs"
                        includes "UnifiedViewer/Base/Tests/**/*.cs"
                        includes "UnifiedViewer/Base/WinFormsSpecific/**/*.cs"
                        includes "UnifiedViewer/WinForms/**/*.cs"
                        join commonSrcFiles
                        }
                    Resources =
                        [
                            resourceset {
                                prefix "GrapeCity.ActiveReports.Viewer.Win"
                                dynamic true
                                basedir ("UnifiedViewer\\WinForms")
                                includes "**/*.resx"
                                includes "Properties/resources/Viewer.bmp"
                            }
                            resourceset {
                                prefix "GrapeCity.Viewer"
                                files (!! "Properties/**/*.resx" @@ "UnifiedViewer/Base")
                            }
                        ]
                    Ref = libs.nunit + libs.moq
                        + ardep ["Extensibility"; "Core"; "Diagnostics"; "Testing.Tools"; "Document"; "ImageExport"]
                }
            }

        arexe("Viewer") *> fun outname -> action {

// example of resource "compilation"
//            do! ResGen {
//                ResgenSettings with
//                    Resources =
//                      [
//                        resourceset {
//                            prefix "GrapeCity.ActiveReports.Viewer.Win"
//                            dynamic true
//                            files (!!"Designer/Export/*.resx" + "**/*.resx" @@ {FailOnEmpty = false; BaseDir = Some "WinViewer"})
//                        }
//                      ]
//                    TargetDir = System.IO.DirectoryInfo "out"
//                }

            do! Csc {
                CscSettings with
                    Out = outname
                    Src = ls "WinViewer/**/*.cs" + "Designer/Export/*.cs" + commonSrcFiles
                    Ref = libs.nunit + libs.moq
                        + ardep ["Extensibility"; "Document"; "Chart"; "Core"; "ImageExport"; "RdfExport"; "Viewer.Win"]                        
                    Resources =
                    [
                        resourceset {
                            prefix "GrapeCity.ActiveReports.Viewer.Win"
                            dynamic true
                            basedir "WinViewer"
                            includes "Designer/Export/*.resx"
                            includes "**/*.resx"
                        }
                    ]
                }
            }
        ]

    rule (ardll("Document") *> fun outname -> action {

        let src = fileset {
            includes "SL/CommonFiles/SafeGraphics.cs"
            includesif DEBUG "SL/CommonFiles/DebugShims.cs"
            includes "SL/DDLib.Net/Controls/**/*.cs"
            includes "SL/DDLib.Net/DDWord/kinsoku.cs"
            includes "SL/DDLib.Net/Utility/*.cs"
            includes "SL/DDLib.Net/ZLib/*.cs"
            includes "PdfExport/AR/PDFRender/BidiTable.cs"
            includes "PdfExport/AR/PDFRender/BidiReference.cs"
            includes "SL/Document/**/*.cs"

            excludes "SL/DDLib.Net/ZLib/ZByteArray.cs"
        }

        do! Csc {
            CscSettings with
                Target = Library
                Out = outname
                Define = ["ARVIEWER_BUILD"]
                Src = src + commonSrcFiles
                Ref = !! (ardll "Extensibility")
                    +? (DEBUG, libs.nunit)
                    +? (DEBUG, ardll "Testing.Tools")
                }
    })

    // this is the other syntax to define rule (seems to be redundant
    addRule (ardll "Core") (fun outname -> action {

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
                Out = outname
                Define = ["DATAMANAGER_HOST_IS_STRYKER"]
                Src = src
                Ref = libs.nunit + ardep ["Extensibility"; "Diagnostics"; "Testing.Tools"; "Document"; "Chart"]
                RefGlobal = ["Microsoft.VisualBasic.dll"]

                Resources =
                [
                    resourceset {
                        prefix "GrapeCity.ActiveReports.ReportsCore"
                        dynamic true
                        basedir "Reports/ReportsCore"
                        includes "ReportObjectModel/Rdl/*.xsd"
                        includes "AssemblyResources/*.bmp"
                        includes "AssemblyResources/*.png"
                        includes "AssemblyResources/IconSet/*.png"
                        includes "AssemblyResources/*.GIF"
                        includes "AssemblyResources/*.ent"
                        includes "AssemblyResources/*.dtd"
                        includes "**/*.resx"
                        includes "AssemblyResources/*.xsd"
                        includes "AssemblyResources/*.xml"
                    }

                    resourceset {
                        dynamic true
                        basedir "SL/AREngine"
                        prefix "GrapeCity.ActiveReports"
                        includes "Resources/*.bmp"
                        includes "Resources/*.png"
                        includes "**/*.resx"
                        excludes "ja/**/*.resx"
                    }

                    resourceset {
                        dynamic true
                        prefix "GrapeCity.ActiveReports.CSS"
                        basedir "SL/CSS"
                        includes "**/*.resx"
                    }
                ]
        }
    })

    // TODO Designer, Xaml, Word, Html, Excel, Dashboard, Design.Win

}