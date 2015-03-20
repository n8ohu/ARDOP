using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
//using System.Math;
using System.Text;
using System.IO;
//using System.BitConverter;


namespace ARDOP
{
	public class WaveTools
	{
		// This class is used to Write or Read a 16  bit sample mono wave file in RIFF format

		//  Subroutine to Write a RIFF waveform file 
		public bool WriteRIFF(string strFilename, int intSampleRate, int intFmtChunkLength, ref byte[] aryWaveData)
		{
			//*************************************************************************
			//	Here is where the file will be created. A
			//	wave file is a RIFF file, which has chunks
			//	of data that describe what the file contains.
			//	A wave RIFF file is put together like this:
			//	The 12 byte RIFF chunk is constructed like this:
			//     Bytes(0 - 3) 'R' 'I' 'F' 'F'
			//	   Bytes 4 - 7 :	Length of file, minus the first 8 bytes of the RIFF description.
			//					(4 bytes for "WAVE" + 24 bytes for format chunk length +
			//					8 bytes for data chunk description + actual sample data size.)
			//     Bytes(8 - 11) 'W' 'A' 'V' 'E'
			//	The 24 byte FORMAT chunk is constructed like this:
			//     Bytes(0 - 3) 'f' 'm' 't' ' '
			//	   Bytes 4 - 7 :	The format chunk length. This is 16 or 18
			//	   Bytes 8 - 9 :	File padding. Always 1.
			//	   Bytes 10- 11:	Number of channels. Either 1 for mono,  or 2 for stereo.
			//	   Bytes 12- 15:	Sample rate.
			//	   Bytes 16- 19:	Number of bytes per second.
			//	   Bytes 20- 21:	Bytes per sample. 1 for 8 bit mono, 2 for 8 bit stereo or
			//					16 bit mono, 4 for 16 bit stereo.
			//	   Bytes 22- 23:	Number of bits per sample.
			//	The DATA chunk is constructed like this:
			//     Bytes(0 - 3) 'd' 'a' 't' 'a'
			//	   Bytes 4 - 7 :	Length of data, in bytes.
			//	   Bytes 8 -...:	Actual sample data.
			//**************************************************************************
			BinaryWriter Writer = default(BinaryWriter);
			FileStream WaveFile = default(FileStream);

			try {
				if (System.IO.File.Exists(strFilename))
					System.IO.File.Delete(strFilename);
				WaveFile = new FileStream(strFilename, FileMode.Create);
				Writer = new BinaryWriter(WaveFile);

				// Set up file with RIFF chunk info.
				char[] ChunkRiff = {
					Convert.ToChar("R"),
					Convert.ToChar("I"),
					Convert.ToChar("F"),
					Convert.ToChar("F")
				};
				char[] ChunkType = {
					Convert.ToChar("W"),
					Convert.ToChar("A"),
					Convert.ToChar("V"),
					Convert.ToChar("E")
				};
				char[] ChunkFmt = {
					Convert.ToChar("f"),
					Convert.ToChar("m"),
					Convert.ToChar("t"),
					Convert.ToChar(" ")
				};
				char[] ChunkData = {
					Convert.ToChar("d"),
					Convert.ToChar("a"),
					Convert.ToChar("t"),
					Convert.ToChar("a")
				};
				short shPad = 1;
				// File padding
				int intLength = 0;
				if (intFmtChunkLength == 16) {
					intLength = aryWaveData.Length + 36;
					// File length, minus first 8 bytes of RIFF description.
				} else if (intFmtChunkLength == 18) {
					intLength = aryWaveData.Length + 38;
					// File length, minus first 8 bytes of RIFF description.
				}
				short shBytesPerSample = 2;
				// Bytes per sample.
				// Fill in the riff info for the wave file.
				Writer.Write(ChunkRiff);
				Writer.Write(intLength);
				Writer.Write(ChunkType);
				// Fill in the format info for the wave file.
				Writer.Write(ChunkFmt);
				Writer.Write(intFmtChunkLength);
				Writer.Write(shPad);
				Writer.Write(Convert.ToInt16(1));
				// mono
				Writer.Write(Convert.ToInt32(intSampleRate));
				// sample rate in samples per second
				Writer.Write(Convert.ToInt32(2 * intSampleRate));
				// bytes per second
				Writer.Write(shBytesPerSample);
				Writer.Write(Convert.ToInt16(16));
				if (intFmtChunkLength == 18) {
					Writer.Write(Convert.ToInt16(0));
				}
				// DateTime.Now fill in the data chunk.
				Writer.Write(ChunkData);
				Writer.Write(Convert.ToInt32(aryWaveData.Length));
				// The length of a the following data file.
				Writer.Write(aryWaveData, 0, aryWaveData.Length);
				Writer.Flush();
				// Flush out any file buffers
				Writer.Close();
				// Close the file now.
				return true;
			} catch (Exception ex) {
				Debug.WriteLine("Exception [WaveTools.WriteRIFF] " + ex.ToString());
				return false;
			}

		}
		//WriteRIFF
		// This class is used to Write or Read a 16  bit sample mono wave file in RIFF format

