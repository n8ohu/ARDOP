using System;

namespace ARDOP
{
	public class Viterbi

	{
		// A class to replace the DLL code written by Phil Karn and adopted to .NET by Randy Miller

		private Int32[,] aryMettab = new Int32[2, 256];
		private double dblSQRT2 = Math.Sqrt(2);
		private double dblLog2E = 1.44269504088896;
		// The two generator polynomials for the NASA Standard  r=1/2,  K=7 (Voyager) 
		private Int32 bytPolyA = 0x6d;
		private Int32 bytPolyB = 0x4f;
		// the offset input  128 = erasure, 28 = perfect 0, 228 = perfect 1
		private int intOffset = 128;
		private double dblLn2 = Math.Log(2);
		private double dblErf2 = Erf(2);
		private Int32 intEncState = 0;
		private Int32 intBitIndex = 7;

		private int intAmplitude;
		// 8-bit parity lookup table
		private byte[] bytPartab = {
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0,
			1,
			0,
			0,
			1,
			1,
			0,
			0,
			1,
			0,
			1,
			1,
			0

		};
		// Butterfly Index Table ' Used instead of MACRO generated call
		private byte[] bytBtfIndx = {
			0,
			1,
			3,
			2,
			3,
			2,
			0,
			1,
			0,
			1,
			3,
			2,
			3,
			2,
			0,
			1,
			2,
			3,
			1,
			0,
			1,
			0,
			2,
			3,
			2,
			3,
			1,
			0,
			1,
			0,
			2,
			3

		};
		private struct stcState
		{
			public UInt32 path;

			public Int32 metric;
			// *********************  C Source  ****************************
			//struct state {
			//	unsigned long path;	/* Decoded path to this state */
			//	long metric;		/* Cumulative metric to this state */
			//};
		}
		// stcState

		// Subroutine to reset the Decoder
		public void ResetEncoder()
		{
			intEncState = 0;
			intBitIndex = 7;
		}
		// Reset

		// Function to compute the normal distribution
		private double Normal(double dblX)
		{
			return 0.5 + 0.5 * Erf(dblX / dblSQRT2);
		}
		// Normal


        static double Erf(double x)
        {
            // constants
            double a1 = 0.254829592;
            double a2 = -0.284496736;
            double a3 = 1.421413741;
            double a4 = -1.453152027;
            double a5 = 1.061405429;
            double p = 0.3275911;

            // Save the sign of x
            int sign = 1;
            if (x < 0)
                sign = -1;
            x = Math.Abs(x);

            // A&S formula 7.1.26
            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return sign * y;
        }
        // Taylor series approximation for the Error function (VB doesn't have the native Erf Function)
		/*private static double Erf(double dblZ)
		{
			double functionReturnValue = 0;
			functionReturnValue = dblZ;
			double dblZSq = dblZ * dblZ;
			double dblZPwr = dblZ;
			double dblNfact = 1;
			if (dblZ > 2) {
				return dblErf2 + ((dblZ - 2) / dblZ) * (1 - dblErf2);
				//an approximation for the tail where the series doesn't converge well
			} else if (dblZ < -2) {
				return -(dblErf2 + ((dblZ + 2) / dblZ) * (1 - dblErf2));
				//an approximation for the tail where the series doesn't converge well
			}
			// Use Taylor series for 2<= dblZ <= 2
			// 21 total terms
			for (int i = 1; i <= 20; i++) {
				dblNfact = dblNfact * i;
				dblZPwr = dblZPwr * dblZSq;
				if ((i % 2) == 0) {
					functionReturnValue += dblZPwr / (dblNfact * (2 * i + 1));
				} else {
					functionReturnValue -= dblZPwr / (dblNfact * (2 * i + 1));
				}
			}
			functionReturnValue = functionReturnValue * 2 / Math.Sqrt(Math.PI);
			return functionReturnValue;
		}*/
		// Erf

