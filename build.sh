#!/bin/bash
# Handle to many files on osx
if [ "$TRAVIS_OS_NAME" == "osx" ] || [ `uname` == "Darwin" ]; then
  ulimit -n 4096
fi

if [ "$TRAVIS_OS_NAME" == "osx" ] || [ `uname` == "Darwin" ]; then
  export OMNISHARP_PACKAGE_OSNAME=osx-x64
else
  export OMNISHARP_PACKAGE_OSNAME=linux-x64
fi

export FrameworkPathOverride=/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5

dotnet restore OmniSharp.sln
dotnet build   OmniSharp.sln
# bash ./scripts/cake-bootstrap.sh "$@"

# cd src/OmniSharp.DotNet.ProjectModel/
# dotnet add OmniSharp.DotNet.ProjectModel.csproj package System.Runtime
