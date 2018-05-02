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

## Publishing

```
dotnet pack -c Release /p:PackageVersion=1.2.3 --version-suffix alpha4
paket push --url https://www.nuget.org/api/v2/package <package_name> --api-key <nuget_key>

```