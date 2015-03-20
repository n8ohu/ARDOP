using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
#region "Imports"
using System.Text.RegularExpressions;
using System.IO.Ports;
using System.IO;
using System.Text;
using System.Threading;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.DirectSound;
#endregion
using System.Windows.Forms;

namespace ARDOP
{
    class Globals
    {
        #region "Debugging Aids"
        // For Optimization and debugging: Remove later
        public static double[] dblSNHistory = new double[100];
        public static Int32[] intInterpCnts = new Int32[100];
        #endregion
        public static Int32 intHistoryPtr;

        #region "Structures"

        // ModemControlBlock contains all TNC setup parametes (saved to ini file) 
        public struct ModemControlBlock
        {
            public string Callsign;
            public string GridSquare;
            public string CaptureDevice;
            public string PlaybackDevice;
            public bool HostTCPIP;
            public string TCPAddress;
            public Int16 TCPPort;
            public bool HostSerial;
            public string SerCOMPort;
            public int SerBaud;
            public bool HostBlueTooth;
            public string HostPairing;
            public string ARQBandwidth;
            public string Mode_Radio;
            public bool CWID;
            public Int16 DriveLevel;
            public bool DebugLog;
            public Int16 Squelch;
            public bool StartMinimized;
            public bool CommandTrace;
            public Int16 LeaderLength;
            public Int16 TrailerLength;
            public bool AccumulateStats;
            public bool DisplayWaterfall;
            public bool DisplaySpectrum;
            public bool SecureHostLogin;
            public string Password;
            public Int16 TuningRange;
            public bool RadioControl;
            public bool LinkedToHost;
            public string FECMode;
            public Int16 FECRepeats;
            public bool FECId;
        }

        public struct Status
        {
            // Structure for passing interface form control updates via thread safe synchronized queue... 
            public string ControlName;
            public string Text;
            public int Value;
            public System.Drawing.Color BackColor;
        }

        public struct Connection
        {

            // Holds all the needed info on the current connection...
            // remote station call sign
            public string strRemoteCallsign;
            // remaining Outbound Buffer bytes to confirm  
            public int intOBBytesToConfirm;
            // Outbound bytes confirmed by ACK and squenced
            public int intBytesConfirmed;
            // Session ID formed by 8 bit Hash of MyCallsign  and strRemoteCallsign always set to &HFF if not connected. 
            public byte bytSessionID;
            // the last PSN passed True for Odd, False for even. 
            public bool blnLastPSNPassed;
            // flag to indicate if this station initiated the connection
            public bool blnInitiatedConnection;
            // computed phase error creep
            public double dblAvgPECreepPerCarrier;
            // date/time of last ID
            public System.DateTime dttLastIDSent;
            // To compute the sample rate error
            public int intTotalSymbols;
            // the call sign that was the target of the successful connect
            public string strTargetCallsign;
            public int intSessionBW;
        }

        public struct TuningStats
        {
            //Statistics used to evaluate and measure performance
            public int intLeaderDetects;
            public int intLeaderSyncs;
            public int intAccumLeaderTracking;
            public double dblFSKTuningSNAvg;
            public int intGoodFSKFrameTypes;
            public int intFailedFSKFrameTypes;
            public int intAccumFSKTracking;
            public int intFSKSymbolCnt;
            public int intGoodFSKFrameDataDecodes;
            public int intFailedFSKFrameDataDecodes;
            public Int32 intAvgFSKQuality;

            public int intFrameSyncs;

            public double dblPSKTuningSNAvg;
            public int intAccumPSKLeaderTracking;
            public double dblAvgPSKRefErr;
            public int intAvgPSKRefCnt;
            public int intAccumPSKTracking;
            public int intPSKSymbolCnt;
            public int intGoodPSKFrameDataDecodes;
            public int intFailedPSKFrameDataDecodes;
            public Int32 intAvgPSKQuality;
        }

        public struct QualityStats
        {
            // Stats used to compute quality 
            public Int32 int4FSKQuality;
            public Int32 int4FSKQualityCnts;
            public Int32 intFSKSymbolsDecoded;
            public Int32[] intPSKQuality;
            public Int32[] intPSKQualityCnts;
            public Int32 intPSKSymbolsDecoded;
        }

