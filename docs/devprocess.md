# Develop/release routines

Describes some routines, mostly notes for myself.

## Developing a new feature (repo owner)

* create feature branch (local)
* push to server
* create PR to Xake/dev branch
* wait for checks, in case of failure push fixes to the same branch
* merge with squash to dev

## Contributing new feature

* clone repo
* create feature branch from origin/dev head)
* push branch to server
* create PR to Xake/dev branch
* wait for checks, in case of failure push fixes to the same branch

## Release

* merge dev to master
* tag the version with `v` prefix

```cmd
git checkout master
git tag -v v1.0
git push --tags
```

## Tests

Running tests on netstandard target:

```
dotnet test -f:netcoreapp2.0
```

Example of selective test run:

```
dotnet test -f:net46 --filter Name~"Rm deletes"
dotnet fake run build.fsx -- test -d FILTER=Rm
```

## Publishing

The commands below assume you've defined `NUGET_KEY` in environment variables.

```
dotnet fake run build.fsx -- pack -d VER=1.2.3
dotnet fake run build.fsx -- push -d VER=1.2.3
```

> Define `SUFFIX` variable set to empty for final releases. Otherwise it defaults to `-alpha`

### Not using build.fsx

Here're the commands issues by a build script.
> Do not use for publishing. This is just for reference.

```
dotnet pack -c Release /p:Version=1.2.3-alpha4
dotnet nuget push out\Xake.1.0.6.344-alpha.nupkg --source https://www.nuget.org/api/v2/package --api-key %NUGET_KEY%

```