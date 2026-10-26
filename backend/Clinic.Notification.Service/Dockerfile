FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Clinic.Notification.Service/Clinic.Notification.Service.csproj", "Clinic.Notification.Service/"]
COPY ["Clinic.Contracts/Clinic.Contracts.csproj", "Clinic.Contracts/"]
RUN dotnet restore "Clinic.Notification.Service/Clinic.Notification.Service.csproj"
COPY . .
WORKDIR /src/Clinic.Notification.Service
RUN dotnet publish "Clinic.Notification.Service.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Clinic.Notification.Service.dll"]
