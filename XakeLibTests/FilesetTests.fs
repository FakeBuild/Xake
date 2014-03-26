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
    let (Files files) = ls "*.sln"
    let PathName (file:FileInfo) = file.Name
    Assert.That (List.map PathName files, Is.EquivalentTo (List.toSeq ["xake.sln"]))

  [<Test>]
  member o.LsMore() =
    let (Files files) = ls "c:\\!\\**\\*.c*"
    let PathName (file:FileInfo) = file.FullName
    files |> List.map PathName |> List.iter System.Console.WriteLine

  [<TestCase(2, ExpectedResult = 4)>]
  [<TestCase(3, ExpectedResult = 9)>]
  member o.Test2 (a:int)  =
    a*a
