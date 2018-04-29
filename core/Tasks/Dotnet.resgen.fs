namespace Xake.Tasks.Dotnet

[<AutoOpen>]
module ResgenImpl =

    open System.IO
    open System.Resources
    open Xake

    // ResGen task and its settings
    type ResgenSettingsType = {

        Resources: ResourceFileset list
        TargetDir: DirectoryInfo
        UseSourcePath: bool

        // TODO single file mode
        // TODO extra command-line args
    }

    let ResgenSettings = {
        Resources = [Empty]
        TargetDir = DirectoryInfo "."
        UseSourcePath = true
    }

    /// Generates binary resource files from resx, txt etc
    let ResGen (settings:ResgenSettingsType) =

        // TODO rewrite everything, it's just demo code
        let resgen baseDir (options:ResourceSetOptions) (resxfile:string) =
            let rcfile =
                Path.Combine(
                    settings.TargetDir.FullName,
                    Path.ChangeExtension(resxfile, ".resource") |> Impl.makeResourceName options baseDir)

            use writer = new ResourceWriter (rcfile)

#if NET46
            use resxreader = new ResXResourceReader (resxfile)

            if settings.UseSourcePath then
                resxreader.BasePath <- Path.GetDirectoryName (resxfile)

            let reader = resxreader.GetEnumerator()
            while reader.MoveNext() do
                writer.AddResource (reader.Key :?> string, reader.Value)
#endif
            rcfile

        recipe {
            for r in settings.Resources do
                let (ResourceFileset (settings,fileset)) = r
                let (Fileset (options,_)) = fileset
                let! (Filelist files) = getFiles fileset

                do files |> List.map (File.getFullName >> resgen options.BaseDir settings) |> ignore
            ()
        }

