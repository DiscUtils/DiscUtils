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

namespace DiscUtils.BootConfig
{
    /// <summary>
    /// The value of an element.
    /// </summary>
    public abstract class ElementValue
    {
        /// <summary>
        /// Gets the format of the value.
        /// </summary>
        public abstract ElementFormat Format { get; }

        /// <summary>
        /// Gets the parent object (only for Device values).
        /// </summary>
        public virtual Guid ParentObject
        {
            get { return Guid.Empty; }
        }

        /// <summary>
        /// Gets a value representing a device (aka partition).
        /// </summary>
        /// <param name="parentObject">Object containing detailed information about the device.</param>
        /// <param name="physicalVolume">The volume to represent.</param>
        /// <returns>The value as an object.</returns>
        public static ElementValue ForDevice(Guid parentObject, PhysicalVolumeInfo physicalVolume)
        {
            return new DeviceElementValue(parentObject, physicalVolume);
        }

        /// <summary>
        /// Gets a value representing the logical boot device.
        /// </summary>
        /// <returns>The boot pseudo-device as an object.</returns>
        public static ElementValue ForBootDevice()
        {
            return new DeviceElementValue();
        }

        /// <summary>
        /// Gets a value representing a string value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value as an object.</returns>
        public static ElementValue ForString(string value)
        {
            return new StringElementValue(value);
        }

        /// <summary>
        /// Gets a value representing an integer value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value as an object.</returns>
        public static ElementValue ForInteger(long value)
        {
            return new IntegerElementValue((ulong)value);
        }

        /// <summary>
        /// Gets a value representing an integer list value.
        /// </summary>
        /// <param name="values">The value to convert.</param>
        /// <returns>The value as an object.</returns>
        public static ElementValue ForIntegerList(long[] values)
        {
            ulong[] ulValues = new ulong[values.Length];
            for (int i = 0; i < values.Length; ++i)
            {
                ulValues[i] = (ulong)values[i];
            }

            return new IntegerListElementValue(ulValues);
        }

        /// <summary>
        /// Gets a value representing a boolean value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value as an object.</returns>
        public static ElementValue ForBoolean(bool value)
        {
            return new BooleanElementValue(value);
        }

        /// <summary>
        /// Gets a value representing a GUID value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value as an object.</returns>
        public static ElementValue ForGuid(Guid value)
        {
            return new GuidElementValue(value.ToString("B"));
        }

        /// <summary>
        /// Gets a value representing a GUID list value.
        /// </summary>
        /// <param name="values">The value to convert.</param>
        /// <returns>The value as an object.</returns>
        public static ElementValue ForGuidList(Guid[] values)
        {
            string[] strValues = new string[values.Length];
            for (int i = 0; i < values.Length; ++i)
            {
                strValues[i] = values[i].ToString("B");
            }

            return new GuidListElementValue(strValues);
        }
    }
}