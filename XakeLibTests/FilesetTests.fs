namespace XakeLibTests

open System.IO

open Xake.DomainTypes
open Xake.Fileset

open NUnit.Framework

[<TestFixture (Description = "Unit tests for Fileset module")>]
type FilesetTests() =

  let currentDir = Path.Combine (__SOURCE_DIRECTORY__, "..")
  let mutable rememberDir = ""

  let pathName (file:FileInfo) = file.Name
  let tolower (s:string) = s.ToLower()

  [<TestFixtureSetUp>]
  member o.Setup() =
    rememberDir <- Directory.GetCurrentDirectory()
    Directory.SetCurrentDirectory currentDir

  [<TestFixtureTearDown>]
  member o.Teardown() =
    Directory.SetCurrentDirectory rememberDir

  [<Test (Description = "Verifies ls function")>]
  member o.LsSimple() =
    let (FileList files) = ls "*.sln"
    Assert.That (List.map pathName files, Is.EquivalentTo (List.toSeq ["xake.sln"]))

  [<Test (Description = "Verifies fileset exec function removes duplicates from result")>]
  member o.ExecDoesntRemovesDuplicates() =

    let (FileList result) = exec (fileset {includes "*.sln"; includes "*.s*"})

    Assert.That (
      result |> List.map (pathName >> tolower) |> List.filter ((=) "xake.sln") |> List.length,
      Is.EqualTo (2))

  [<Test>]
  member o.LsMore() =
    let (FileList files) = ls "c:/!/**/*.c*"
    let PathName (file:FileInfo) = file.FullName
    files |> List.map PathName |> List.iter System.Console.WriteLine

  [<Test>]
  member o.LsParent() =
    let (FileList files) = exec (+ "c:/!/bak" ++ "../../!/*.c*")
    let PathName (file:FileInfo) = file.FullName
    files |> List.map PathName |> List.iter System.Console.WriteLine

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

    let (FileList files) = exec fileset
    let PathName (file:FileInfo) = file.FullName
    files |> List.map PathName |> List.iter System.Console.WriteLine

  [<Test>]
  member o.ShortForm() =

    let fileset =
        !! "*.rdl" + "*.rdlx" + "../jparsec/src/main/**/A*.java" @@ """c:\!\bak"""

    let fileset1 =
        + @"c:\!\bak" + "*.rdl" + "*.rdlx" + "../jparsec/src/main/**/A*.java"

    let (FileList files) = exec fileset
    let PathName (file:FileInfo) = file.FullName
    files |> List.map PathName |> List.iter System.Console.WriteLine

  [<Test>]
  [<ExpectedException>]
  member o.CombineFilesetsWithBasedirs() =
    let fs1 = fileset {
      basedir @"c:\!"
      includes @"bak\*.css"
    }

    let fs2 = fileset {
      basedir @"c:\!\bak"
      includes @"*.rdl"

      join fs1
    }
    exec fs2 |> ignore

  [<Test>]
  member o.CombineFilesets() =
    let fs1 = fileset {includes @"c:\!\bak\bak\*.css"}
    let fs2 = fileset {includes @"c:\!\bak\*.rdl"}

    exec (fs1 + fs2) |> ignore

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
  [<TestCase("c:\\abc\\..\\*.c", "c:\\abc\\..\\a.c", ExpectedResult = true)>]

  [<TestCase("c:\\abc\\..\\*.c", "c:\\a.c", ExpectedResult = true)>]
  [<TestCase("c:\\abc\\..\\*.c", "c:\\abc\\..\\a.c", ExpectedException = typeof<System.ArgumentException>)>]

  member o.MaskWithParent(m,t) = matches m "" t