		// Function to return the Log base 2 of a value (not native to VB)
		private double Log2(double dblX)
		{
			return Math.Log(dblX) / dblLn2;
		}
		// Log2

		// Generate aryMettab based on amplitude, S/N, bias and scale

		public void GenerateMetrics(Int32 intAmp, double dblSNdb, double dblBias, Int32 intScale)
		{
			intAmplitude = intAmp;
			double dblNoise = 0;
			double dblP0 = 0;
			double dblP1 = 0;
			double dblEsn0 = 0;
			double[,] dblMetrics = new double[2, 255];

			dblEsn0 = Math.Pow(10, (dblSNdb / 10));
			dblNoise = 0.5 / dblEsn0;
			dblNoise = Math.Sqrt(dblNoise);
			dblP1 = Normal(((0 - intOffset + 0.5) / intAmp - 1) / dblNoise);
			dblP0 = Normal(((0 - intOffset + 0.5) / intAmp + 1) / dblNoise);
			dblMetrics[0, 0] = Convert.ToInt32(Log2(2 * dblP0 / (dblP0 + dblP1)) - dblBias);
			dblMetrics[1, 0] = Convert.ToInt32(Log2(2 * dblP1 / (dblP0 + dblP1)) - dblBias);
			for (int s = 1; s <= 254; s++) {
				if (s == 28) {
					s = s + 0;
				}

				dblP1 = Normal(((s - intOffset + 0.5) / intAmp - 1) / dblNoise) - Normal(((s - intOffset - 0.5) / intAmp - 1) / dblNoise);
				dblP0 = Normal(((s - intOffset + 0.5) / intAmp + 1) / dblNoise) - Normal(((s - intOffset - 0.5) / intAmp + 1) / dblNoise);
				dblMetrics[0, s] = Convert.ToInt32(Log2(2 * dblP0 / (dblP0 + dblP1)) - dblBias);
				dblMetrics[1, s] = Convert.ToInt32(Log2(2 * dblP1 / (dblP0 + dblP1)) - dblBias);
			}
			//	}
			for (int i = 0; i <= 254; i++) {
				aryMettab[0, i] = Convert.ToInt32(Math.Floor(dblMetrics[0, i] * intScale + 0.5));
				aryMettab[1, i] = Convert.ToInt32(Math.Floor(dblMetrics[1, i] * intScale + 0.5));
				// Debug.WriteLine("Metrics(0,1 " & i.ToString & "): " & Format(aryMettab(0, i), "#000") & "   " & Format(aryMettab(1, i), "#000"))
			}

			// ********************** Oiginal C code ***************************************
			// private Int32[,] mettab = new Int32[2, 256];
			//{
			//      return gen_met(mettab, amplitude, esNoRationInDB, bias, scale);
			//  }

			//      /* Generate metric tables for a soft-decision convolutional decoder
			// * assuming gaussian noise on a PSK channel.
			// *
			// * Works from "first principles" by evaluating the normal probability
			// * function and then computing the log-likelihood function
			// * for every possible received symbol value
			// *
			// * Copyright 1995 Phil Karn, KA9Q
			// */
			//#include <stdlib.h>
			//#include <math.h>
			//#include "viterbi.h"

			///* Generate log-likelihood metrics for 8-bit soft quantized channel
			// * assuming AWGN and BPSK
			// */
			//      VITERBIKERNAL_API(Int)
			//gen_met(
			//int mettab[][256],	/* Metric table, [sent sym][rx symbol] */
			//int amp,		/* Signal amplitude, units */
			//double esn0,		/* Es/N0 ratio in dB */
			//double bias,		/* Metric bias; 0 for viterbi, rate for sequential */
			//int scale		/* Scale factor */
			//){
			//	double noise;
			//	double n;
			//	int s,bit;
			//	double metrics[2][256];
			//	double p0,p1;

			//	/* Es/N0 as power ratio */
			//	esn0 = pow(10.,esn0/10);

			//	noise = 0.5/esn0;	/* only half the noise for BPSK */
			//	noise = sqrt(noise);	/* noise/signal Voltage ratio */

			//	/* Zero is a special value, since this sample includes all
			//	 * lower samples that were clipped to this value, i.e., it
			//	 * takes the whole lower tail of the curve 
			//	 */
			//	p1 = normal(((0-OFFSET+0.5)/amp - 1)/noise);	/* P(s|1) */

			//	/* Prob of this value occurring for a 0-bit */	/* P(s|0) */
			//	p0 = normal(((0-OFFSET+0.5)/amp + 1)/noise);
			//	metrics[0][0] = log2(2*p0/(p1+p0)) - bias;
			//	metrics[1][0] = log2(2*p1/(p1+p0)) - bias;

			//	for(s=1;s<255;s++){
			//		/* P(s|1), prob of receiving s given 1 transmitted */
			//		p1 = normal(((s-OFFSET+0.5)/amp - 1)/noise) -
			//			normal(((s-OFFSET-0.5)/amp - 1)/noise);

			//		/* P(s|0), prob of receiving s given 0 transmitted */
			//		p0 = normal(((s-OFFSET+0.5)/amp + 1)/noise) -
			//			normal(((s-OFFSET-0.5)/amp + 1)/noise);

			//#ifdef notdef
			//		printf("P(%d|1) = %lg, P(%d|0) = %lg\n",s,p1,s,p0);
			//#End If
			//		metrics[0][s] = log2(2*p0/(p1+p0)) - bias;
			//		metrics[1][s] = log2(2*p1/(p1+p0)) - bias;
			//	}
			//	/* 255 is also a special value */
			//	/* P(s|1) */
			//	p1 = 1 - normal(((255-OFFSET-0.5)/amp - 1)/noise);
			//	/* P(s|0) */
			//	p0 = 1 - normal(((255-OFFSET-0.5)/amp + 1)/noise);

			//	metrics[0][255] = log2(2*p0/(p1+p0)) - bias;
			//	metrics[1][255] = log2(2*p1/(p1+p0)) - bias;
			//#ifdef	notdef
			//	/* The probability of a raw symbol error is the probability
			//	 * that a 1-bit would be received as a sample with value
			//	 * 0-128. This is the offset normal curve integrated from -Inf to 0.
			//	 */
			//	printf("symbol Pe = %lg\n",normal(-1/noise));
			//#End If
			//	for(bit=0;bit<2;bit++){
			//		for(s=0;s<256;s++){
			//			/* Scale and round to nearest integer */
			//			mettab[bit][s] = floor(metrics[bit][s] * scale + 0.5);
			//#ifdef	notdef
			//			printf("metrics[%d][%d] = %lg, mettab = %d\n",
			//			 bit,s,metrics[bit][s],mettab[bit][s]);
			//#End If
			//		}
			//	}
			//}
			// ***************************************************************
		}
		// GenerateMetrics


