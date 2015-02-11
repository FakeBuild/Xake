namespace XakeLibTests

open System.IO

open Xake.DomainTypes
open Xake.Fileset

open NUnit.Framework

[<TestFixture (Description = "Unit tests for Fileset module")>]
type FilesetTests() =

    let currentDir = Path.Combine (__SOURCE_DIRECTORY__, "..")

    let name (file:FileInfo) = file.Name
    let fullname (file:FileInfo) = file.FullName

    let tolower (s:string) = s.ToLower()

    let IsAny() = Is.Not.All.Not

    let dirfullname (f:DirectoryInfo) = f.FullName
    let MockFileSystem root =
        {
        FileSystem with
            GetDisk = fun d -> root </> d.Substring(0,1) + "_drive" // Path.DirectorySeparatorChar.ToString()
            GetDirRoot = fun _ -> root
        }

    let getFilesAt start f =
        let (Filelist files) = f |> toFileList start
        in files

    let getFiles = getFilesAt currentDir

    let getFiles1 vroot start f =
        let fs =
            {
            FileSystem with
                GetDisk = fun d -> vroot </> d.Substring(0,1) + "_drive" // Path.DirectorySeparatorChar.ToString()
                GetDirRoot = fun _ -> vroot
            }
        let (Filelist files) = f |> (toFileList1 fs (vroot </> start))
        in files

    let root1 = currentDir </> "testdata" </> "withdrive" // </> "c_drive" </> "rpt"

    [<Test (Description = "Verifies ls function")>]
    member o.LsSimple() =

        let files = ls "a*" |> getFiles1 root1 @"c:\rpt"
        Assert.That (files |> List.map name, Is.EquivalentTo (List.toSeq ["a.rdl"]))

    [<Test (Description = "Verifies strange DOS (ok Windows) behavior when looking for files by '*.txt' mask")>]
    member o.LsThreeLetterExtension() =

        let files = ls "*.rdl" |> getFiles1 root1 @"c:\rpt"
        Assert.That (files |> List.map name, Is.EquivalentTo (List.toSeq ["a.rdl"; "b.rdl"; "c.rdlx"; "c1.rdlx"]))

    [<Test (Description = "Verifies fileset exec function removes duplicates from result")>]
    member o.ExecDoesntRemovesDuplicates() =

        Assert.That (
            fileset {includes "*.rdl"; includes "*.rdl*"}
            |> getFiles1 root1 @"c:\rpt"
            |> List.map (name >> tolower) |> List.filter ((=) "a.rdl") |> List.length,
            Is.EqualTo (2))

    [<Test>]
    member o.LsRecursive() =
        Assert.That(
            ls "c:/rpt/**/e.rdl" |> getFiles1 root1 "" |> List.map fullname |> List.toArray,
            Is.All.EndsWith("rpt" </> "nested" </> "nested2" </> "e.rdl").IgnoreCase)

    [<Test (Description = "Verifies 'explicit' rules, which match file regardless actual file presense")>]
    member o.LsExplicit() =
        Assert.That(
            ls "c:/rpt/aaa.rdl" |> getFiles1 root1 "" |> List.map fullname |> List.toArray,
            Is.All.EndsWith("rpt" </> "aaa.rdl").IgnoreCase)

    [<Test>]
    member o.LsExcludes() =

        let fileNames = (!! "*.rdl*" -- "*.rdlx") |> getFilesAt (root1 </> "c_drive" </> "rpt") |> List.map name
        fileNames |> List.iter System.Console.WriteLine
        Assert.That (fileNames, Is.All.EndsWith("rdl"))

    [<Test>]
    member o.Builder() =
        let fileset = fileset {
            basedir @"c:\rpt"

            includes "*.rdl"
            includes "*.rdlx"
            
            includes @"..\jparsec\src\main/**/*.java"

            do! fileset {
                includes @"c:\bak\*.css"
            }
        }

        let files = fileset |> getFiles1 root1 "" |> List.map name
        in
        do files |> List.iter System.Console.WriteLine
        do Assert.That(
            files,
            Constraints.Constraint.op_BitwiseOr( IsAny().EndsWith(".java"), IsAny().EndsWith(".css"))
            )        

    [<Test>]
    member o.ExplicitFileYieldsFile() =

        let sampleFileName = "NonExistingFile.rdl"

        let fileset = fileset {
            basedir @"c:\rpt"

            includes "*.rdl"
            includes sampleFileName
        }

        Assert.That(
            fileset |> getFiles1 root1 "" |> List.map name,
            IsAny().EndsWith(sampleFileName)
            )

    [<Test>]
    member o.ShortForm() =

        let fileset =
                ls "*.rdl" + "*.rdlx" + "../jparsec/src/main/**/*.java" @@ """c:\rpt"""
        Assert.That(
            fileset |> getFiles1 root1 "" |> List.map name,
            Constraints.Constraint.op_BitwiseOr( IsAny().EndsWith(".java"), IsAny().EndsWith(".rdlx"))
            )

    [<Test>]
    [<ExpectedException>]
    member o.CombineFilesetsWithBasedirs() =
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

    [<Test>]
    member o.CombineFilesets() =
        let fs1 = fileset {includes @"c:\!\bak\*.css"}
        let fs2 = fileset {includes @"c:\!\bak\*.rdl"}

        (fs1 + fs2) |> getFiles |> ignore

    [<TestCase("c:\\**\\*.*", "c:\\", ExpectedResult = false)>]
    [<TestCase("c:\\**\\*.*", "",         ExpectedException = typeof<System.ArgumentException> )>]
    [<TestCase("", "aa",                            ExpectedException = typeof<System.ArgumentException> )>]
    [<TestCase("c:\\**\\*.*", "c:\\a.c", ExpectedResult = true)>]
    [<TestCase("c:\\**\\", "c:\\a.c", ExpectedResult = false)>]
    [<TestCase("c:\\*\\?.c", "c:\\!\\a.c", ExpectedResult = true)>]
    [<TestCase("c:\\*\\?.c*", "c:\\!\\a.c", ExpectedResult = true)>]

    [<TestCase("c:\\a.c", "d:\\a.c", ExpectedResult = false)>]
    [<TestCase("c:\\a.c", "C:\\A.c", ExpectedResult = true)>]
    [<TestCase("c:\\*.c", "d:\\a.c", ExpectedResult = false)>]
    [<TestCase("c:\\*.c", "c:\\a.c", ExpectedResult = true)>]

    [<TestCase("c:\\abc\\*.c", "c:\\a.c", ExpectedResult = false)>]
    [<TestCase("c:\\abc\\*.c", "c:\\def\\a.c", ExpectedResult = false)>]
    [<TestCase("c:\\abc\\*.c", "c:\\abc\\def\\a.c", ExpectedResult = false)>]
    [<TestCase("c:\\abc\\*.c", "c:\\abc\\a.c", ExpectedResult = true)>]

    [<TestCase("c:\\*\\*.c", "c:\\abc\\a.c", ExpectedResult = true)>]
    [<TestCase("c:\\*\\*.c", "c:\\abc\\def\\a.c", ExpectedResult = false)>]
    [<TestCase("c:\\**\\*.c", "c:\\abc\\def\\a.c", ExpectedResult = true)>]
    member o.MaskTests(m,t) = matches m "" t

    [<TestCase("c:\\*\\*.c", "c:\\abc\\def\\..\\a.c", ExpectedResult = true)>]
    [<TestCase("c:\\*.c", "c:\\abc\\..\\a.c", ExpectedResult = true)>]

    [<TestCase("c:\\abc\\..\\*.c", "c:\\a.c", ExpectedResult = true)>]
    [<TestCase("c:\\abc\\def\\..\\..\\*.c", "c:\\a.c", ExpectedResult = true)>]
    [<TestCase("c:\\abc\\..\\..\\*.c", "c:\\a.c", ExpectedResult = false)>]
    [<TestCase("c:\\abc\\**\\..\\*.c", "c:\\a.c", ExpectedResult = true)>]
    [<TestCase("c:\\abc\\..\\*.c", "c:\\abc\\..\\a.c", ExpectedResult = true)>]

    member o.MaskWithParent(m,t) = matches m "" t
