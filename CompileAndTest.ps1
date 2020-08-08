<#
.SYNOPSIS
Execute build (nuget restore plus build) and then runs
nunit tests.
#>
param (
    [string] $buildConfiguration = "release"
)

Write-Host "Restoring nuget packages with nuget commandline"
$nugetLocation = Get-NugetLocation
set-alias nuget $nugetLocation 
nuget restore .\src

Write-Host "Executing a build of solution with msbuild"
$msbuildLocation = Get-LatestMsbuildLocation
set-alias msb $msbuildLocation 
msb .\src\TestWebApp.sln /p:Configuration=release

if ($false -eq $?)
{
  Write-Error "Msbuild exit code indicate build failure."
  Write-Host "##vso[task.logissue type=error]Msbuild exit code indicate build failure."
  exit(1)
}

Write-Host "executing publishing of web site with msbuild with version"
msb .\src\TestWebApp\TestWebApp.csproj /p:DeployOnBuild=true /p:WebPublishMethod=Package /p:PackageAsSingleFile=true /p:OutDir=".\$webSiteOutDir" /p:Configuration=$buildConfiguration

if ($false -eq $?)
{
  Write-Error "Msbuild exit code indicate build failure."
  Write-Host "##vso[task.logissue type=error]Msbuild exit code indicate failure during website publish."
  exit(1)
}

Write-Host "Running nunit tests with console runner"
$nunitConsoleRunner = GEt-NunitTestsConsoleRunner
set-alias nunit "$nunitConsoleRunner"

if (![System.String]::IsNullOrEmpty($buildId)) 
{
    Write-Host "running nunit inside a Azure DevOps build"
    nunit ".\src\TestWebApp.Tests\Bin\$buildConfiguration\TestWebApp.Tests.dll" /out:TestResult.xml
}
else 
{
    Write-Host "running nunit"
    nunit ".\src\TestWebApp.Tests\Bin\$buildConfiguration\TestWebApp.Tests.dll"
}


if ($false -eq $?)
{
  Write-Error "Nunit runner exit code indicate test failure."
  Write-Host "##vso[task.logissue type=error]Nunit runner exit code indicate test failure."
  exit(2)
}