using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Binary_File_Parser
{
    public class BinaryFileParser
    {
        #region Members

        public int StateLength { get;   set; } = 0;

        public int FieldLength { get; set; } = 0;
        public int FieldOffset { get; set; } = 0;

        #endregion // Members

        #region Constructor(s)

        /// <summary>
        /// Default Constructor
        /// </summary>
        public BinaryFileParser()
        {
        }

        #endregion // Constructor(s)

        #region Private Methods

        /// <summary>
        /// Get the field location in terms of byteID and the MSBit of the field within the byte. 
        /// </summary>
        /// <param name="filterBitID"></param>
        /// <param name="byteID"></param>
        /// <param name="bitID"></param>
        /// <returns></returns>
        /// Assumptions 
        ///                                                                            byte[0]                byte[1]  ...
        ///     OffSet is the bitID starting from the left side and counting up... 7,6,5,4,3,2,1,0  ||  7,6,5,4,3,12,1,0  || ...
        ///                                                                        | | |                | | |
        ///                                                                        | | |                | | |
        ///                                                                        | | offset:2         | | offset:10
        ///                                                                        | offset:1           | offset:9
        ///                                                                        offset:0             offset:8
        private bool getFieldLocation(int byteCount, int offset, ref int byteID, ref int bitID)
        {
            bool status = true;
            bitID = 0;

            // this value represents the byte in linear order from the beginning of the array (starting with byte 0, 1, etc)
            float byteBitIDs = ((float)(offset) / (float)8);

            // this value represents the byte in the array i.e. byte[0]  byte[1]  byte[2]...
            byteID = (int)byteBitIDs;

            // strip off the integer and keep the remainder 
            float bitValue = (float)byteBitIDs - (int)byteBitIDs;

            // identify the bit assoicated with the remainder
            if (bitValue == 0)
                bitID = 7;
            else if (bitValue == 0.125)
                bitID = 6;
            else if (bitValue == 0.25)
                bitID = 5;
            else if (bitValue == 0.375)
                bitID = 4;
            else if (bitValue == 0.50)
                bitID = 3;
            else if (bitValue == 0.625)
                bitID = 2;
            else if (bitValue == 0.75)
                bitID = 1;
            else if (bitValue == 0.875)
                bitID = 0;
            else
            {
                status = false;
                bitID = -1;
            }

            return status;
        }


        /// <summary>
        /// Extract the number of specified consecutive bits from a given byte array
        /// </summary>
        /// <param name="byteID"></param>
        /// <param name="bitID"></param>
        /// <returns></returns>
        private long getFieldValue(int byteID, int bitID, int width, byte[] data)
        {
            long fldValue = 0x00;
            int bitCount = 0;

            // How this works:
            //  'For' loop accumulates the field in chunks of bytes..
            //   The first byte being extracted lops off the MSBits not contained in the field
            //   Thus the bits used in the MSBytes begin with the specified field byte # and Bit #
            //   the loop gets whole bytes for all subsequent bytes being extracted from the data
            //   Once enough bits are accumulated, the loop is exited.  The field value is shifted
            //   to the rights for the number of 'Extra' bits of extracted data.

            // assemble the byte(s) of data containing the field.
            for (int i = byteID; (width - bitCount) > 0; i++)
            {
                if (i == byteID)
                {
                    // lop off the leading bits... bitID represent the bit within a byte where a field starts -- 7-0
                    fldValue = data[i] & (uint)(Math.Pow(2, bitID + 1) - 1);
                    bitCount += bitID + 1;
                }
                else
                {
                    fldValue = (fldValue << 8) | data[i];
                    bitCount += 8;
                }
            }

            // lop off the trailing bits that are not part of the field.
            if (bitCount > width)
                fldValue = fldValue >> (bitCount - width);

            return fldValue;
        }

        #endregion // Private Methods

        #region Public Methods

        /// <summary>
        /// get the location of a field within a byte array
        /// </summary>
        /// <param name="stateLength"></param>
        /// <param name="fieldOffset"></param>
        /// <param name="byteID"></param>
        /// <param name="bitID"></param>
        public void GetFieldLocation(int stateLength, int fieldOffset,  ref int byteID, ref int bitID) 
        {
            // stateLength -- number of bytes contained in a state
            // fieldOffset -- number of bits from state data array [0]

            // byteID      -- state data byte in which the MSBit of the field is located.
            // bitID       -- bit location of the field's MSBit.
            getFieldLocation(stateLength, fieldOffset, ref byteID, ref bitID);
        }


        /// <summary>
        /// Get the location of the Next opCode field
        /// </summary>
        /// <param name="path"></param>
        /// <param name="startState"></param>
        /// <param name="opCode"></param>
        /// <returns></returns>
        public int GetNextLocation(string path, int startState, byte opCode, int byteID, int bitID)
        {
            int stateNumber = -1;
            bool found = false;
            byte[] stateData = new byte[StateLength];
            byte fldValue = 0x00;

            //
            // NOTE:  'using' closes the FileStream and the BinaryReader when exited
            //

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    //loop until found or end of file is encountered
                    using (BinaryReader br = new BinaryReader(fs, new ASCIIEncoding()))
                    {
                        if ((StateLength > 0) && (byteID != -1) && (bitID != -1))
                        {
                            // advance the file point to the state location
                            fs.Position = startState * StateLength;

                            int stateCount = 0;

                            // loop until the opcode is located or the end of file is encountered.
                            while (!found && (fs.Position < fs.Length - StateLength))
                            {
                                // reset to all zeros
                                Array.Clear(stateData, 0, StateLength);

                                // read the state data bytes in a temporary byte[]
                                Array.Copy(br.ReadBytes(StateLength), stateData, stateData.Length);

                                // Extract the Field from the state data... 
                                // getFieldValue returns a long which is truncated to a byte 
                                fldValue = (byte)getFieldValue(byteID, bitID, FieldLength, stateData);

                                if (fldValue == opCode)
                                {
                                    stateNumber = startState + stateCount;
                                    found = true;  // exits the 'while' loop
                                }

                                stateCount += 1;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                stateNumber = -1;
            }

            return stateNumber;
        }
        #endregion // Public Methods
    }
}