. .\common.ps1

Write-Host "Checking for signing key..."
if(-not (Test-Path $signingkey))
{
  Write-Host "Signing key missing"
  Exit
}

# Set revision info in version.cs
Write-Host "Setting version information..."
$now = get-date
$rev = & ${hg} id -i
$filebuild = (new-timespan -start (get-date -year 2010 -month 1 -day 1) -end $now).Days
$filerev = [int]((($now.Hour * 60 + $now.Minute) * 60 + $now.Second) / 2)
$lines = Get-Content "${basedir}\Version.cs"
$lines = $lines | Foreach-Object { $_ -replace "AssemblyDescription\(.*\)", "AssemblyDescription(""Revision: $rev"")" }
$lines = $lines | Foreach-Object { $_ -replace "AssemblyFileVersion\(.*\)", "AssemblyFileVersion(""${ver}.${filebuild}.${filerev}"")" }
$lines = $lines | Foreach-Object { $_ -replace "AssemblyVersion\(.*\)", "AssemblyVersion(""${fullver}"")" }
$lines | Set-Content "${basedir}\Version.cs";

# Clean up
Write-Host "Cleaning old build output..."
if(Test-Path ${basedir}\Help\Output)
{
  del ${basedir}\Help\Output -recurse -force
}
& ${msbuild} "${basedir}\DiscUtils.sln" /t:Clean Debug | out-null
& ${msbuild} "${basedir}\DiscUtils.sln" /t:Clean Release | out-null
& ${msbuild} "${basedir}\DiscUtils.sln" /t:Clean SignedRelease | out-null

# Compile
Write-Host "Compiling..."
& ${msbuild} "${basedir}\DiscUtils.sln" /m /t:Build /p:Configuration=SignedRelease
# | out-null
if(-not $?)
{
  Write-Host "Visual Studio Build Failed"
  Exit
}


# Check assembly signed
Write-Host "Checking library signature..."
$assm = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom("${basedir}\src\bin\SignedRelease\discutils.dll")
if(-not $assm.GetName().GetPublicKeyToken())
{
  Write-Host "Assembly not signed"
  Exit
}

# Restore Version.cs
Write-Host "Resetting version information..."
$lines = Get-Content "${basedir}\Version.cs"
$lines = $lines | Foreach-Object { $_ -replace "AssemblyDescription\(.*\)", "AssemblyDescription(""Private Build"")" }
$lines = $lines | Foreach-Object { $_ -replace "AssemblyFileVersion\(.*\)", "AssemblyFileVersion(""${fullver}"")" }
$lines = $lines | Foreach-Object { $_ -replace "AssemblyVersion\(.*\)", "AssemblyVersion(""${fullver}"")" }
$lines | Set-Content "${basedir}\Version.cs"

# Check FxCop
#Write-Host "Checking output against FxCop..."
#$fxcopOutput = & "${fxcop}" /p:${basedir}\DiscUtils.fxcop /o:${basedir}\FxCopReport.xml | out-string
#if(Test-Path ${basedir}\FxCopReport.xml)
#{
#  Write-Host "FxCop failed"
#  Write-Host $fxcopOutput
#  Exit
#}

# Generate help
Write-Host "Generating help... (takes a long time)"
& ${msbuild} ${basedir}\Help\Library.shfbproj /p:Configuration=SignedRelease
# | out-null

Write-Host "Build Complete!"
