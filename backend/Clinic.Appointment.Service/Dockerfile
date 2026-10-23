FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Clinic.Appointment.Service/Clinic.Appointment.Service.csproj", "Clinic.Appointment.Service/"]
COPY ["Clinic.Contracts/Clinic.Contracts.csproj", "Clinic.Contracts/"]
RUN dotnet restore "Clinic.Appointment.Service/Clinic.Appointment.Service.csproj"
COPY . .
WORKDIR /src/Clinic.Appointment.Service
RUN dotnet publish "Clinic.Appointment.Service.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Clinic.Appointment.Service.dll"]

