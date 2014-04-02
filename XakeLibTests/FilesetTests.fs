namespace XakeLibTests

open System.IO

open Xake.DomainTypes
open Xake.Fileset

open NUnit.Framework

[<TestFixture (Description = "Unit tests for Fileset module")>]
type FilesetTests() =

  let currentDir = Path.Combine (__SOURCE_DIRECTORY__, "..")
  let mutable rememberDir = ""

  let name (file:FileInfo) = file.Name
  let fullname (file:FileInfo) = file.FullName

  let tolower (s:string) = s.ToLower()

  let IsAny() = Is.Not.All.Not

  [<TestFixtureSetUp>]
  member o.Setup() =
    rememberDir <- Directory.GetCurrentDirectory()
    Directory.SetCurrentDirectory currentDir

  [<TestFixtureTearDown>]
  member o.Teardown() =
    Directory.SetCurrentDirectory rememberDir

  [<Test (Description = "Verifies ls function")>]
  member o.LsSimple() =
    let files = ls "*.sln" |> getFiles
    Assert.That (files |> List.map name, Is.EquivalentTo (List.toSeq ["xake.sln"]))

  [<Test (Description = "Verifies fileset exec function removes duplicates from result")>]
  member o.ExecDoesntRemovesDuplicates() =

    Assert.That (
      fileset {includes "*.sln"; includes "*.s*"}
      |> getFiles
      |> List.map (name >> tolower) |> List.filter ((=) "xake.sln") |> List.length,
      Is.EqualTo (2))

  [<Test>]
  member o.LsMore() =
    ls "c:/!/**/*.c*" |> getFiles |> List.map fullname |> List.iter System.Console.WriteLine
    Assert.That(
      ls "c:/!/**/*.c*" |> getFiles |> List.map fullname |> List.toArray,
      IsAny().EqualTo(@"C:\!\main.c").IgnoreCase)

  [<Test>]
  member o.LsParent() =
    (+ "c:/!/bak" ++ "../../!/*.c*") |> getFiles |> List.map fullname |> List.iter System.Console.WriteLine

  [<Test>]
  member o.LsExcludes() =
    let fileNames = (+ "c:/!/bak" ++ "m*.rdl*" ++ "a*.rdl*" -- "*.rdlx") |> getFiles |> List.map name
    fileNames |> List.iter System.Console.WriteLine
    Assert.That (fileNames, Is.All.Not.Contains("rdlx"))

  [<Test>]
  member o.Builder() =
    let fileset = fileset {
      basedir @"c:\!\bak"

      includes "*.rdl"
      includes "*.rdlx"
      
      includes @"..\jparsec\src\main/**/A*.java"

      do! fileset {
        includes @"c:\!\bak\*.css"
      }
    }

    fileset |> getFiles |> List.map fullname |> List.iter System.Console.WriteLine

  [<Test>]
  member o.ShortForm() =

    let fileset =
        ls "*.rdl" + "*.rdlx" + "../jparsec/src/main/**/A*.java" @@ """c:\!\bak"""

    let fileset1 =
        + @"c:\!\bak" + "*.rdl" +? (false,"*.rdlx") + "../jparsec/src/main/**/A*.java"

    fileset |> getFiles |> List.map fullname |> List.iter System.Console.WriteLine

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
    scan fs2 |> ignore

  [<Test>]
  member o.CombineFilesets() =
    let fs1 = fileset {includes @"c:\!\bak\bak\*.css"}
    let fs2 = fileset {includes @"c:\!\bak\*.rdl"}

    scan (fs1 + fs2) |> ignore

  [<TestCase("c:\\**\\*.*", "c:\\", ExpectedResult = false)>]
  [<TestCase("c:\\**\\*.*", "",     ExpectedException = typeof<System.ArgumentException> )>]
  [<TestCase("", "aa",              ExpectedException = typeof<System.ArgumentException> )>]
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
  [<TestCase("c:\\abc\\..\\*.c", "c:\\abc\\..\\a.c", ExpectedException = typeof<System.ArgumentException>)>]

  member o.MaskWithParent(m,t) = matches m "" t