        public struct SessionStats
        {
            // Structure for Session Statistics
            public System.DateTime dttSessionStart;
            public int intTotalBytesSent;
            public int intTotalBytesReceived;
            public int intFrameTypeDecodes;
            public double[] ModeQuality;
            public double dblSearchForLeaderAvg;
            public int intMax1MinThruput;
            public int intGearShifts;

            public bool blnStatsValid;
        }

        // Structure Assignments
        public static SessionStats stcSessionStats;
        public static ModemControlBlock MCB = new ModemControlBlock();
        public static TuningStats stcTuningStats;
        public static QualityStats stcQualityStats;

        public static Connection stcConnection;
        private struct DeviceDescription
        {
            public DeviceInformation info;
            public override string ToString()
            {
                return info.Description;
            }
            public DeviceDescription(DeviceInformation d)
            {
                info = d;
            }
        }
        #endregion

        #region "Objects"
        // Objects...
        public static INIFile objIniFile;
        public static object objIniFileLock = new object();
        public static MemoryStream memWaveStream;
        public static Radio objRadio = null;
        #endregion
        public static object objDataToSendLock = new object();

        #region "Integers"
        // Integers...
        public static int intSavedTuneLineLow = 0;
        public static int intSavedTuneLineHi = 0;
        // for testing
        public static int intBreakCounts = 0;

        public static int intTestFrameCorrectCnt = 0;


        #endregion

        #region "Doubles"
        // Doubles
        public static double dblMaxLeaderSN;
        #endregion
        public static double dblOffsetHz;

        #region "Arrays"
        // Data receivied correctly from host and waiting to send via TNC 
        public static byte[] bytDataToSend = new byte[-1 + 1];
        // Data captured by TNC and waiting trans mission to the Host. Note for FEC mode this may buffer 
        public static byte[] bytReceivedDataQueue = new byte[-1 + 1];
        // several repeated frames 
        #endregion


        #region "Date/Time"
        // Date/Time
        #endregion
        public static System.DateTime dttTestStart;

        #region "Strings"
        // Strings...
        public static string strExecutionDirectory = Application.StartupPath + "\\";
        public static string strProductVersion = Application.ProductVersion;
        public static string strWavDirectory;
        public static string strLastWavStream;

        public static string strRcvFrameTag;
        #endregion

        #region "Graphics"
        //Graphics
        #endregion
        public static Bitmap bmpConstellation;

        #region "Queues"
        // Queues...

        public static Queue queTNCStatus = Queue.Synchronized(new Queue());
        #endregion

        #region "Enums"
        public enum ReceiveState
        {
            // used for initial receive testing...later put in correct protocol states
            SearchingForLeader,
            AcquireSymbolSync,
            AcquireFrameSync,
            AcquireFrameType,
            DecodeFrameType,
            AcquireFrame,
            DecodeFrame
        }
        // used for initial testing
        public static ReceiveState State;

        public enum ProtocolState
        {
            OFFLINE,
            DISC,
            ISS,
            IRS,
            IDLE,
            FECSend,
            FECRcv
        }

        public static ProtocolState ARDOPState;
        #endregion


        #region "Public Subs/Functions"

        //Clear all Tuning Stats
        public static void ClearTuningStats()
        {
            var _with1 = stcTuningStats;
            _with1.intLeaderDetects = 0;
            _with1.intLeaderSyncs = 0;
            _with1.intFrameSyncs = 0;
            _with1.intAccumFSKTracking = 0;
            _with1.intFSKSymbolCnt = 0;
            _with1.intAccumPSKTracking = 0;
            _with1.intPSKSymbolCnt = 0;
            _with1.intGoodFSKFrameTypes = 0;
            _with1.intFailedFSKFrameTypes = 0;
            _with1.intGoodFSKFrameDataDecodes = 0;
            _with1.intFailedFSKFrameDataDecodes = 0;
            _with1.intGoodPSKFrameDataDecodes = 0;
            _with1.intFailedPSKFrameDataDecodes = 0;
            _with1.intAvgFSKQuality = 0;
            _with1.intAvgPSKQuality = 0;
            _with1.dblFSKTuningSNAvg = 0;
            _with1.dblPSKTuningSNAvg = 0;
            _with1.dblAvgPSKRefErr = 0;
            _with1.intAvgPSKRefCnt = 0;
        }
        //ClearTuningStats

