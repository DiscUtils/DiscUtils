. .\common.ps1


# Clean the layout dir
del ${basedir}\layout -recurse -force
New-Item -path "${basedir}" -name "layout" -type directory | out-null


# Create the source zip
& $hg archive "${basedir}\layout\src"
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
Copy-Item "${basedir}\utils\NTFSExtract\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\VHDCreate\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\VHDDump\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\iSCSIBrowse\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\VirtualDiskConvert\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\VolInfo\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\DiskDump\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\OSClone\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\BCDDump\bin\Release\*.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\LICENSE.TXT" "${basedir}\layout\bin\LICENSE.TXT"
pushd layout\bin
& $zip a -r -tzip "${basedir}\layout\DiscUtilsBin-${ver}.zip" "*.*"
popd


# Create the PowerShell zip
New-Item -path "${basedir}\layout\" -name "powershell" -type directory | out-null
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\bin\release\*.dll" "${basedir}\layout\powershell"
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\bin\release\*.ps1xml" "${basedir}\layout\powershell"
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\bin\release\*.xml" "${basedir}\layout\powershell"
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\bin\release\*.psd1" "${basedir}\layout\powershell"
Copy-Item "${basedir}\LICENSE.TXT" "${basedir}\layout\powershell\LICENSE.TXT"
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\README.TXT" "${basedir}\layout\powershell"
pushd layout\powershell
& $zip a -r -tzip "${basedir}\layout\DiscUtilsPowerShell-${ver}.zip" "*.*"
popd