		public byte[] DecodeFromSymbolBits(byte[] bytSymbolBits)
		{
			// Normally called with bytSymboBits padded with 16 0 symbols tagged on to "flush" decoder
			// Number of Output bytes = (bytSymbolsBits.length - 16)/16

			Int32 intMetric = 0;

			byte[] bytOutput = new byte[bytSymbolBits.Length / 16 - 1];
			Int32 ptrOutput = 0;
			Int32 intBitcnt = 0;
			Int32[] intMets = new Int32[4];
			Int32 intBestmetric = default(Int32);
			Int32 intBestState = default(Int32);
			Int32 i = default(Int32);
			stcState[,] stcState = new stcState[2, 64];
			Int32 ptrCurrent = 0;
			Int32 ptrNext = 1;
			int intSymbolsPtr = 0;

			//	 Initialize starting metrics to prefer 0 state 
			stcState[ptrCurrent, 0].metric = 0;
			for (i = 1; i <= 63; i++) {
				stcState[ptrCurrent, i].metric = -999999;
			}
			stcState[ptrCurrent, 0].path = 0;
			for (intBitcnt = 0; intBitcnt <= 8 * bytOutput.Length - 1; intBitcnt++) {
				intMets[0] = aryMettab[0, bytSymbolBits[intSymbolsPtr]] + aryMettab[0, bytSymbolBits[intSymbolsPtr + 1]];
				//		mets[0] = mettab[0][symbols[0]] + mettab[0][symbols[1]];
				intMets[1] = aryMettab[0, bytSymbolBits[intSymbolsPtr]] + aryMettab[1, bytSymbolBits[intSymbolsPtr + 1]];
				//		mets[1] = mettab[0][symbols[0]] + mettab[1][symbols[1]];
				intMets[2] = aryMettab[1, bytSymbolBits[intSymbolsPtr]] + aryMettab[0, bytSymbolBits[intSymbolsPtr + 1]];
				//		mets[2] = mettab[1][symbols[0]] + mettab[0][symbols[1]];
                intMets[3] = aryMettab[1, bytSymbolBits[intSymbolsPtr]] + aryMettab[1, bytSymbolBits[intSymbolsPtr + 1]];
				//		mets[3] = mettab[1][symbols[0]] + mettab[1][symbols[1]];
				intSymbolsPtr += 2;

				// Do the Butterfly calcs here Implemented as a loop vs Macro 
				for (i = 0; i <= 31; i++) {
					int intM0 = 0;
					int intM1 = 0;
					int sym = bytBtfIndx[i];
					//	 ACS for 0 branch 
					intM0 = stcState[ptrCurrent, i].metric + intMets[sym];
					intM1 = stcState[ptrCurrent, i + 32].metric + intMets[3 ^ sym];
					if (intM0 > intM1) {
						stcState[ptrNext, 2 * i].metric = intM0;
						stcState[ptrNext, 2 * i].path = stcState[ptrCurrent, i].path << 1;
					} else {
						stcState[ptrNext, 2 * i].metric = intM1;
						stcState[ptrNext, 2 * i].path = (stcState[ptrCurrent, i + 32].path << 1) | Convert.ToUInt32(1);
					}
					//	ACS for 1 branch 
					intM0 = stcState[ptrCurrent, i].metric + intMets[3 ^ sym];
					intM1 = stcState[ptrCurrent, i + 32].metric + intMets[sym];
					if (intM0 > intM1) {
						stcState[ptrNext, 2 * i + 1].metric = intM0;
						stcState[ptrNext, 2 * i + 1].path = stcState[ptrCurrent, i].path << 1;
					} else {
						stcState[ptrNext, 2 * i + 1].metric = intM1;
						stcState[ptrNext, 2 * i + 1].path = (stcState[ptrCurrent, i + 32].path << 1) | Convert.ToUInt32(1);
					}
				}

				//		 Swap current and next states 
				if ((intBitcnt & 1) != 0) {
					ptrCurrent = 0;
					ptrNext = 1;
				} else {
					ptrCurrent = 1;
					ptrNext = 0;
				}

				if (intBitcnt > bytSymbolBits.Length - 7) {
					//	 In tail, poison non-zero nodes 
					for (i = 1; i <= 63; i += 2) {
						stcState[ptrCurrent, i].metric = -9999999;
					}
				}
				//	 Produce output every 8 bits once path memory is full
				if (((intBitcnt % 8) == 5) & intBitcnt > 32) {
					//	Find current best path
					intBestmetric = stcState[ptrCurrent, 0].metric;
					intBestState = 0;
					for (i = 1; i <= 63; i++) {
						if (stcState[ptrCurrent, i].metric > intBestmetric) {
							intBestmetric = stcState[ptrCurrent, i].metric;
							intBestState = i;
						}
					}
					//Debug.WriteLine("Beststate:" & intBestState.ToString & " metric=" & stcState(ptrCurrent, intBestState).metric.ToString & "  path=" & stcState(ptrCurrent, intBestState).path.ToString)
					bytOutput[ptrOutput] = Convert.ToByte(stcState[ptrCurrent, intBestState].path >> 24);
					ptrOutput += 1;
				}
			}
			//	Output remaining bits from 0 state 
			if (intBitcnt % 8 != 6) {
				stcState[ptrCurrent, 0].path = stcState[ptrCurrent, 0].path << (6 - (intBitcnt % 8));
			}
			bytOutput[ptrOutput] = Convert.ToByte(stcState[ptrCurrent, 0].path >> 24);
			ptrOutput += 1;
			bytOutput[ptrOutput] = Convert.ToByte(0xff & stcState[ptrCurrent, 0].path >> 16);
			ptrOutput += 1;
			bytOutput[ptrOutput] = Convert.ToByte(0xff & stcState[ptrCurrent, 0].path >> 8);
			ptrOutput += 1;
			bytOutput[ptrOutput] = Convert.ToByte(0xff & stcState[ptrCurrent, 0].path);
			intMetric = stcState[ptrCurrent, 0].metric;
			return bytOutput;
		}
		// DecodeFromSymbolBits