        // Sub to Initialize before a new Connection
        public static void InitializeConnection()
        {
            var _with2 = stcConnection;
            _with2.strRemoteCallsign = "";
            // remote station call sign
            _with2.intOBBytesToConfirm = 0;
            // remaining bytes to confirm  
            _with2.intBytesConfirmed = 0;
            // Outbound bytes confirmed by ACK and squenced
            _with2.bytSessionID = 0xff;
            // Session ID 
            _with2.blnLastPSNPassed = false;
            // the last PSN passed True for Odd, False for even. 
            _with2.blnInitiatedConnection = false;
            // flag to indicate if this station initiated the connection
            _with2.dblAvgPECreepPerCarrier = 0;
            // computed phase error creep
            _with2.dttLastIDSent = DateTime.Now;
            // date/time of last ID
            _with2.intTotalSymbols = 0;
            // To compute the sample rate error
            _with2.strTargetCallsign = "";
            // the call sign that was the target of the successful connect
            _with2.intSessionBW = 200;
        }
        // InitializeConnection

        // Sub to Clear the Quality Stats
        public static void ClearQualityStats()
        {
            var _with3 = stcQualityStats;
            _with3.int4FSKQuality = 0;
            // ERROR: Not supported in C#: ReDimStatement

            // Quality for 4PSK, 8PSK  modulation modes
            _with3.int4FSKQualityCnts = 0;
            // ERROR: Not supported in C#: ReDimStatement

            // Counts for 4PSK, 8PSK modulation modes 
            //need to get total quantity of PSK modes
            _with3.intFSKSymbolsDecoded = 0;
            _with3.intPSKSymbolsDecoded = 0;
        }
        //ClearQualityStats

        // Sub to Write Tuning Stats to the Debug Log 
        public static void LogTuningStats()
        {
            if (!MCB.DebugLog)
                return;
            // only log if debug logging enabled
            try
            {
                Int32 intTotFSKDecodes = stcTuningStats.intGoodFSKFrameDataDecodes + stcTuningStats.intFailedFSKFrameDataDecodes;
                Int32 intTotPSKDecodes = stcTuningStats.intGoodPSKFrameDataDecodes + stcTuningStats.intFailedPSKFrameDataDecodes;

                var _with4 = stcTuningStats;
                Logs.WriteDebug("***TuningStats***");
                Logs.WriteDebug("     LeaderDetects=" + _with4.intLeaderDetects.ToString() + "   AvgS+N:N(dBpwr)=" + Strings.Format(_with4.dblFSKTuningSNAvg, "#.0") + "  LeaderSyncs=" + _with4.intLeaderSyncs.ToString() + "  LeaderAccumResyncs=" + _with4.intAccumLeaderTracking.ToString());

                Logs.WriteDebug("  FSK:");
                Logs.WriteDebug("     FrameSyncs=" + _with4.intFrameSyncs.ToString() + "   Good Frame Type Decodes=" + _with4.intGoodFSKFrameTypes.ToString() + "  Failed Frame Type Decodes =" + _with4.intFailedFSKFrameTypes.ToString());
                Logs.WriteDebug("     Good FSK Data Frame Decodes=" + _with4.intGoodFSKFrameDataDecodes.ToString() + "   Failed FSK Data Frame Decodes=" + _with4.intFailedFSKFrameDataDecodes.ToString());
                Logs.WriteDebug("     AccumFSKTracking=" + _with4.intAccumFSKTracking.ToString() + "   SymbolCnt=" + _with4.intFSKSymbolCnt.ToString() + "   Good Data Frame Decodes=" + _with4.intGoodFSKFrameDataDecodes.ToString() + "   Failed Data Frame Decodes=" + _with4.intFailedFSKFrameDataDecodes.ToString());
                Logs.WriteDebug("     TestFramesCorrectCnt= " + intTestFrameCorrectCnt.ToString());

                Logs.WriteDebug(" ");
                Logs.WriteDebug("  PSK:");
                Logs.WriteDebug("     Good PSK Data Frame Decodes=" + _with4.intGoodPSKFrameDataDecodes.ToString() + "   Failed PSK Data Frame Decodes=" + _with4.intFailedPSKFrameDataDecodes.ToString());
                Logs.WriteDebug("     AccumPSKTracking=" + _with4.intAccumPSKTracking.ToString() + "/" + _with4.intPSKSymbolCnt.ToString() + " PSK Symbols");
                if (_with4.intAvgPSKRefCnt > 0)
                {
                    Logs.WriteDebug("     AvgPSKRefErr=" + Strings.Format(57.3 * _with4.dblAvgPSKRefErr / _with4.intAvgPSKRefCnt, "#.0") + " deg for " + _with4.intAvgPSKRefCnt.ToString() + " Carrier x Frames");
                }


                var _with5 = stcQualityStats;
                Logs.WriteDebug(" ");
                Logs.WriteDebug("  Quality:");
                if (intTotFSKDecodes > 0)
                {
                    Logs.WriteDebug("     Avg 4FSK Frame Quality=" + Strings.Format(_with5.int4FSKQuality / intTotFSKDecodes, "#"));
                }
                if (_with5.intPSKQualityCnts[0] > 0)
                {
                    Logs.WriteDebug("     Avg 4PSK Quality=" + Strings.Format(_with5.intPSKQuality[0] / _with5.intPSKQualityCnts[0], "#"));
                }
                if (_with5.intPSKQualityCnts[1] > 0)
                {
                    Logs.WriteDebug("     Avg 8PSK Quality=" + Strings.Format(_with5.intPSKQuality[1] / _with5.intPSKQualityCnts[1], "#"));
                }

            }
            catch
            {
            }

        }
        // LogTuningStats

