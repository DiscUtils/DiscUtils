$version = "0.8.0.0"

foreach ($file in Get-ChildItem .\* -include AssemblyInfo.cs -recurse)
{
  $lines = Get-Content $file;
  $lines | Foreach-Object { $_ -replace "Version\(.*\)", "Version(""$version"")"} | Set-Content $file;
}
