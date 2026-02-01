FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["MatchMaking.Service.sln", "./"]
COPY ["src/MatchMaking.Service/MatchMaking.Service.csproj", "src/MatchMaking.Service/"]
COPY ["src/MatchMaking.Shared/MatchMaking.Shared.csproj", "src/MatchMaking.Shared/"]

RUN dotnet restore "MatchMaking.Service.sln"

COPY . .

WORKDIR "/src/src/MatchMaking.Service"
RUN dotnet build "MatchMaking.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MatchMaking.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MatchMaking.Service.dll"]
