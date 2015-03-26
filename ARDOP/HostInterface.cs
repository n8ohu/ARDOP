using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Text;
using System.Timers;
using nsoftware.IPWorks;
using System.Windows.Forms;




namespace ARDOP
{
    public class HostInterface
    {
        // This class provides the TNC side of the TNC Host interface.  I
        // It is intended to work with TCPIP, SERIAL, or BLUETOOTH interfaces although currently on TCPIP is fully implemented. 
        // A similar (though not exactly the same) class is used in the Host to establish the Host to TNC interface. 

        #region "Declarations"

        private Ipdaemon withEventsField_objTCPIP = new Ipdaemon();
        private Ipdaemon objTCPIP
        {
            get { return withEventsField_objTCPIP; }
            set
            {
                if (withEventsField_objTCPIP != null)
                {
                    withEventsField_objTCPIP.OnConnected -= objTCPIP_OnConnected;
                    withEventsField_objTCPIP.OnDataIn -= objTCPIP_OnDataIn;
                    withEventsField_objTCPIP.OnDisconnected -= objTCPIP_OnDisconnected;
                    withEventsField_objTCPIP.OnError -= objTCPIP_OnError;
                }
                withEventsField_objTCPIP = value;
                if (withEventsField_objTCPIP != null)
                {
                    withEventsField_objTCPIP.OnConnected += objTCPIP_OnConnected;
                    withEventsField_objTCPIP.OnDataIn += objTCPIP_OnDataIn;
                    withEventsField_objTCPIP.OnDisconnected += objTCPIP_OnDisconnected;
                    withEventsField_objTCPIP.OnError += objTCPIP_OnError;
                }
            }
        }
        private SerialPort objSerial = new SerialPort();
        private System.Timers.Timer withEventsField_tmrPollQueue = new System.Timers.Timer();
        private System.Timers.Timer tmrPollQueue
        {
            get { return withEventsField_tmrPollQueue; }
            set
            {
                if (withEventsField_tmrPollQueue != null)
                {
                    withEventsField_tmrPollQueue.Elapsed -= tmrPollQueue_Elapsed;
                }
                withEventsField_tmrPollQueue = value;
                if (withEventsField_tmrPollQueue != null)
                {
                    withEventsField_tmrPollQueue.Elapsed += tmrPollQueue_Elapsed;
                }
            }

        }
        //  Objects

        private Main objMain;
        // Private WithEvents objBlueTooth As New 

        // Strings
        private string strTCPIPConnectionID;
        private string strInterfaceType = "";
        private string strTCPIPAddress;
        private string strSerialPort;

        private string strBlueToothPairing;
        // Integers
        private Int32 intHostIBData_CmdPtr = 0;
        private Int32 intTCPIPPort;

        private Int32 intSerialBaud;
        //Booleans 

        private bool blnProcessingCmdData = false;
        //Arrays
        private byte[] bytHostIBData_CmdBuffer = new byte[-1 + 1];
        private byte[] bytToSend;
        string[] strAllDataModes = {
		"4FSK.200.50S",
		"4FSK.200.50",
		"4PSK.200.100S",
		"4PSK.200.100",
		"8PSK.200.100",
		"4FSK.500.100S",
		"4FSK.500.100",
		"4PSK.500.100",
		"8PSK.500.100",
		"4PSK.500.167",
		"8PSK.500.167",
		"4FSK.1000.100",
		"4PSK.1000.100",
		"8PSK.1000.100",
		"4PSK.1000.167",
		"8PSK.1000.167",
		"4FSK.2000.100\"4PSK.2000.100",
		"8PSK.2000.100",
		"4PSK.2000.167",
		"8PSK.2000.167"

	};
        private string[] strARQBW = {
		"200MAX",
		"500MAX",
		"1000MAX",
		"2000MAX",
		"200FORCED",
		"500FORCED",
		"1000FORCED",
		"2000FORCED"
		#endregion
	};

        //Queues
        //a queue of byte arrays waiting to be sent to host
        private Queue queDataForHost = Queue.Synchronized(new Queue());


        //#Region "Public Properties"

        //Public ReadOnly Property InterfaceType() As String
        //    Get
        //        Return strInterfaceType
        //    End Get
        //End Property

        //Public ReadOnly Property IsHostRDY() As Boolean
        //    Get
        //        Return blnHostRDY
        //    End Get
        //End Property

        //Public ReadOnly Property Connected() As Boolean
        //    Get
        //        Return strTCPIPConnectionID <> ""
        //    End Get
        //End Property

        //#End Region

        #region "Public Subs/Functions"
        // Subroutine to set TCPIP Address, port# and interface type
        public void TCPIPProperties(string strAddress, Int32 intPort)
        {
            strTCPIPAddress = strAddress;
            intTCPIPPort = intPort;
            strInterfaceType = "TCPIP";
        }

        // Subroutine to set Serial COM port, baud and interface type
        public void SerialProperties(string strComPort, Int32 intBaud)
        {
            strSerialPort = strComPort;
            intSerialBaud = intBaud;
            strInterfaceType = "SERIAL";
        }

        // Subroutine to set BlueTooth pairing and interface type
        public void BluetoothProperties(string strPairing)
        {
            strBlueToothPairing = strPairing;
            strInterfaceType = "BLUETOOTH";
        }

