@echo off
.paket/paket.bootstrapper.exe
.paket/paket.exe install

fsi build.fsx