if (Test-Path test-results) { Remove-Item -Recurse -Force test-results }
$projects = @(
    "tests/GameServer.API.Tests/GameServer.API.Tests.csproj",
    "tests/GameServer.Docker.Agent.Tests/GameServer.Docker.Agent.Tests.csproj",
    "tests/GameServer.Web.Tests/GameServer.Web.Tests.csproj",
    "tests/GameServer.API.Client.Tests/GameServer.API.Client.Tests.csproj",
    "tests/GameServer.Integration.Tests/GameServer.Integration.Tests.csproj"
)
foreach ($proj in $projects) {
    $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
    Write-Host "Running tests for $projName..."
    dotnet test $proj -- --coverage --coverage-output-format cobertura --results-directory "./test-results/$projName"
}
reportgenerator -reports:"./test-results/**/*.cobertura.xml" -targetdir:"./coverage-report" -reporttypes:"TextSummary;Html" "-filefilters:-*Migrations*;-*.g.cs;-*Snapshot*;-*OpenApi*;-*Program.cs"
