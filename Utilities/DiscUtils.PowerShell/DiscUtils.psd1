#
# PowerShell module manifest for module 'DiscUtils'
#
#
# Copyright (c) 2008-2009, Kenneth Bell
#
# Permission is hereby granted, free of charge, to any person obtaining a
# copy of this software and associated documentation files (the "Software"),
# to deal in the Software without restriction, including without limitation
# the rights to use, copy, modify, merge, publish, distribute, sublicense,
# and/or sell copies of the Software, and to permit persons to whom the
# Software is furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
# FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
# DEALINGS IN THE SOFTWARE.
#

@{

# Script module or binary module file associated with this manifest
ModuleToProcess = 'DiscUtils.PowerShell.dll'

# Version number of this module.
ModuleVersion = '0.8.0.0'

# ID used to uniquely identify this module
GUID = '464120bc-c6c2-4cd2-92c3-68532193590d'

# Author of this module
Author = 'Kenneth Bell'

# Company or vendor of this module
CompanyName = 'http://discutils.codeplex.com'

# Copyright statement for this module
Copyright = 'Copyright © Kenneth Bell 2008-2009'

# Description of the functionality provided by this module
Description = 'This is the DiscUtils PowerShell module for accessing virtual disk images'

# Minimum version of the Windows PowerShell engine required by this module
PowerShellVersion = '2.0'

# Name of the Windows PowerShell host required by this module
PowerShellHostName = ''

# Minimum version of the Windows PowerShell host required by this module
PowerShellHostVersion = ''

# Minimum version of the .NET Framework required by this module
DotNetFrameworkVersion = ''

# Minimum version of the common language runtime (CLR) required by this module
CLRVersion = ''

# Processor architecture (None, X86, Amd64, IA64) required by this module
ProcessorArchitecture = 'None'

# Modules that must be imported into the global environment prior to importing this module
RequiredModules = @()

# Assemblies that must be loaded prior to importing this module
RequiredAssemblies = @('DiscUtils.PowerShell.dll')

# Script files (.ps1) that are run in the caller's environment prior to importing this module
ScriptsToProcess = @()

# Type files (.ps1xml) to be loaded when importing this module
TypesToProcess = @('DiscUtils.Types.ps1xml')

# Format files (.ps1xml) to be loaded when importing this module
FormatsToProcess = @('DiscUtils.Format.ps1xml')

# Modules to import as nested modules of the module specified in ModuleToProcess
NestedModules = @()

# Functions to export from this module
FunctionsToExport = '*'

# Cmdlets to export from this module
CmdletsToExport = '*'

# Variables to export from this module
VariablesToExport = '*'

# Aliases to export from this module
AliasesToExport = '*'

# List of all files packaged with this module
FileList = @('DiscUtils.psd1','DiscUtils.dll','DiscUtils.PowerShell.dll', 'DiscUtils.Common.dll',
             'DiscUtils.Format.ps1xml','DiscUtils.Types.ps1xml','DiscUtils.PowerShell.dll-Help.xml',
             'LICENSE.TXT')

# Private data to pass to the module specified in ModuleToProcess
PrivateData = ''

}
