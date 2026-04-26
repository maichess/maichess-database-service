FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

ARG GITHUB_ACTOR

COPY nuget.config ./
COPY MaichessDatabaseService.csproj ./
RUN --mount=type=secret,id=GITHUB_TOKEN \
    GITHUB_TOKEN=$(cat /run/secrets/GITHUB_TOKEN) \
    dotnet restore MaichessDatabaseService.csproj

COPY . .
RUN dotnet publish MaichessDatabaseService.csproj \
    -c Release -o /app/publish --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MaichessDatabaseService.dll"]
