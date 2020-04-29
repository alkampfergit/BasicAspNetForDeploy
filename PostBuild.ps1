<#
.SYNOPSIS
Simply execute some package action to prepare all artifacts that 
will be included as result of the build and ready to be released.
#>
param (
    [string] $webSiteOutDir = "release",
    [string] $destinationDirectory = "artifacts",
    [string] $buildConfiguration = "release"
)

Import-Module BuildUtils

$runningDirectory = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition

Write-Host 'Modifying web.config'
###----Manipulation of configuration file
$testWebAppPublishedLocation = "$runningDirectory\src\TestWebApp\$webSiteOutDir\_publishedWebSites\TestWebApp"
$configFile = "$testWebAppPublishedLocation\web.config"
$xml = [xml](Get-Content $configFile)
Edit-XmlNodes $xml -xpath "/configuration/appSettings/add[@key='Key1']/@value" -value "__SAMPPLEASPNET_KEY1__"
Edit-XmlNodes $xml -xpath "/configuration/appSettings/add[@key='Key2']/@value" -value "__SAMPPLEASPNET_KEY2__"
Edit-XmlNodes $xml -xpath "/configuration/connectionStrings/add[@name='db']/@connectionString" -value "__SAMPPLEASPNET_CONNECTION__"

$xml.save($configFile)

$webPublishedDirectory = "$runningDirectory\src\TestWebApp\$webSiteOutDir\_publishedWebSites"

$sevenZipExe = Get-7ZipLocation
set-alias sz $sevenZipExe 

if (![System.IO.Path]::IsPathRooted($destinationDirectory)) {
  Write-Host "Path is not rooted, prepended script location for web archive"
  $webSiteZippedFile = "$runningDirectory\$destinationDirectory\website.7z"
}
else {
  Write-Host "Path is rooted using destination directory for web archive"
  $webSiteZippedFile = "$destinationDirectory\website.7z"
}

Write-Output "Archiving WEB SITE located at $webPublishedDirectory in file $webSiteZippedFile"
sz a -mx=9 -r -mmt=on $webSiteZippedFile $webPublishedDirectory

if (![System.IO.Path]::IsPathRooted($destinationDirectory)) {
  Write-Host "Path is not rooted, prepended script location for sql project file"
  $dbProjectSiteZippedFile = "$runningDirectory\$destinationDirectory\sqldatabase.7z"
}
else {
  Write-Host "Path is rooted using destination directory for sql project file"
  $dbProjectSiteZippedFile = "$destinationDirectory\sqldatabase.7z"
}

$sqlProjectOutputLocation = "$runningDirectory\src\TestWebApp.SqlDatabase\bin\$buildConfiguration"
Write-Output "Archiving DATABASE PROJECT OUTPUT located at $sqlProjectOutputLocation in file $dbProjectSiteZippedFile"
sz a -mx=9 -r -mmt=on $dbProjectSiteZippedFile $sqlProjectOutputLocation

