open System.IO
open System.Resources
open System.Collections

let resgen (resxfile:string) =

    use resxreader = new ResXResourceReader (resxfile)
    resxreader.BasePath <- Path.GetDirectoryName (resxfile)

    let rcfile = Path.ChangeExtension(resxfile, "resources")
    use writer = new ResourceWriter (rcfile)

    let reader = resxreader.GetEnumerator()
    while reader.MoveNext() do
        printfn "%A: %A" reader.Key reader.Value
        writer.AddResource (reader.Key :?> string, reader.Value)

    writer.Close()

resgen @"C:\projects\AR9\Source\WinViewer\Resources.resx"
resgen @"C:\projects\AR9\Source\WinViewer\ViewerForm.resx"

let getRelative (root:string) (path:string) =
    if System.String.IsNullOrEmpty(root) then path
    elif path.ToLowerInvariant().StartsWith (root.ToLowerInvariant()) then
        let d = if root.[root.Length - 1] = Path.DirectorySeparatorChar then 0 else 1
        path.Substring(root.Length + d)
    else
        path

let basedir = @"C:\projects\AR9\Source\HtmlExport"
for file in Directory.EnumerateFiles (basedir, "*.resx", SearchOption.AllDirectories) do

    let path = getRelative basedir file
    let resfilePath = path.Replace(Path.DirectorySeparatorChar, '.')
    printfn "%A: %A" file resfilePath