		//  Subroutine to Write a floating point file in RIFF waveform file format for debugging
		public bool WriteFloatingRIFF(string strFilename, int intSampleRate, int intFmtChunkLength, ref double[] aryFloatingData)
		{
			int intSample = 0;
			// First find the max value of the floating to use as a nomalizer
			double dblDataMax = 0;
			for (int j = 0; j <= aryFloatingData.Length - 1; j++) {
				dblDataMax = Math.Max(dblDataMax, Math.Abs(aryFloatingData[j]));
			}
			double dblScale = 32000 / dblDataMax;
			// will scale the value to 32000 max (16 bit)
			byte[] aryWaveData = new byte[2 * aryFloatingData.Length];
			for (int j = 0; j <= aryFloatingData.Length - 1; j++) {
				intSample = Convert.ToInt32(dblScale * aryFloatingData[j]);
				aryWaveData[2 * j] = Convert.ToByte(intSample & 0xff);
				// LSByte
				aryWaveData[2 * j + 1] = Convert.ToByte((intSample & 0xff00) >> 8);
				// MSbyte
			}
			//*************************************************************************
			//	Here is where the file will be created. A
			//	wave file is a RIFF file, which has chunks
			//	of data that describe what the file contains.
			//	A wave RIFF file is put together like this:
			//	The 12 byte RIFF chunk is constructed like this:
			//     Bytes(0 - 3) 'R' 'I' 'F' 'F'
			//	   Bytes 4 - 7 :	Length of file, minus the first 8 bytes of the RIFF description.
			//					(4 bytes for "WAVE" + 24 bytes for format chunk length +
			//					8 bytes for data chunk description + actual sample data size.)
			//     Bytes(8 - 11) 'W' 'A' 'V' 'E'
			//	The 24 byte FORMAT chunk is constructed like this:
			//     Bytes(0 - 3) 'f' 'm' 't' ' '
			//	   Bytes 4 - 7 :	The format chunk length. This is 16 or 18
			//	   Bytes 8 - 9 :	File padding. Always 1.
			//	   Bytes 10- 11:	Number of channels. Either 1 for mono,  or 2 for stereo.
			//	   Bytes 12- 15:	Sample rate.
			//	   Bytes 16- 19:	Number of bytes per second.
			//	   Bytes 20- 21:	Bytes per sample. 1 for 8 bit mono, 2 for 8 bit stereo or
			//					16 bit mono, 4 for 16 bit stereo.
			//	   Bytes 22- 23:	Number of bits per sample.
			//	The DATA chunk is constructed like this:
			//     Bytes(0 - 3) 'd' 'a' 't' 'a'
			//	   Bytes 4 - 7 :	Length of data, in bytes.
			//	   Bytes 8 -...:	Actual sample data.
			//**************************************************************************
			BinaryWriter Writer = default(BinaryWriter);
			FileStream WaveFile = default(FileStream);

			try {
				if (System.IO.File.Exists(strFilename))
					System.IO.File.Delete(strFilename);
				WaveFile = new FileStream(strFilename, FileMode.Create);
				Writer = new BinaryWriter(WaveFile);

				// Set up file with RIFF chunk info.
				char[] ChunkRiff = {
					Convert.ToChar("R"),
					Convert.ToChar("I"),
					Convert.ToChar("F"),
					Convert.ToChar("F")
				};
				char[] ChunkType = {
					Convert.ToChar("W"),
					Convert.ToChar("A"),
					Convert.ToChar("V"),
					Convert.ToChar("E")
				};
				char[] ChunkFmt = {
					Convert.ToChar("f"),
					Convert.ToChar("m"),
					Convert.ToChar("t"),
					Convert.ToChar(" ")
				};
				char[] ChunkData = {
					Convert.ToChar("d"),
					Convert.ToChar("a"),
					Convert.ToChar("t"),
					Convert.ToChar("a")
				};
				short shPad = 1;
				// File padding
				int intLength = 0;
				if (intFmtChunkLength == 16) {
					intLength = aryWaveData.Length + 36;
					// File length, minus first 8 bytes of RIFF description.
				} else if (intFmtChunkLength == 18) {
					intLength = aryWaveData.Length + 38;
					// File length, minus first 8 bytes of RIFF description.
				}
				short shBytesPerSample = 2;
				// Bytes per sample.
				// Fill in the riff info for the wave file.
				Writer.Write(ChunkRiff);
				Writer.Write(intLength);
				Writer.Write(ChunkType);
				// Fill in the format info for the wave file.
				Writer.Write(ChunkFmt);
				Writer.Write(intFmtChunkLength);
				Writer.Write(shPad);
				Writer.Write(Convert.ToInt16(1));
				// mono
				Writer.Write(Convert.ToInt32(intSampleRate));
				// sample rate in samples per second
				Writer.Write(Convert.ToInt32(2 * intSampleRate));
				// bytes per second
				Writer.Write(shBytesPerSample);
				Writer.Write(Convert.ToInt16(16));
				if (intFmtChunkLength == 18) {
					Writer.Write(Convert.ToInt16(0));
				}
				// DateTime.Now fill in the data chunk.
				Writer.Write(ChunkData);
				Writer.Write(Convert.ToInt32(aryWaveData.Length));
				// The length of a the following data file.
				Writer.Write(aryWaveData, 0, aryWaveData.Length);
				Writer.Flush();
				// Flush out any file buffers
				Writer.Close();
				// Close the file now.
				return true;
			} catch (Exception ex) {
						Debug.WriteLine("Exception [WaveTools.WriteRIFF] " + ex.ToString());
				return false;
			}

		}
		//WriteFloatingRIFF
		//  Subroutine to Write a RIFF waveform to a memory Stream (no disc access) 
		public bool WriteRIFFStream(ref MemoryStream WaveStream, int intSampleRate, int intFmtChunkLength, ref byte[] aryWaveData)
		{
			//*************************************************************************
			//	Here is where the file will be created. A
			//	wave file is a RIFF file, which has chunks
			//	of data that describe what the file contains.
			//	A wave RIFF file is put together like this:
			//	The 12 byte RIFF chunk is constructed like this:
			//     Bytes(0 - 3) 'R' 'I' 'F' 'F'
			//	   Bytes 4 - 7 :	Length of file, minus the first 8 bytes of the RIFF description.
			//					(4 bytes for "WAVE" + 24 bytes for format chunk length +
			//					8 bytes for data chunk description + actual sample data size.)
			//     Bytes(8 - 11) 'W' 'A' 'V' 'E'
			//	The 24 byte FORMAT chunk is constructed like this:
			//     Bytes(0 - 3) 'f' 'm' 't' ' '
			//	   Bytes 4 - 7 :	The format chunk length. This is 16 or 18
			//	   Bytes 8 - 9 :	File padding. Always 1.
			//	   Bytes 10- 11:	Number of channels. Either 1 for mono,  or 2 for stereo.
			//	   Bytes 12- 15:	Sample rate.
			//	   Bytes 16- 19:	Number of bytes per second.
			//	   Bytes 20- 21:	Bytes per sample. 1 for 8 bit mono, 2 for 8 bit stereo or
			//					16 bit mono, 4 for 16 bit stereo.
			//	   Bytes 22- 23:	Number of bits per sample.
			//	The DATA chunk is constructed like this:
			//     Bytes(0 - 3) 'd' 'a' 't' 'a'
			//	   Bytes 4 - 7 :	Length of data, in bytes.
			//	   Bytes 8 -...:	Actual sample data.
			//**************************************************************************
			//TODO: Test of "back porch" extension for PTT
			//ReDim Preserve aryWaveData(aryWaveData.Length + 1023) ' Adds 42.6 ms additional dead time before removal of PTT
			byte[] bytTemp = new byte[-1 + 1];
			if (intFmtChunkLength == 16) {
				if ((WaveStream != null))
					WaveStream.Flush();
				WaveStream = new MemoryStream(43 + aryWaveData.Length);
			} else if (intFmtChunkLength == 18) {
				if ((WaveStream != null))
					WaveStream.Flush();
				WaveStream = new MemoryStream(45 + aryWaveData.Length);
			} else {
				return false;
			}

			// Set up file with RIFF chunk info.
			short shPad = 1;
			// File padding
			int intLength = 0;
			if (intFmtChunkLength == 16) {
				intLength = aryWaveData.Length + 36;
				// File length, minus first 8 bytes of RIFF description.
			} else if (intFmtChunkLength == 18) {
				intLength = aryWaveData.Length + 38;
				// File length, minus first 8 bytes of RIFF description.
			}
			short shBytesPerSample = 2;
			// Bytes per sample.

			// Fill in the riff info for the wave file.
			AppendBytes(ref bytTemp, Globals.GetBytes("RIFF"));
			AppendBytes(ref bytTemp, Int32ToBytes(intLength));
            AppendBytes(ref bytTemp, Globals.GetBytes("WAVE"));
            AppendBytes(ref bytTemp, Globals.GetBytes("fmt "));
			AppendBytes(ref bytTemp, Int32ToBytes(intFmtChunkLength));
			AppendBytes(ref bytTemp, Int16ToBytes(shPad));
			AppendBytes(ref bytTemp, Int16ToBytes(1));
			// mono
			AppendBytes(ref bytTemp, Int32ToBytes(intSampleRate));
			AppendBytes(ref bytTemp, Int32ToBytes(2 * intSampleRate));
			AppendBytes(ref bytTemp, Int16ToBytes(2));
			// bytes/sample
			if (intFmtChunkLength == 18) {
				AppendBytes(ref bytTemp, Int16ToBytes(2));
				// bytes/sample
			}
			AppendBytes(ref bytTemp, Int16ToBytes(16));
			// bits/sample
			// DateTime.Now fill in the data chunk.
            AppendBytes(ref bytTemp, Globals.GetBytes("data"));
			AppendBytes(ref bytTemp, Int32ToBytes(aryWaveData.Length));
			AppendBytes(ref bytTemp, aryWaveData);
			WaveStream.Write(bytTemp, 0, bytTemp.Length);
			return true;
		}
		//WriteRIFFStream




