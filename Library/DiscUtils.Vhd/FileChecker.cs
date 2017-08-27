//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Streams;
#if !NETCORE
using System.Runtime.Serialization;
#endif

namespace DiscUtils.Vhd
{
    /// <summary>
    /// VHD file format verifier, that identifies corrupt VHD files.
    /// </summary>
    public class FileChecker
    {
        private readonly Stream _fileStream;
        private Footer _footer;
        private DynamicHeader _dynamicHeader;

        private TextWriter _report;
        private ReportLevels _reportLevels;

        private ReportLevels _levelsDetected;
        private readonly ReportLevels _levelsConsideredFail = ReportLevels.Errors;

        /// <summary>
        /// Initializes a new instance of the FileChecker class.
        /// </summary>
        /// <param name="stream">The VHD file stream.</param>
        public FileChecker(Stream stream)
        {
            _fileStream = stream;
        }

        /// <summary>
        /// Verifies the VHD file, generating a report and a pass/fail indication.
        /// </summary>
        /// <param name="reportOutput">The destination for the report.</param>
        /// <param name="levels">How verbose the report should be.</param>
        /// <returns><c>true</c> if the file is valid, else false.</returns>
        public bool Check(TextWriter reportOutput, ReportLevels levels)
        {
            _report = reportOutput;
            _reportLevels = levels;
            _levelsDetected = ReportLevels.None;

            try
            {
                DoCheck();
            }
            catch (AbortException ae)
            {
                ReportError("File system check aborted: " + ae);
                return false;
            }
            catch (Exception e)
            {
                ReportError("File system check aborted with exception: " + e);
                return false;
            }

            return (_levelsDetected & _levelsConsideredFail) == 0;
        }

        private static void Abort()
        {
            throw new AbortException();
        }

        private void DoCheck()
        {
            CheckFooter();

            if (_footer == null || _footer.DiskType != FileType.Fixed)
            {
                CheckHeader();
            }

            if (_footer == null)
            {
                ReportError("Unable to continue - no valid header or footer");
                Abort();
            }

            CheckFooterFields();

            if (_footer.DiskType != FileType.Fixed)
            {
                CheckDynamicHeader();

                CheckBat();
            }
        }

        private void CheckBat()
        {
            int batSize = MathUtilities.RoundUp(_dynamicHeader.MaxTableEntries * 4, Sizes.Sector);
            if (_dynamicHeader.TableOffset > _fileStream.Length - batSize)
            {
                ReportError("BAT: BAT extends beyond end of file");
                return;
            }

            _fileStream.Position = _dynamicHeader.TableOffset;
            byte[] batData = StreamUtilities.ReadExact(_fileStream, batSize);
            uint[] bat = new uint[batSize / 4];
            for (int i = 0; i < bat.Length; ++i)
            {
                bat[i] = EndianUtilities.ToUInt32BigEndian(batData, i * 4);
            }

            for (int i = _dynamicHeader.MaxTableEntries; i < bat.Length; ++i)
            {
                if (bat[i] != uint.MaxValue)
                {
                    ReportError("BAT: Padding record '" + i + "' should be 0xFFFFFFFF");
                }
            }

            uint dataStartSector = uint.MaxValue;
            for (int i = 0; i < _dynamicHeader.MaxTableEntries; ++i)
            {
                if (bat[i] < dataStartSector)
                {
                    dataStartSector = bat[i];
                }
            }

            if (dataStartSector == uint.MaxValue)
            {
                return;
            }

            long dataStart = (long)dataStartSector * Sizes.Sector;
            uint blockBitmapSize =
                (uint)MathUtilities.RoundUp(_dynamicHeader.BlockSize / Sizes.Sector / 8, Sizes.Sector);
            uint storedBlockSize = _dynamicHeader.BlockSize + blockBitmapSize;

            bool[] seenBlocks = new bool[_dynamicHeader.MaxTableEntries];
            for (int i = 0; i < _dynamicHeader.MaxTableEntries; ++i)
            {
                if (bat[i] != uint.MaxValue)
                {
                    long absPos = (long)bat[i] * Sizes.Sector;

                    if (absPos + storedBlockSize > _fileStream.Length)
                    {
                        ReportError("BAT: block stored beyond end of stream");
                    }

                    if ((absPos - dataStart) % storedBlockSize != 0)
                    {
                        ReportError(
                            "BAT: block stored at invalid start sector (not a multiple of size of a stored block)");
                    }

                    uint streamBlockIdx = (uint)((absPos - dataStart) / storedBlockSize);
                    if (seenBlocks[streamBlockIdx])
                    {
                        ReportError("BAT: multiple blocks occupying same file space");
                    }

                    seenBlocks[streamBlockIdx] = true;
                }
            }
        }

