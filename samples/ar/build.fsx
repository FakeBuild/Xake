// xake build file for activereports source code
(*
 TODO
    * RELEASE/DEBUG builds
    * Silverlight (MS build now, own task later)
    * FlashViewer
*)

//#r @"..\..\bin\Xake.Core.dll"
#r @"\projects\Mine\xake\bin\Xake.Core.dll"
open Xake

let DEBUG = true

////// utility methods
let ardll name = @"out\GrapeCity.ActiveReports." + name + ".v9.dll"
let arexe name = @"out\GrapeCity.ActiveReports." + name + ".v9.exe"
let ardep = List.fold (fun fs f -> fs + (ardll f)) Fileset.Empty

let commonSrcFiles = fileset {
    includes "CommonAssemblyInfo.cs"
    includes "VersionInfo.cs"
    includes "AssemblyNames.cs"
    includes "CommonFiles/SmartAssembly.Attributes.cs"
    }

let dlls =
    List.map ardll <|
    [
        "Extensibility"
        "Diagnostics"
        "Chart"
        "Document"
        "Core"
        "OracleClient"
        "RdfExport"; "XmlExport"; "Image.Unsafe"; "ImageExport"; "HtmlExport"; "Export.Word"; "Export.Excel"
        "Viewer.Win"; "Design.Win"
    ]

let executables =
    List.map arexe <|
    [
        "Viewer"; "Designer"
    ]

// external tools/libs that has to be copied to 'out' folder
module libs =
    let nunit = !! "out\\nunit.framework.dll"
    let xmldiff = !! "out\\XmlDiffPatch.dll"
    let moq = !! "out\\moq.dll"
    let moqseq = !! "out\\moq.sequences.dll"
    let iTextSharp = !! "out\\iTextSharp.dll"
    let OpenXml = !! "out\\DocumentFormat.OpenXml.dll"
    let qwhale = !! "out\\Qwhale.All.dll"
    let testing_tools = ardll "Testing.Tools"

    let InteropExcel = !! "out\\Microsoft.Office.Interop.Excel.dll"
    let VbeInterop = !! "out\\Microsoft.Vbe.Interop.dll"
    let office = !! "out\\office.dll"

