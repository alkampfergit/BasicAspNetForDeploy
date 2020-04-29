.\PreBuild.ps1 -configuration release

Write-Host "Restoring nuget packages with nuget commandline"
$nugetLocation = Get-NugetLocation
set-alias nuget $nugetLocation 
nuget restore .\src

Write-Host "Executing a build of solution with msbuild"
$msbuildLocation = Get-LatestMsbuildLocation
set-alias msb $msbuildLocation 
msb .\src\TestWebApp.sln /p:Configuration=release

Write-Host "executing publishing of web site with msbuild with version"
msb .\src\TestWebApp\TestWebApp.csproj /p:DeployOnBuild=true /p:WebPublishMethod=Package /p:PackageAsSingleFile=true /p:OutDir=".\release" /p:Configuration=release

Write-Host "Running nunit tests with console runner"
$nunitConsoleRunner = GEt-NunitTestsConsoleRunner
set-alias nunit "$nunitConsoleRunner"

nunit .\src\TestWebApp.Tests\Bin\Release\TestWebApp.Tests.dll

.\PostBuild.ps1 -webSiteOutDir release -destinationDirectory .\artifacts -buildConfiguration release