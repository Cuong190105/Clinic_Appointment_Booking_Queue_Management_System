FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Clinic.Identity.Service/Clinic.Identity.Service.csproj", "Clinic.Identity.Service/"]
RUN dotnet restore "Clinic.Identity.Service/Clinic.Identity.Service.csproj"
COPY . .
WORKDIR /src/Clinic.Identity.Service
RUN dotnet publish "Clinic.Identity.Service.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /var/lib/clinic/identity-keys && chown -R $APP_UID:$APP_UID /app /var/lib/clinic
USER $APP_UID
ENTRYPOINT ["dotnet", "Clinic.Identity.Service.dll"]
