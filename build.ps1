$ver = "0.1"

$basedir = "$pwd"
$utilsdir = "C:\utils"
$sbconsole = "C:\Program Files\EWSoftware\Sandcastle Help File Builder\SandcastleBuilderConsole.exe"
$vcsexpress = "C:\Program Files\Microsoft Visual Studio 9.0\Common7\IDE\VCSExpress.exe"

# Clean up
del ${basedir}\help -recurse -force
& ${vcsexpress} "${basedir}\DiscUtils.sln" /clean Debug | out-null
& ${vcsexpress} "${basedir}\DiscUtils.sln" /clean Release | out-null

# Compile
& ${vcsexpress} "${basedir}\DiscUtils.sln" /build Release | out-null
& ${vcsexpress} "${basedir}\DiscUtils.sln" /build Debug | out-null

# Generate help
& ${sbconsole} ${basedir}\Library.shfb | out-null
