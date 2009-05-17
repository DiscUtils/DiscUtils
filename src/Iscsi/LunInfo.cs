//
// Copyright (c) 2009, Kenneth Bell
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


using System.Globalization;

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// The known classes of SCSI device.
    /// </summary>
    public enum LunClass
    {
        /// <summary>
        /// Device is block storage (i.e. normal disk)
        /// </summary>
        BlockStorage = 0x00,

        /// <summary>
        /// Device is sequential access storage
        /// </summary>
        TapeStorage = 0x01,

        /// <summary>
        /// Device is a printer.
        /// </summary>
        Printer = 0x02,

        /// <summary>
        /// Device is a SCSI processor.
        /// </summary>
        Processor = 0x03,

        /// <summary>
        /// Device is write-once storage.
        /// </summary>
        WriteOnceStorage = 0x04,

        /// <summary>
        /// Device is a CD/DVD drive.
        /// </summary>
        OpticalDisc = 0x05,

        /// <summary>
        /// Device is a scanner (obsolete).
        /// </summary>
        Scanner = 0x06,

        /// <summary>
        /// Device is optical memory (some optical discs).
        /// </summary>
        OpticalMemory = 0x07,

        /// <summary>
        /// Device is a media changer device.
        /// </summary>
        Jukebox = 0x08,

        /// <summary>
        /// Communications device (obsolete).
        /// </summary>
        Communications = 0x09,

        /// <summary>
        /// Device is a Storage Array (e.g. RAID).
        /// </summary>
        StorageArray = 0x0C,

        /// <summary>
        /// Device is Enclosure Services.
        /// </summary>
        EnclosureServices = 0x0D,

        /// <summary>
        /// Device is a simplified block device.
        /// </summary>
        SimplifiedDirectAccess = 0x0E,

        /// <summary>
        /// Device is an optical card reader/writer device.
        /// </summary>
        OpticalCard = 0x0F,

        /// <summary>
        /// Device is a Bridge Controller.
        /// </summary>
        BridgeController = 0x10,

        /// <summary>
        /// Device is an object-based storage device.
        /// </summary>
        ObjectBasedStorage = 0x11,

        /// <summary>
        /// Device is an Automation/Drive interface.
        /// </summary>
        AutomationDriveInterface = 0x12,

        /// <summary>
        /// Device is a Security Manager.
        /// </summary>
        SecurityManager = 0x13,

        /// <summary>
        /// Device is a well-known device, as defined by SCSI specifications.
        /// </summary>
        WellKnown = 0x1E,
    }

    /// <summary>
    /// Provides information about an iSCSI LUN.
    /// </summary>
    public class LunInfo
    {
        private long _lun;
        private LunClass _deviceType;
        private bool _removable;
        private string _vendorId;
        private string _productId;
        private string _productRevision;

        internal LunInfo(long lun, LunClass type, bool removable, string vendor, string product, string revision)
        {
            _lun = lun;
            _deviceType = type;
            _removable = removable;
            _vendorId = vendor;
            _productId = product;
            _productRevision = revision;
        }

        /// <summary>
        /// Gets the Logical Unit Number of this device.
        /// </summary>
        public long Lun
        {
            get { return _lun;}
        }

        /// <summary>
        /// Gets the type (or class) of this device.
        /// </summary>
        public LunClass DeviceType
        {
            get { return _deviceType; }
        }

        /// <summary>
        /// Gets whether this Lun has removable media.
        /// </summary>
        public bool Removable
        {
            get { return _removable; }
        }

        /// <summary>
        /// Gets the vendor id (registered name) for this device.
        /// </summary>
        public string VendorId
        {
            get { return _vendorId; }
        }

        /// <summary>
        /// Gets the product id (name) for this device.
        /// </summary>
        public string ProductId
        {
            get
            {
                return _productId;
            }
        }

        /// <summary>
        /// Gets the product revision for this device.
        /// </summary>
        public string ProductRevision
        {
            get { return _productRevision; }
        }

        /// <summary>
        /// Gets the LUN as a string.
        /// </summary>
        /// <returns>The LUN</returns>
        public override string ToString()
        {
            if ((((ulong)_lun) & 0xFF00000000000000) == 0)
            {
                return (_lun >> (6 * 8)).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                return _lun.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
