using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Kinect_v1.Model
{

    class FpsCounter
    {
        Stopwatch watch = new Stopwatch();
        private Queue window;
        private int windowlength;

        public FpsCounter(int winsize)
        {
            windowlength = winsize;
            watch.Start();
            window = new Queue(winsize);
        }

        public void clear()
        {
            window.Clear();
        }

        public string RawTick()
        {
            long timein = watch.ElapsedMilliseconds;
            watch.Restart();

            return (1000/timein).ToString();
        }

        public void restart()
        {
            watch.Restart();
        }

        public string TickMs(bool fullcycle)
        {
            long timein = watch.ElapsedMilliseconds;

            System.Diagnostics.Debug.WriteLine(timein);

            if (fullcycle)
                watch.Restart();
            else
                watch.Reset();

            if (window.Count >= windowlength)
                window.Dequeue();

            window.Enqueue(timein);

            long sum = 0;

            foreach (long time in window)
                sum = sum + time;

            if (window.Count < windowlength)
                return "cal";

            if (sum != 0 && windowlength != 0 && (sum / windowlength) != 0)
                return ((sum / windowlength)).ToString();

            return "-1";
        }

        public string Tick()
        {
            long timein = watch.ElapsedMilliseconds;
            watch.Restart();

            if (window.Count >= windowlength)
                window.Dequeue();

            window.Enqueue(timein);

            long sum = 0;

            foreach (long time in window)
                sum = sum + time;

            if (window.Count < windowlength)
                return "cal";

            if (sum != 0 && windowlength != 0 && (sum/windowlength) != 0)
                return (1000/(sum/windowlength)).ToString();
            
            return "-1";
        }

    }
}
