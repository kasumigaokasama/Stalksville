# Build context: apps/api (solution root — project references reach sibling projects).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Stalksville.Worker/Stalksville.Worker.csproj -c Release -o /app/publish

# The worker references Infrastructure, which needs the ASP.NET framework reference,
# so it runs on the aspnet runtime image too.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Stalksville.Worker.dll"]
