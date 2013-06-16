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
Copy-Item "${basedir}\Help\Output\DiscUtilsClassLibrary.chm" "${basedir}\layout\doc\DiscUtils-${ver}.chm"
Copy-Item "${basedir}\Help\Output\DiscUtilsClassLibrary.msha" "${basedir}\layout\doc\HelpContentSetup.msha"
Copy-Item "${basedir}\Help\Output\DiscUtilsClassLibrary.mshc" "${basedir}\layout\doc\DiscUtilsClassLibrary.mshc"
Copy-Item "${basedir}\Help\Output\DiscUtils.xml" "${basedir}\layout\doc\DiscUtils.xml"
Copy-Item "${basedir}\Help\Install_VS2010_Help.bat" "${basedir}\layout\doc\Install_VS2010_Help.bat"
Copy-Item "${basedir}\LICENSE.TXT" "${basedir}\layout\doc\LICENSE.TXT"
pushd layout\doc
& $zip a -r -tzip "${basedir}\layout\DiscUtilsDoc-${ver}.zip" "*.*"
popd


# Create the Binary zip
New-Item -path "${basedir}\layout\" -name "bin" -type directory | out-null
Copy-Item "${basedir}\utils\ISOCreate\bin\SignedRelease\*.dll" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\ISOCreate\bin\SignedRelease\ISOCreate.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\NTFSDump\bin\SignedRelease\NTFSDump.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\FileExtract\bin\SignedRelease\FileExtract.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\VHDCreate\bin\SignedRelease\VHDCreate.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\VHDDump\bin\SignedRelease\VHDDump.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\VHDXDump\bin\SignedRelease\VHDXDump.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\iSCSIBrowse\bin\SignedRelease\iSCSIBrowse.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\VirtualDiskConvert\bin\SignedRelease\VirtualDiskConvert.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\VolInfo\bin\SignedRelease\VolInfo.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\DiskDump\bin\SignedRelease\DiskDump.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\OSClone\bin\SignedRelease\OSClone.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\BCDDump\bin\SignedRelease\BCDDump.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\DiskClone\bin\SignedRelease\DiskClone.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\MSBuildTask\bin\SignedRelease\DiscUtils.MSBuild.dll" "${basedir}\layout\bin"
Copy-Item "${basedir}\utils\FileRecover\bin\SignedRelease\FileRecover.exe" "${basedir}\layout\bin"
Copy-Item "${basedir}\LICENSE.TXT" "${basedir}\layout\bin\LICENSE.TXT"
pushd layout\bin
& $zip a -r -tzip "${basedir}\layout\DiscUtilsBin-${ver}.zip" "*.*"
popd


# Create the PowerShell zip
New-Item -path "${basedir}\layout\" -name "powershell" -type directory | out-null
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\bin\SignedRelease\*.dll" "${basedir}\layout\powershell"
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\bin\SignedRelease\*.ps1xml" "${basedir}\layout\powershell"
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\bin\SignedRelease\*.xml" "${basedir}\layout\powershell"
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\bin\SignedRelease\*.psd1" "${basedir}\layout\powershell"
Copy-Item "${basedir}\LICENSE.TXT" "${basedir}\layout\powershell\LICENSE.TXT"
Copy-Item "${basedir}\utils\DiscUtils.PowerShell\README.TXT" "${basedir}\layout\powershell"
pushd layout\powershell
& $zip a -r -tzip "${basedir}\layout\DiscUtilsPowerShell-${ver}.zip" "*.*"
popd