		// C Source code
		//	return 0;
		//}

		//      viterbi(ref metric, output, symbolBits, (uint)symbolBits.Length, mettab);

		//      return output;
		//  }
		//      /* Viterbi decoder for K=7 rate=1/2 convolutional code
		// * Copyright 1995 Phil Karn, KA9Q
		// */

		//#include "viterbi.h"

		///* The basic Viterbi decoder operation, called a "butterfly"
		// * operation because of the way it looks on a trellis diagram. Each
		// * butterfly involves an Add-Compare-Select (ACS) operation on the two nodes
		// * where the 0 and 1 paths from the current node merge at the next step of
		// * the trellis.
		// *
		// * The code polynomials are assumed to have 1's on both ends. Given a
		// * function encode_state() that returns the two symbols for a given
		// * encoder state in the low two bits, such a code will have the following
		// * identities for even 'n' < 64:
		// *
		// * 	encode_state(n) = encode_state(n+65)
		// *	encode_state(n+1) = encode_state(n+64) = (3 ^ encode_state(n))
		// *
		// * Any convolutional code you would actually want to use will have
		// * these properties, so these assumptions aren't too limiting.
		// *
		// * Doing this as a macro lets the compiler evaluate at compile time the
		// * many expressions that depend on the loop index and encoder state and
		// * emit them as immediate arguments.
		// * This makes an enormous difference on register-starved machines such
		// * as the Intel x86 family where evaluating these expressions at runtime
		// * would spill over into memory.
		// */
		//#define	BUTTERFLY(i,sym) { \
		//	int m0,m1;\
		//\
		//	/* ACS for 0 branch */\
		//	m0 = state[i].metric + mets[sym];	/* 2*i */\
		//	m1 = state[i+32].metric + mets[3^sym];	/* 2*i + 64 */\
		//	if(m0 > m1){\
		//		next[2*i].metric = m0;\
		//		next[2*i].path = state[i].path << 1;\
		//	} else {\
		//		next[2*i].metric = m1;\
		//		next[2*i].path = (state[i+32].path << 1)|1;\
		//	}\
		//	/* ACS for 1 branch */\
		//	m0 = state[i].metric + mets[3^sym];	/* 2*i + 1 */\
		//	m1 = state[i+32].metric + mets[sym];	/* 2*i + 65 */\
		//	if(m0 > m1){\
		//		next[2*i+1].metric = m0;\
		//		next[2*i+1].path = state[i].path << 1;\
		//	} else {\
		//		next[2*i+1].metric = m1;\
		//		next[2*i+1].path = (state[i+32].path << 1)|1;\
		//	}\
		//}