		// Function to read a 16 bit/sample mono wave file and return intSampleRate and the sampled data in bytWaveData()
		public bool ReadRIFF(string strFilename, ref int intSampleRate, ref byte[] bytWaveData)
		{

			// returns true if successful, false if not. intSampleRate and bytWaveData updated by reference
			if (!System.IO.File.Exists(strFilename))
				return false;
			try {
				FileStream fs = new FileStream(strFilename, FileMode.Open);
				byte[] bytHeader = new byte[46];
				fs.Read(bytHeader, 0, 46);
				int intFmtChunkLength = System.BitConverter.ToInt32(bytHeader, 16);
				intSampleRate = System.BitConverter.ToInt32(bytHeader, 24);
				int intDataBytes = 0;
				byte[] bytBuffer = new byte[-1 + 1];
				if (intFmtChunkLength == 16) {
					intDataBytes = System.BitConverter.ToInt32(bytHeader, 40);
					bytBuffer = new byte[intDataBytes + 44];
					fs.Read(bytBuffer, 0, intDataBytes + 44);
					bytWaveData = new byte[intDataBytes];
					Array.Copy(bytBuffer, 44, bytWaveData, 0, intDataBytes);
				} else if (intFmtChunkLength == 18) {
					intDataBytes = System.BitConverter.ToInt32(bytHeader, 42);
					bytBuffer = new byte[intDataBytes + 46];
					fs.Read(bytBuffer, 0, intDataBytes + 46);
					bytWaveData = new byte[intDataBytes];
					Array.Copy(bytBuffer, 46, bytWaveData, 0, intDataBytes);
				}
				fs.Close();

			} catch {
				return false;
			}
			return true;
		}
		//ReadRIFF

