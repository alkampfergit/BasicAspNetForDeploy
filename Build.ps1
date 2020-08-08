<#
.SYNOPSIS
Build the entire project
#>
param (
    [string] $webSiteOutDir = "release",
    [string] $destinationDirectory = "artifacts",
    [string] $buildConfiguration = "release"
)

.\PreBuild.ps1 -configuration $buildConfiguration

Write-Host "Restoring nuget packages with nuget commandline"
$nugetLocation = Get-NugetLocation
set-alias nuget $nugetLocation 
nuget restore .\src

Write-Host "Executing a build of solution with msbuild"
$msbuildLocation = Get-LatestMsbuildLocation
set-alias msb $msbuildLocation 
msb .\src\TestWebApp.sln /p:Configuration=release

Write-Host "executing publishing of web site with msbuild with version"
msb .\src\TestWebApp\TestWebApp.csproj /p:DeployOnBuild=true /p:WebPublishMethod=Package /p:PackageAsSingleFile=true /p:OutDir=".\$webSiteOutDir" /p:Configuration=$buildConfiguration

Write-Host "Running nunit tests with console runner"
$nunitConsoleRunner = GEt-NunitTestsConsoleRunner
set-alias nunit "$nunitConsoleRunner"

nunit ".\src\TestWebApp.Tests\Bin\$webSiteOutDir\TestWebApp.Tests.dll"

.\PostBuild.ps1 -webSiteOutDir $webSiteOutDir -destinationDirectory .\artifacts -buildConfiguration $buildConfiguration