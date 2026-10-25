FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Clinic.Scheduling.Service/Clinic.Scheduling.Service.csproj", "Clinic.Scheduling.Service/"]
COPY ["Clinic.Contracts/Clinic.Contracts.csproj", "Clinic.Contracts/"]
RUN dotnet restore "Clinic.Scheduling.Service/Clinic.Scheduling.Service.csproj"
COPY . .
WORKDIR /src/Clinic.Scheduling.Service
RUN dotnet publish "Clinic.Scheduling.Service.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Clinic.Scheduling.Service.dll"]

