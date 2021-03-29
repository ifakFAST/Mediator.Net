$ErrorActionPreference = "Stop"

$dirJson = "D:\Sonstiges\NoBackup\Newtonsoft.Json_13.0.1"
$dirJsonSrc = Join-Path -Path $dirJson -ChildPath "Src/Newtonsoft.Json"

$baseDir  = $PSScriptRoot
$dirDst = [System.IO.Path]::GetFullPath((Join-Path -Path $baseDir -ChildPath "../../Mediator.Net/MediatorLib/Json"))


if (!(Test-Path $dirJson)) {
  "Source folder $dirJson does not exist!"
  Read-Host "Press any key to exit"
  exit
}


$confirm = Read-Host "Delete folder $dirDst and copy files?"

if (($confirm -ne "y") -and ($confirm -ne "yes")) {
  exit
}

Remove-Item $dirDst -Recurse -Force -ErrorAction Ignore

robocopy $dirJsonSrc $dirDst "/S"

Remove-Item -Recurse (Join-Path -Path $dirDst -ChildPath "bin") -ErrorAction Ignore
Remove-Item -Recurse (Join-Path -Path $dirDst -ChildPath "obj") -ErrorAction Ignore
Remove-Item -Recurse (Join-Path -Path $dirDst -ChildPath "Properties")
Remove-Item (Join-Path -Path $dirDst -ChildPath "Dynamic.snk")
Remove-Item (Join-Path -Path $dirDst -ChildPath "Newtonsoft.Json.csproj")
Remove-Item (Join-Path -Path $dirDst -ChildPath "Newtonsoft.Json.ruleset")

Get-ChildItem -Path $dirDst -Recurse |
ForEach-Object {
  if (Test-Path -Path $_.FullName -PathType leaf) {
    $_.FullName
    $lines = Get-Content $_.FullName
    $lines = $lines | ForEach-Object {  $_.Replace(' Newtonsoft.Json', ' Ifak.Fast.Json').Replace('"Newtonsoft.Json', '"Ifak.Fast.Json') } 
    $linesStr = $lines -join "`r`n" 
    $linesStr | Set-Content -Encoding UTF8 -NoNewline -Path $_.FullName
  }
}

Read-Host "Press any key to continue"