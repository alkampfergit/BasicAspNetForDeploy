<#
.SYNOPSIS
Execute build (nuget restore plus build) and then runs
nunit tests.
#>
param (
    [string] $buildConfiguration = "release",
    [bool] $skipTests = $false
)

$runningDirectory = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$srcDirectory = "$runningDirectory\src"
$slnName = "$runningDirectory\src\TestWebApp.sln" 
$testProject = "$runningDirectory\src\TestWebApp\TestWebApp.csproj" 

Write-Host "Restoring nuget packages with nuget commandline"
$nugetLocation = Get-NugetLocation
set-alias nuget $nugetLocation 
nuget restore $srcDirectory

Write-Host "Executing a build of solution with msbuild"
$msbuildLocation = Get-LatestMsbuildLocation
set-alias msb $msbuildLocation 
msb $slnName /p:Configuration=$buildConfiguration

if ($false -eq $?)
{
  Write-Error "Msbuild exit code indicate build failure."
  Write-Host "##vso[task.logissue type=error]Msbuild exit code indicate build failure."
  exit(1)
}

Write-Host "executing publishing of web site with msbuild with version"
msb $testProject /p:DeployOnBuild=true /p:WebPublishMethod=Package /p:PackageAsSingleFile=true /p:OutDir=".\$webSiteOutDir" /p:Configuration=$buildConfiguration

if ($false -eq $?)
{
  Write-Error "Msbuild exit code indicate build failure."
  Write-Host "##vso[task.logissue type=error]Msbuild exit code indicate failure during website publish."
  exit(1)
}

if ($false -eq $skipTests) 
{
  Write-Host "Running nunit tests with console runner"
  $nunitConsoleRunner = Get-NunitTestsConsoleRunner
  Write-Host "Get-NunitTestsConsoleRunner Found nunit console runner at: $nunitConsoleRunner"
  set-alias nunit $nunitConsoleRunner

  if (![System.String]::IsNullOrEmpty($buildId)) 
  {
      Write-Host "running nunit inside a Azure DevOps build: nunit "$runningDirectory\src\TestWebApp.Tests\Bin\$buildConfiguration\TestWebApp.Tests.dll" /out:TestResult.xml"
      nunit "$runningDirectory\src\TestWebApp.Tests\Bin\$buildConfiguration\TestWebApp.Tests.dll" /out:TestResult.xml
  }
  else 
  {
      Write-Host "running nunit: $nunitConsoleRunner $runningDirectory\src\TestWebApp.Tests\Bin\$buildConfiguration\TestWebApp.Tests.dll"
      nunit "$runningDirectory\src\TestWebApp.Tests\Bin\$buildConfiguration\TestWebApp.Tests.dll"
  }

  if ($false -eq $?)
  {
    Write-Error "Nunit runner exit code indicate test failure."
    Write-Host "##vso[task.logissue type=error]Nunit runner exit code indicate test failure."
    exit(2)
  }
}
