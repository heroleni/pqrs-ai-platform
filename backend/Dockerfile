FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/PqrsPlatform.sln .
COPY backend/src/PqrsPlatform.Domain/PqrsPlatform.Domain.csproj src/PqrsPlatform.Domain/
COPY backend/src/PqrsPlatform.Application/PqrsPlatform.Application.csproj src/PqrsPlatform.Application/
COPY backend/src/PqrsPlatform.Infrastructure/PqrsPlatform.Infrastructure.csproj src/PqrsPlatform.Infrastructure/
COPY backend/src/PqrsPlatform.Api/PqrsPlatform.Api.csproj src/PqrsPlatform.Api/

RUN dotnet restore src/PqrsPlatform.Api/PqrsPlatform.Api.csproj

COPY backend/src/ src/
RUN dotnet publish src/PqrsPlatform.Api/PqrsPlatform.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

COPY widget/ /app/wwwroot/

EXPOSE 8080
ENTRYPOINT ["dotnet", "PqrsPlatform.Api.dll"]