		//extern unsigned char Partab[];	/* Parity lookup table */

		///* The path memory for each state is 32 bits. This is slightly shorter
		// * than we'd like for K=7, especially since we chain back every 8 bits.
		// * But it fits so nicely into a 32-bit machine word...
		// */
		//struct state {
		//	unsigned long path;	/* Decoded path to this state */
		//	long metric;		/* Cumulative metric to this state */
		//};



		//static int gEncState=0;
		//static int gBitIndex=7;

		//VITERBIKERNAL_API void reset()
		//{
		//	gEncState = 0;
		//	gBitIndex = 7;
		//}

		///* Convolutionally encode a single byte into a 2 bit symbol */
		//VITERBIKERNAL_API unsigned char encodeBit(
		//	unsigned char data)
		//{
		//	int bit0;
		//	int bit1;

		//                  If (gBitIndex < 0) Then
		//		gBitIndex = 7;

		//	gEncState = (gEncState << 1) | ((data >> gBitIndex) & 1);
		//	bit0 = Partab[gEncState & POLYA];
		//	bit1 = Partab[gEncState & POLYB];
		//	gBitIndex --;

		//	if (bit0 == 0 && bit1 == 0)
		//		return 0;
		//	else if ( bit0  == 0 && bit1 == 1 )
		//		return 1;
		//	else if ( bit0 == 1 && bit1 == 1 )
		//		return 2;
		//	else //  if ( bit == 1 && bit == 0 )
		//		return 3;

