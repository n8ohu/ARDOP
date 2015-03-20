using Microsoft.VisualBasic;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ARDOP
{
    class Logs
    {
        // A class for writing text to debug and exception logs
        #region "Objects"
        #endregion
        private static object objLogLock = new object();

        #region "Public Shared Subs"

        // Subroutine to write the indicated text to the exception log...
        public static void Exception(string strText)
        {
            System.DateTime dttTimestamp = DateTime.UtcNow;
            Debug.WriteLine("ARDOP EXCEPTION Log: " + strText);
            lock (objLogLock)
            {
                System.IO.File.WriteAllText(Globals.strExecutionDirectory + "Logs\\ARDOP_WinTNC_Exceptions_" + Strings.Format(dttTimestamp, "yyyyMMdd") + ".log", Strings.Format(dttTimestamp, "yyyy/MM/dd HH:mm:ss") + " [" + Application.ProductVersion + "] " + strText + Constants.vbCrLf);
            }
        }

        // Subroutine to write the indicated text to the debug log...
        public static void WriteDebug(string strText)
        {
            lock (objLogLock)
            {
                System.DateTime dttTimestamp = DateTime.UtcNow;
                string strTS = Strings.Format(dttTimestamp, "yyyy/MM/dd HH:mm:ss") + "." + Strings.Format(dttTimestamp.Millisecond, "000");
                System.IO.File.WriteAllText(Globals.strExecutionDirectory + "Logs\\ARDOP_WinTNC_Debug_" + Strings.Format(dttTimestamp, "yyyyMMdd") + ".log", strTS + " [" + Application.ProductVersion + "] " + strText + Constants.vbCrLf);
                Debug.WriteLine("ARDOP Debug log:  " + strText + Constants.vbCrLf);
                // for testing
            }
        }

        #endregion

    }
}
