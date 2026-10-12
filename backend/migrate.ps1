param(
  [Parameter(Mandatory=$true)]
  [string]$Message
)

dotnet ef migrations add $Message --project Clinic.Infrastructure.Sqlserver --startup-project Clinic.API --context ApplicationDbContext