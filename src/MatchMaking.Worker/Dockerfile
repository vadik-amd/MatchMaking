FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["MatchMaking.Worker.sln", "./"]
COPY ["src/MatchMaking.Worker/MatchMaking.Worker.csproj", "src/MatchMaking.Worker/"]
COPY ["src/MatchMaking.Shared/MatchMaking.Shared.csproj", "src/MatchMaking.Shared/"]

RUN dotnet restore "MatchMaking.Worker.sln"

COPY . .

WORKDIR "/src/src/MatchMaking.Worker"
RUN dotnet build "MatchMaking.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MatchMaking.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MatchMaking.Worker.dll"]