// creates a "copy file" rule (used for external deps)
let copyToOutput lib srcpath = ("out" </> lib) *> fun outfile -> action {do! cp (srcpath </> lib) outfile.FullName}

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
        do! need (executables @ dlls)
    })

    rules [
        copyToOutput "nunit.framework.dll"              "Tools/NUnit"
        copyToOutput "XmlDiffPatch.dll"                 "Tools/XmlDiff"
        copyToOutput "moq.dll"                          "Tools/Moq.3.1"
        copyToOutput "moq.sequences.dll"                "Tools/Moq.3.1"
        copyToOutput "iTextSharp.dll"                   "ExternalLibs/iTextSharp/build"
        copyToOutput "DocumentFormat.OpenXml.dll"       "ExternalLibs/OpenXMLSDKV2.0"
        copyToOutput "Qwhale.All.dll"                   "ExternalLibs/QwhaleEditor"
        copyToOutput "DocumentFormat.OpenXml.dll"       "ExternalLibs/OpenXMLSDKV2.0"

        copyToOutput "Microsoft.Office.Interop.Excel.dll"  "Tools/Excel"
        copyToOutput "Microsoft.Vbe.Interop.dll"           "Tools/Excel"
        copyToOutput "office.dll"                          "Tools/Excel"
    ]

    rules [
        ardll "Extensibility" *> fun outname -> action {

            let sources = fileset {
                includes "Extensibility/**/*.cs"
                includes "SL/CommonFiles/SafeGraphics.cs"
                join commonSrcFiles
            }

            do! (csc {
                out outname
                src sources
                refif DEBUG libs.nunit

                resourceslist [
                    resourceset {
                        prefix "GrapeCity.ActiveReports"
                        dynamic true
                        basedir "." // TODO looks redundant
                        includes "Extensibility/**/*.resx"
                    }
                ]
            })
        }
    
        ardll "Diagnostics" *> fun outname -> action {

            do! Csc {
                CscSettings with
                    Out = outname
                    Src = !!"Diagnostics/**/*.cs" + commonSrcFiles
                    Ref = Fileset.Empty +? (DEBUG,libs.nunit)
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
                    Ref = !! (ardll "Extensibility")
                        + libs.nunit
                        + libs.xmldiff
            }
        }

        ardll("Chart") *> fun outname -> action {
            (* example of simplified syntax *)
            do! (csc {
                out outname
                define ["ARNET"]
                src (!! "SL/ARChart/**/*.cs" + commonSrcFiles)
                refif DEBUG libs.nunit
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
                    Ref = ardep ["Extensibility"; "Core"] +? (DEBUG, libs.nunit) +? (DEBUG, libs.moq)
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
                    Ref = ardep ["Extensibility"; "Core"; "Diagnostics"; "Document"] +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit) +? (DEBUG, libs.moq)
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
                    Ref = ardep ["Extensibility"; "Core"; "Diagnostics"; "Document"; "RdfExport"] +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit) +? (DEBUG, libs.moq) +? (DEBUG, libs.moqseq)
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
                    Ref = ardep ["Extensibility"; "Core"; "Diagnostics"; "Document"; "Image.Unsafe"; "RdfExport"]
                        +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit) +? (DEBUG, libs.moq)
            }
        }
            
        ardll("HtmlExport") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = fileset {
                        includes "HtmlExport/**/*.cs"
                        includes "SL/CommonFiles/SafeGraphics.cs"
                        includes "SL/DDLib.Net/DDWord/kinsoku.cs"
                        join (fileset {
                            basedir @"Reports\ReportsCore"
                            includes @"Rendering\CumulativeTotalsHelper.cs"
                            includes @"Rendering\Tools\Cache\*.cs"
                            includes @"Rendering\Tools\Text\*.cs"
                            includes @"FontProcessor\*.cs"
                        })
                        join commonSrcFiles
                        }
                    Resources =
                        [
                        resourceset {
                            prefix "GrapeCity.ActiveReports.Export.Html"
                            dynamic true
                            basedir "HtmlExport"
                            
                            includes "**/*.bmp"
                            includes "AR/*.resx"
                            includes "DDR/AssemblyResources/*.resx"
                            includes "DDR/Core/HtmlRenderers/*.png"
                            includes "DDR/Core/HtmlRenderers/tocScript.js"
                            includes "DDR/Core/HtmlRenderers/tocStyles.txt"
                        }
                        ]
                    Ref = ardep ["Extensibility"; "Document"; "Core"; "Diagnostics"; "RdfExport"]
                        +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit) +? (DEBUG, libs.moq)
            }
        }
        
        ardll("Export.Excel") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Unsafe = true
                    Out = outname
                    Src = fileset {
                        includes "ExcelExport/**/*.cs"
                        includes "CommonFiles/*.cs"
                        includes "SL/CommonFiles/SafeGraphics.cs"

                        includes "SL/Document/ResourceStorage/HashCalculator.cs"
                        includes "SL/Exports/*.cs"

                        includes "SL/DDLib.NET/ZLib/*.cs"
                        excludes "SL/DDLib.NET/ZLib/ZByteArray.cs"

                        includes "SL/DDLib.NET/Shared/LengthConverter.cs"
                        includes "SL/DDLib.NET/ZipArchive/ZipArchive.cs"
                        includes "SL/Document/Document/LayoutUtils.cs"

                        includes "SL/DDLib.NET/Utility/GraphicsUtility.cs"
                        includes "SL/DDLib.NET/Core/DDFormat.cs"

                        join commonSrcFiles
                        }
                    Resources =
                        [
                        resourceset {
                            prefix "GrapeCity.ActiveReports.Export.Excel"
                            dynamic true
                            basedir "ExcelExport"
                            
                            includes "ExcelExport/*.resources"
                            includes "ExcelExport/**/*.resx"
                            includes "ExcelExport/**/*.xml"
                            includes "ExcelExport/**/*.png"
                            includes "ExcelExport/**/*.bmp"
                            includes "DDR/ExcelTemplateGenerator/Empty.xls"
                            includes "DDR/ExcelTemplateGenerator/EmptyJP.xls"
                        }
                        ]
                    Ref = ardep ["Extensibility"; "Document"; "Core"; "Diagnostics"; "RdfExport"]
                        +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit) + libs.OpenXml
                        +? (DEBUG, libs.InteropExcel) +? (DEBUG, libs.VbeInterop) +? (DEBUG, libs.office)
                    RefGlobal = ["WindowsBase.dll"; "System.Core.dll"]
            }
        }

        ardll("Export.Word") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = fileset {
                        includes "WordExport/**/*.cs"
                        includes "SL/CommonFiles/SafeGraphics.cs"
                        includes "SL/DDLib.Net/DDWord/kinsoku.cs"
                        includes @"Reports\ReportsCore\Rendering\CumulativeTotalsHelper.cs"
                        includes @"Reports\ReportsCore\Rendering\Tools\Cache\*.cs"
                        includes @"Reports\ReportsCore\Rendering\Tools\Text\*.cs"
                        includes @"Reports\ReportsCore\FontProcessor\*.cs"

                        //////
                        includes "SL/DDLib.Net/DDWord/kinsoku.cs"
                        includes "WordExport/AssemblyInfo.cs"
                        includes "WordExport/AR/**/*.cs"
                        includes "WordExport/DDR/**/*.cs"

                        includes @"SL\CommonFiles\SafeGraphics.cs"
                        includes @"HtmlExport\AR\Mht\*"

                        includes "SL/Exports/*.cs"

                        includes @"SL/DDLib.Net\Drawing\ColorTools.cs"
                        includes @"SL/DDLib.Net\Drawing\MetaFileSaver.cs"
                        includes @"SL/DDLib.Net\Shared\Disposer.cs"
                        includes @"SL/DDLib.Net\Shared\LengthConverter.cs"
                        includes @"SL/DDLib.Net\Shared\MathTools.cs"

                        includes @"SL\Document\Document\LayoutUtils.cs"

                        join commonSrcFiles
                        }
                    Resources =
                        [
                        resourceset {
                            prefix "GrapeCity.ActiveReports.Export.Word"
                            dynamic true
                            basedir "WordExport"
                            
                            includes "**/*.bmp"
                            includes "AR/Resources.resx"
                            includes "AR/RtfExport.resx"
                            includes "DDR/**/*.resx"
                            includes "DDR/**/*.xml"
                            includes "DDR/**/*.png"
                        }
                        ]
                    Ref = ardep ["Extensibility"; "Document"; "Core"; "Diagnostics"; "RdfExport"]
                        +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit)
            }
        }

        ardll("Viewer.Win") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = fileset {
                        // basedir "UnifiedViewer" TODO fileset does not support combiinng filesets with different basedir
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
                                basedir "UnifiedViewer/Base"
                                includes "Properties/**/*.resx"
                            }
                        ]
                    Ref = ardep ["Extensibility"; "Core"; "Diagnostics"; "Document"; "ImageExport"]
                        +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit) +? (DEBUG, libs.moq)
            }
        }

        ardll("Design.Win") *> fun outname -> action {

            do! need ["out\\Qwhale.All.dll"]
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = fileset {
                        includes "Design/**/*.cs"
                        includes "SL/DDLib.Net/DDWord/kinsoku.cs"

                        includes @"SL\AREngine\General\designtimehelper.cs"
                        includes @"SL\AREngine\UnitTest\ImageComparer.cs"

                        join commonSrcFiles
                        }
                    Resources =
                        [
                            resourceset {
                                prefix "GrapeCity.ActiveReports.Design"
                                dynamic true
                                basedir "Design"
                                includes "**/*.resx"
                                excludes "Editor/StringConsts.resx"
                                includes @"Resources\**\*.rpx"
                                includes @"Toolbox\**\*.bmp"
                                includes "Resources/CommonResources/Toolbox.bmp"
                                includes "Resources/CommonResources/Designer.bmp"
                                includes "Resources/CommonResources/ReportExplorer.bmp"
                            }
                        ]
                    Ref = ardep ["Extensibility"; "Core"; "Diagnostics"; "Document"; "Chart"; "Viewer.Win"]
                        +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit) +? (DEBUG, libs.moq)
                    CommandArgs =
                        [
                            "/r:Qwhale=out\\Qwhale.All.dll"
                        ]
            }
        }

        arexe("Viewer") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = ls "WinViewer/**/*.cs" + "Designer/Export/*.cs" + commonSrcFiles
                    Ref = ardep ["Extensibility"; "Document"; "Chart"; "Core"; "ImageExport"; "RdfExport"; "Viewer.Win"]                        
                        +? (DEBUG, libs.nunit) +? (DEBUG, libs.moq)
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

        arexe("Designer") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = ls "Designer/**/*.cs" + commonSrcFiles
                    Ref = ardep ["Extensibility"; "Design.Win"; "Document"; "Core"; "ImageExport"; "RdfExport"; "Viewer.Win"]
                        +? (DEBUG, libs.nunit) +? (DEBUG, libs.moq) +? (DEBUG, libs.moqseq)
                    Resources =
                    [
                        resourceset {
                            prefix "GrapeCity.ActiveReports.Designer.Win"
                            dynamic true
                            basedir "Designer"
                            includes "**/*.resx"
                        }
                    ]
            }
        }
        ]

    rule (ardll("Document") *> fun outname -> action {

        let src = fileset {
            includes "SL/CommonFiles/SafeGraphics.cs"
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
                Ref = !! (ardll "Extensibility") +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit)
                Resources =
                [
                    resourceset {
                        prefix "GrapeCity.ActiveReports.Document.Section"
                        dynamic true
                        basedir "SL/Document"
                        includes "**/*.resx"
                        excludes "ja/**/*.resx"
                        includes "Resources/*.png"
                        includes "Resources/*.cur"
                        includes "Resources/*.png"
                    }
                ]
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
                Ref = ardep ["Extensibility"; "Diagnostics"; "Document"; "Chart"] +? (DEBUG, libs.testing_tools) +? (DEBUG, libs.nunit)
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

    // TODO Xaml, Excel, Dashboard

}