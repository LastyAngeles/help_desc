FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
ARG INSTALL_CLRDBG=exit
RUN bash -c "${INSTALL_CLRDBG}"
    
ENV ASPNETCORE_URLS http://*:5000
EXPOSE 5000

COPY . /app

ENTRYPOINT ["dotnet", "HelpDesc.Host.dll"]