        // Enable a TCPIP, Serial,  or BlueTooth Link with the Host (Opens the port for listening...Does NOT initiate the connection!) 
        public bool EnableHostLink()
        {
            Globals.Status stcStatus = null;
            Globals.MCB.LinkedToHost = false;
            stcStatus.BackColor = Color.Khaki;
            // preset to yellow ....connection switches to green.
            stcStatus.ControlName = "lblHost";
            if (strInterfaceType == "TCPIP")
            {
                stcStatus.Text = "TCPIP";
                try
                {
                    if ((objTCPIP != null))
                    {
                        objTCPIP.Listening = false;
                        objTCPIP.Shutdown();
                        objTCPIP.Dispose();
                        objTCPIP = null;
                        objTCPIP = new Ipdaemon();
                    }
                    strTCPIPConnectionID = "";
                    objTCPIP.LocalPort = intTCPIPPort;
                    objTCPIP.LocalHost = strTCPIPAddress;
                    objTCPIP.Listening = true;
                    Globals.queTNCStatus.Enqueue(stcStatus);
                    // blnHostRDY = True
                    Logs.WriteDebug("[HostInterface.EnableHostLink] objTCPIP.Listening = " + objTCPIP.Listening.ToString() + " on " + strTCPIPAddress + " Port:" + intTCPIPPort.ToString());
                }
                catch (Exception Ex)
                {
                    Logs.Exception("[HostInterface.EnableHostLink]  Err: " + Ex.ToString());
                    stcStatus.BackColor = Color.LightSalmon;
                    // set to red ....
                    Globals.queTNCStatus.Enqueue(stcStatus);
                    return false;
                }
                bytHostIBData_CmdBuffer = null;
                intHostIBData_CmdPtr = 0;
                return true;
                // incomplete 
            }
            else if (strInterfaceType == "SERIAL")
            {
                stcStatus.Text = "Host Serial";
            }
            else if (strInterfaceType == "BLUETOOTH")
            {
                stcStatus.Text = "Host BlueTooth";
                // BlueTooth link initialization here????
                return true;
            }
            return false;
        }

        // Function to send a text command to the Host
        private bool SendCommandToHost(string strText)
        {
            //  This is from TNC side as identified by the leading "c:"   (Host side sends "C:")
            // Subroutine to send a line of text (terminated with <Cr>) on the command port... All commands beging with "c:" and end with <Cr>
            // A two byte CRC appended following the <Cr>
            // The strText cannot contain a "c:" sequence or a <Cr>
            // Returns TRUE if command sent successfully.
            // Form byte array to send with CRC
            // TODO:  Complete for Serial and BlueTooth

            byte[] bytToSend = GetBytes("c:" + strText.Trim().ToUpper() + Constants.vbCr);
            Array.Resize(ref bytToSend, bytToSend.Length + 2);
            // resize 2 bytes larger for CRC
            GenCRC16(ref bytToSend, 2, bytToSend.Length - 3, 0xffff);
            // Generate CRC starting after "c:"  

            if (strInterfaceType == "TCPIP")
            {
                if (objTCPIP == null)
                    return false;
                if (string.IsNullOrEmpty(strTCPIPConnectionID))
                    return false;
                try
                {
                    objTCPIP.Send(strTCPIPConnectionID, bytToSend);
                    if (Globals.MCB.CommandTrace)
                        Logs.WriteDebug("[HostInterface.SendCommandToHost] Command Trace to Host  c:" + strText.Trim().ToUpper());
                    return true;
                }
                catch
                {
                    //Logs.Exception("[HostInterface.SendCommandToHost]  c:" + strText + "  Err:" + Err.Number.ToString() + " " + Err.Description);
                    return false;
                }

            }
            else if (strInterfaceType == "SERIAL")
            {
            }
            else if (strInterfaceType == "BLUETOOTH")
            {
                // This will handle BlueTooth connections ... TODO: Add BlueTooth
                return false;
                // Temporary
            }
            else
            {
                Logs.Exception("[HostInterface.SendCommandToHost]  No TCPIP, serial,  or BlueTooth parameters");
            }
            return false;
        }

        // Function to queue a text command to the Host
        public void QueueCommandToHost(string strText)
        {
            //  This is from TNC side as identified by the leading "c:"   (Host side sends "C:")
            // A two byte CRC appended following the <Cr>
            // The strText cannot contain a "c:" sequence or a <Cr>
            // Returns TRUE if command sent successfully.
            // Form byte array to send with CRC


            byte[] bytToSend = GetBytes("c:" + strText.Trim().ToUpper() + Constants.vbCr);
            Array.Resize(ref bytToSend, bytToSend.Length + 2);
            // resize 2 bytes larger for CRC
            GenCRC16(ref bytToSend, 2, bytToSend.Length - 3, 0xffff);
            // Generate CRC starting after "c:"  
            queDataForHost.Enqueue(bytToSend);
        }