		//	return 0;
		//}

		///* Viterbi decoder */
		//                        VITERBIKERNAL_API(Int)
		//viterbi(
		//unsigned long *metric,	/* Final path metric (returned value) */
		//unsigned char *data,	/* Decoded output data */
		//unsigned char *symbols,	/* Raw deinterleaved input symbols */
		//unsigned int nbits,	/* Number of output bits */
		//int mettab[2][256]	/* Metric table, [sent sym][rx symbol] */
		//){
		//	unsigned int bitcnt = 0;
		//	int mets[4];
		//	long bestmetric;
		//	int beststate,i;
		//	struct state state0[64],state1[64],*state,*next;

		//	state = state0;
		//	next = state1;

		//	/* Initialize starting metrics to prefer 0 state */
		//	state[0].metric = 0;
		//	for(i=1;i<64;i++)
		//		state[i].metric = -999999;
		//	state[0].path = 0;

		//	for(bitcnt = 0;bitcnt < nbits;bitcnt++){
		//		/* Read input symbol pair and compute all possible branch
		//		 * metrics
		//		 */
		//		mets[0] = mettab[0][symbols[0]] + mettab[0][symbols[1]];
		//		mets[1] = mettab[0][symbols[0]] + mettab[1][symbols[1]];
		//		mets[2] = mettab[1][symbols[0]] + mettab[0][symbols[1]];
		//		mets[3] = mettab[1][symbols[0]] + mettab[1][symbols[1]];
		//		symbols += 2;

		//		/* These macro calls were generated by genbut.c */
		//		BUTTERFLY(0,0);
		//		BUTTERFLY(1,1);
		//		BUTTERFLY(2,3);
		//		BUTTERFLY(3,2);
		//		BUTTERFLY(4,3);
		//		BUTTERFLY(5,2);
		//		BUTTERFLY(6,0);
		//		BUTTERFLY(7,1);
		//		BUTTERFLY(8,0);
		//		BUTTERFLY(9,1);
		//		BUTTERFLY(10,3);
		//		BUTTERFLY(11,2);
		//		BUTTERFLY(12,3);
		//		BUTTERFLY(13,2);
		//		BUTTERFLY(14,0);
		//		BUTTERFLY(15,1);
		//		BUTTERFLY(16,2);
		//		BUTTERFLY(17,3);
		//		BUTTERFLY(18,1);
		//		BUTTERFLY(19,0);
		//		BUTTERFLY(20,1);
		//		BUTTERFLY(21,0);
		//		BUTTERFLY(22,2);
		//		BUTTERFLY(23,3);
		//		BUTTERFLY(24,2);
		//		BUTTERFLY(25,3);
		//		BUTTERFLY(26,1);
		//		BUTTERFLY(27,0);
		//		BUTTERFLY(28,1);
		//		BUTTERFLY(29,0);
		//		BUTTERFLY(30,2);
		//		BUTTERFLY(31,3);

