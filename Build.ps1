<#
.SYNOPSIS
Build the entire project
#>
param (
    [string] $webSiteOutDir = "release",
    [string] $destinationDirectory = "artifacts",
    [string] $buildConfiguration = "release",
    [bool] $skipTests = $false
)

.\PreBuild.ps1 -configuration $buildConfiguration

.\CompileAndTest.ps1 -configuration $buildConfiguration -skipTests $skipTests

.\PostBuild.ps1 -webSiteOutDir $webSiteOutDir -destinationDirectory $destinationDirectory -buildConfiguration $buildConfiguration