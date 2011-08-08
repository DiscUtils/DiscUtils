$old = "2008-2010"
$new = "2008-2011"

foreach ($file in Get-ChildItem .\* -include *.cs -recurse)
{
  $lines = Get-Content $file;
  $lines | Foreach-Object { $_ -replace $old, $new } | Set-Content $file;
}


$lines = Get-Content "Help\Library.shfbproj"
$lines | Foreach-Object { $_ -replace $old, $new} | Set-Content "Help\Library.shfbproj"
