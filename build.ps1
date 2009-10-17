$ver = "0.1"

$basedir = "$pwd"
$utilsdir = "C:\utils"
$msbuild = "c:\windows\Microsoft.NET\Framework\v3.5\msbuild.exe"
$vcsexpress = "C:\Program Files\Microsoft Visual Studio 9.0\Common7\IDE\VCSExpress.exe"

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


# Generate help
& ${msbuild} ${basedir}\Library.shfbproj
