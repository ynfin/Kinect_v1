using System;
using System.IO;
using System.Text;

namespace Kinect_v1
{
    class Logger
    {
        string logPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)+@"\KinectLogs";
        private string _fullpath;
        private int framecount = 1;
        public bool fileisopen;

        StringBuilder mainstring = new StringBuilder();

        public void newLoggerFile()
        {
            initiateStringWithDetails();
            _fullpath = createFilePath(logPath);
        }

        private void initiateStringWithDetails()
        {
            mainstring.AppendLine("\n\nKinect Motion Capture Log - "+DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
            mainstring.AppendLine("----------------------------------------------------------------------");
            mainstring.AppendLine("MachineName:  " + Environment.MachineName);
            mainstring.AppendLine("UserName:     " + Environment.UserName);
            mainstring.AppendLine("OSVersion:    " + Environment.OSVersion);
            mainstring.AppendLine("Runtime:      " + Environment.Version);
            mainstring.AppendLine("Processors:   " + Environment.ProcessorCount);
            mainstring.AppendLine("OS is 64bit:  " + Environment.Is64BitOperatingSystem);
            mainstring.AppendLine("App is 64bit: " + Environment.Is64BitProcess);
            mainstring.AppendLine("----------------------------------------------------------------------");
            mainstring.AppendLine("format:<frame #>,<X>,<Y>,<Z>,<Time>");
            mainstring.AppendLine("----------------------------------------------------------------------\n");
            mainstring.AppendLine("<STARTLOG>");

        }

        public void appendLogline(int[] coords, TimeSpan timespan)
        {
            mainstring.AppendLine(framecount +"."+coords[0]+"."+coords[1]+"."+coords[2]+"."+timespan);
            framecount ++;
        
            //if ((framecount % 100) == 0)
                System.Diagnostics.Debug.WriteLine("Frames captured: " + framecount);
        }
        public void appendLogline(float[] coords, TimeSpan timespan)
        {
            mainstring.AppendLine(
                framecount + "," + 
                coords[0].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + 
                coords[1].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + 
                coords[2].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + 
                timespan);
            
            framecount++;

            if ((framecount % 100) == 0)
                System.Diagnostics.Debug.WriteLine("Frames captured: " + framecount);
        }

        private string createFilePath(string path)
        {
            return path+@"\KinectLog_" + DateTime.Now.ToString().Replace('/','_').Replace(':','_').Replace(' ','_') + ".txt";
        }

        public void dumpToFile()
        {
            if (!Directory.Exists(_fullpath))
                Directory.CreateDirectory(logPath);

            mainstring.AppendLine("<ENDLOG>");
            StreamWriter file = new StreamWriter(_fullpath);
            file.WriteLine(mainstring);
            file.Close();
        }


        public void clear()
        {
            mainstring.Clear();
            framecount = 0;
        }

        ~Logger()
        {
            mainstring.Clear();
            mainstring = null;
        }


    }
}
