param(
  [Parameter(Mandatory=$true)]
  [string]$TestClass
)

dotnet test --filter "FullyQualifiedName~$TestClass"