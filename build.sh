#!/bin/bash
mono .paket/paket.bootstrapper.exe
mono .paket/paket.exe install
fsharpi build.fsx $*