        // Function to convert string Text (ASCII) to byte array
        public static byte[] GetBytes(string strText)
        {
            // Converts a text string to a byte array...

            byte[] bytBuffer = new byte[strText.Length];
            for (int intIndex = 0; intIndex <= bytBuffer.Length - 1; intIndex++)
            {
                bytBuffer[intIndex] = Convert.ToByte(Strings.Asc(strText.Substring(intIndex, 1)));
            }
            return bytBuffer;
        }
        //GetBytes

        // Function to Get ASCII string from a byte array
        public static string GetString(byte[] bytBuffer, int intFirst = 0, int intLast = -1)
        {
            // Converts a byte array to a text string...

            if (intFirst > bytBuffer.GetUpperBound(0))
                return "";
            if (intLast == -1 | intLast > bytBuffer.GetUpperBound(0))
                intLast = bytBuffer.GetUpperBound(0);

            StringBuilder sbdInput = new StringBuilder();
            for (int intIndex = intFirst; intIndex <= intLast; intIndex++)
            {
                byte bytSingle = bytBuffer[intIndex];
                if (bytSingle != 0)
                    sbdInput.Append(Strings.Chr(bytSingle));
            }
            return sbdInput.ToString();
        }
        //GetString

        // Function to generate a UTC Timestamp string "yyyy/MM/dd HH:mm"
        public static string Timestamp()
        {
            // This function returns the current time/date in 
            // 2004/08/24 05:33 format string...
            return Strings.Format(System.DateTime.UtcNow, "yyyy/MM/dd HH:mm");
        }
        //Timestamp

        // Function to generate an Extended UTC Timestamp string "yyyy/MM/dd HH:mm:ss"
        public static string TimestampEx()
        {
            // This function returns the current time/date in 
            // 2004/08/24 05:33:12 format string...
            return Strings.Format(DateTime.UtcNow, "yyyy/MM/dd HH:mm:ss");
        }

        // Function to compare two Byte Arrays
        public static bool CompareByteArrays(byte[] ary1, byte[] ary2)
        {
            if ((ary1 == null) & (ary2 == null))
                return true;
            if ((ary1 == null) | (ary2 == null))
                return false;
            if (ary1.Length != ary2.Length)
                return false;
            for (int i = 0; i <= ary1.Length - 1; i++)
            {
                if (ary1[i] != ary2[i])
                    return false;
            }
            return true;
        }
        //CompareByteArrays