        private void CheckDynamicHeader()
        {
            long lastHeaderEnd = _footer.DataOffset + 512;
            long pos = _footer.DataOffset;
            while (pos != -1)
            {
                if (pos % 512 != 0)
                {
                    ReportError("DynHeader: Unaligned header @{0}", pos);
                }

                _fileStream.Position = pos;
                Header hdr = Header.FromStream(_fileStream);

                if (hdr.Cookie == DynamicHeader.HeaderCookie)
                {
                    if (_dynamicHeader != null)
                    {
                        ReportError("DynHeader: Duplicate dynamic header found");
                    }

                    _fileStream.Position = pos;
                    _dynamicHeader = DynamicHeader.FromStream(_fileStream);

                    if (pos + 1024 > lastHeaderEnd)
                    {
                        lastHeaderEnd = pos + 1024;
                    }
                }
                else
                {
                    ReportWarning("DynHeader: Undocumented header found, with cookie '" + hdr.Cookie + "'");

                    if (pos + 512 > lastHeaderEnd)
                    {
                        lastHeaderEnd = pos + 1024;
                    }
                }

                pos = hdr.DataOffset;
            }

            if (_dynamicHeader == null)
            {
                ReportError("DynHeader: No dynamic header found");
                return;
            }

            if (_dynamicHeader.TableOffset < lastHeaderEnd)
            {
                ReportError("DynHeader: BAT offset is before last header");
            }

            if (_dynamicHeader.TableOffset % 512 != 0)
            {
                ReportError("DynHeader: BAT offset is not sector aligned");
            }

            if (_dynamicHeader.HeaderVersion != 0x00010000)
            {
                ReportError("DynHeader: Unrecognized header version");
            }

            if (_dynamicHeader.MaxTableEntries != MathUtilities.Ceil(_footer.CurrentSize, _dynamicHeader.BlockSize))
            {
                ReportError("DynHeader: Max table entries is invalid");
            }

            if ((_dynamicHeader.BlockSize != Sizes.OneMiB * 2) && (_dynamicHeader.BlockSize != Sizes.OneKiB * 512))
            {
                ReportWarning("DynHeader: Using non-standard block size '" + _dynamicHeader.BlockSize + "'");
            }

            if (!Utilities.IsPowerOfTwo(_dynamicHeader.BlockSize))
            {
                ReportError("DynHeader: Block size is not a power of 2");
            }

            if (!_dynamicHeader.IsChecksumValid())
            {
                ReportError("DynHeader: Invalid checksum");
            }

            if (_footer.DiskType == FileType.Dynamic && _dynamicHeader.ParentUniqueId != Guid.Empty)
            {
                ReportWarning("DynHeader: Parent Id is not null for dynamic disk");
            }
            else if (_footer.DiskType == FileType.Differencing && _dynamicHeader.ParentUniqueId == Guid.Empty)
            {
                ReportError("DynHeader: Parent Id is null for differencing disk");
            }

            if (_footer.DiskType == FileType.Differencing && _dynamicHeader.ParentTimestamp > DateTime.UtcNow)
            {
                ReportWarning("DynHeader: Parent timestamp is greater than current time");
            }
        }

