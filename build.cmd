@echo off
dotnet restore build.proj
dotnet fake run build.fsx -- build