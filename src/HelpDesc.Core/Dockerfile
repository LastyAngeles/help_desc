FROM mcr.microsoft.com/dotnet/runtime:7.0
ARG INSTALL_CLRDBG=exit
RUN bash -c "${INSTALL_CLRDBG}"

WORKDIR /app
COPY . /app
ENTRYPOINT ["dotnet", "HelpDesc.Core.dll"]