        private void CheckFooterFields()
        {
            if (_footer.Cookie != "conectix")
            {
                ReportError("Footer: Invalid VHD cookie - should be 'connectix'");
            }

            if ((_footer.Features & ~1) != 2)
            {
                ReportError("Footer: Invalid VHD features - should be 0x2 or 0x3");
            }

            if (_footer.FileFormatVersion != 0x00010000)
            {
                ReportError("Footer: Unrecognized VHD file version");
            }

            if (_footer.DiskType == FileType.Fixed && _footer.DataOffset != -1)
            {
                ReportError("Footer: Invalid data offset - should be 0xFFFFFFFF for fixed disks");
            }
            else if (_footer.DiskType != FileType.Fixed && (_footer.DataOffset == 0 || _footer.DataOffset == -1))
            {
                ReportError("Footer: Invalid data offset - should not be 0x0 or 0xFFFFFFFF for non-fixed disks");
            }

            if (_footer.Timestamp > DateTime.UtcNow)
            {
                ReportError("Footer: Invalid timestamp - creation time in file is greater than current time");
            }

            if (_footer.CreatorHostOS != "Wi2k" && _footer.CreatorHostOS != "Mac ")
            {
                ReportWarning("Footer: Creator Host OS is not a documented value ('Wi2K' or 'Mac '), is '" +
                              _footer.CreatorHostOS + "'");
            }

            if (_footer.OriginalSize != _footer.CurrentSize)
            {
                ReportInfo("Footer: Current size of the disk doesn't match the original size");
            }

            if (_footer.CurrentSize == 0)
            {
                ReportError("Footer: Current size of the disk is 0 bytes");
            }

            if (!_footer.Geometry.Equals(Geometry.FromCapacity(_footer.CurrentSize)))
            {
                ReportWarning("Footer: Disk Geometry does not match documented Microsoft geometry for this capacity");
            }

            if (_footer.DiskType != FileType.Fixed && _footer.DiskType != FileType.Dynamic &&
                _footer.DiskType != FileType.Differencing)
            {
                ReportError("Footer: Undocumented disk type, not Fixed, Dynamic or Differencing");
            }

            if (!_footer.IsChecksumValid())
            {
                ReportError("Footer: Invalid footer checksum");
            }

            if (_footer.UniqueId == Guid.Empty)
            {
                ReportWarning("Footer: Unique Id is null");
            }
        }

        private void CheckFooter()
        {
            _fileStream.Position = _fileStream.Length - Sizes.Sector;
            byte[] sector = StreamUtilities.ReadExact(_fileStream, Sizes.Sector);

            _footer = Footer.FromBytes(sector, 0);
            if (!_footer.IsValid())
            {
                ReportError("Invalid VHD footer at end of file");
            }
        }

        private void CheckHeader()
        {
            _fileStream.Position = 0;
            byte[] headerSector = StreamUtilities.ReadExact(_fileStream, Sizes.Sector);

            Footer header = Footer.FromBytes(headerSector, 0);
            if (!header.IsValid())
            {
                ReportError("Invalid VHD footer at start of file");
            }

            _fileStream.Position = _fileStream.Length - Sizes.Sector;
            byte[] footerSector = StreamUtilities.ReadExact(_fileStream, Sizes.Sector);

            if (!Utilities.AreEqual(footerSector, headerSector))
            {
                ReportError("Header and footer are different");
            }

            if (_footer == null || !_footer.IsValid())
            {
                _footer = header;
            }
        }

        private void ReportInfo(string str, params object[] args)
        {
            _levelsDetected |= ReportLevels.Information;
            if ((_reportLevels & ReportLevels.Information) != 0)
            {
                _report.WriteLine("INFO: " + str, args);
            }
        }

        private void ReportWarning(string str, params object[] args)
        {
            _levelsDetected |= ReportLevels.Warnings;
            if ((_reportLevels & ReportLevels.Warnings) != 0)
            {
                _report.WriteLine("WARNING: " + str, args);
            }
        }

        private void ReportError(string str, params object[] args)
        {
            _levelsDetected |= ReportLevels.Errors;
            if ((_reportLevels & ReportLevels.Errors) != 0)
            {
                _report.WriteLine("ERROR: " + str, args);
            }
        }

#if !NETCORE
        [Serializable]
#endif
        private sealed class AbortException : InvalidFileSystemException
        {
            
        }
    }
}