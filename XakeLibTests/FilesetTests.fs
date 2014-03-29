namespace XakeLibTests

open System.IO

open Xake.DomainTypes
open Xake.Fileset

open NUnit.Framework

[<TestFixture>]
/// Unit tests for Fileset module
type FilesetTests() =

  let currentDir = Path.Combine (__SOURCE_DIRECTORY__, "..")
  let mutable rememberDir = ""

  [<TestFixtureSetUp>]
  member o.Setup() =
    rememberDir <- Directory.GetCurrentDirectory()
    Directory.SetCurrentDirectory currentDir
  [<TestFixtureTearDown>]
  member o.Teardown() =
    Directory.SetCurrentDirectory rememberDir

  [<Test>]
  member o.LsSimple() =
    let (FileList files) = ls "*.sln"
    let PathName (file:FileInfo) = file.Name
    Assert.That (List.map PathName files, Is.EquivalentTo (List.toSeq ["xake.sln"]))

  [<Test>]
  member o.LsMore() =
    let (FileList files) = ls "c:/!/**/*.c*"
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
        !! "*.rdl" ++ "*.rdlx" ++ "../jparsec/src/main/**/A*.java"
        <<< @"c:\!\bak"

    let (FileList files) = exec fileset
    let PathName (file:FileInfo) = file.FullName
    files |> List.map PathName |> List.iter System.Console.WriteLine

  [<Test>]
  [<ExpectedException>]
  member o.CombineFilesets() =
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

  [<TestCase(2, ExpectedResult = 4)>]
  [<TestCase(3, ExpectedResult = 9)>]
  member o.Test2 (a:int)  =
    a*a
