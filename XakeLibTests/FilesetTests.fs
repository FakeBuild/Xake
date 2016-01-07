module ``Fileset type``

open System.IO

open Xake
open Xake.Fileset

open NUnit.Framework

module private impl =
    let currentDir = Path.Combine (__SOURCE_DIRECTORY__, "..")

    let tolower (s:string) = s.ToLower()

    let isAny() = Is.Not.All.Not

    let getFilesAt start f =
        let (Filelist files) = f |> toFileList start
        in files

    let getFiles = getFilesAt currentDir

    let getFiles1 vroot start f =
        let fs =
            {
            FileSystem with
                GetDisk = fun d -> vroot </> d.Substring(0,1) + "_drive"
                GetDirRoot = fun _ -> vroot
            }
        let (Filelist files) = f |> (toFileList1 fs (vroot </> start))
        in files

    let root1 = currentDir </> "testdata" </> "withdrive"

open impl

[<Test>]
let ``could list a files in folder by wildcard``() =

    let files = ls "a*" |> getFiles1 root1 @"c:\rpt"
    Assert.That (files |> List.map File.getFileName, Is.EquivalentTo (List.toSeq ["a.rdl"]))

[<Test; Platform("Win")>]
let ``follows weird DOS (on Windows platform) behavior when looking for files by '*.txt' mask``() =

    let files = ls "*.rdl" |> getFiles1 root1 @"c:\rpt"
    Assert.That (files |> List.map File.getFileName, Is.EquivalentTo (List.toSeq ["a.rdl"; "b.rdl"; "c.rdlx"; "c1.rdlx"]))

[<Test; Platform("Unix")>]
let ``does NOT follow weird DOS (on Windows platform) behavior when looking for files by '*.txt' mask``() =

    let files = ls "*.rdl" |> getFiles1 root1 @"c:\rpt"
    Assert.That (files |> List.map File.getFileName, Is.EquivalentTo (List.toSeq ["a.rdl"; "b.rdl"]))

[<Test>]
let ``exec function removes duplicates from result``() =

    Assert.That (
        fileset {includes "*.rdl"; includes "*.rdl*"}
        |> getFiles1 root1 @"c:\rpt"
        |> List.map (File.getFileName >> tolower) |> List.filter ((=) "a.rdl") |> List.length,
        Is.EqualTo (2))

[<Test>]
let ``could search recursively ('**' mask) ``() =
    Assert.That(
        ls "c:/rpt/**/e.rdl" |> getFiles1 root1 "" |> List.map File.getFullName |> List.toArray,
        Is.All.EndsWith("rpt" </> "nested" </> "nested2" </> "e.rdl").IgnoreCase)

[<Test>]
let ``allows excluding files by mask``() =

    let fileNames = (!! "*.rdl*" -- "*.rdlx") |> getFilesAt (root1 </> "c_drive" </> "rpt") |> List.map File.getFileName
    fileNames |> List.iter System.Console.WriteLine
    Assert.That (fileNames, Is.All.EndsWith("rdl"))

[<Test>]
let ``could search and return directories``() =

    let files = ls "c:/*/" |> getFiles1 root1 @"c:\rpt"
    Assert.That (files |> List.map File.getFileName, Is.EquivalentTo (List.toSeq ["bak"; "rpt"; "jparsec"]))

[<Test>]
let ``privides builder computation``() =
    let fileset = fileset {
        basedir @"c:\rpt"

        includes "*.rdl"
        includes "*.rdlx"
            
        includes @"..\jparsec\src\main/**/*.java"

        do! fileset {
            includes @"c:\bak\*.css"
        }
    }

    let files = fileset |> getFiles1 root1 "" |> List.map File.getFileName
    in
    do files |> List.iter System.Console.WriteLine
    do Assert.That(
        files,
        Constraints.Constraint.op_BitwiseOr( isAny().EndsWith(".java"), isAny().EndsWith(".css"))
        )        

[<Test>]
let ``handles 'explicit' rules, which match file regardless actual file presense``() =
    Assert.That(
        ls "c:/rpt/aaa.rdl" |> getFiles1 root1 "" |> List.map File.getFullName |> List.toArray,
        Is.All.EndsWith("rpt" </> "aaa.rdl").IgnoreCase)

[<Test>]
let ``explicitly specified file yields non-existing file``() =

    let sampleFileName = "NonExistingFile.rdl"

    let fileset = fileset {
        basedir @"c:\rpt"

        includes "*.rdl"
        includes sampleFileName
    }

    Assert.That(
        fileset |> getFiles1 root1 "" |> List.map File.getFileName,
        isAny().EndsWith(sampleFileName)
        )

[<Test>]
let ``providers operators for short definition``() =

    let fileset =
            ls "*.rdl" + "*.rdlx" + "../jparsec/src/main/**/*.java" @@ """c:\rpt"""
    Assert.That(
        fileset |> getFiles1 root1 "" |> List.map File.getFileName,
        Constraints.Constraint.op_BitwiseOr( isAny().EndsWith(".java"), isAny().EndsWith(".rdlx"))
        )

[<Test>]
let ``could combine filesets into new one``() =
    let fs1 = fileset {includes @"c:\!\bak\*.css"}
    let fs2 = fileset {includes @"c:\!\bak\*.rdl"}

    (fs1 + fs2) |> getFiles |> ignore

[<Test>]
[<ExpectedException>]
let ``combine filesets with different base dirs are not implemented yet``() =
    let fs1 = fileset {
        basedir @"c:\!"
        includes @"bak\*.css"
        includesif false @"debug\*.css"
    }

    let fs2 = fileset {
        basedir @"c:\!\bak"
        includes @"*.rdl"

        join fs1
    }
    fs2 |> getFiles |> ignore
