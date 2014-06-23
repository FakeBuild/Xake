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
    let qwhale_filename = "ExternalLibs\QwhaleEditor\Qwhale.All.dll"
    let nunit      = !! "Tools/NUnit/nunit.framework.dll"
    let xmldiff    = !! "Tools/XmlDiff/XmlDiffPatch.dll"
    let moq        = !! "Tools/Moq.3.1/moq.dll"
    let moqseq     = !! "Tools/Moq.3.1/moq.sequences.dll"
    let iTextSharp = !! "ExternalLibs/iTextSharp/build/iTextSharp.dll"
    let OpenXml    = !! "ExternalLibs/OpenXMLSDKV2.0/DocumentFormat.OpenXml.dll"
    let qwhale     = !! qwhale_filename

let dlls =
    List.map ardll <|
    [
        "Extensibility"
        "Diagnostics"; "Testing.Tools"
        "Chart"
        "Document"
        "Core"
        "OracleClient"
        "RdfExport"; "XmlExport"; "Image.Unsafe"; "ImageExport"; "HtmlExport"; "WordExport"
        "Viewer.Win"; "Design.Win"
    ]

let executables =
    List.map arexe <|
    [
        "Viewer"; "Designer"
    ]

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
            
        ardll("HtmlExport") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = fileset {
                        includes "HtmlExport/**/*.cs"
                        includes "SL/CommonFiles/SafeGraphics.cs"
                        includes "SL/DDLib.Net/DDWord/kinsoku.cs"
                        includes @"Reports\ReportsCore\Rendering\CumulativeTotalsHelper.cs"
                        includes @"Reports\ReportsCore\Rendering\Tools\Cache\*.cs"
                        includes @"Reports\ReportsCore\Rendering\Tools\Text\*.cs"
                        includes @"Reports\ReportsCore\FontProcessor\*.cs"

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
                    Ref = libs.nunit + libs.moq
                        +? (false, ardll "Testing.Tools")
                        + ardep ["Extensibility"; "Document"; "Core"; "Diagnostics"; "RdfExport"]
            }
        }

        ardll("WordExport") *> fun outname -> action {
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
                    Ref = libs.nunit
                        +? (false, ardll "Testing.Tools")
                        + ardep ["Extensibility"; "Document"; "Core"; "Diagnostics"; "RdfExport"]
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
                    Ref = libs.nunit + libs.moq
                        + ardep ["Extensibility"; "Core"; "Diagnostics"; "Testing.Tools"; "Document"; "ImageExport"]
            }
        }

        ardll("Design.Win") *> fun outname -> action {
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
                    Ref = libs.nunit + libs.moq
                        + ardep ["Extensibility"; "Core"; "Diagnostics"; "Testing.Tools"; "Document"; "Chart"; "Viewer.Win"]
                    CommandArgs =
                        [
                            "/r:Qwhale=" + libs.qwhale_filename
                        ]
            }
        }

        arexe("Viewer") *> fun outname -> action {
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

        arexe("Designer") *> fun outname -> action {
            do! Csc {
                CscSettings with
                    Out = outname
                    Src = ls "Designer/**/*.cs" + commonSrcFiles
                    Ref = libs.nunit + libs.moq + libs.moqseq
                        + ardep ["Extensibility"; "Design.Win"; "Document"; "Core"; "ImageExport"; "RdfExport"; "Viewer.Win"]
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

    // TODO Designer, Xaml, Excel, Dashboard

}