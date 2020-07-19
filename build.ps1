param (
    [Parameter(Mandatory=$true)][int]$BuildNumber,
    [Parameter(Mandatory=$false)][string]$CommitSHA
)

$basePath = Pwd
$csProjPath = Join-Path $basePath GMeet.csproj
$buildPath = Join-Path $basePath bin/build

[xml]$xmlDoc = Get-Content $csProjPath
$versionElement = $xmlDoc['Project']['PropertyGroup']['Version']
$version = [version]$versionElement.InnerText
$newVersion = "$($version.Major).$($version.Minor).$($BuildNumber)"

if ($CommitSHA) {
    $newVersion = "$($newVersion)+$($CommitSHA.SubString(0, 7))"
}

$versionElement.InnerText = $newVersion
$xmlDoc.Save($csProjPath)

if (Test-Path $buildPath -PathType Container) {
    rm -rf $buildPath
}

$uid = sh -c 'id -u'
$gid = sh -c 'id -g'

docker run --rm -v "$($basePath):/var/src" mcr.microsoft.com/dotnet/core/sdk:3.1.302-alpine3.12 ash -c "dotnet publish -c Release /var/src/GMeet.csproj -o /var/src/bin/build && chown -R $($uid):$($gid) /var/src"

$nuspecPath = Join-Path $buildPath gmeet.nuspec
$nupkgPath = Join-Path $buildPath "gmeet.$($newVersion).nupkg"
cp gmeet.nuspec $nuspecPath

[xml]$xmlDoc = Get-Content $nuspecPath
$xmlDoc['package']['metadata']['version'].InnerText = $newVersion
$xmlDoc.Save($nuspecPath)

Compress-Archive -Path "$($buildPath)/*" -DestinationPath $nupkgPath

Write-Host "::set-env name=VERSION::$newVersion"
Write-Host "::set-env name=PKG_PATH::$nupkgPath"
