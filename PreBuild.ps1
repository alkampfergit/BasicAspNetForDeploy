param (
    [string] $configuration = "release"
)

Install-package VsSetup -Confirm:$false -Scope CurrentUser -Force
Import-Module Vssetup

Install-package BuildUtils -Confirm:$false -Scope CurrentUser -Force
Import-Module BuildUtils

$runningDirectory = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition

$gitVersion = Invoke-Gitversion

Write-Host "Assembly version is: $($gitVersion.assemblyVersion)"
Write-Host "File version is: $($gitVersion.assemblyFileVersion)"
Write-Host "Nuget version is: $($gitVersion.nugetVersion)"
Write-Host "Informational version is: $($gitVersion.assemblyInformationalVersion)"
Write-Host "Full Semver is: $($gitVersion.fullSemver)"

$buildId = $env:BUILD_BUILDID
Write-Host "Build id variable is $buildId"
if (![System.String]::IsNullOrEmpty($buildId)) 
{
  Write-Host "Running in an Azure Devops Build"

  Write-Host "##vso[build.updatebuildnumber]BasicAspNetForDeploy - $($gitVersion.fullSemver)"
  Write-Host "##vso[task.setvariable variable=nugetVersion;]$($gitVersion.nugetVersion)"

  # if ($dumpVariables) 
  # {
  #     Write-Host "Dumping all environment variable of the build"
  #     $var = (gci env:*).GetEnumerator() | Sort-Object Name
  #     $out = ""
  #     Foreach ($v in $var) {$out = $out + "`t{0,-28} = {1,-28}`n" -f $v.Name, $v.Value}

  #     write-output "dump variables on $outFolder\EnvVar.md"
  #     $fileName = "$outFolder\EnvVar.md"
  #     set-content $fileName $out
  #     write-output "##vso[task.addattachment type=Distributedtask.Core.Summary;name=Environment Variables;]$fileName"
  # }

  Write-Output "Modifying all assemblyinfo files to add versioning on $sourceFolder"
  Update-SourceVersion -SrcPath "$runningDirectory/src" `
    -assemblyVersion $gitVersion.assemblyVersion `
    -fileAssemblyVersion $gitVersion.assemblyFileVersion `
    -assemblyInformationalVersion $gitVersion.assemblyInformationalVersion
}