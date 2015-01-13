using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace Kinect_v1
{
    class dataCapture
    {

        private KinectSensor _sensor = null;
        private ColorFrameReader _colorFrame = null;
        private WriteableBitmap _colorBitmap = null;

        public dataCapture()
        {
            // initialize kinect device
        }

    }
}