        // Subroutine to compute a 8 bit CRC value and append it to the Data...
        public static void GenCRC8(ref byte[] Data)
        {
            // For  CRC-8-CCITT =    x^8 + x^7 +x^3 + x^2 + 1  intPoly = 1021 Init FFFF

            int intPoly = 0xc6;
            // This implements the CRC polynomial  x^8 + x^7 +x^3 + x^2 + 1
            Int32 intRegister = 0xff;

            foreach (byte bytSingle in Data)
            {
                // for each bit processing MS bit first
                for (int i = 7; i >= 0; i += -1)
                {
                    bool blnBit = (bytSingle & Convert.ToByte(Math.Pow(2, i))) != 0;
                    // the MSB of the register is set
                    if ((intRegister & 0x80) == 0x80)
                    {
                        // Shift left, place data bit as LSB, then divide
                        // Register := shiftRegister left shift 1
                        // Register := shiftRegister xor polynomial
                        if (blnBit)
                        {
                            intRegister = 0xff & (1 + 2 * intRegister);
                        }
                        else
                        {
                            intRegister = 0xff & (2 * intRegister);
                        }
                        intRegister = intRegister ^ intPoly;
                        // the MSB is not set
                    }
                    else
                    {
                        // Register is not divisible by polynomial yet.
                        // Just shift left and bring current data bit onto LSB of shiftRegister
                        if (blnBit)
                        {
                            intRegister = 0xff & (1 + 2 * intRegister);
                        }
                        else
                        {
                            intRegister = 0xff & (2 * intRegister);
                        }
                    }
                }
            }
            Array.Resize(ref Data, Data.Length + 1);
            //make the Data array one byte larger
            Data[Data.Length - 1] = Convert.ToByte(intRegister & 0xff);
            // LS 8 bits of Register
        }
        //GenCRC8


        // Subroutine to compute a 16 bit CRC value and append it to the Data...
        public static void GenCRC16(ref byte[] Data, Int32 intStartIndex, Int32 intStopIndex)
        {
            // For  CRC-16-CCITT =    x^16 + x^12 +x^5 + 1  intPoly = 1021 Init FFFF
            // intSeed is the seed value for the shift register and must be in the range 0-&HFFFF

            int intPoly = 0x8810;
            // This implements the CRC polynomial  x^16 + x^12 +x^5 + 1
            Int32 intRegister = 0xffff;
            //initialize the register to all 1's 

            for (int j = intStartIndex; j <= intStopIndex; j++)
            {
                // for each bit processing MS bit first
                for (int i = 7; i >= 0; i += -1)
                {
                    bool blnBit = (Data[j] & Convert.ToByte(Math.Pow(2, i))) != 0;
                    // the MSB of the register is set
                    if ((intRegister & 0x8000) == 0x8000)
                    {
                        // Shift left, place data bit as LSB, then divide
                        // Register := shiftRegister left shift 1
                        // Register := shiftRegister xor polynomial
                        if (blnBit)
                        {
                            intRegister = 0xffff & (1 + 2 * intRegister);
                        }
                        else
                        {
                            intRegister = 0xffff & (2 * intRegister);
                        }
                        intRegister = intRegister ^ intPoly;
                        // the MSB is not set
                    }
                    else
                    {
                        // Register is not divisible by polynomial yet.
                        // Just shift left and bring current data bit onto LSB of shiftRegister
                        if (blnBit)
                        {
                            intRegister = 0xffff & (1 + 2 * intRegister);
                        }
                        else
                        {
                            intRegister = 0xffff & (2 * intRegister);
                        }
                    }
                }
            }
            // Put the two CRC bytes after the stop index
            Data[intStopIndex + 1] = Convert.ToByte((intRegister & 0xff00) / 256);
            // MS 8 bits of Register
            Data[intStopIndex + 2] = Convert.ToByte(intRegister & 0xff);
            // LS 8 bits of Register
        }
        //GenCRC16

        // Function to compute a 16 bit CRC value and check it against the last 2 bytes of Data (the CRC) ..
        public static bool CheckCRC16(ref byte[] Data)
        {
            // Returns True if CRC matches, else False
            // For  CRC-16-CCITT =    x^16 + x^12 +x^5 + 1  intPoly = 1021 Init FFFF
            // intSeed is the seed value for the shift register and must be in the range 0-&HFFFF

            int intPoly = 0x8810;
            // This implements the CRC polynomial  x^16 + x^12 +x^5 + 1
            Int32 intRegister = 0xffff;
            // initialize the register to all 1's

            // 2 bytes short of data length
            for (int j = 0; j <= Data.Length - 3; j++)
            {
                // for each bit processing MS bit first
                for (int i = 7; i >= 0; i += -1)
                {
                    bool blnBit = (Data[j] & Convert.ToByte(Math.Pow(2, i))) != 0;
                    // the MSB of the register is set
                    if ((intRegister & 0x8000) == 0x8000)
                    {
                        // Shift left, place data bit as LSB, then divide
                        // Register := shiftRegister left shift 1
                        // Register := shiftRegister xor polynomial
                        if (blnBit)
                        {
                            intRegister = 0xffff & (1 + 2 * intRegister);
                        }
                        else
                        {
                            intRegister = 0xffff & (2 * intRegister);
                        }
                        intRegister = intRegister ^ intPoly;
                        // the MSB is not set
                    }
                    else
                    {
                        // Register is not divisible by polynomial yet.
                        // Just shift left and bring current data bit onto LSB of shiftRegister
                        if (blnBit)
                        {
                            intRegister = 0xffff & (1 + 2 * intRegister);
                        }
                        else
                        {
                            intRegister = 0xffff & (2 * intRegister);
                        }
                    }
                }
            }

            // Compare the register with the last two bytes of Data (the CRC) 
            if (Data[Data.Length - 2] == Convert.ToByte((intRegister & 0xff00) / 256))
            {
                if (Data[Data.Length - 1] == Convert.ToByte(intRegister & 0xff))
                {
                    return true;
                }
            }
            return false;
        }
        //CheckCRC16

