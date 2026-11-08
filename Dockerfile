FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GeometryDash.Api/GeometryDash.Api.csproj GeometryDash.Api/
RUN dotnet restore GeometryDash.Api/GeometryDash.Api.csproj

COPY GeometryDash.Api/ GeometryDash.Api/
RUN dotnet publish GeometryDash.Api/GeometryDash.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 10000

CMD ["sh", "-c", "dotnet GeometryDash.Api.dll --migrate && exec dotnet GeometryDash.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]