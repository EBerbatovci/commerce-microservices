FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY ["Directory.Build.props", "."]
COPY ["src/Catalog.API/Catalog.API.csproj", "src/Catalog.API/"]
RUN dotnet restore "src/Catalog.API/Catalog.API.csproj"
COPY . .
RUN dotnet publish "src/Catalog.API/Catalog.API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Catalog.API.dll"]
