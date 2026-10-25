FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Clinic.Queue.Service/Clinic.Queue.Service.csproj", "Clinic.Queue.Service/"]
COPY ["Clinic.Contracts/Clinic.Contracts.csproj", "Clinic.Contracts/"]
RUN dotnet restore "Clinic.Queue.Service/Clinic.Queue.Service.csproj"
COPY . .
WORKDIR /src/Clinic.Queue.Service
RUN dotnet publish "Clinic.Queue.Service.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Clinic.Queue.Service.dll"]