        // Subroutine to Update FSK Frame Decoding Stats
        public static void UpdateFSKFrameDecodeStats(bool blnDecodeSuccess)
        {
            if (!MCB.AccumulateStats)
                return;
            if (blnDecodeSuccess)
            {
                stcTuningStats.intGoodFSKFrameTypes += 1;
            }
            else
            {
                stcTuningStats.intFailedFSKFrameTypes += 1;
            }
        }
        //UpdateFSKFrameDecodeStats

        public static string[] EnumeratePlaybackDevices()
	{
		// Get the Windows enumerated Playback devices adding a "-n" tag (-1, -2, etc.)  if any duplicate names 
		Microsoft.DirectX.DirectSound.DevicesCollection cllPlaybackDevices = new Microsoft.DirectX.DirectSound.DevicesCollection();
		//DeviceInformation objDI = new DeviceInformation();
		int intCtr = 0;
		int intDupeDeviceCnt = 0;

		string[] strPlaybackDevices = new string[cllPlaybackDevices.Count];
		foreach (DeviceInformation objDI in cllPlaybackDevices) {
			DeviceDescription objDD = new DeviceDescription(objDI);
			strPlaybackDevices[intCtr] = objDD.ToString().Trim();
			intCtr += 1;
		}

		for (int i = 0; i <= strPlaybackDevices.Length - 1; i++) {
			intDupeDeviceCnt = 1;
			for (int j = i + 1; j <= strPlaybackDevices.Length - 1; j++) {
				if (strPlaybackDevices[j] == strPlaybackDevices[i]) {
					intDupeDeviceCnt += 1;
					strPlaybackDevices[j] = strPlaybackDevices[i] + "-" + intDupeDeviceCnt.ToString();
				}
			}
		}
		return strPlaybackDevices;
	}

        public static string[] EnumerateCaptureDevices()
	{
		// Get the Windows enumerated Capture devices adding a "-n" tag (-1, -2, etc.) if any duplicate names 
		CaptureDevicesCollection cllCaptureDevices = new CaptureDevicesCollection();
		//DeviceInformation objDI = new DeviceInformation();
		int intCtr = 0;
		int intDupeDeviceCnt = 0;

		string[] strCaptureDevices = new string[cllCaptureDevices.Count];
		foreach (DeviceInformation objDI in cllCaptureDevices) {
			DeviceDescription objDD = new DeviceDescription(objDI);
			strCaptureDevices[intCtr] = objDD.ToString().Trim();
			intCtr += 1;
		}

		for (int i = 0; i <= strCaptureDevices.Length - 1; i++) {
			intDupeDeviceCnt = 1;
			for (int j = i + 1; j <= strCaptureDevices.Length - 1; j++) {
				if (strCaptureDevices[j] == strCaptureDevices[i]) {
					intDupeDeviceCnt += 1;
					strCaptureDevices[j] = strCaptureDevices[i] + "-" + intDupeDeviceCnt.ToString();
				}
			}
		}
		return strCaptureDevices;
	}