        // Function to send byte array to the host with Data type tag 
        public void QueueDataToHost(byte[] bytData)
        {
            //  This is from TNC side as identified by the leading "d:"   (Host side sends data with leading  "D:")
            // includes 16 bit CRC check on Data Len + Data (does not CRC the leading "d:")
            // bytData contains a "tag" in its leading 3 bytes: "ARQ", "FEC" or "ERR" which are examined and stripped by Host (optionally used by host for display)
            // Max data size should be 2000 bytes or less for timing purposes
            // Returns TRUE if data sent successfully.

            byte[] bytToSend = new byte[2 + 2 + bytData.Length + 2];
            // add bytes for: "d:", 2 byte data count,  and 2 byte CRC
            Array.Copy(bytData, 0, bytToSend, 4, bytData.Length);
            bytToSend[0] = 0x64;
            //  "d" indicates data from TNC
            bytToSend[1] = 0x3a;
            // ":"
            bytToSend[2] = ((bytData.Length) & 0xff00) >> 8;
            // MS byte of count  (Includes strDataType but does not include the two trailing CRC bytes)
            bytToSend[3] = ((bytData.Length) & 0xff);
            //LS byte of count

            // Compute the CRC starting with the bytCount + Data tag + Data (skipping over the "d:")
            GenCRC16(ref bytToSend, 2, bytToSend.Length - 3, 0xffff);
            queDataForHost.Enqueue(bytToSend);


            //If strInterfaceType = "TCPIP" Then
            //    If objTCPIP Is Nothing Then Return False
            //    If strTCPIPConnectionID = "" Then Return False
            //    Dim dttStartWaitForReady As Date = Now
            //    While Now.Subtract(dttStartWaitForReady).TotalMilliseconds < 1000 And (Not blnHostRDY)
            //        Thread.Sleep(20)
            //    End While
            //    If Not blnHostRDY Then Logs.Exception("[HostInterface.SendDataToHost] Err: 1000 msec timeout waiting for RDY. ")
            //    Try
            //        objTCPIP.Send(strTCPIPConnectionID, bytToSend)
            //        If MCB.CommandTrace Then Logs.WriteDebug("[HostInterfaceSendDataToHost] Data Trace to Host: " & bytData.Length.ToString & " bytes")
            //        ' blnRDY = True
            //        Return True
            //    Catch Ex As Exception  ' May need to accomodate a "would block" error here.
            //        Logs.Exception("[HostInterface.SendDataToHost]  Err:" & Ex.ToString)
            //        'blnRDY = True
            //        Return False
            //    End Try
            //ElseIf strInterfaceType = "SERIAL" Then
            //    Return False ' Temporary
            //ElseIf strInterfaceType = "BLUETOOTH" Then
            //    ' This will handle BlueTooth connections ... TODO: Add BlueTooth
            //    Return False ' Temporary
            //Else
            //    Logs.Exception("[HostInterface.SendDataToHost]  No TCPIP, serial,  or BlueTooth parameters")
            //End If
            //Return False
        }

        // Function to terminate the Host link
        public bool TerminateHostLink()
        {
            Globals.Status stcStatus = null;
            Globals.MCB.LinkedToHost = false;
            stcStatus.BackColor = Color.DarkGray;
            stcStatus.ControlName = "lblHost";

            if (strInterfaceType == "TCPIP")
            {
                try
                {
                    if ((objTCPIP != null))
                    {
                        objTCPIP.Shutdown();
                        objTCPIP.Dispose();
                        objTCPIP = null;
                    }
                    strTCPIPConnectionID = "";
                    Globals.queTNCStatus.Enqueue(stcStatus);
                }
                catch (Exception Ex)
                {
                    Logs.Exception("[HostInterface.TerminateHostLink]  Err: " + Ex.ToString());
                    stcStatus.BackColor = Color.LightSalmon;
                    // set to red ....
                    Globals.queTNCStatus.Enqueue(stcStatus);
                    return false;
                }
                return true;

            }
            else if (strInterfaceType == "SERIAL")
            {
            }
            else if (strInterfaceType == "BLUETOOTH")
            {
            }
            else
            {
                Logs.Exception("[HostInterface.TerminateHostLink]  Interface Type not set");
            }
            return false;
        }

        // Subroutine to establish pointer back to main calling program
        public HostInterface(Main objM)
        {
            objMain = objM;
            tmrPollQueue.Interval = 100;
            tmrPollQueue.Start();
        }
        #endregion


        #region "Private Subs/Functions"

        private void objTCPIP_OnConnected(object sender, nsoftware.IPWorks.IpdaemonConnectedEventArgs e)
        {
            Globals.Status stcStatus = null;
            stcStatus.BackColor = Color.LightGreen;
            stcStatus.ControlName = "lblHost";
            stcStatus.Text = "TCPIP on port " + objTCPIP.LocalPort.ToString();

            if (string.IsNullOrEmpty(strTCPIPConnectionID))
            {
                strTCPIPConnectionID = e.ConnectionId;
                if (Globals.MCB.CommandTrace)
                    Logs.WriteDebug("[HostInterface.objTCPIP_OnConnected] Connected to host with ID=" + strTCPIPConnectionID);
                if (SendCommandToHost("RDY"))
                {
                    Globals.queTNCStatus.Enqueue(stcStatus);
                    strTCPIPConnectionID = e.ConnectionId;
                    Globals.MCB.LinkedToHost = true;
                    bytHostIBData_CmdBuffer = new byte[-1 + 1];
                    intHostIBData_CmdPtr = 0;
                }
                else
                {
                    Logs.Exception("[HostInterface.objTCPIP_OnConnected] Failure to send c:RDY reply on ConnectionId " + e.ConnectionId);
                    objTCPIP.Disconnect(e.ConnectionId);
                    Globals.MCB.LinkedToHost = false;
                    strTCPIPConnectionID = "";
                }
            }
            else
            {
                Logs.Exception("[HostInterface.objTCPIP_OnConnected] Connection request received while already connected. Reject connection.");
                objTCPIP.Disconnect(e.ConnectionId);
            }
        }
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_objTCPIP_OnDataIn_strCommandFromHost_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();



