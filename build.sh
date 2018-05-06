#!/bin/bash
dotnet restore build.proj
dotnet fake run build.fsx -- build