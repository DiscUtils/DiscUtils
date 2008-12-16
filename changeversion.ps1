$version = "0.2.0.0"

foreach ($file in Get-ChildItem .\* -include AssemblyInfo.cs -recurse)
{
  $lines = Get-Content $file;
  $lines | Foreach-Object { $_ -replace "Version\(.*\)", "Version(""$version"")"} | Set-Content $file;
}


$lines = Get-Content "Library.shfb"
$lines | Foreach-Object { $_ -replace "<HelpFileVersion>.*</HelpFileVersion>", "<HelpFileVersion>$version</HelpFileVersion>"} | Set-Content "Library.shfb"