        // This creates a first-in first-out buffer and pointers to handle receiving commands or data.
        // Data may be received on NON CMD or Data frame boundaries. (intended to handle buffer and latency issues)

        // Needs addition of storing last data or command to be able to repeat in case response from Host was "CRCFAULT"

        string static_objTCPIP_OnDataIn_strCommandFromHost;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_objTCPIP_OnDataIn_bytDataFromHost_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        byte[] static_objTCPIP_OnDataIn_bytDataFromHost;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_objTCPIP_OnDataIn_intDataBytesToReceive_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        Int32 static_objTCPIP_OnDataIn_intDataBytesToReceive;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_objTCPIP_OnDataIn_blnReceivingCMD_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        bool static_objTCPIP_OnDataIn_blnReceivingCMD;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_objTCPIP_OnDataIn_blnReceivingData_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        bool static_objTCPIP_OnDataIn_blnReceivingData;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_objTCPIP_OnDataIn_intDataBytePtr_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        Int32 static_objTCPIP_OnDataIn_intDataBytePtr;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_objTCPIP_OnDataIn_intCMDStartPtr_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        Int32 static_objTCPIP_OnDataIn_intCMDStartPtr;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_objTCPIP_OnDataIn_intDataStartPtr_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        Int32 static_objTCPIP_OnDataIn_intDataStartPtr;
        private void objTCPIP_OnDataIn(object sender, nsoftware.IPWorks.IpdaemonDataInEventArgs e)
        {
            lock (static_objTCPIP_OnDataIn_strCommandFromHost_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_objTCPIP_OnDataIn_strCommandFromHost_Init))
                    {
                        static_objTCPIP_OnDataIn_strCommandFromHost = "";
                    }
                }
                finally
                {
                    static_objTCPIP_OnDataIn_strCommandFromHost_Init.State = 1;
                }
            }
            lock (static_objTCPIP_OnDataIn_bytDataFromHost_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_objTCPIP_OnDataIn_bytDataFromHost_Init))
                    {
                        static_objTCPIP_OnDataIn_bytDataFromHost = null;
                    }
                }
                finally
                {
                    static_objTCPIP_OnDataIn_bytDataFromHost_Init.State = 1;
                }
            }
            lock (static_objTCPIP_OnDataIn_intDataBytesToReceive_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_objTCPIP_OnDataIn_intDataBytesToReceive_Init))
                    {
                        static_objTCPIP_OnDataIn_intDataBytesToReceive = 0;
                    }
                }
                finally
                {
                    static_objTCPIP_OnDataIn_intDataBytesToReceive_Init.State = 1;
                }
            }
            lock (static_objTCPIP_OnDataIn_blnReceivingCMD_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_objTCPIP_OnDataIn_blnReceivingCMD_Init))
                    {
                        static_objTCPIP_OnDataIn_blnReceivingCMD = false;
                    }
                }
                finally
                {
                    static_objTCPIP_OnDataIn_blnReceivingCMD_Init.State = 1;
                }
            }
            lock (static_objTCPIP_OnDataIn_blnReceivingData_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_objTCPIP_OnDataIn_blnReceivingData_Init))
                    {
                        static_objTCPIP_OnDataIn_blnReceivingData = false;
                    }
                }
                finally
                {
                    static_objTCPIP_OnDataIn_blnReceivingData_Init.State = 1;
                }
            }
            lock (static_objTCPIP_OnDataIn_intDataBytePtr_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_objTCPIP_OnDataIn_intDataBytePtr_Init))
                    {
                        static_objTCPIP_OnDataIn_intDataBytePtr = 0;
                    }
                }
                finally
                {
                    static_objTCPIP_OnDataIn_intDataBytePtr_Init.State = 1;
                }
            }
            lock (static_objTCPIP_OnDataIn_intCMDStartPtr_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_objTCPIP_OnDataIn_intCMDStartPtr_Init))
                    {
                        static_objTCPIP_OnDataIn_intCMDStartPtr = 0;
                    }
                }
                finally
                {
                    static_objTCPIP_OnDataIn_intCMDStartPtr_Init.State = 1;
                }
            }
            lock (static_objTCPIP_OnDataIn_intDataStartPtr_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_objTCPIP_OnDataIn_intDataStartPtr_Init))
                    {
                        static_objTCPIP_OnDataIn_intDataStartPtr = 0;
                    }
                }
                finally
                {
                    static_objTCPIP_OnDataIn_intDataStartPtr_Init.State = 1;
                }
            }

            AppendDataToBuffer(e.TextB, ref bytHostIBData_CmdBuffer);
        SearchForStart:

            // look for start of Command ("C:")  or Data (D:") and establish start pointer (Capital C or D indicates from Host)
            if (!(static_objTCPIP_OnDataIn_blnReceivingCMD | static_objTCPIP_OnDataIn_blnReceivingData))
            {
                for (int i = intHostIBData_CmdPtr; i <= bytHostIBData_CmdBuffer.Length - 2; i++)
                {
                    // search for ASCII "C:"
                    if (bytHostIBData_CmdBuffer[i] == 0x43 & bytHostIBData_CmdBuffer[i + 1] == 0x3a)
                    {
                        // start of command.
                        static_objTCPIP_OnDataIn_intCMDStartPtr = i;
                        static_objTCPIP_OnDataIn_blnReceivingCMD = true;
                        blnProcessingCmdData = true;
                        static_objTCPIP_OnDataIn_blnReceivingData = false;
                        break; // TODO: might not be correct. Was : Exit For
                        // search for ASCII "D:"
                    }
                    else if (bytHostIBData_CmdBuffer[i] == 0x44 & bytHostIBData_CmdBuffer[i + 1] == 0x3a)
                    {
                        // start of Data
                        static_objTCPIP_OnDataIn_intDataStartPtr = i;
                        static_objTCPIP_OnDataIn_blnReceivingCMD = false;
                        static_objTCPIP_OnDataIn_blnReceivingData = true;
                        blnProcessingCmdData = true;
                        static_objTCPIP_OnDataIn_intDataBytesToReceive = 0;
                        //If MCB.CommandTrace Then Logs.WriteDebug("[HostInterface.objTCPIP.OnDataIn] Data Trace from host")
                        break; // TODO: might not be correct. Was : Exit For
                    }
                }
            }

            if (static_objTCPIP_OnDataIn_blnReceivingCMD)
            {
                // Look for <Cr> with room for 2 byte CRC
                for (int i = static_objTCPIP_OnDataIn_intCMDStartPtr; i <= bytHostIBData_CmdBuffer.Length - 3; i++)
                {
                    // search for Carriage Return which signals the end of a Command (note 2 CRC bytes to follow)
                    if (bytHostIBData_CmdBuffer[i] == 0xd)
                    {
                        byte[] bytCmd = new byte[i - static_objTCPIP_OnDataIn_intCMDStartPtr + 1];
                        // 2 bytes added for CRC, and "C:" skipped
                        Array.Copy(bytHostIBData_CmdBuffer, static_objTCPIP_OnDataIn_intCMDStartPtr + 2, bytCmd, 0, bytCmd.Length);
                        //copy over the Command (less :C:") and the 2 byte CRC
                        // check the CRC
                        if (CheckCRC16(ref bytCmd, 0xffff))
                        {
                            //CRC OK:
                            Array.Resize(ref bytCmd, bytCmd.Length - 2);
                            // Drop off the CRC
                            static_objTCPIP_OnDataIn_strCommandFromHost = GetString(bytCmd).ToUpper().Trim();
                            if (Globals.MCB.CommandTrace)
                                Logs.WriteDebug("[HostInterface.objTCPIP.OnDataIn] Command Trace from host: C:" + static_objTCPIP_OnDataIn_strCommandFromHost);
                            // Process the received and CRC checked command here:
                            //host can receive commands or data
                            if (static_objTCPIP_OnDataIn_strCommandFromHost != "RDY")
                            {
                                ProcessCommandFromHost(static_objTCPIP_OnDataIn_strCommandFromHost);
                                //(sends reply or Fault + RDY)
                            }
                        }
                        else
                        {
                            SendCommandToHost("CRCFAULT");
                            // indicates to Host to repeat the command
                            SendCommandToHost("RDY");
                        }
                        // resize buffer and reset pointer
                        blnProcessingCmdData = false;
                        static_objTCPIP_OnDataIn_blnReceivingCMD = false;
                        // resize the buffer, and set pointer to it's start.
                        byte[] bytTemp = new byte[bytHostIBData_CmdBuffer.Length - i - 3];
                        //skip past the 2 byte CRC
                        if (bytTemp.Length > 0)
                            Array.Copy(bytHostIBData_CmdBuffer, i + 3, bytTemp, 0, bytTemp.Length);
                        bytHostIBData_CmdBuffer = bytTemp;
                        intHostIBData_CmdPtr = 0;
                        if (bytHostIBData_CmdBuffer.Length > 0)
                            goto SearchForStart;
                    }
                }
            }

            if (static_objTCPIP_OnDataIn_blnReceivingData)
            {
                // Data length must always be >0 for a legitimate data frame:
                if (static_objTCPIP_OnDataIn_intDataBytesToReceive == 0)
                {
                    if (bytHostIBData_CmdBuffer.Length - static_objTCPIP_OnDataIn_intDataStartPtr >= 4)
                    {
                        // Compute the byte count to receive plus 2 additional bytes for the 16 bit CRC
                        static_objTCPIP_OnDataIn_intDataBytesToReceive = (bytHostIBData_CmdBuffer[intHostIBData_CmdPtr + 2] << 8) + bytHostIBData_CmdBuffer[intHostIBData_CmdPtr + 3] + 2;
                        // includes  data + 2 byte CRC
                        static_objTCPIP_OnDataIn_bytDataFromHost = new byte[static_objTCPIP_OnDataIn_intDataBytesToReceive + 2];
                        // make 2 larger to include the byte count (CRC computed starting with the byte Count)
                        static_objTCPIP_OnDataIn_bytDataFromHost[0] = bytHostIBData_CmdBuffer[intHostIBData_CmdPtr + 2];
                        // MSB of count
                        static_objTCPIP_OnDataIn_bytDataFromHost[1] = bytHostIBData_CmdBuffer[intHostIBData_CmdPtr + 3];
                        // LSB of count
                        static_objTCPIP_OnDataIn_intDataBytePtr = 2;
                        intHostIBData_CmdPtr = intHostIBData_CmdPtr + 4;
                        // advance pointer past "D:" and byte count
                        //If MCB.CommandTrace Then Logs.WriteDebug("[HostInterface.objTCPIP.OnDataIn] Total data to Receive = " & (intDataBytesToReceive + 2).ToString & " bytes")
                    }
                }
                if (static_objTCPIP_OnDataIn_intDataBytesToReceive > 0 & (intHostIBData_CmdPtr < bytHostIBData_CmdBuffer.Length))
                {
                    for (int i = 0; i <= bytHostIBData_CmdBuffer.Length - intHostIBData_CmdPtr - 1; i++)
                    {
                        static_objTCPIP_OnDataIn_bytDataFromHost[static_objTCPIP_OnDataIn_intDataBytePtr] = bytHostIBData_CmdBuffer[intHostIBData_CmdPtr];
                        static_objTCPIP_OnDataIn_intDataBytePtr += 1;
                        intHostIBData_CmdPtr += 1;
                        static_objTCPIP_OnDataIn_intDataBytesToReceive -= 1;
                        if (static_objTCPIP_OnDataIn_intDataBytesToReceive == 0)
                            break; // TODO: might not be correct. Was : Exit For
                    }
                    //If MCB.CommandTrace Then Logs.WriteDebug("[HostInterface.objTCPIP.OnDataIn2] Data to Receive = " & intDataBytesToReceive.ToString & " bytes")
                    if (static_objTCPIP_OnDataIn_intDataBytesToReceive == 0)
                    {
                        // Process bytDataFromHost here (check CRC etc) 
                        if (Globals.CheckCRC16(ref static_objTCPIP_OnDataIn_bytDataFromHost))
                        {
                            if (Globals.MCB.CommandTrace)
                                Logs.WriteDebug("[HostInterface.objTCPIP.OnDataIn] Data Trace from Host:" + (static_objTCPIP_OnDataIn_bytDataFromHost.Length).ToString() + " bytes. CRC OK");
                            //If MCB.CommandTrace Then Logs.WriteDebug("[HostInterface.objTCPIP.OnDataIn] " & GetString(bytDataFromHost))
                            // Prevents any use of data by protocol during an append operation.
                            lock (Globals.objDataToSendLock)
                            {
                                byte[] bytDataToAppend = new byte[static_objTCPIP_OnDataIn_bytDataFromHost.Length - 4];
                                Array.Copy(static_objTCPIP_OnDataIn_bytDataFromHost, 2, bytDataToAppend, 0, static_objTCPIP_OnDataIn_bytDataFromHost.Length - 4);
                                objMain.objProtocol.AddDataToDataToSend(bytDataToAppend);
                                // Append data here to inbound queue
                                // Test Code
                                if (Globals.MCB.CommandTrace)
                                    Logs.WriteDebug("[IBDataQueue updated] size: " + Globals.bytDataToSend.Length.ToString() + " bytes");
                                // End Test code
                            }
                            if (Globals.MCB.CommandTrace)
                                Logs.WriteDebug("[HostInterface.objTCPIP.OnDataIn] Send RDY command to Host");
                            SendCommandToHost("RDY");
                            // This signals the host more data or commands can be accepted
                        }
                        else
                        {
                            if (Globals.MCB.CommandTrace)
                                Logs.WriteDebug("[HostInterface.objTCPIP.OnDataIn] Data Trace from Host:" + (static_objTCPIP_OnDataIn_bytDataFromHost.Length).ToString() + " bytes. CRC Fail");
                            Logs.Exception("[HostInterface.objTCPIP.OnDataIn] Data Trace from Host:" + (static_objTCPIP_OnDataIn_bytDataFromHost.Length).ToString() + " bytes. CRC Fail");
                            //If MCB.CommandTrace Then Logs.WriteDebug("[HostInterface.objTCPIP.OnDataIn] " & GetString(bytDataFromHost))
                            //blnRDY = True
                            SendCommandToHost("CRCFAULT");
                            SendCommandToHost("RDY");
                        }
                        blnProcessingCmdData = false;
                        static_objTCPIP_OnDataIn_blnReceivingData = false;
                        // resize the buffer, and set pointer to it's start.
                        if (intHostIBData_CmdPtr >= bytHostIBData_CmdBuffer.Length - 1)
                        {
                            bytHostIBData_CmdBuffer = new byte[-1 + 1];
                            // clear the buffer and zero the pointer
                            intHostIBData_CmdPtr = 0;
                            // resize the buffer, and set pointer to it's start.
                        }
                        else
                        {
                            byte[] bytTemp = new byte[bytHostIBData_CmdBuffer.Length - intHostIBData_CmdPtr - 1];
                            Array.Copy(bytHostIBData_CmdBuffer, intHostIBData_CmdPtr, bytTemp, 0, bytTemp.Length);
                            bytHostIBData_CmdBuffer = bytTemp;
                            intHostIBData_CmdPtr = 0;
                        }
                    }
                }
            }


        }

        private void AppendDataToBuffer(byte[] bytNewData, ref byte[] bytBuffer)
        {
            if (bytNewData.Length == 0)
                return;
            int intStartPtr = bytBuffer.Length;
            Array.Resize(ref bytBuffer, bytBuffer.Length + bytNewData.Length);
            Array.Copy(bytNewData, 0, bytBuffer, intStartPtr, bytNewData.Length);
        }

        private void objTCPIP_OnDisconnected(object sender, nsoftware.IPWorks.IpdaemonDisconnectedEventArgs e)
        {
            strTCPIPConnectionID = "";
            Globals.Status stcStatus = null;
            stcStatus.BackColor = Color.LightSalmon;
            stcStatus.ControlName = "lblHost";
            Globals.queTNCStatus.Enqueue(stcStatus);
        }

        private void objTCPIP_OnError(object sender, nsoftware.IPWorks.IpdaemonErrorEventArgs e)
        {
            Logs.Exception("[HostInterface.objTCPIP_OnError] Err: " + e.Description);

        }


        private void ProcessCommandFromHost(string strCMD)
        {
            string strCommand = strCMD.ToUpper().Trim();
            string strParameters = "";
            string strFault = "";

            int ptrSpace = strCommand.IndexOf(" ");
            if (ptrSpace != -1)
            {
                strParameters = strCommand.Substring(ptrSpace).Trim();
                strCommand = strCommand.Substring(0, ptrSpace);
            }

            switch (strCommand)
            {
                case "ARQBW":
                    bool blnModeOK = false;
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.MCB.ARQBandwidth);
                    }
                    else
                    {
                        for (int i = 0; i <= strARQBW.Length - 1; i++)
                        {
                            if (strParameters == strARQBW[i])
                            {
                                Globals.MCB.ARQBandwidth = strParameters;
                                blnModeOK = true;
                                break; // TODO: might not be correct. Was : Exit For
                            }
                        }
                        if (blnModeOK == false)
                            strFault = "Syntax Err:" + strCMD;
                    }
                    break;
                case "BUFFER":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + objMain.objProtocol.DataToSend.ToString());
                    }
                    else
                    {
                        strFault = "Syntax Err:" + strCMD;
                    }
                    break;
                case "CLOSE":
                    objMain.blnClosing = true;
                    objTCPIP.Linger = false;
                    break;
                case "CODEC":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + objMain.blnCodecStarted.ToString().ToUpper());
                    }
                    else if (strParameters == "TRUE")
                    {
                        objMain.StopCodec(strFault);
                        objMain.StartCodec(strFault);
                    }
                    else if (strParameters == "FALSE")
                    {
                        objMain.StopCodec(strFault);
                    }
                    else
                    {
                        strFault = "Syntax Err:" + strCMD;
                    }
                    break;
                case "CWID":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + MCB.CWID.ToString);
                    }
                    else if (strParameters == "TRUE" | strParameters == "FALSE")
                    {
                        Globals.MCB.CWID = Convert.ToBoolean(strParameters);
                    }
                    else
                    {
                        strFault = "Syntax Err:" + strCMD;
                    }
                    break;
                case "DATATOSEND":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.bytDataToSend.Length.ToString());
                    }
                    else if (strParameters == 0)
                    {
                        objMain.objProtocol.ClearDataToSend();
                    }
                    else
                    {
                        strFault = "Syntax Err:" + strCMD;
                    }

                    break;
                case "DEBUGLOG":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.MCB.DebugLog.ToString());
                    }
                    else if (strParameters == "TRUE" | strParameters == "FALSE")
                    {
                        Globals.MCB.DebugLog = Convert.ToBoolean(strParameters);
                    }
                    else
                    {
                        strFault = "Syntax Err:" + strCMD;
                    }
                    break;
                case "DRIVELEVLE":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.MCB.DriveLevel.ToString());
                    }
                    else
                    {
                        if (Information.IsNumeric(strParameters))
                        {
                            if (Convert.ToInt32(strParameters) >= 0 & Convert.ToInt32(strParameters) <= 100)
                            {
                                Globals.MCB.DriveLevel = Convert.ToInt32(strParameters);
                            }
                            else
                            {
                                strFault = "Syntax Err:" + strCMD;
                            }
                        }
                    }
                    break;
                case "FECID":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.MCB.FECId.ToString());
                    }
                    else if (strParameters == "TRUE" | strParameters == "FALSE")
                    {
                        Globals.MCB.FECId = Convert.ToBoolean(strParameters);
                    }
                    else
                    {
                        strFault = "Syntax Err:" + strCMD;
                    }
                    break;
                case "FECMODE":
                    bool blnModeOK = false;
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.MCB.FECMode);
                    }
                    else
                    {
                        for (int i = 0; i <= strAllDataModes.Length - 1; i++)
                        {
                            if (strParameters == strAllDataModes[i])
                            {
                                Globals.MCB.FECMode = strParameters;
                                blnModeOK = true;
                                break; // TODO: might not be correct. Was : Exit For
                            }
                        }
                        if (blnModeOK == false)
                            strFault = "Syntax Err:" + strCMD;
                    }
                    break;
                case "FECREPEATS":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.MCB.FECRepeats.ToString());
                    }
                    else
                    {
                        if (Information.IsNumeric(strParameters))
                        {
                            if (Convert.ToInt32(strParameters) >= 0 & Convert.ToInt32(strParameters) <= 5)
                            {
                                Globals.MCB.FECRepeats = Convert.ToInt32(strParameters);
                            }
                            else
                            {
                                strFault = "Syntax Err:" + strCMD;
                            }
                        }
                    }
                    break;
                case "FECSEND":
                    if (ptrSpace == -1)
                    {
                        strFault = "Syntax Err:" + strCMD;
                    }
                    else
                    {
                        if (strParameters == "TRUE")
                        {
                            byte[] bytData = new byte[-1 + 1];
                            // this will force using the data in the current inbound buffer
                            objMain.objProtocol.StartFEC(bytData, Globals.MCB.FECMode, Globals.MCB.FECRepeats, Globals.MCB.FECId);
                        }
                        else if (strParameters == "FALSE")
                        {
                            objMain.objProtocol.AbortFEC();
                        }
                        else
                        {
                            strFault = "Syntax Err:" + strCMD;
                        }
                    }
                    break;
                case "GRIDSQUARE":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + MCB.GridSquare);
                    }
                    else
                    {
                        if ((strParameters.Trim().Length < 7))
                        {
                            Globals.MCB.GridSquare = strParameters.Trim();
                        }
                        else
                        {
                            strFault = "Syntax Err:" + strCMD;
                        }
                    }
                    break;
                case "LEADER":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.MCB.LeaderLength.ToString());
                    }
                    else
                    {
                        if (Information.IsNumeric(strParameters))
                        {
                            if (Convert.ToInt32(strParameters) >= 100 & Convert.ToInt32(strParameters) <= 2000)
                            {
                                Globals.MCB.LeaderLength = 10 * Math.Round(Convert.ToInt32(strParameters) / 10);
                            }
                            else
                            {
                                strFault = "Syntax Err:" + strCMD;
                            }
                            Globals.MCB.GridSquare = strParameters.Trim();
                        }
                        else
                        {
                            strFault = "Syntax Err:" + strCMD;
                        }
                    }
                    break;
                case "MYCALL":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.MCB.Callsign);
                    }
                    else if (Globals.CheckValidCallsignSyntax(strParameters))
                    {
                        Globals.MCB.Callsign = strParameters;
                    }
                    else
                    {
                        strFault = "Syntax Err:" + strCMD;
                    }

                    break;
                case "STATE":
                    if (ptrSpace == -1)
                    {
                        SendCommandToHost(strCommand + " " + Globals.ARDOPState.ToString());
                    }
                    else
                    {
                        strFault = "Syntax Err:" + strCMD;
                    }
                    break;
                case "VERSION":
                    SendCommandToHost("VERSION " + Application.ProductName + "_" + Application.ProductVersion);
                    break;
                case "RDY":

                    break;
                default:
                    strFault = "CMD not recoginized";
                    break;
            }
            if (strFault.Length > 0)
            {
                Logs.Exception("[ProcessCommandFromHost] Cmd Rcvd=" + strCommand + "   Fault=" + strFault);
                SendCommandToHost("FAULT " + strFault);
            }
            SendCommandToHost("RDY");
            // signals host a new command may be sent
        }

        // Subroutine to compute a 16 bit CRC value and append it to the Data...
        private void GenCRC16(ref byte[] Data, Int32 intStartIndex, Int32 intStopIndex, Int32 intSeed = 0xffff)
        {
            // For  CRC-16-CCITT =    x^16 + x^12 +x^5 + 1  intPoly = 1021 Init FFFF
            // intSeed is the seed value for the shift register and must be in the range 0-&HFFFF

            int intPoly = 0x8810;
            // This implements the CRC polynomial  x^16 + x^12 +x^5 + 1
            Int32 intRegister = intSeed;

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
        private bool CheckCRC16(ref byte[] Data, Int32 intSeed = 0xffff)
        {
            // Returns True if CRC matches, else False
            // For  CRC-16-CCITT =    x^16 + x^12 +x^5 + 1  intPoly = 1021 Init FFFF
            // intSeed is the seed value for the shift register and must be in the range 0-&HFFFF

            int intPoly = 0x8810;
            // This implements the CRC polynomial  x^16 + x^12 +x^5 + 1
            Int32 intRegister = intSeed;

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

        // Function to convert string Text (ASCII) to byte array
        private byte[] GetBytes(string strText)
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
        private string GetString(byte[] bytBuffer, int intFirst = 0, int intLast = -1)
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


        private void tmrPollQueue_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            byte[] bytToSend = null;

            tmrPollQueue.Stop();
            if ((blnProcessingCmdData == false) & queDataForHost.Count > 0)
            {
                bytToSend = queDataForHost.Dequeue();
                if (strInterfaceType == "TCPIP")
                {
                    if ((!((objTCPIP == null)) && (!string.IsNullOrEmpty(strTCPIPConnectionID))))
                    {
                        try
                        {
                            objTCPIP.Send(strTCPIPConnectionID, bytToSend);
                        }
                        catch (Exception ex)
                        {
                            Logs.Exception("[HostInterface.tmrPollQueue] TCPIP Interface Exception: " + ex.ToString());
                        }
                    }

                }
                else if (strInterfaceType == "SERIAL")
                {
                }
                else if (strInterfaceType == "BLUETOOTH")
                {
                    // This will handle BlueTooth connections ... TODO: Add BlueTooth
                }
            }
            tmrPollQueue.Start();
        }
        static bool InitStaticVariableHelper(Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag flag)
        {
            if (flag.State == 0)
            {
                flag.State = 2;
                return true;
            }
            else if (flag.State == 2)
            {
                throw new Microsoft.VisualBasic.CompilerServices.IncompleteInitialization();
            }
            else
            {
                return false;
            }
        }

        #endregion

    }
}
