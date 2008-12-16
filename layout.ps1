$ver = "0.2"

$basedir = "$pwd"
$utilsdir = "C:\utils"
$svn = "C:\Program Files\SlikSvn\bin\svn.exe"
$zip = "${utilsdir}\7za.exe"


# Clean the layout dir
del ${basedir}\layout -recurse -force
New-Item -path "${basedir}" -name "layout" -type directory | out-null


# Create the source zip
& $svn export -r HEAD "${basedir}" "${basedir}\layout\src"
pushd layout\src
& $zip a -r -tzip  "${basedir}\layout\DiscUtilsSrc-${ver}.zip" "*.*"
popd


# Create the Doc zip
New-Item "${basedir}\layout\doc" -type directory | out-null
Copy-Item "${basedir}\Help\Documentation.chm" "${basedir}\layout\doc\DiscUtils-${ver}.chm"
Copy-Item "${basedir}\LICENSE.TXT" "${basedir}\layout\doc\LICENSE.TXT"
pushd layout\doc
& $zip a -r -tzip "${basedir}\layout\DiscUtilsDoc-${ver}.zip" "*.*"
popd


# Create the Binary zip
New-Item -path "${basedir}\layout\" -name "bin" -type directory | out-null
Copy-Item "${basedir}\utils\ISOCreate\bin\Release\*.dll" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\ISOCreate\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\ISOExtract\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\FATExtract\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\NTFSDump\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\LICENSE.TXT" "${basedir}\layout\bin\LICENSE.TXT"
pushd layout\bin
& $zip a -r -tzip "${basedir}\layout\DiscUtilsBin-${ver}.zip" "*.*"
popd

