#!/bin/bash
# Build script for BiketaBai project
# Usage: ./build.sh

cd "$(dirname "$0")"
dotnet build BiketaBai.csproj

