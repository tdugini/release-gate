FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ReleaseGate.slnx ./
COPY apps/api/ReleaseGate.Api/ReleaseGate.Api.csproj apps/api/ReleaseGate.Api/
RUN dotnet restore apps/api/ReleaseGate.Api/ReleaseGate.Api.csproj

COPY apps/api/ReleaseGate.Api/ apps/api/ReleaseGate.Api/
RUN dotnet publish apps/api/ReleaseGate.Api/ReleaseGate.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER app
ENTRYPOINT ["dotnet", "ReleaseGate.Api.dll"]