        public static byte[] ASCIIto6Bit(byte[] bytASCII)
        {
            // Input must be 8 bytes which will convert to 6 bytes of packed 6 bit characters and
            // inputs must be the ASCII character set values from 32 to 95....
            Int64 intSum = default(Int64);
            Int64 intMask = default(Int64);
            byte[] bytReturn = new byte[6];
            for (int i = 0; i <= 3; i++)
            {
                intSum = 64 * intSum + bytASCII[i] - 32;
            }
            for (int j = 0; j <= 2; j++)
            {
                intMask = Convert.ToInt32(255 * (Math.Pow(256, (2 - j))));
                bytReturn[j] = Convert.ToByte((intSum & intMask) / Convert.ToInt32((Math.Pow(256, (2 - j)))));
            }
            intSum = 0;
            for (int i = 0; i <= 3; i++)
            {
                intSum = 64 * intSum + bytASCII[i + 4] - 32;
            }
            for (int j = 0; j <= 2; j++)
            {
                intMask = Convert.ToInt32(255 * (Math.Pow(256, (2 - j))));
                bytReturn[j + 3] = Convert.ToByte((intSum & intMask) / Convert.ToInt32((Math.Pow(256, (2 - j)))));
            }
            return bytReturn;
        }

        public static byte[] Bit6ToASCII(byte[] byt6Bit)
        {
            // Input must be 6 bytes which represent packed 6 bit characters that well 
            // result will be 8 ASCII character set values from 32 to 95...

            Int64 intSum = default(Int64);
            Int64 intMask = default(Int64);
            byte[] bytReturn = new byte[8];

            for (int i = 0; i <= 2; i++)
            {
                intSum = 256 * intSum + byt6Bit[i];
            }
            for (int j = 0; j <= 3; j++)
            {
                intMask = Convert.ToInt32(63 * (Math.Pow(64, (3 - j))));
                bytReturn[j] = Convert.ToByte(32 + (intSum & intMask) / Convert.ToInt32((Math.Pow(64, (3 - j)))));
            }
            for (int i = 0; i <= 2; i++)
            {
                intSum = 256 * intSum + byt6Bit[i + 3];
            }
            for (int j = 0; j <= 3; j++)
            {
                intMask = Convert.ToInt32(63 * (Math.Pow(64, (3 - j))));
                bytReturn[j + 4] = Convert.ToByte(32 + (intSum & intMask) / Convert.ToInt32((Math.Pow(64, (3 - j)))));
            }
            return bytReturn;
        }

        public static bool CheckValidCallsignSyntax(string strCallsign)
        {
            int intDashIndex = strCallsign.IndexOf("-");
            // no -SSID
            if (intDashIndex == -1)
            {
                if (strCallsign.Trim().Length > 7 | strCallsign.Trim().Length < 3)
                    return false;
            }
            else
            {
                string strCallNoSSID = strCallsign.Substring(0, intDashIndex).Trim();
                if (strCallNoSSID.Length > 7 | strCallNoSSID.Length < 3)
                    return false;
                string strSSID = strCallsign.Substring(intDashIndex + 1).Trim();
                if (strSSID.Length != 1)
                    return false;
            }
            return true;
        }

        public static byte[] CompressCallsign(string strCallsign)
        {
            if (!CheckValidCallsignSyntax(strCallsign))
                return null;
            int intDashIndex = strCallsign.IndexOf("-");
            // if No SSID
            if (intDashIndex == -1)
            {
                strCallsign = strCallsign.PadRight(7).ToUpper() + "0";
                return ASCIIto6Bit(GetBytes(strCallsign));
                // compress to 6 bit  6 bytes total
            }
            else
            {
                strCallsign = strCallsign.Substring(0, intDashIndex).ToUpper().PadRight(7).ToUpper() + strCallsign.Substring(intDashIndex + 1).Trim().ToUpper();
                byte[] bytReturn = ASCIIto6Bit(GetBytes(strCallsign));
                // compress to 6 bit ascii (upper case)
                return bytReturn;
                // 6 bytes total
            }
        }

        public static string DeCompressCallsign(byte[] bytCallsign)
        {
            if (bytCallsign.Length != 6)
                return "";
            byte[] bytTest = Bit6ToASCII(bytCallsign);
            // Value of "0" so No SSID
            if (bytTest[7] == 48)
            {
                Array.Resize(ref bytTest, 7);
                return GetString(bytTest).Trim();
            }
            else
            {
                string strWithSSID = GetString(bytTest);
                return strWithSSID.Substring(0, 7).Trim() + "-" + strWithSSID.Substring(strWithSSID.Length - 1);
            }
        }
        #endregion

	}
}