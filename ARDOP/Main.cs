using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.DirectX.DirectSound;

namespace ARDOP
{
    public partial class Main : Form
    {
        // Objects, classes & forms
        private object objCodecLock = new object();
        public EncodeModulate objMod;
        public DemodulateDecode objDemod = new DemodulateDecode(this);
        // Private objTestReceive As New TestReceive(Me) ' for testing and place holder for protocol class
        // Final protocol implementation.
        public ARDOPprotocol objProtocol = new ARDOPprotocol(this);
        public BusyDetector objBusy = new BusyDetector();
        private Test frmTest;

        public HostInterface objHI = new HostInterface(this);
        // Sound Card/Direct Sound 
        private Int32 intCaptureBufferSize;
        private Int32 intNextCaptureOffset;
        // 2400 bytes or 1200 16 bit samples (nominally every 100 ms @ 12000 sample rate)  
        private Int32 intNotifySize = 2400;
        private SecondaryBuffer bufPlayback = null;
        private CaptureDevicesCollection cllCaptureDevices;
        private Microsoft.DirectX.DirectSound.DevicesCollection cllPlaybackDevices;
        private Capture devCaptureDevice;
        private Microsoft.DirectX.DirectSound.Device devSelectedPlaybackDevice;
        private Notify objApplicationNotify = null;
        private CaptureBuffer objCapture = null;
        private Guid objCaptureDeviceGuid = Guid.Empty;
        private AutoResetEvent objNotificationEvent = null;
        private SecondaryBuffer objPlayback = null;
        private Guid objPlaybackDeviceGuid = Guid.Empty;
        private BufferPositionNotify[] stcPositionNotify = new BufferPositionNotify[intNumberRecordNotifications + 1];

        private WaveFormat stcSCFormat;
        // Strings

        private string strTCPIPConnectionID;
        // Integers
        private Int32 intSampPerSym;
        private Int32 intRepeatCnt;
        private Int32 intAmp = 29000;
        private Int32 intNumOfCarriers;
        //(creates appox ~3.2 second circular buffer)
        private const Int32 intNumberRecordNotifications = 32;
        private Int32 intBMPSpectrumWidth = 256;
        private Int32 intBMPSpectrumHeight = 62;
        private Int32 intHostIBData_CmdPtr = 0;
        // time in ms between FEC frames 
        private Int32 intFECFrameGap = 300;

        //Booleans
        private bool blnSCCapturing;
        public bool blnCodecStarted;
        private bool blnInTestMode = true;

        public bool blnClosing = false;
        // Doubles

        private double dblPhase;
        //Dates
        private System.DateTime dttNextPlay;

        private System.DateTime dttLastSoundCardSample;
        // Arrays
        private double[] dblCarFreq;
        private byte[] bytToSend;
        private byte[] bytSymToSend;
        private Int32[] intSamples;
        private double[] dblPhaseInc;
        private double[] dblCPPhaseOffset;

        private byte[] bytHostIBData_CmdBuffer = new byte[-1 + 1];
        // Graphics
        // Public bmpConstellation As Bitmap
        private Bitmap bmpSpectrum;
        private Bitmap bmpNewSpectrum;
        private Graphics graConstellation;

        private Graphics graFrequency;
        // Threads
        // Notification thread for capturing data in the sound card
        private Thread thrNotify = null;

        // Structures
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

        // Properties
        public bool SoundIsPlaying
        {
            get
            {
                if ((objPlayback == null))
                    return false;
                if (objPlayback.Status.Playing)
                    return true;
                return false;
            }
        }




