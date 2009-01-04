$old = "2008"
$new = "2008-2009"

foreach ($file in Get-ChildItem .\* -include *.cs -recurse)
{
  $lines = Get-Content $file;
  $lines | Foreach-Object { $_ -replace $old, $new } | Set-Content $file;
}


$lines = Get-Content "Library.shfb"
$lines | Foreach-Object { $_ -replace $old, $new} | Set-Content "Library.shfb"