		//		/* Swap current and next states */
		//		if(bitcnt & 1){
		//			state = state0;
		//			next = state1;
		//		} else {
		//			state = state1;
		//			next = state0;
		//		}
		//		if(bitcnt > nbits-7){
		//			/* In tail, poison non-zero nodes */
		//			for(i=1;i<64;i += 2)
		//				state[i].metric = -9999999;
		//		}
		//		/* Produce output every 8 bits once path memory is full */
		//		if((bitcnt % 8) == 5 && bitcnt > 32){
		//			/* Find current best path */
		//			bestmetric = state[0].metric;
		//			beststate = 0;
		//			for(i=1;i<64;i++){
		//				if(state[i].metric > bestmetric){
		//					bestmetric = state[i].metric;
		//					beststate = i;
		//				}
		//			}
		//#ifdef	notdef
		//			printf("metrics[%d] = %d state = %lx\n",beststate,
		//			   state[beststate].metric,state[beststate].path);
		//#End If
		//			*data++ = state[beststate].path >> 24;
		//		}

		//	}
		//	/* Output remaining bits from 0 state */
		//	if((i = bitcnt % 8) != 6)
		//		state[0].path <<= 6-i;

		//	*data++ = state[0].path >> 24;
		//	*data++ = state[0].path >> 16;
		//	*data++ = state[0].path >> 8;
		//	*data = state[0].path;

		//	*metric = state[0].metric;
		//	return 0;
		//}


		// Function to generate Viterbi encoded I and Q symbol bits from binary input bytes
		public byte[] EncodeBytesToSymbolBits(ref byte[] bytData, bool blnMod = true)
		{

			// if blnMod is set will set bit values to modulation levels (default 128 +/- amp) 
			// Data is the input data in bytes
			//Generates an output byte array that is 16 x the input in size  
			// Each output symbol is two sequential bytes:
			//     First byte is the I value 0 or 1
			//     Second is the Q value 0 or 1

			byte[] bytSymbols = new byte[16 * bytData.Length];
			// 16 times data length 
			Int32 intEncState = 0;
			int intSymbolPtr = 0;
			byte bytH1 = 0x1;
			for (int j = 0; j <= bytData.Length - 1; j++) {
				for (int i = 7; i >= 0; i += -1) {
					intEncState = intEncState << 1 | (bytData[j] >> i) & bytH1;
					bytSymbols[intSymbolPtr] = bytPartab[intEncState & bytPolyA];
					bytSymbols[intSymbolPtr + 1] = bytPartab[intEncState & bytPolyB];
					intSymbolPtr += 2;
				}
			}
			if (blnMod) {
				for (int i = 0; i <= bytSymbols.Length - 1; i++) {
					bytSymbols[i] = Convert.ToByte((intOffset - intAmplitude) + 2 * intAmplitude * bytSymbols[i]);
				}
			}
			return bytSymbols;
			// ******************  Original Phil Karn C code ********************************
			//      /* Convolutionally encode data into binary symbols */
			//int encode(
			//unsigned char *symbols,
			//unsigned char *data,
			//unsigned int nbytes)
			//{
			//	unsigned char encstate;
			//	int i;

			//	encstate = 0;
			//	while(nbytes-- != 0){
			//		for(i=7;i>=0;i--){
			//			encstate = (encstate << 1) | ((*data >> i) & 1);
			//			*symbols++ = Partab[encstate & POLYA];
			//			*symbols++ = Partab[encstate & POLYB];
			//		}
			//		data++;
			//	}
			//	return 0;
			//}
			// **********************************************************************
		}
		// EncodeByteToSymbolBits 

	}


}

