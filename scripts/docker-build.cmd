@echo off

SET configuration=Debug
SET dotnetVersion=net7.0
SET installDebugClr="cd ../ && apt-get update && apt-get install -y --no-install-recommends unzip && apt-get install -y curl && rm -rf var/lib/apt/lists/* && curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg"
SET coreProject=HelpDesk.Core
SET hostProject=HelpDesk.Host

dotnet publish ../src/%coreProject%/%coreProject%.csproj -c %configuration%
dotnet publish ../src/%hostProject%/%hostProject%.csproj -c %configuration%

docker build ../src/%coreProject%/bin/%configuration%/%dotnetVersion%/publish -t help.desk/core:debug -f ../src/%coreProject%/Dockerfile --build-arg INSTALL_CLRDBG=%installDebugClr%
docker build ../src/%hostProject%/bin/%configuration%/%dotnetVersion%/publish -t help.desk/host:debug -f ../src/%hostProject%/Dockerfile --build-arg INSTALL_CLRDBG=%installDebugClr%