        private void InitializeFromIni()
        {
            objIniFile.Load();

            MCB.Callsign = objIniFile.GetString("ARDOP_Win TNC", "Callsign", "");
            MCB.StartMinimized = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "StartMinimized", "False"));
            MCB.DebugLog = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "DebugLog", "True"));
            MCB.CommandTrace = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "CommandTrace", "False"));
            MCB.CaptureDevice = objIniFile.GetString("ARDOP_Win TNC", "CaptureDevice", "");
            MCB.PlaybackDevice = objIniFile.GetString("ARDOP_Win TNC", "PlaybackDevice", "");
            MCB.LeaderLength = objIniFile.GetInteger("ARDOP_Win TNC", "LeaderLength", 120);
            MCB.TrailerLength = objIniFile.GetInteger("ARDOP_Win TNC", "TrailerLength", 0);
            MCB.ARQBandwidth = objIniFile.GetString("ARDOP_Win TNC", "ARQBandwidth", "500Max");
            MCB.Mode_Radio = objIniFile.GetString("ARDOP_Win TNC", "Mode_Radio", "HF SSB");
            MCB.DriveLevel = objIniFile.GetInteger("ARDOP_Win TNC", "DriveLevel", 90);
            MCB.Squelch = objIniFile.GetInteger("ARDOP_Win TNC", "Squelch", 5);
            MCB.AccumulateStats = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "Accum Stats", "True"));
            MCB.DisplayWaterfall = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "Display Waterfall", "True"));
            MCB.DisplaySpectrum = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "Display Spectrum", "False"));
            MCB.SecureHostLogin = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "SecureHostLogin", "False"));
            MCB.Password = objIniFile.GetString("ARDOP_Win TNC", "LoginPassword", "");
            MCB.TuningRange = objIniFile.GetInteger("ARDOP_Win TNC", "TuningRange", 100);
            MCB.RadioControl = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "Enable Radio Control", "False"));
            MCB.FECRepeats = objIniFile.GetInteger("ARDOP_Win TNC", "FECRepeats", 2);
            MCB.FECMode = objIniFile.GetString("ARDOP_Win TNC", "FECMode", "4PSK.500.100");
            MCB.FECId = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "FECId", "True"));
            ToolStripMenuItem1.Enabled = MCB.RadioControl;

            if (MCB.RadioControl)
            {
                if ((objRadio == null))
                {
                    objRadio = new Radio();
                    objRadio.OpenControlPort();
                    objRadio.OpenPttPort();
                }
            }

        }

        private void Main_FormClosed(object sender, System.Windows.Forms.FormClosedEventArgs e)
        {
            System.Environment.Exit(0);
        }

        private void Main_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            objHI.TerminateHostLink();
            if ((objRadio != null))
            {
                objRadio.CloseRadio();
                objRadio = null;
            }
            string strFault = "";
            StopCodec(ref strFault);
            if (strFault.Length > 0)
                Logs.Exception("[Main_FormClosing] Fault: " + strFault);
        }

        private void Main_Load(object sender, System.EventArgs e)
        {
            // Create subdirectories as required...
            if (Directory.Exists(strExecutionDirectory + "Logs") == false)
                Directory.CreateDirectory(strExecutionDirectory + "Logs");
            if (Directory.Exists(strExecutionDirectory + "Wav") == false)
                Directory.CreateDirectory(strExecutionDirectory + "Wav");
            strWavDirectory = strExecutionDirectory + "Wav\\";
            // Set inital window position and size...
            objIniFile = new INIFile(strExecutionDirectory + "ARDOP_Win TNC.ini");
            InitializeFromIni();
            objMod = new EncodeModulate();
            // initializes all the leaders etc. 
            dttLastSoundCardSample = Now;
            tmrPoll.Start();
            string strFault = "";
            tmrStartCODEC.Start();
            //Dim objPA As New PortAudioVB '  Enable only for testing PortAudio

            if (blnInTestMode)
                ShowTestFormToolStripMenuItem.Enabled = true;

            // This keeps the size of the graphics panels constant to handle cases where font size (in Control Panel, Display is 125% or 150% and recenters the waterfall panel. 
            Drawing.Point dpWaterfallCorner = pnlWaterfall.Location;
            pnlWaterfall.Left = (dpWaterfallCorner.X + pnlWaterfall.Width / 2) - 105;
            pnlWaterfall.Height = 63;
            pnlWaterfall.Width = 210;
            pnlConstellation.Height = 91;
            pnlConstellation.Width = 91;
            Logs.WriteDebug("[ARDOP_Win TNC.Main.Load] Command line =" + Microsoft.VisualBasic.Command().Trim);
            // nothing in command line so use ini values
            if (string.IsNullOrEmpty(Microsoft.VisualBasic.Command().Trim))
            {
                // use ini file values for host and port
                Globals.MCB.TCPAddress = Globals.objIniFile.GetString("ARDOP Win", "TCPIP Address", "127.0.0.1");
                MCB.TCPPort = objIniFile.GetInteger("ARDOP Win", "TCPIP Port", 8515);
                MCB.HostTCPIP = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "HostTCPIP", "True"));
                if (MCB.HostTCPIP)
                    objHI.TCPIPProperties(MCB.TCPAddress, MCB.TCPPort);
                //
                MCB.HostSerial = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "HostSerial", "False"));
                MCB.SerCOMPort = objIniFile.GetString("ARDOP Win", "SerialCOMPort", "none");
                MCB.SerBaud = objIniFile.GetInteger("ARDOP Win", "SerialBaud", 19200);
                if (MCB.HostSerial)
                    objHI.SerialProperties(MCB.SerCOMPort, MCB.SerBaud);
                //
                MCB.HostBlueTooth = Convert.ToBoolean(objIniFile.GetString("ARDOP_Win TNC", "HostBlueTooth", "False"));
                MCB.HostPairing = objIniFile.GetString("ARDOP_Win TNC", "Host Pairing", "none");
                if (MCB.HostBlueTooth)
                    objHI.BluetoothProperties(MCB.HostPairing);
                CloseToolStripMenuItem.Enabled = true;

            }
            else
            {
                // test command line parameters for validity: "TCPIP TCPIPPort# TCPIPAddress", Serial POrt,   or  "BLUETOOTH BlueToothPairing"
                string[] strCmds = Microsoft.VisualBasic.Command().Split(Convert.ToChar(" "));
                if (strCmds.Length == 3 && (strCmds(0).ToUpper == "TCPIP" & (Information.IsNumeric(strCmds(1).Trim) & (Convert.ToInt32(strCmds(1).Trim) >= 0 & Convert.ToInt32(strCmds(1).Trim) < 65536))))
                {
                    // TCPIP parameters OK so use these in place of ini values
                    MCB.HostTCPIP = true;
                    MCB.HostSerial = false;
                    MCB.HostBlueTooth = false;
                    objHI.TCPIPProperties(strCmds(2).Trim, Convert.ToInt32(strCmds(1).Trim));
                    MCB.TCPPort = Convert.ToInt32(strCmds(1).Trim);
                    MCB.TCPAddress = strCmds(2).Trim;
                }
                else if (strCmds.Length == 3 && (strCmds(0).ToUpper == "SERIAL" & Information.IsNumeric(strCmds(1).Trim) & (Convert.ToInt32(strCmds(1).Trim) >= 9600)))
                {
                    MCB.HostTCPIP = false;
                    MCB.HostSerial = true;
                    MCB.HostBlueTooth = false;
                    objHI.SerialProperties(strCmds(1).Trim.ToUpper, Convert.ToInt32(strCmds(2).Trim));
                    MCB.SerCOMPort = strCmds(1).Trim.ToUpper;
                    MCB.SerBaud = Convert.ToInt32(strCmds(2).Trim);
                    // Preliminay ....may need work for bluetooth
                }
                else if (strCmds.Length == 2 && strCmds(0).ToUpper == "BLUETOOTH")
                {
                    MCB.HostTCPIP = false;
                    MCB.HostSerial = false;
                    MCB.HostBlueTooth = true;
                    objHI.BluetoothProperties(strCmds(1));
                    MCB.HostPairing = strCmds(1);
                }
                else
                {
                    Logs.Exception("[Main.Load] Syntax error in command line: " + Microsoft.VisualBasic.Command() + "   ... ini file values used.");
                }
                CloseToolStripMenuItem.Enabled = false;
            }
            objHI.EnableHostLink();
        }

        private void BasicSetupToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            TNCSetup dlgSetup = new TNCSetup();
            string strFault = "";
            dlgSetup.ShowDialog();
            if (dlgSetup.DialogResult == System.Windows.Forms.DialogResult.OK)
            {
                ToolStripMenuItem1.Enabled = MCB.RadioControl;

                if (StopCodec(ref strFault))
                {
                    if (!StartCodec(ref strFault))
                    {
                        Logs.Exception("[BasicSetupDialog] Failure to restart Codec after setup change");
                    }
                }
                else
                {
                    Logs.Exception("[BasicSetupDialog] Failure to stop Codec after setup change");
                }
                objMod = null;
                objMod = new EncodeModulate();
            }
        }

        private void tmrPoll_Tick(object sender, System.EventArgs e)
        {
            tmrPoll.Stop();
            Status stcStatus = null;
            if (Now.Subtract(dttLastSoundCardSample).TotalSeconds > 10)
            {
                tmrStartCODEC.Interval = 1000;
                dttLastSoundCardSample = Now;
                tmrStartCODEC.Start();
                Logs.Exception("[tmrPoll_Tick] > 10 seconds with no sound card samples...Restarting Codec");
            }
            if (objPlayback != null && (!objPlayback.Status.Playing))
            {
                // This handles the normal condition of going from Sending (Playback) to Receiving (Recording)
                Debug.WriteLine("[tmrPoll.Tick] Play stop. Length = " + Strings.Format(Now.Subtract(dttTestStart).TotalMilliseconds, "#") + " ms");
                objPlayback = null;
                intRepeatCnt -= 1;

                if (blnInTestMode)
                {
                    try
                    {
                        frmTest.UpdateFrameCounter(intRepeatCnt);
                    }
                    catch
                    {
                    }
                }
                if (intRepeatCnt > 0)
                {
                    dttNextPlay = Now.AddSeconds(2);
                }
                else if (MCB.AccumulateStats)
                {
                    tmrLogStats.Start();
                }
                KeyPTT(false);
                // clear the transmit lable 
                stcStatus.BackColor = SystemColors.Control;
                stcStatus.ControlName = "lblXmtFrame";
                // clear the transmit label
                queTNCStatus.Enqueue(stcStatus);
                switch (objProtocol.ARDOPProtocolState)
                {
                    case ProtocolState.FECSend:
                        if (objProtocol.GetNextFECFrame())
                        {
                            intRepeatCnt = 1;
                            dttNextPlay = Now.AddMilliseconds(intFECFrameGap);
                        }
                        break;
                }
            }
            else if ((objPlayback == null) & intRepeatCnt > 0 & Now.Subtract(dttNextPlay).TotalMilliseconds > 0)
            {
                State = ReceiveState.SearchingForLeader;
                PlaySoundStream();
            }

            while (queTNCStatus.Count > 0)
            {
                try
                {
                    stcStatus = (Status)queTNCStatus.Dequeue;
                    switch (stcStatus.ControlName)
                    {
                        // Receive controls:
                        case "lblQuality":
                            lblQuality.Text = stcStatus.Text;
                            break;
                        case "ConstellationPlot":
                            DisplayPlot();
                            intRepeatCnt += 0;
                            break;
                        case "lblXmtFrame":
                            lblXmtFrame.Text = stcStatus.Text;
                            lblXmtFrame.BackColor = stcStatus.BackColor;
                            break;
                        case "lblRcvFrame":
                            lblRcvFrame.Text = stcStatus.Text;
                            lblRcvFrame.BackColor = stcStatus.BackColor;
                            break;
                        case "prgReceiveLevel":
                            prgReceiveLevel.Value = stcStatus.Value;
                            // < 12% of Full scale (16 bit A/D)
                            if (stcStatus.Value < 64)
                            {
                                prgReceiveLevel.ForeColor = Color.SkyBlue;
                                // > 88% of full scale (16 bit A/D)
                            }
                            else if (stcStatus.Value > 170)
                            {
                                prgReceiveLevel.ForeColor = Color.LightSalmon;
                            }
                            else
                            {
                                prgReceiveLevel.ForeColor = Color.LightGreen;
                            }
                            break;
                        case "lblOffset":
                            lblOffset.Text = stcStatus.Text;
                            break;
                        case "lblHost":
                            if (!string.IsNullOrEmpty(stcStatus.Text))
                                lblHost.Text = stcStatus.Text;
                            lblHost.BackColor = stcStatus.BackColor;
                            break;
                        case "lblState":
                            lblState.Text = stcStatus.Text;
                            lblState.BackColor = stcStatus.BackColor;
                            break;
                    }

                }
                catch
                {
                    // WMLogs.Exception("[tmrPoll.Tick] queTNCStatus Err: " & Err.Description)
                    break; // TODO: might not be correct. Was : Exit While
                }
            }
            //If queDataForHost.Count > 0 Then
            //    Dim bytDataForHost() As Byte = queDataForHost.Dequeue
            //    objHI.SendDataToHost(bytDataForHost)
            //End If
            if (blnClosing)
            {
                this.Close();
            }
            else
            {
                tmrPoll.Start();
            }

        }

        // Subroutine to repaint the FSK Quality Plot
        private void DisplayPlot()
        {
            try
            {
                if ((graConstellation != null))
                {
                    graConstellation.Dispose();
                }
                graConstellation = pnlConstellation.CreateGraphics;
                graConstellation.Clear(Color.Black);
                if ((bmpConstellation != null))
                {
                    graConstellation.DrawImage(bmpConstellation, 0, 0);
                    // Display the constellation
                }
            }
            catch (Exception ex)
            {
                //WMLogs.Exception("[DisplayConstellation] Trace=" & Trace.ToString & "  Err: " & ex.ToString)
            }
        }

        // Subroutine to repaint the Constellation graphic
        private void DisplayConstellation()
        {
            int Trace = 0;
            if (this.WindowState == FormWindowState.Minimized)
                return;
            Trace = 1;
            try
            {
                if ((graConstellation != null))
                {
                    Trace = 2;
                    graConstellation.Dispose();
                }
                Trace = 3;
                graConstellation = pnlConstellation.CreateGraphics;
                Trace = 4;
                graConstellation.Clear(Color.Black);
                Trace = 5;
                if ((bmpConstellation != null))
                {
                    Trace = 6;
                    graConstellation.DrawImage(bmpConstellation, 0, 0);
                    // Display the constellation
                }
            }
            catch (Exception ex)
            {
                Logs.Exception("[DisplayConstellation] Trace=" + Trace.ToString + "  Err: " + ex.ToString);
            }
        }

        public bool PlaySoundStream()
        {
            bool functionReturnValue = false;
            //   Plays the .wav stream with the selected Playback device
            //   Returns True if no errors False otherwise...
            System.DateTime dttStartPlay = default(System.DateTime);
            int intTrace = 0;
            Status stcStatus = null;
            if (!blnCodecStarted)
                return false;
            if ((memWaveStream == null))
            {
                Logs.Exception("[PlaySoundFile] memWaveStream is nothing");
                return false;
            }
            else if (objPlayback != null && objPlayback.Status.Playing)
            {
                Logs.Exception("[PlaySoundFile] objPlayback is Playing, Call AbortWaveStream");
                AbortSoundStream();
            }
            intTrace = 1;
            try
            {
                intTrace = 2;
                devSelectedPlaybackDevice.SetCooperativeLevel(this.Handle, CooperativeLevel.Priority);
                //dttStartWaveStreamPlay = Now
                KeyPTT(true);
                // Activate PTT before starting sound play
                Microsoft.DirectX.DirectSound.BufferDescription bufPlaybackFlags = new Microsoft.DirectX.DirectSound.BufferDescription();
                // The following flags required to allow playing when not in focus and 
                // to allow adjusting the volume...
                bufPlaybackFlags.Flags = (BufferDescriptionFlags)BufferDescriptionFlags.GlobalFocus + BufferDescriptionFlags.ControlVolume;
                intTrace = 3;
                memWaveStream.Seek(0, SeekOrigin.Begin);
                // reset the pointer to the origin
                intTrace = 4;
                objPlayback = new SecondaryBuffer(memWaveStream, bufPlaybackFlags, devSelectedPlaybackDevice);
                objPlayback.Volume = Math.Min(-5000 + 50 * MCB.DriveLevel, 0);
                // -5000=off, 0=full volume
                intTrace = 5;
                objPlayback.Play(0, BufferPlayFlags.Default);
                //objWMProtocol.SetCaptureState(CaptureState.Transmitting)
                dttTestStart = Now;
                dttStartPlay = Now;
                intTrace = 6;
                // wait up to 300 ms for start of playback.
                while (Now.Subtract(dttStartPlay).TotalMilliseconds < 300 & !objPlayback.Status.Playing)
                {
                    Thread.Sleep(10);
                }
                intTrace = 7;
                if (objPlayback.Status.Playing)
                {
                    intTrace = 8;
                    if (MCB.DebugLog)
                        Logs.WriteDebug("[PlaySoundStream] Stream: " + strLastWavStream);
                    stcStatus.ControlName = "lblXmtFrame";
                    stcStatus.Text = strLastWavStream;
                    stcStatus.BackColor = Color.LightSalmon;
                    queTNCStatus.Enqueue(stcStatus);
                    stcStatus.ControlName = "lblRcvFrame";
                    stcStatus.BackColor = SystemColors.Control;
                    stcStatus.Text = "";
                    queTNCStatus.Enqueue(stcStatus);
                    functionReturnValue = true;
                }
                else
                {
                    intTrace = 9;
                    KeyPTT(false);
                    functionReturnValue = false;
                    //objWMProtocol.SetCaptureState(CaptureState.SearchForLeader)
                    Logs.WriteDebug("[PlaySoundStream] Failure to start objPlayback");
                    Logs.Exception("[PlaySoundStream] Failure to start objPlayback");
                }
            }
            catch (Exception e)
            {
                Logs.Exception("[PlaySoundStream] Kill PTT on exception: " + e.ToString + "  intTrace=" + intTrace.ToString);
                KeyPTT(false);
                functionReturnValue = false;
                //objWMProtocol.SetCaptureState(CaptureState.SearchForLeader)
            }
            return functionReturnValue;
        }

        private bool KeyPTT(bool blnPTT)
        {
            //  Returns TRUE if successful False otherwise

            // If PTT goes Active clear the counts and sums for PTT Latency Measurement.
            if (blnPTT)
            {

                //blnLatencyCalculated = False
                //intPTTOnCount = 0
                //dblPTTOnLevelSum = 0
                //dttPTTApply = Now
            }
            else if (!blnPTT)
            {
                //dttPTTRelease = Now
            }
            if (MCB.RadioControl & (objRadio != null))
                objRadio.PTT(blnPTT);
            //blnPTT = PTT
            //objWMPort.SendCommand("PTT " & PTT.ToString)
            return true;
        }

        public bool StartCodec(ref string strFault)
	{
		bool functionReturnValue = false;
		//Returns true if successful
		Thread.Sleep(100);
		// This delay is necessary for reliable starup following a StopCodec
		lock (objCodecLock) {
			dttLastSoundCardSample = Now;
			bool blnSpectrumSave = MCB.DisplaySpectrum;
			bool blnWaterfallSave = MCB.DisplayWaterfall;
			System.DateTime dttStartWait = Now;
			MCB.DisplayWaterfall = false;
			MCB.DisplaySpectrum = false;
			string[] strCaptureDevices = EnumerateCaptureDevices();
			string[] strPlaybackDevices = EnumeratePlaybackDevices();
			functionReturnValue = false;
			DeviceInformation objDI = new DeviceInformation();
			int intPtr = 0;
			// Playback devices
			try {
				cllPlaybackDevices = null;

				cllPlaybackDevices = new Microsoft.DirectX.DirectSound.DevicesCollection();
				if ((devSelectedPlaybackDevice != null)) {
					devSelectedPlaybackDevice.Dispose();
					devSelectedPlaybackDevice = null;
				}

				foreach (DeviceInformation objDI in cllPlaybackDevices) {
					DeviceDescription objDD = new DeviceDescription(objDI);
					if (strPlaybackDevices(intPtr) == MCB.PlaybackDevice) {
						if (MCB.DebugLog)
							Logs.WriteDebug("[Main.StartCodec] Setting SelectedPlaybackDevice = " + MCB.PlaybackDevice);
						devSelectedPlaybackDevice = new Device(objDD.info.DriverGuid);
						functionReturnValue = true;
						break; // TODO: might not be correct. Was : Exit For
					}
					intPtr += 1;
				}
				if (!functionReturnValue) {
					strFault = "Playback Device setup, Device " + MCB.PlaybackDevice + " not found in Windows enumerated Playback Devices";
				}
			} catch (Exception ex) {
				strFault = Err.Number.ToString + "/" + Err.Description;
				Logs.Exception("[StartCodec], Playback Device setup] Err: " + ex.ToString);
				functionReturnValue = false;
			}
			if (functionReturnValue) {
				// Capture Device
				CaptureBufferDescription dscheckboxd = new CaptureBufferDescription();
				try {
					functionReturnValue = false;
					cllCaptureDevices = null;
					cllCaptureDevices = new CaptureDevicesCollection();
					intPtr = 0;
					for (int i = 0; i <= cllCaptureDevices.Count - 1; i++) {
						if (MCB.CaptureDevice == strCaptureDevices(i)) {
							objCaptureDeviceGuid = cllCaptureDevices(i).DriverGuid;
							devCaptureDevice = new Capture(objCaptureDeviceGuid);
							stcSCFormat.SamplesPerSecond = 12000;
							// 12000 Hz sample rate 
							stcSCFormat.Channels = 1;
							stcSCFormat.BitsPerSample = 16;
							stcSCFormat.BlockAlign = 2;
							stcSCFormat.AverageBytesPerSecond = 2 * 12000;
							stcSCFormat.FormatTag = WaveFormatTag.Pcm;
							objApplicationNotify = null;
							objCapture = null;
							// Set the buffer sizes
							intCaptureBufferSize = intNotifySize * intNumberRecordNotifications;
							// Create the capture buffer
							dscheckboxd.BufferBytes = intCaptureBufferSize;
							stcSCFormat.FormatTag = WaveFormatTag.Pcm;
							dscheckboxd.Format = stcSCFormat;
							// Set the format during creatation
							if ((objCapture != null)) {
								objCapture.Dispose();
								objCapture = null;
							}
							//objCapture = New CaptureBuffer(dscheckboxd, devCaptureDevice)
							intNextCaptureOffset = 0;
							WriteTextToSpectrum("CODEC Start OK", Brushes.LightGreen);
							while (Now.Subtract(dttStartWait).TotalSeconds < 3) {
								Application.DoEvents();
								Thread.Sleep(100);
							}
							objCapture = new CaptureBuffer(dscheckboxd, devCaptureDevice);
							InititializeNotifications();
							objCapture.Start(true);
							// start with looping
							InititializeSpectrum(Color.Black);

							functionReturnValue = true;
						}
					}
					if (!functionReturnValue) {
						strFault = "Could not find DirectSound capture device " + MCB.CaptureDevice.ToUpper;
						//Logs.Exception("[Main.StartCodec] Could not find DirectSound capture device " & MCB.CaptureDevice & " in Windows enumerated Capture Devices")
					}
				} catch (Exception ex) {
					strFault = Err.Number.ToString + "/" + Err.Description;
					functionReturnValue = false;
					//Logs.Exception("[Main.StartCodec] Err: " & ex.ToString)
				}
			}


			if (functionReturnValue) {
				if (MCB.DebugLog)
					Logs.WriteDebug("[Main.StartCodec] Successful start of codec");
				objProtocol.ARDOPProtocolState = ProtocolState.DISC;
			} else {
				if (MCB.DebugLog)
					Logs.WriteDebug("[Main.StartCodec] CODEC Start Failed");
				WriteTextToSpectrum("CODEC Start Failed", Brushes.Red);
				objProtocol.ARDOPProtocolState = ProtocolState.OFFLINE;
				while (Now.Subtract(dttStartWait).TotalSeconds < 3) {
					Application.DoEvents();
					Thread.Sleep(100);
				}
				tmrStartCODEC.Interval = 5000;
				tmrStartCODEC.Start();
			}
			InititializeSpectrum(Color.Black);
			MCB.DisplayWaterfall = blnWaterfallSave;
			MCB.DisplaySpectrum = blnSpectrumSave;
		}
		return functionReturnValue;
	}

        public bool StopCodec(ref string strFault)
        {
            bool functionReturnValue = false;
            // Stop the capture
            lock (objCodecLock)
            {
                try
                {
                    if (MCB.DebugLog)
                        Logs.WriteDebug("[Main.StopCodec] Stop thrNotify with blnSCCapturing = False");
                    blnSCCapturing = false;
                    // this should end the wait thread if it is still running
                    Thread.Sleep(200);
                    //If blnInWaitThread Then
                    if (thrNotify != null && thrNotify.IsAlive)
                    {
                        if (MCB.DebugLog)
                            Logs.WriteDebug("[Main.StopCodec] Aborting thrNotify");
                        thrNotify.Abort();
                        Thread.Sleep(100);
                        thrNotify.Join(3000);
                    }
                    thrNotify = null;
                    //blnInWaitThread = False
                    //lblCapture.BackColor = Color.LightSalmon
                    // Stop the buffer
                    if (objCapture != null)
                    {
                        objCapture.Stop();
                        objCapture.Dispose();
                    }
                    objCapture = null;
                    if (devCaptureDevice != null)
                        devCaptureDevice.Dispose();
                    devCaptureDevice = null;
                    if (devSelectedPlaybackDevice != null)
                    {
                        devSelectedPlaybackDevice.Dispose();
                    }
                    devSelectedPlaybackDevice = null;
                    if (MCB.DebugLog)
                        Logs.WriteDebug("[Main.StopCodec] = True");
                    functionReturnValue = true;
                    objProtocol.ARDOPProtocolState = ProtocolState.OFFLINE;
                }
                catch (Exception ex)
                {
                    Logs.Exception("[Main.StopCodec] Err: " + ex.ToString);
                    if (MCB.DebugLog)
                        Logs.WriteDebug("[Main.StopCodec] = False");
                    strFault = Err.Number.ToString + "/" + Err.Description;
                    functionReturnValue = false;
                }
                //blnEnableCaptureRestart = False
            }
            return functionReturnValue;
        }

        private void InititializeNotifications()
        {
            //  Subroutine to initialize the notifications on the capture buffer...

            if (null == objCapture)
            {
                Logs.Exception("[Main.InitializeNotifications] Capture Buffer is Nothing");
                return;
            }

            // Create a thread to monitor the notify events
            if (null == thrNotify)
            {
                thrNotify = new Thread(new ThreadStart(CaptureWaitThread));
                thrNotify.Priority = ThreadPriority.Highest;
                // Create a notification event, for when the sound stops playing
                objNotificationEvent = new AutoResetEvent(false);
                thrNotify.Start();
            }

            // Setup the notification positions
            int i = 0;
            for (i = 0; i <= intNumberRecordNotifications - 1; i++)
            {
                stcPositionNotify(i).Offset = intNotifySize * i + intNotifySize - 1;
                // The following recommended in place of "objNotificationEvent.Handle" as a more reliable handle 
                stcPositionNotify(i).EventNotifyHandle = objNotificationEvent.SafeWaitHandle.DangerousGetHandle;
            }

            objApplicationNotify = new Notify(objCapture);

            // Tell DirectSound when to notify the application. The notification will come in the from 
            // of signaled events that are handled in the notify thread...
            objApplicationNotify.SetNotificationPositions(stcPositionNotify, intNumberRecordNotifications);
        }

        // This is the main thread to capture sound card data which is also used as a polling function
        private void CaptureWaitThread()
        {
            // It is active whenever the Sound card is enabled and sampling data
            // It should receive a buffer notification every 1200 samples/12000 sample rate  or every 100 ms...
            blnSCCapturing = true;
            if (MCB.DebugLog)
                Logs.WriteDebug("[CaptureWaitThread] Startup");
            try
            {
                while (blnSCCapturing)
                {
                    //  Sit here and wait for a notification message to arrive
                    //  this should be about every 100 ms or 10 symbols (1200 samples @ 12000 Samples/sec)
                    objNotificationEvent.WaitOne(-1, true);
                    ProcessCapturedData();
                    // Looks for incoming data and commands
                }
            }
            catch (Exception ex)
            {
                Logs.Exception("[Main.CaptureWaitThread] Err: " + ex.ToString);
            }
            if (MCB.DebugLog)
                Logs.WriteDebug("[Main.CaptureWaitThread] Exit");
        }


        public void AbortSoundStream()
        {
            if (objPlayback != null && objPlayback.Status.Playing)
            {
                try
                {
                    objPlayback.Stop();
                    KeyPTT(false);
                    objPlayback = null;
                    Logs.Exception("[Main.AbortSoundStream] Stream Stopped, PTT=False, and objPlayback set to Nothing");
                }
                catch (Exception ex)
                {
                    Logs.Exception("[Main.AbortSoundStream] Err: " + ex.ToString);
                }
            }
        }

        private void InititializeSpectrum(System.Drawing.Color Color)
        {
            try
            {
                if (bmpSpectrum != null)
                {
                    bmpSpectrum.Dispose();
                    bmpSpectrum = null;
                }
                graFrequency = pnlWaterfall.CreateGraphics;
                graFrequency.Clear(Color);
                intBMPSpectrumWidth = 256;
                intBMPSpectrumHeight = 62;
                bmpSpectrum = new Bitmap(intBMPSpectrumWidth, intBMPSpectrumHeight);

                // Set each pixel in bmpSpectrum to black.
                int intX = 0;
                for (intX = 0; intX <= intBMPSpectrumWidth - 1; intX++)
                {
                    int intY = 0;
                    for (intY = 0; intY <= intBMPSpectrumHeight - 1; intY++)
                    {
                        if (intX == 103)
                        {
                            bmpSpectrum.SetPixel(intX, intY, Color.Tomato);
                        }
                        else
                        {
                            bmpSpectrum.SetPixel(intX, intY, Color.Black);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        // Initialize the Spectrum/Waterfall display to black. 
        private void ClearSpectrum()
        {
            try
            {
                if (bmpSpectrum != null)
                {
                    bmpSpectrum.Dispose();
                    bmpSpectrum = null;
                }
                graFrequency = pnlWaterfall.CreateGraphics;
                graFrequency.Clear(Color.Black);
                intBMPSpectrumWidth = 256;
                intBMPSpectrumHeight = 62;
                bmpSpectrum = new Bitmap(intBMPSpectrumWidth, intBMPSpectrumHeight);
            }
            catch
            {
            }
        }

        private void WriteTextToSpectrum(string strText, Brush objBrush)
        {
            try
            {
                ClearSpectrum();
                Graphics graComposit = Graphics.FromImage(bmpSpectrum);
                Font objFont = default(Font);
                objFont = new System.Drawing.Font("Microsoft Sans Serif", 12, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, Convert.ToByte(0));
                graComposit.DrawString(strText, objFont, objBrush, 100 - 5 * strText.Length, 20);
                if ((graFrequency != null))
                {
                    graFrequency.Dispose();
                    // this permits writing back to the graFrequency without a GDI+ fault.
                }

                graComposit = pnlWaterfall.CreateGraphics;
                graComposit.DrawImage(bmpSpectrum, 0, 0);
                // Draw the new bitmap in one update to avoid a possible GDI+ fault
            }
            catch (Exception ex)
            {
                Logs.Exception("[WriteTextToSpectrum] Err:  " + ex.ToString);
            }
        }

        internal void UpdatePhaseConstellation(ref Int16[] intPhases, ref Int16[] intMag, string strMod, ref Int32 intQuality, bool blnQAM = false)
        {
            // Subroutine to update bmpConstellation plot for PSK modes...
            // Skip plotting and calulations of intPSKPhase(0) as this is a reference phase (9/30/2014)
            try
            {
                int intPSKPhase = Convert.ToInt32(strMod.Substring(0, 1));
                double dblPhaseError = 0;
                double dblPhaseErrorSum = 0;
                Int32 intPSKIndex = default(Int32);
                Int32 intX = default(Int32);
                Int32 intY = default(Int32);
                Int32 intP = default(Int32);
                double dblRad = 0;
                double dblAvgRad = 0;
                double intMagMax = 0;
                double dblPi4 = 0.25 * Math.PI;
                double dbPhaseStep = 2 * Math.PI / intPSKPhase;
                double dblRadError = 0;
                double dblPlotRotation = 0;
                Status stcStatus = default(Status);
                int yCenter = 0;
                int xCenter = 0;

                switch (intPSKPhase)
                {
                    case 4:
                        intPSKIndex = 0;
                        break;
                    case 8:
                        intPSKIndex = 1;
                        break;
                    case 16:
                        intPSKIndex = 2;
                        break;
                }

                bmpConstellation = new Bitmap(89, 89);
                // Draw the axis and paint the black background area
                yCenter = (bmpConstellation.Height - 1) / 2;
                xCenter = (bmpConstellation.Width - 1) / 2;
                for (int x = 0; x <= bmpConstellation.Width - 1; x++)
                {
                    for (int y = 0; y <= bmpConstellation.Height - 1; y++)
                    {
                        if (y == yCenter | x == xCenter)
                        {
                            bmpConstellation.SetPixel(x, y, Color.Tomato);
                        }
                    }
                }

                // skip the magnitude of the reference in calculation
                for (int j = 1; j <= intMag.Length - 1; j++)
                {
                    intMagMax = Math.Max(intMagMax, intMag(j));
                    // find the max magnitude to auto scale
                    dblAvgRad += intMag(j);
                }
                dblAvgRad = dblAvgRad / (intMag.Length - 1);
                // the average radius
                // For i As Integer = 0 To intPhases.Length - 1
                // Don't plot the first phase (reference)
                for (int i = 1; i <= intPhases.Length - 1; i++)
                {
                    dblRad = 40 * intMag(i) / intMagMax;
                    // scale the radius dblRad based on intMagMax
                    intX = Convert.ToInt32(xCenter + dblRad * Math.Cos(dblPlotRotation + intPhases(i) / 1000));
                    intY = Convert.ToInt32(yCenter + dblRad * Math.Sin(dblPlotRotation + intPhases(i) / 1000));
                    intP = Convert.ToInt32(Math.Round(0.001 * intPhases(i) / dbPhaseStep));
                    // compute the Phase and Raduius errors
                    dblRadError += Math.Pow((dblAvgRad - intMag(i)), 2);
                    dblPhaseError = Abs(((0.001 * intPhases(i)) - intP * dbPhaseStep));
                    // always positive and < .5 *  dblPhaseStep
                    dblPhaseErrorSum += dblPhaseError;
                    if (intX != xCenter & intY != yCenter)
                        bmpConstellation.SetPixel(intX, intY, Color.Yellow);
                    // don't plot on top of axis
                }
                dblRadError = Sqrt(dblRadError / (intPhases.Length - 1)) / dblAvgRad;
                if (blnQAM)
                {
                    // include Radius error for QAM ...Lifted from WINMOR....may need work
                    intQuality = Convert.ToInt32(Math.Max(0, (1 - dblRadError) * (100 - 200 * (dblPhaseErrorSum / (intPhases.Length - 1)) / dbPhaseStep)));
                }
                else
                {
                    // This gives good quality with probable seccessful decoding threshold around quality value of 60 to 70 
                    intQuality = Convert.ToInt32((100 - 200 * (dblPhaseErrorSum / (intPhases.Length - 1)) / dbPhaseStep));
                    // ignore radius error for (PSK) but include for QAM
                    //Debug.WriteLine("  Avg Radius Error: " & Format(dblRadError, "0.0"))
                }

                if (MCB.AccumulateStats)
                {
                    stcQualityStats.intPSKQualityCnts(intPSKIndex) += 1;
                    stcQualityStats.intPSKQuality(intPSKIndex) += intQuality;
                    stcQualityStats.intPSKSymbolsDecoded += intPhases.Length;
                }
                stcStatus.ControlName = "lblQuality";
                stcStatus.Text = strMod + " Quality: " + intQuality.ToString;
                queTNCStatus.Enqueue(stcStatus);
                stcStatus.ControlName = "ConstellationPlot";
                queTNCStatus.Enqueue(stcStatus);
            }
            catch (Exception ex)
            {
                Logs.Exception("[Main.UpdatePhaseConstellation] Err: " + ex.ToString);
            }
        }

        internal void Update4FSKConstellation(ref Int32[] intToneMags, ref Int32 intQuality)
        {
            // Subroutine to update bmpConstellation plot for 4FSK modes...


            try
            {

                double dblRad = 0;
                Int32 intToneSum = 0;
                double intMagMax = 0;
                double dblPi4 = 0.25 * Math.PI;

                double dblDistanceSum = 0;
                double dblPlotRotation = 0;
                Status stcStatus = default(Status);
                int yCenter = 0;
                int xCenter = 0;
                Int32 intRad = default(Int32);
                Int32 intFSKQual = default(Int32);
                System.Drawing.Color clrPixel = default(System.Drawing.Color);
                bmpConstellation = new Bitmap(89, 89);
                // Draw the axis and paint the black background area
                yCenter = (bmpConstellation.Height - 1) / 2;
                xCenter = (bmpConstellation.Width - 1) / 2;
                for (int x = 0; x <= bmpConstellation.Width - 1; x++)
                {
                    for (int y = 0; y <= bmpConstellation.Height - 1; y++)
                    {
                        if (y == yCenter | x == xCenter)
                        {
                            bmpConstellation.SetPixel(x, y, Color.DeepSkyBlue);
                        }
                    }
                }

                //Dim intMinMag As Integer = 1000000.0
                //Dim intMaxMag As Integer = 0
                //For i As Integer = 0 To intToneMags.Length - 1
                //    If intToneMags(i) < intMinMag Then
                //        intMinMag = intToneMags(i)
                //    End If
                //    If intToneMags(i) > intMaxMag Then
                //        intMaxMag = intToneMags(i)
                //    End If
                //Next
                //Debug.WriteLine("Mag Tone Min =" & intMinMag.ToString & "  Mag Tone Max = " & intMaxMag.ToString)

                // for the number of symbols represented by intToneMags
                for (int i = 0; i <= (intToneMags.Length - 1); i += 4)
                {
                    intToneSum = intToneMags(i) + intToneMags(i + 1) + intToneMags(i + 2) + intToneMags(i + 3);
                    if (intToneMags(i) > intToneMags(i + 1) & intToneMags(i) > intToneMags(i + 2) & intToneMags(i) > intToneMags(i + 3))
                    {
                        intRad = 45 - 60 * (intToneMags(i + 1) + intToneMags(i + 2) + intToneMags(i + 3)) / intToneSum;
                        if (intRad < 15)
                        {
                            clrPixel = Color.Tomato;
                        }
                        else if (intRad < 30)
                        {
                            clrPixel = Color.Gold;
                        }
                        else
                        {
                            clrPixel = Color.Lime;
                        }
                        bmpConstellation.SetPixel(xCenter + intRad, yCenter + 1, clrPixel);
                        // don't plot on top of axis
                        bmpConstellation.SetPixel(xCenter + intRad, yCenter - 1, clrPixel);
                        // don't plot on top of axis
                        bmpConstellation.SetPixel(xCenter + intRad, yCenter + 2, clrPixel);
                        // don't plot on top of axis
                        bmpConstellation.SetPixel(xCenter + intRad, yCenter - 2, clrPixel);
                        // don't plot on top of axis
                    }
                    else if (intToneMags(i + 1) > intToneMags(i) & intToneMags(i + 1) > intToneMags(i + 2) & intToneMags(i + 1) > intToneMags(i + 3))
                    {
                        intRad = 45 - 60 * (intToneMags(i) + intToneMags(i + 2) + intToneMags(i + 3)) / intToneSum;
                        if (intRad < 15)
                        {
                            clrPixel = Color.Tomato;
                        }
                        else if (intRad < 30)
                        {
                            clrPixel = Color.Gold;
                        }
                        else
                        {
                            clrPixel = Color.Lime;
                        }
                        bmpConstellation.SetPixel(xCenter + 1, yCenter + intRad, clrPixel);
                        // don't plot on top of axis
                        bmpConstellation.SetPixel(xCenter - 1, yCenter + intRad, clrPixel);
                        // don't plot on top of axis
                        bmpConstellation.SetPixel(xCenter + 2, yCenter + intRad, clrPixel);
                        // don't plot on top of axis
                        bmpConstellation.SetPixel(xCenter - 2, yCenter + intRad, clrPixel);
                        // don't plot on top of axis
                    }
                    else if (intToneMags(i + 2) > intToneMags(i) & intToneMags(i + 2) > intToneMags(i + 1) & intToneMags(i + 2) > intToneMags(i + 3))
                    {
                        intRad = 45 - 60 * (intToneMags(i + 1) + intToneMags(i) + intToneMags(i + 3)) / intToneSum;
                        if (intRad < 15)
                        {
                            clrPixel = Color.Tomato;
                        }
                        else if (intRad < 30)
                        {
                            clrPixel = Color.Gold;
                        }
                        else
                        {
                            clrPixel = Color.Lime;
                        }
                        bmpConstellation.SetPixel(xCenter - intRad, yCenter + 1, clrPixel);
                        // don't plot on top of axis
                        bmpConstellation.SetPixel(xCenter - intRad, yCenter - 1, clrPixel);
                        // don't plot on top of axis
                        bmpConstellation.SetPixel(xCenter - intRad, yCenter + 2, clrPixel);
                        // don't plot on top of axis
                        bmpConstellation.SetPixel(xCenter - intRad, yCenter - 2, clrPixel);
                        // don't plot on top of axis
                    }
                    else
                    {
                        intRad = 45 - 60 * (intToneMags(i + 1) + intToneMags(i + 2) + intToneMags(i)) / intToneSum;
                        if (intRad < 15)
                        {
                            clrPixel = Color.Tomato;
                        }
                        else if (intRad < 30)
                        {
                            clrPixel = Color.Gold;
                        }
                        else
                        {
                            clrPixel = Color.Lime;
                        }
                    }
                    bmpConstellation.SetPixel(xCenter + 1, yCenter - intRad, clrPixel);
                    // don't plot on top of axis
                    bmpConstellation.SetPixel(xCenter - 1, yCenter - intRad, clrPixel);
                    // don't plot on top of axis
                    bmpConstellation.SetPixel(xCenter + 2, yCenter - intRad, clrPixel);
                    // don't plot on top of axis
                    bmpConstellation.SetPixel(xCenter - 2, yCenter - intRad, clrPixel);
                    // don't plot on top of axis

                    dblDistanceSum += (45 - intRad);
                }
                intFSKQual = Convert.ToInt32(100 - 1.666 * (dblDistanceSum / (intToneMags.Length / 4)));
                // factor 1.666 emperically chosen for calibration (Qual range 25 to 100)
                stcStatus.ControlName = "lblQuality";
                stcStatus.Text = "4FSK Quality: " + intFSKQual.ToString;
                queTNCStatus.Enqueue(stcStatus);

                if (MCB.AccumulateStats)
                {
                    stcQualityStats.int4FSKQualityCnts += 1;
                    stcQualityStats.int4FSKQuality += intFSKQual;
                }
                stcStatus.ControlName = "ConstellationPlot";
                queTNCStatus.Enqueue(stcStatus);
            }
            catch (Exception ex)
            {
                Logs.Exception("[Main.Update4FSKConstellation] Err: " + ex.ToString);
            }
        }
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateWaterfall_dblI_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();

        // Subroutine to update the waterfall display

        double[] static_UpdateWaterfall_dblI;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateWaterfall_dblQ_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        double[] static_UpdateWaterfall_dblQ;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateWaterfall_dblReF_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        double[] static_UpdateWaterfall_dblReF;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateWaterfall_dblImF_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        double[] static_UpdateWaterfall_dblImF;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateWaterfall_aryLastY_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        int[] static_UpdateWaterfall_aryLastY;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateWaterfall_intPtr_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        int static_UpdateWaterfall_intPtr;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateWaterfall_dblMag_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        double[] static_UpdateWaterfall_dblMag;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateWaterfall_FFT_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        FFT static_UpdateWaterfall_FFT;
        Int32 static_UpdateWaterfall_intWaterfallRow;
        // pointer to the current waterfall row 
        int static_UpdateWaterfall_intLastBusyStatus;
        private void UpdateWaterfall(ref byte[] bytNewSamples)
        {
            lock (static_UpdateWaterfall_dblI_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateWaterfall_dblI_Init))
                    {
                        static_UpdateWaterfall_dblI = new double[1024];
                    }
                }
                finally
                {
                    static_UpdateWaterfall_dblI_Init.State = 1;
                }
            }
            lock (static_UpdateWaterfall_dblQ_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateWaterfall_dblQ_Init))
                    {
                        static_UpdateWaterfall_dblQ = new double[1024];
                    }
                }
                finally
                {
                    static_UpdateWaterfall_dblQ_Init.State = 1;
                }
            }
            lock (static_UpdateWaterfall_dblReF_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateWaterfall_dblReF_Init))
                    {
                        static_UpdateWaterfall_dblReF = new double[1024];
                    }
                }
                finally
                {
                    static_UpdateWaterfall_dblReF_Init.State = 1;
                }
            }
            lock (static_UpdateWaterfall_dblImF_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateWaterfall_dblImF_Init))
                    {
                        static_UpdateWaterfall_dblImF = new double[1024];
                    }
                }
                finally
                {
                    static_UpdateWaterfall_dblImF_Init.State = 1;
                }
            }
            lock (static_UpdateWaterfall_aryLastY_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateWaterfall_aryLastY_Init))
                    {
                        static_UpdateWaterfall_aryLastY = new int[256];
                    }
                }
                finally
                {
                    static_UpdateWaterfall_aryLastY_Init.State = 1;
                }
            }
            lock (static_UpdateWaterfall_intPtr_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateWaterfall_intPtr_Init))
                    {
                        static_UpdateWaterfall_intPtr = 0;
                    }
                }
                finally
                {
                    static_UpdateWaterfall_intPtr_Init.State = 1;
                }
            }
            lock (static_UpdateWaterfall_dblMag_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateWaterfall_dblMag_Init))
                    {
                        static_UpdateWaterfall_dblMag = new double[207];
                    }
                }
                finally
                {
                    static_UpdateWaterfall_dblMag_Init.State = 1;
                }
            }
            lock (static_UpdateWaterfall_FFT_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateWaterfall_FFT_Init))
                    {
                        static_UpdateWaterfall_FFT = new FFT();
                    }
                }
                finally
                {
                    static_UpdateWaterfall_FFT_Init.State = 1;
                }
            }

            double dblMagAvg = 0;
            int intTrace = 0;
            Int32 intTuneLineLow = default(Int32);
            Int32 intTuneLineHi = default(Int32);
            Int32 intDelta = default(Int32);
            System.Drawing.Color clrTLC = Color.Chartreuse;
            int intBusyStatus = 0;
            for (int i = 0; i <= bytNewSamples.Length - 1; i += 2)
            {
                static_UpdateWaterfall_dblI(static_UpdateWaterfall_intPtr) = Convert.ToDouble(System.BitConverter.ToInt16(bytNewSamples, i));
                static_UpdateWaterfall_intPtr += 1;
                if (static_UpdateWaterfall_intPtr > 1023)
                    break; // TODO: might not be correct. Was : Exit For
            }
            if (static_UpdateWaterfall_intPtr < 1024)
                return;
            static_UpdateWaterfall_intPtr = 0;
            static_UpdateWaterfall_FFT.FourierTransform(1024, static_UpdateWaterfall_dblI, static_UpdateWaterfall_dblQ, static_UpdateWaterfall_dblReF, static_UpdateWaterfall_dblImF, false);
            for (int i = 0; i <= static_UpdateWaterfall_dblMag.Length - 1; i++)
            {
                //starting at ~300 Hz to ~2700 Hz Which puts the center of the signal in the center of the window (~1500Hz)
                static_UpdateWaterfall_dblMag(i) = (Math.Pow(static_UpdateWaterfall_dblReF(i + 25), 2) + Math.Pow(static_UpdateWaterfall_dblImF(i + 25), 2));
                // first pass 
                dblMagAvg += static_UpdateWaterfall_dblMag(i);
            }
            intDelta = (stcConnection.intSessionBW / 2 + MCB.TuningRange) / 11.719;
            intTuneLineLow = Max((103 - intDelta), 3);
            intTuneLineHi = Min((103 + intDelta), 203);

            intBusyStatus = objBusy.BusyDetect(static_UpdateWaterfall_dblMag, intTuneLineLow, intTuneLineHi);
            if (intBusyStatus == 1)
            {
                clrTLC = Color.Fuchsia;

            }
            else if (intBusyStatus == 2)
            {
                clrTLC = Color.Gold;
            }
            if (static_UpdateWaterfall_intLastBusyStatus == 0 & intBusyStatus > 0)
            {
                objHI.QueueCommandToHost("BUSY TRUE");
                Debug.WriteLine("BUSY TRUE");
                static_UpdateWaterfall_intLastBusyStatus = intBusyStatus;
            }
            else if (intBusyStatus == 0 & static_UpdateWaterfall_intLastBusyStatus > 0)
            {
                objHI.QueueCommandToHost("BUSY FALSE");
                Debug.WriteLine("BUSY FALSE");
                static_UpdateWaterfall_intLastBusyStatus = intBusyStatus;
            }

            try
            {
                dblMagAvg = Math.Log10(dblMagAvg / 5000);
                for (int i = 0; i <= static_UpdateWaterfall_dblMag.Length - 1; i++)
                {
                    // The following provides some AGC over the waterfall to compensate for avg input level.
                    double y1 = (0.25 + 2.5 / dblMagAvg) * Math.Log10(0.01 + static_UpdateWaterfall_dblMag(i));
                    System.Drawing.Color objColor = default(System.Drawing.Color);
                    //  Set the pixel color based on the intensity (log) of the spectral line
                    if (y1 > 6.5)
                    {
                        objColor = Color.Orange;
                        // Strongest spectral line 
                    }
                    else if (y1 > 6)
                    {
                        objColor = Color.Khaki;
                    }
                    else if (y1 > 5.5)
                    {
                        objColor = Color.Cyan;
                    }
                    else if (y1 > 5)
                    {
                        objColor = Color.DeepSkyBlue;
                    }
                    else if (y1 > 4.5)
                    {
                        objColor = Color.RoyalBlue;
                    }
                    else if (y1 > 4)
                    {
                        objColor = Color.Navy;
                    }
                    else
                    {
                        objColor = Color.Black;
                        // Weakest spectral line
                    }
                    if (i == 103)
                    {
                        bmpSpectrum.SetPixel(i, static_UpdateWaterfall_intWaterfallRow, Color.Tomato);
                        // 1500 Hz line (center)
                    }
                    else if ((i == intTuneLineLow | i == intTuneLineLow - 1 | i == intTuneLineHi | i == intTuneLineHi + 1))
                    {
                        bmpSpectrum.SetPixel(i, static_UpdateWaterfall_intWaterfallRow, clrTLC);
                    }
                    else
                    {
                        bmpSpectrum.SetPixel(i, static_UpdateWaterfall_intWaterfallRow, objColor);
                        // Else plot the pixel as received
                    }
                }
                // Using a new bit map allows merging the two parts of bmpSpectrum and plotting all at one time to eliminate GDI+ fault.
                intTrace = 1;
                bmpNewSpectrum = new Bitmap(bmpSpectrum.Width, bmpSpectrum.Height);
                // Top rectangle of the waterfall is Waterfall Row to bottom of bmpSpectrum
                intTrace = 2;
                Rectangle destRect1 = new Rectangle(0, 0, bmpSpectrum.Width, bmpSpectrum.Height - static_UpdateWaterfall_intWaterfallRow);
                // Now create rectangle for the bottom part of the waterfall. Top of bmpSpectrum to intWaterfallRow -1
                intTrace = 3;
                Rectangle destRect2 = new Rectangle(0, bmpSpectrum.Height - static_UpdateWaterfall_intWaterfallRow, bmpSpectrum.Width, static_UpdateWaterfall_intWaterfallRow);
                // Create a new graphics area to draw into the new bmpNewSpectrum
                intTrace = 4;
                Graphics graComposit = Graphics.FromImage(bmpNewSpectrum);
                // add the two rectangles to the graComposit
                intTrace = 5;
                graComposit.DrawImage(bmpSpectrum, destRect1, 0, static_UpdateWaterfall_intWaterfallRow, bmpSpectrum.Width, bmpSpectrum.Height - static_UpdateWaterfall_intWaterfallRow, GraphicsUnit.Pixel);
                intTrace = 6;
                graComposit.DrawImage(bmpSpectrum, destRect2, 0, 0, bmpSpectrum.Width, static_UpdateWaterfall_intWaterfallRow, GraphicsUnit.Pixel);
                intTrace = 7;
                graComposit.Dispose();
                //  the new composit bitmap has been constructed
                intTrace = 8;
                if ((graFrequency != null))
                {
                    intTrace = 9;
                    graFrequency.Dispose();
                    // this permits writing back to the graFrequency without a GDI+ fault.
                }
                intTrace = 10;
                graFrequency = pnlWaterfall.CreateGraphics;
                intTrace = 11;
                graFrequency.DrawImage(bmpNewSpectrum, 0, 0);
                // Draw the new bitmap in one update to avoid a possible GDI+ fault
                static_UpdateWaterfall_intWaterfallRow -= 1;
                // Move the WaterFallRow back to point to the oldest received spectrum 
                intTrace = 12;
                if (static_UpdateWaterfall_intWaterfallRow < 0)
                    static_UpdateWaterfall_intWaterfallRow = bmpSpectrum.Height - 1;
                // Makes the bitmap a circular buffer
            }
            catch (Exception ex)
            {
                Logs.Exception("[Main.UpdateWaterfall] Err #: " + Err.Number.ToString + "  Exception: " + ex.ToString + "  intTrace=" + intTrace.ToString);
            }
        }
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateSpectrum_dblI_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        // UpdateWaterfall

        // Subroutine to update the spectrum display

        double[] static_UpdateSpectrum_dblI;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateSpectrum_dblQ_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        double[] static_UpdateSpectrum_dblQ;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateSpectrum_dblReF_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        double[] static_UpdateSpectrum_dblReF;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateSpectrum_dblImF_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        double[] static_UpdateSpectrum_dblImF;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateSpectrum_aryLastY_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        int[] static_UpdateSpectrum_aryLastY;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateSpectrum_intPtr_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        int static_UpdateSpectrum_intPtr;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateSpectrum_dblMag_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        double[] static_UpdateSpectrum_dblMag;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateSpectrum_FFT_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        FFT static_UpdateSpectrum_FFT;
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_UpdateSpectrum_intPriorY_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        int[] static_UpdateSpectrum_intPriorY;
        int static_UpdateSpectrum_intLastBusyStatus;
        double static_UpdateSpectrum_dblMaxScale;
        private void UpdateSpectrum(ref byte[] bytNewSamples)
        {
            lock (static_UpdateSpectrum_dblI_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateSpectrum_dblI_Init))
                    {
                        static_UpdateSpectrum_dblI = new double[1024];
                    }
                }
                finally
                {
                    static_UpdateSpectrum_dblI_Init.State = 1;
                }
            }
            lock (static_UpdateSpectrum_dblQ_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateSpectrum_dblQ_Init))
                    {
                        static_UpdateSpectrum_dblQ = new double[1024];
                    }
                }
                finally
                {
                    static_UpdateSpectrum_dblQ_Init.State = 1;
                }
            }
            lock (static_UpdateSpectrum_dblReF_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateSpectrum_dblReF_Init))
                    {
                        static_UpdateSpectrum_dblReF = new double[1024];
                    }
                }
                finally
                {
                    static_UpdateSpectrum_dblReF_Init.State = 1;
                }
            }
            lock (static_UpdateSpectrum_dblImF_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateSpectrum_dblImF_Init))
                    {
                        static_UpdateSpectrum_dblImF = new double[1024];
                    }
                }
                finally
                {
                    static_UpdateSpectrum_dblImF_Init.State = 1;
                }
            }
            lock (static_UpdateSpectrum_aryLastY_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateSpectrum_aryLastY_Init))
                    {
                        static_UpdateSpectrum_aryLastY = new int[256];
                    }
                }
                finally
                {
                    static_UpdateSpectrum_aryLastY_Init.State = 1;
                }
            }
            lock (static_UpdateSpectrum_intPtr_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateSpectrum_intPtr_Init))
                    {
                        static_UpdateSpectrum_intPtr = 0;
                    }
                }
                finally
                {
                    static_UpdateSpectrum_intPtr_Init.State = 1;
                }
            }
            lock (static_UpdateSpectrum_dblMag_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateSpectrum_dblMag_Init))
                    {
                        static_UpdateSpectrum_dblMag = new double[207];
                    }
                }
                finally
                {
                    static_UpdateSpectrum_dblMag_Init.State = 1;
                }
            }
            lock (static_UpdateSpectrum_FFT_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateSpectrum_FFT_Init))
                    {
                        static_UpdateSpectrum_FFT = new FFT();
                    }
                }
                finally
                {
                    static_UpdateSpectrum_FFT_Init.State = 1;
                }
            }
            lock (static_UpdateSpectrum_intPriorY_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_UpdateSpectrum_intPriorY_Init))
                    {
                        static_UpdateSpectrum_intPriorY = new int[257];
                    }
                }
                finally
                {
                    static_UpdateSpectrum_intPriorY_Init.State = 1;
                }
            }

            double[] dblMagBusy = new double[207];
            double dblMagMax = 1E-10;
            double dblMagMin = 10000000000.0;
            Int32 intTuneLineLow = default(Int32);
            Int32 intTuneLineHi = default(Int32);
            Int32 intDelta = default(Int32);
            int intBusyStatus = 0;
            int Trace = 0;
            System.Drawing.Color clrTLC = Color.Chartreuse;

            for (int i = 0; i <= bytNewSamples.Length - 1; i += 2)
            {
                static_UpdateSpectrum_dblI(static_UpdateSpectrum_intPtr) = Convert.ToDouble(System.BitConverter.ToInt16(bytNewSamples, i));
                static_UpdateSpectrum_intPtr += 1;
                if (static_UpdateSpectrum_intPtr > 1023)
                    break; // TODO: might not be correct. Was : Exit For
            }

            if (static_UpdateSpectrum_intPtr < 1024)
                return;

            //FFT.FourierTransform(1024, dblI, dblQ, dblReF, dblImF, False)
            //For i As Integer = 0 To dblMag.Length - 1
            //    'starting at ~300 Hz to ~2700 Hz Which puts the center of the signal in the center of the window (~1500Hz)
            //    dblMag(i) = (dblReF(i + 25) ^ 2 + dblImF(i + 25) ^ 2) ' first pass 
            //    dblMagAvg += dblMag(i)
            //Next i
            //intDelta = (stcConnection.intSessionBW \ 2 + MCB.TuningRange) / 11.719
            //intTuneLineLow = Max((103 - intDelta), 3)
            //intTuneLineHi = Min((103 + intDelta), 203)

            //intBusyStatus = objBusy.BusyDetect(dblMag, intTuneLineLow, intTuneLineHi)
            //If intBusyStatus = 1 Then
            //    clrTLC = Color.Fuchsia

            //ElseIf intBusyStatus = 2 Then
            //    clrTLC = Color.Orange
            //End If



            // Busy detector call needs work...OK in Waterfall!

            static_UpdateSpectrum_intPtr = 0;
            static_UpdateSpectrum_FFT.FourierTransform(1024, static_UpdateSpectrum_dblI, static_UpdateSpectrum_dblQ, static_UpdateSpectrum_dblReF, static_UpdateSpectrum_dblImF, false);
            Trace = 1;
            intDelta = ((stcConnection.intSessionBW / 2) + MCB.TuningRange) / 11.719;
            intTuneLineLow = Max((103 - intDelta), 3);
            intTuneLineHi = Min((103 + intDelta), 203);


            for (int i = 0; i <= static_UpdateSpectrum_dblMag.Length - 1; i++)
            {
                //starting at ~300 Hz to ~2700 Hz Which puts the center of the window at 1500Hz
                dblMagBusy(i) = (Math.Pow(static_UpdateSpectrum_dblReF(i + 25), 2) + Math.Pow(static_UpdateSpectrum_dblImF(i + 25), 2));
                //dblMag(i) = 0.2 * (dblReF(i + 25) ^ 2 + dblImF(i + 25) ^ 2) + 0.8 * dblMag(i) ' first pass 
                static_UpdateSpectrum_dblMag(i) = 0.2 * dblMagBusy(i) + 0.8 * static_UpdateSpectrum_dblMag(i);
                // first pass 
                dblMagMax = Math.Max(dblMagMax, static_UpdateSpectrum_dblMag(i));
                dblMagMin = Math.Min(dblMagMin, static_UpdateSpectrum_dblMag(i));
            }
            intBusyStatus = objBusy.BusyDetect(dblMagBusy, intTuneLineLow, intTuneLineHi);
            if (intBusyStatus == 1)
            {
                clrTLC = Color.Fuchsia;
            }
            else if (intBusyStatus == 2)
            {
                clrTLC = Color.Orange;
            }
            if (static_UpdateSpectrum_intLastBusyStatus == 0 & intBusyStatus > 0)
            {
                objHI.QueueCommandToHost("BUSY TRUE");
                Debug.WriteLine("BUSY TRUE");
                static_UpdateSpectrum_intLastBusyStatus = intBusyStatus;
            }
            else if (intBusyStatus == 0 & static_UpdateSpectrum_intLastBusyStatus > 0)
            {
                objHI.QueueCommandToHost("BUSY FALSE");
                Debug.WriteLine("BUSY FALSE");
                static_UpdateSpectrum_intLastBusyStatus = intBusyStatus;
            }
            // This performs an auto scaling mechansim with fast attack and slow release
            // more than 10000:1 difference Max:Min
            if (dblMagMin / dblMagMax < 0.0001)
            {
                static_UpdateSpectrum_dblMaxScale = Max(dblMagMax, static_UpdateSpectrum_dblMaxScale * 0.9);
            }
            else
            {
                static_UpdateSpectrum_dblMaxScale = Max(10000 * dblMagMin, dblMagMax);
            }
            Trace = 2;
            try
            {
                //            'InititializeSpectrum(Color.Black)
                bmpNewSpectrum = new Bitmap(intBMPSpectrumWidth, intBMPSpectrumHeight);
                for (int i = 0; i <= static_UpdateSpectrum_dblMag.Length - 1; i++)
                {
                    // The following provides some AGC over the waterfall to compensate for avg input level.
                    int y1 = Convert.ToInt32(-0.25 * (intBMPSpectrumHeight - 1) * Log10((Max(static_UpdateSpectrum_dblMag(i), static_UpdateSpectrum_dblMaxScale / 10000)) / static_UpdateSpectrum_dblMaxScale));
                    // range should be 0 to bmpSpectrumHeight -1
                    System.Drawing.Color objColor = Color.Yellow;
                    // Redraw center and bandwidth lines if change in display
                    if (intTuneLineLow != intSavedTuneLineLow)
                    {
                        for (int j = 0; j <= intBMPSpectrumHeight - 1; j++)
                        {
                            bmpNewSpectrum.SetPixel(103, j, Color.Tomato);
                            bmpNewSpectrum.SetPixel(intSavedTuneLineLow, j, Color.Black);
                            bmpNewSpectrum.SetPixel(intSavedTuneLineHi, j, Color.Black);
                            bmpNewSpectrum.SetPixel(Max(0, intSavedTuneLineLow - 1), j, Color.Black);
                            bmpNewSpectrum.SetPixel(intSavedTuneLineHi + 1, j, Color.Black);
                            bmpNewSpectrum.SetPixel(intTuneLineLow, j, clrTLC);
                            bmpNewSpectrum.SetPixel(intTuneLineHi, j, clrTLC);
                            bmpNewSpectrum.SetPixel(intTuneLineLow - 1, j, clrTLC);
                            bmpNewSpectrum.SetPixel(intTuneLineHi + 1, j, clrTLC);
                        }
                        intSavedTuneLineHi = intTuneLineHi;
                        intSavedTuneLineLow = intTuneLineLow;
                    }
                    // Clear the old pixels and put in new ones if not on a center or Tuning lines
                    if (!((i == 103) | (i == intTuneLineHi) | (i == intTuneLineLow) | (i == intTuneLineHi + 1) | (i == intTuneLineLow - 1)))
                    {
                        // Set the prior plotted pixels to black
                        bmpNewSpectrum.SetPixel(i, static_UpdateSpectrum_intPriorY(i), Color.Black);
                        if (static_UpdateSpectrum_intPriorY(i) < (intBMPSpectrumHeight - 2))
                            bmpNewSpectrum.SetPixel(i, static_UpdateSpectrum_intPriorY(i) + 1, Color.Black);
                        static_UpdateSpectrum_intPriorY(i) = y1;
                        bmpNewSpectrum.SetPixel(i, y1, Color.Yellow);
                        if (y1 < (intBMPSpectrumHeight - 2))
                            bmpNewSpectrum.SetPixel(i, y1 + 1, Color.Gold);
                    }
                }
                Trace = 10;
                if ((graFrequency != null))
                {
                    Trace = 11;
                    graFrequency.Dispose();
                    // this permits writing back to the graFrequency without a GDI+ fault.
                }
                graFrequency = pnlWaterfall.CreateGraphics;
                graFrequency.DrawImage(bmpNewSpectrum, 0, 0);
                // Draw the new bitmap in one update to avoid a possible GDI+ fault
            }
            catch (Exception ex)
            {
                Logs.Exception("[Main.UpdateSpectrum] Err #: " + Err.Number.ToString + "  Exception: " + ex.ToString + "  Trace=" + Trace.ToString);
            }
        }
        readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_ProcessCapturedData_intGraphicsCtr_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
        // UpdateSpectrum

        // Subroutine to process Sound card Data capture events

        // Subroutine to process data captured by the CODEC...
        Int32 static_ProcessCapturedData_intGraphicsCtr;
        private void ProcessCapturedData()
        {
            byte[] bytCaptureData = new byte[-1 + 1];
            int intReadPos = 0;
            int intCapturePos = 0;
            int intLockSize = 0;
            Status stcStatus = null;
            int intRcvPeak = 0;
            lock (static_ProcessCapturedData_intGraphicsCtr_Init)
            {
                try
                {
                    if (InitStaticVariableHelper(static_ProcessCapturedData_intGraphicsCtr_Init))
                    {
                        static_ProcessCapturedData_intGraphicsCtr = 0;
                    }
                }
                finally
                {
                    static_ProcessCapturedData_intGraphicsCtr_Init.State = 1;
                }
            }
            try
            {
                if (null == objCapture)
                    return;
                // Get the data in the CaptureBuffer
                objCapture.GetCurrentPosition(intCapturePos, intReadPos);
                intLockSize = intReadPos - intNextCaptureOffset;
                if (intLockSize < 0)
                    intLockSize += intCaptureBufferSize;
                // Block align lock size so that we are always write on a boundary
                intLockSize -= intLockSize % intNotifySize;
                if (0 == intLockSize)
                    return;
                dttLastSoundCardSample = Now;
                // Read the capture buffer.
                if (!blnSCCapturing)
                    return;
                bytCaptureData = Convert.ToByte(objCapture.Read(intNextCaptureOffset, typeof(byte), LockFlag.None, intLockSize));

            }
            catch (Exception ex)
            {
                Logs.Exception("[Main.ProcessCapturedData]1 Err:  " + ex.ToString);
                return;
            }
            try
            {
                int intSample = 0;
                for (int i = 0; i <= bytCaptureData.Length - 1; i += 2)
                {
                    intSample = System.BitConverter.ToInt16(bytCaptureData, i);
                    intRcvPeak = Math.Max(intRcvPeak, Math.Abs(intSample));
                }
                if (!blnSCCapturing)
                    return;
                // The following code blocks waterfall operation and processing if playing.  Commented out temporarily for full duplex testing
                //If objPlayback IsNot Nothing AndAlso objPlayback.Status.Playing Then
                //    intNextCaptureOffset += bytCaptureData.Length
                //    intNextCaptureOffset = intNextCaptureOffset Mod intCaptureBufferSize ' Circular buffer
                //    Exit Sub  ' if playing don't update waterfall or Receive level indicator
                //End If
                // ********************
                stcStatus.Value = Math.Min(Convert.ToInt32(Math.Sqrt(intRcvPeak)), 181);
                stcStatus.ControlName = "prgReceiveLevel";
                queTNCStatus.Enqueue(stcStatus);
                objProtocol.ProcessNewSamples(bytCaptureData);
                if (MCB.DisplayWaterfall)
                {
                    UpdateWaterfall(ref bytCaptureData);
                }
                else if (MCB.DisplaySpectrum)
                {
                    UpdateSpectrum(ref bytCaptureData);
                }
                else
                {
                    static_ProcessCapturedData_intGraphicsCtr += 1;
                    if (static_ProcessCapturedData_intGraphicsCtr > 20)
                    {
                        WriteTextToSpectrum("Graphics Disabled", Brushes.Yellow);
                        static_ProcessCapturedData_intGraphicsCtr = 0;
                    }

                }
                intNextCaptureOffset += bytCaptureData.Length;
                intNextCaptureOffset = intNextCaptureOffset % intCaptureBufferSize;
                // Circular buffer
            }
            catch (Exception ex)
            {
                Logs.Exception("[Main.ProcessCapturedData]3 Err: " + ex.ToString);
            }
        }

        // Test code to send a frame from the Test Form...throw away code
        public void SendTestFrame(Int32[] intFilteredSamples, string strLastFileStream, Int32 intRepeats)
        {
            int intPeak = 0;
            double dblRMS = 0;
            WaveTools objWT = new WaveTools();


            ClearTuningStats();
            ClearQualityStats();
            objWT.ComputePeakToRMS(intFilteredSamples, intPeak, dblRMS);
            Debug.WriteLine("Filtered " + strLastFileStream + ".wav  Pk = " + intPeak.ToString + "   RMS = " + Strings.Format(dblRMS, "#.0") + "  Pk:RMS = " + Strings.Format(intPeak / dblRMS, "#.00"));
            objMod.CreateWaveStream(intFilteredSamples);
            State = ReceiveState.SearchingForLeader;
            PlaySoundStream();
            dblMaxLeaderSN = 0;
            intRepeatCnt = intRepeats;
        }

        // Subroutine called by the protocol to send a data or command frame

        public void SendFrame(Int32[] intFilteredSamples, string strLastFileStream)
        {
            if (!object.ReferenceEquals(objPlayback, (null)))
            {
                while (objPlayback.Status.Playing)
                {
                    Thread.Sleep(50);
                }
            }
            objMod.CreateWaveStream(intFilteredSamples);
            PlaySoundStream();
        }

        private void WaterfallToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            MCB.DisplaySpectrum = false;
            ClearSpectrum();
            MCB.DisplayWaterfall = true;
            objIniFile.WriteString("ARDOP_Win TNC", "Display Waterfall", MCB.DisplayWaterfall.ToString);
            objIniFile.WriteString("ARDOP_Win TNC", "Display Spectrum", MCB.DisplaySpectrum.ToString);
            objIniFile.Flush();
        }

        private void SpectrumToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            MCB.DisplayWaterfall = false;
            ClearSpectrum();
            MCB.DisplaySpectrum = true;
            objIniFile.WriteString("ARDOP_Win TNC", "Display Waterfall", MCB.DisplayWaterfall.ToString);
            objIniFile.WriteString("ARDOP_Win TNC", "Display Spectrum", MCB.DisplaySpectrum.ToString);
            objIniFile.Flush();
        }

        private void DisableToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            MCB.DisplaySpectrum = false;
            MCB.DisplayWaterfall = false;
            //WriteTextToSpectrum("Disabled", Brushes.Yellow)
            objIniFile.WriteString("ARDOP_Win TNC", "Display Waterfall", MCB.DisplayWaterfall.ToString);
            objIniFile.WriteString("ARDOP_Win TNC", "Display Spectrum", MCB.DisplaySpectrum.ToString);
            objIniFile.Flush();
        }

        private void tmrStartCODEC_Tick(object sender, System.EventArgs e)
        {
            string strFault = "";
            tmrStartCODEC.Stop();

            if (!StartCodec(ref strFault))
            {
                Logs.Exception("[tmrStartCodec_Tick] Failure to start Codec! Fault= " + strFault);
                blnCodecStarted = false;
            }
            else
            {
                Debug.WriteLine("Codec Started OK");
                blnCodecStarted = true;
            }
        }


        private void HelpToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
        }

        private void HelpToolStripMenuItem1_Click(System.Object sender, System.EventArgs e)
        {
            // Opens a file dialog to access log files for reading or deletion...
            try
            {
                OpenFileDialog dlgLogs = new OpenFileDialog();
                dlgLogs.Title = "Select a Log File to View...";
                dlgLogs.InitialDirectory = strExecutionDirectory + "Logs\\";
                dlgLogs.Filter = "Log File(.log;.txt)|*.log;*.txt";
                dlgLogs.RestoreDirectory = true;
                dlgLogs.Multiselect = true;
                if (dlgLogs.ShowDialog() == Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        Process.Start(dlgLogs.FileName);
                    }
                    catch
                    {
                        Interaction.MsgBox(Err.Description, MsgBoxStyle.Information);
                    }
                }
            }
            catch
            {
                Logs.Exception("[Main.mnuLogs_Click] " + Err.Description);
            }
        }

        private void tmrLogStats_Tick(object sender, System.EventArgs e)
        {
            // This timer provides a 1 sec delay on looping test frames to allow stats to complete before logging
            tmrLogStats.Stop();
            LogTuningStats();
        }


        private void rdoFSK500New_CheckedChanged(System.Object sender, System.EventArgs e)
        {
        }

        private void ToolStripMenuItem1_Click(System.Object sender, System.EventArgs e)
        {
            if ((objRadio == null))
                objRadio = new Radio();
            objRadio.ShowDialog();
            if (objRadio.DialogResult == Windows.Forms.DialogResult.OK)
            {
                objRadio.CloseRadio();
                objRadio.Close();
                objRadio = null;
                if (MCB.RadioControl)
                {
                    objRadio = new Radio();
                    objRadio.OpenControlPort();
                    objRadio.OpenPttPort();
                }
            }
        }

        private void AbortToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            AbortSoundStream();
        }

        private void TwoToneTestToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            objMod.CreateWaveStream(objMod.ModTwoToneTest());
            strLastWavStream = "5 Sec Two Tone Test";
            PlaySoundStream();
        }

        private void CloseToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            if ((frmTest != null))
                frmTest.Close();
            this.Close();
        }

        private void ShowTestFormToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            if ((frmTest == null))
            {
                frmTest = new Test(this);
                frmTest.Show();
            }
            else
            {
                frmTest.BringToFront();
            }

        }

        // Test Code

        private void TestDataToHostToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            byte[] bytTest = GetBytes("ARQARQ Test Data from TNC");
            try
            {
            }
            catch (Exception ex)
            {
                Logs.WriteDebug("[ARQTestDataToHost Click] Err: " + ex.ToString);
            }
        }


        private void TestFECDataToHostToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            byte[] bytTest = GetBytes("FECFEC Test Data from TNC");
            try
            {
                objHI.QueueDataToHost(bytTest);
            }
            catch (Exception ex)
            {
                Logs.WriteDebug("[FECTestDataToHost Click] Err: " + ex.ToString);
            }
        }
        //End Test Code

        private void CWIDToolStripMenuItem_Click(System.Object sender, System.EventArgs e)
        {
            objMod.CWID("de KN6KB");

        }
        public Main()
        {
            Load += Main_Load;
            FormClosing += Main_FormClosing;
            FormClosed += Main_FormClosed;
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

    }
}