		private byte[] Int32ToBytes(Int32 int32)
		{
			byte[] bytTemp = new byte[4];
			//  LSByte first
			bytTemp[0] = Convert.ToByte(int32 & 0xff);
			bytTemp[1] = Convert.ToByte((int32 & 0xff00) / Convert.ToInt32(Math.Pow(2, 8)));
			bytTemp[2] = Convert.ToByte((int32 & 0xff0000) / Convert.ToInt32(Math.Pow(2, 16)));
			bytTemp[3] = Convert.ToByte((int32 & 0xff000000) / Convert.ToInt32(Math.Pow(2, 24)));
			return bytTemp;
		}

		private byte[] Int16ToBytes(Int16 int16)
		{
			byte[] bytTemp = new byte[2];
			//  LS byte first 
			bytTemp[0] = Convert.ToByte(int16 & 0xff);
			bytTemp[1] = Convert.ToByte((int16 & 0xff00) / 256);
			return bytTemp;
		}

		private void AppendBytes(ref byte[] Buffer, byte[] NewBytes)
		{
			if (NewBytes.Length != 0) {
				Array.Resize(ref Buffer, Buffer.Length + NewBytes.Length);
				Array.Copy(NewBytes, 0, Buffer, Buffer.Length - NewBytes.Length, NewBytes.Length);
			}
		}

		public void ComputePeakToRMS(Int32[] intSamples, ref Int32 intPeak, ref double dblRMS)
		{
			intPeak = 0;
			double dblSum = 0;
			for (int i = 0; i <= intSamples.Length - 1; i++) {
				if (Math.Abs(intSamples[i]) > intPeak)
					intPeak = Math.Abs(intSamples[i]);
							dblSum += Convert.ToDouble(intSamples[i]) * Convert.ToDouble(intSamples[i]);
			}
			dblRMS = Math.Sqrt(dblSum / intSamples.Length);
		}


		public WaveTools ()
		{
		}
	}
}

