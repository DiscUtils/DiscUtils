. .\common.ps1

if(-not (Test-Path $signingkey))
{
  Write-Host "Signing key missing"
  Exit
}

# Set revision info in version.cs
$now = get-date
$rev = & ${hg} id -i
$filebuild = (new-timespan -start (get-date -year 2010 -month 1 -day 1) -end $now).Days
$filerev = (($now.Hour * 60 + $now.Minute) * 60 + $now.Second) / 2
$lines = Get-Content "${basedir}\Version.cs"
$lines = $lines | Foreach-Object { $_ -replace "AssemblyDescription\(.*\)", "AssemblyDescription(""Revision: $rev"")" }
$lines = $lines | Foreach-Object { $_ -replace "AssemblyFileVersion\(.*\)", "AssemblyFileVersion(""${ver}.${filebuild}.${filerev}"")" }
$lines = $lines | Foreach-Object { $_ -replace "AssemblyVersion\(.*\)", "AssemblyVersion(""${fullver}"")" }
$lines | Set-Content "${basedir}\Version.cs";

# Clean up
del ${basedir}\help -recurse -force | out-null
& ${vcsexpress} "${basedir}\DiscUtils.sln" /clean Debug | out-null
& ${vcsexpress} "${basedir}\DiscUtils.sln" /clean Release | out-null

# Enable code signing
$lines = Get-Content "${basedir}\src\DiscUtils.csproj";
$lines | Foreach-Object { $_ -replace "<SignAssembly>.*</SignAssembly>", "<SignAssembly>true</SignAssembly>"} | Set-Content "${basedir}\src\DiscUtils.csproj";


# Compile
& ${vcsexpress} "${basedir}\DiscUtils.sln" /build Release | out-null
& ${vcsexpress} "${basedir}\DiscUtils.sln" /build Debug | out-null


# Disable code signing again
$lines = Get-Content "${basedir}\src\DiscUtils.csproj";
$lines | Foreach-Object { $_ -replace "<SignAssembly>.*</SignAssembly>", "<SignAssembly>false</SignAssembly>"} | Set-Content "${basedir}\src\DiscUtils.csproj";

# Restore Version.cs
$lines = Get-Content "${basedir}\Version.cs"
$lines = $lines | Foreach-Object { $_ -replace "AssemblyDescription\(.*\)", "AssemblyDescription(""Private Build"")" }
$lines = $lines | Foreach-Object { $_ -replace "AssemblyFileVersion\(.*\)", "AssemblyFileVersion(""${fullver}"")" }
$lines = $lines | Foreach-Object { $_ -replace "AssemblyVersion\(.*\)", "AssemblyVersion(""${fullver}"")" }
$lines | Set-Content "${basedir}\Version.cs";

# Generate help
& ${msbuild} ${basedir}\Library.shfbproj
