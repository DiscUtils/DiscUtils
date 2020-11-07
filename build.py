import glob
import os
import shutil
import logging
import sys
import pickle
from edk2toollib.utility_functions import RunCmd
from edk2toollib.windows.locate_tools import QueryVcVariables

current_dir = os.path.abspath(os.path.dirname(__file__))
utilities_dir = os.path.join(current_dir, "Utilities")

delete_path = os.path.join(current_dir, "*.exe")
delete_count = 0
for delete_file in glob.iglob(delete_path):
    os.remove(delete_file)
    delete_count += 1

print(f"Deleted {delete_count} EXE files")

delete_path = os.path.join(current_dir, "*.vhd")
delete_count = 0
for delete_file in glob.iglob(delete_path):
    os.remove(delete_file)
    delete_count += 1
print(f"Deleted {delete_count} VHD files")

delete_path = os.path.join(current_dir, "*.dll")
delete_count = 0
for delete_file in glob.iglob(delete_path):
    os.remove(delete_file)
    delete_count += 1
print(f"Deleted {delete_count} DLL files")


utilities_i_care_about = ["VHDCreate"]
msbuild = "msbuild"
exes = {}
for util in utilities_i_care_about:
    util_path = os.path.join(utilities_dir, util)
    print(f"Running MsBuild on {util}")
    ret = RunCmd(msbuild, '-m .', workingdir = util_path)
    if ret != 0:
        print(f"MsBuild returned {ret}")
        ret = RunCmd(msbuild, '-m .', workingdir = util_path, logging_level=logging.WARN)
        sys.exit(ret)

find_path = os.path.join(utilities_dir, "**","bin", "**", "*.exe")
print(find_path)
for found_file in glob.iglob(find_path,recursive=True):
    file_name = os.path.basename(found_file)
    exes[file_name] = found_file

# running commands
commands = [
    ('VHDCreate.exe', '-ft FAT32 -sz 20MB test.vhd')
]

for cmd, args in commands:
    print(f"Running {cmd} {args}")
    cmd_path = exes[cmd]
    cmd_dir = os.path.dirname(cmd_path)
    ret = RunCmd(cmd_path, args, outstream=sys.stdout)