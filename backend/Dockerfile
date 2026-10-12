# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Clinic.API/Clinic.API.csproj", "Clinic.API/"]
COPY ["Clinic.Application/Clinic.Application.csproj", "Clinic.Application/"]
COPY ["Clinic.Domain/Clinic.Domain.csproj", "Clinic.Domain/"]
COPY ["Clinic.Infrastructure.Sqlserver/Clinic.Infrastructure.Sqlserver.csproj", "Clinic.Infrastructure.Sqlserver/"]
RUN dotnet restore "Clinic.API/Clinic.API.csproj"

COPY . .
WORKDIR "/src/Clinic.API"
RUN dotnet publish "Clinic.API.csproj" -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Clinic.API.dll"]
