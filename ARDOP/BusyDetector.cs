using Microsoft.VisualBasic;
using System;


namespace ARDOP
{
	public class BusyDetector
	{

		public int intLastStart;
		public int intLastStop;
		public double dblAvgPk2BaselineRatio;
		public double dblAvgBaselineSlow;

		public double dblAvgBaselineFast;
		public int LastStart {
			get { return intLastStart; }

			set { intLastStart = value; }
		}

		public int LastStop {
			get { return intLastStop; }

			set { intLastStop = value; }
		}

		public double AvgPk2BaselineRatio {
			get { return dblAvgPk2BaselineRatio; }

			set { dblAvgPk2BaselineRatio = value; }
		}

		public double AvgBaselineSlow {
			get { return dblAvgBaselineSlow; }

			set { dblAvgBaselineSlow = value; }
		}

		public double AvgBaselineFast {
			get { return dblAvgBaselineFast; }

			set { dblAvgBaselineFast = value; }
		}
		readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_BusyDetect_intBusyCount_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();


		// subroutine to implement a busy detector based on 1024 point FFT
		// this only called while searching for leader ...once leader detected, no longer called.
		// First look at simple peak over the frequency band of  interest.
		// Status:  May 28, 2014.  Some initial encouraging results. But more work needed.
		//       1) Use of Start and Stop ranges good and appear to work well ...may need some tweaking +/_ a few bins.
		//       2) Using a Fast attack and slow decay for the dblAvgPk2BaselineRation number e.g.
		//       dblAvgPk2BaselineRatio = Max(dblPeakPwrAtFreq / dblAvgBaselineX, 0.9 * dblAvgPk2BaselineRatio)
		// Seems to work well for the narrow detector. Tested on real CW, PSK, RTTY. 
		// Still needs work on the wide band detector. (P3, P4 etc)  May be issues with AGC speed. (my initial on-air tests using AGC Fast).
		// Ideally can find a set of parameters that won't require user "tweaking"  (Busy Squelch) but that is an alternative if needed. 
		// use of technique of re initializing some parameters on a change in detection bandwidth looks good and appears to work well with 
		// scanning.  Could be expanded with properties to "read back" these parameters so they could be saved and re initialize upon each new frequency. 


		int static_BusyDetect_intBusyCount;
		readonly Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag static_BusyDetect_intLastBusy_Init = new Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag();
		int static_BusyDetect_intLastBusy;
		public int BusyDetect(ref double[] dblMag, int intStart, int intStop)
		{
			double dblAvgBaseline = 0;
			double dblPeakPwrAtFreq = 0;
			double dblAvgBaselineX = 0;
			lock (static_BusyDetect_intBusyCount_Init) {
				try {
					if (InitStaticVariableHelper(static_BusyDetect_intBusyCount_Init)) {
						static_BusyDetect_intBusyCount = 0;
					}
				} finally {
					static_BusyDetect_intBusyCount_Init.State = 1;
				}
			}
			lock (static_BusyDetect_intLastBusy_Init) {
				try {
					if (InitStaticVariableHelper(static_BusyDetect_intLastBusy_Init)) {
						static_BusyDetect_intLastBusy = 0;
					}
				} finally {
					static_BusyDetect_intLastBusy_Init.State = 1;
				}
			}
			double dblAlpha = 0.1;
			// This factor affects the filtering smaller = slower filtering

			int intPkIndx = 0;

            if (!(ARDOP.Globals.State == ARDOP.Globals.ReceiveState.SearchingForLeader | ARDOP.Globals.State == ARDOP.Globals.ReceiveState.AcquireSymbolSync | ARDOP.Globals.State == ARDOP.Globals.ReceiveState.AcquireFrameSync))
				return static_BusyDetect_intLastBusy;


			// cover a range that matches the bandwidth expanded (+/-) by the tuning range
			for (int i = intStart; i <= intStop; i++) {

				if (dblMag[i] > dblPeakPwrAtFreq) {
					dblPeakPwrAtFreq = dblMag[i];
					intPkIndx = i;
				}
				dblAvgBaseline += dblMag[i];
			}
            if (intPkIndx == 0)
                return -1; //false;
			// add in the bins above and below the peak (about 58 Hz total bandwidth)
			dblPeakPwrAtFreq = dblPeakPwrAtFreq + dblMag[intPkIndx - 2] + dblMag[intPkIndx - 1] + dblMag[intPkIndx + 1] + dblMag[intPkIndx + 2];
			dblAvgBaselineX = (dblAvgBaseline - dblPeakPwrAtFreq) / (intStop - intStart - 5);
			// the avg Pwr per bin ignoring the area near the peak
			dblPeakPwrAtFreq = dblPeakPwrAtFreq / 5;
			//the avg Power per bin in the region of the peak (peak +/- 2 bins...about 58 Hz)
			// dblAvgBaseline = Max(100, (dblAvgBaseline - dblPeakFreq) / (intStop - intStart))
			if (intStart == intLastStart & intStop == intLastStop) {
				dblAvgPk2BaselineRatio = Math.Max(dblPeakPwrAtFreq / dblAvgBaselineX, 0.9 * dblAvgPk2BaselineRatio);

				//dblAvgPk2BaselineRatio = (1 - dblAlpha) * dblAvgPk2BaselineRatio + (dblAlpha * dblPeakPwrAtFreq / dblAvgBaselineX)
				dblAvgBaselineSlow = (1 - 0.15) * dblAvgBaselineSlow + 0.15 * dblAvgBaseline;
				dblAvgBaselineFast = 0.5 * (dblAvgBaseline + dblAvgBaselineFast);
			} else {
				dblAvgPk2BaselineRatio = dblPeakPwrAtFreq / dblAvgBaselineX;
				dblAvgBaselineSlow = dblAvgBaseline;
				dblAvgBaselineFast = dblAvgBaseline;
				intLastStart = intStart;
				intLastStop = intStop;
			}

			//6 + MCB.Squelch Then 'And DateTime.Now.Subtract(dttPTTRelease).TotalMilliseconds > 300 Then
			if (dblAvgPk2BaselineRatio > 7) {
				dblAvgBaselineSlow = dblAvgBaseline;
				dblAvgBaselineFast = dblAvgBaseline;
				if (static_BusyDetect_intLastBusy > 0) {
					static_BusyDetect_intLastBusy = 1;
					return 1;
				} else {
					static_BusyDetect_intLastBusy = 1;
					return 0;
				}


				//  (dblAvgBaselineFast / dblAvgBaselineSlow > 1.2 Or dblAvgBaselineFast / dblAvgBaselineSlow < 0.8333) Then ' (1 + MCB.Squelch / 5) Or dblAvgBaselineFast / dblAvgBaselineSlow < 1 / (1 + MCB.Squelch / 5)) Then
			} else if (false) {
				//And DateTime.Now.Subtract(dttPTTRelease).TotalMilliseconds > 300 Then
				// This detects wide band "pulsy" modes like Pactor 3, MFSK etc
				if (static_BusyDetect_intLastBusy > 0) {
					static_BusyDetect_intLastBusy = 2;
					return 2;
				} else {
					static_BusyDetect_intLastBusy = 2;
					return 0;
				}
			}
			static_BusyDetect_intLastBusy = 0;
			return 0;
		}
		static bool InitStaticVariableHelper(Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag flag)
		{
			if (flag.State == 0) {
				flag.State = 2;
				return true;
			} else if (flag.State == 2) {
				throw new Microsoft.VisualBasic.CompilerServices.IncompleteInitialization();
			} else {
				return false;
			}
		}
		// BusyDetect
	}

}

