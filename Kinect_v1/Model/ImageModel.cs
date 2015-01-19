using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.Util;
using System.Windows.Media.Imaging;
using System.Drawing;

namespace Kinect_v1.Model
{
    class ImageModel
    {
        // Color related data
        Image<Hsv, Byte> ColorImage = null;
        Image<Gray, Byte> ColorProcessed = null;
        PointF gravityCenter = new PointF(-1,-1);


        Image<Gray, Byte> DepthImage = null;
        Image<Gray, Byte> InfraredImage = null;

        Hsv lowerColorThreshold = new Hsv(10, 10, 10);
        Hsv upperColorThreshold = new Hsv(200, 200, 200);

        public Image<Hsv,Byte> BitmapToColorImage(WriteableBitmap inputFrame)
        {
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(inputFrame));
            MemoryStream ms = new MemoryStream();
            encoder.Save(ms);
            return new Image<Hsv, Byte>(new Bitmap(ms));
        }

        public Image<Gray, Byte> BitmapToGrayscaleImage(WriteableBitmap inputFrame)
        {
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(inputFrame));
            MemoryStream ms = new MemoryStream();
            encoder.Save(ms);
            return new Image<Gray, Byte>(new Bitmap(ms));
        }

        public void setColorFrame(WriteableBitmap rawInput)
        {
            System.Diagnostics.Debug.WriteLine("color is started");
            this.ColorImage = BitmapToColorImage(rawInput);
            System.Diagnostics.Debug.WriteLine("color is set");
        }

        public void setDepthFrame(WriteableBitmap rawInput)
        {
            this.DepthImage = BitmapToGrayscaleImage(rawInput);
            // add the kinect formatted frame here as well, in order to map everything after locating colorcoordinates.
        }

        public void setInfraredFrame(WriteableBitmap rawInput)
        {
            this.InfraredImage = BitmapToGrayscaleImage(rawInput);
        }

        public void triggerProcessing()
        {
            processColorImage();
        }

        public void processColorImage()
        {
            this.ColorProcessed = this.ColorImage.InRange(this.lowerColorThreshold, this.upperColorThreshold);
            this.ColorProcessed = this.ColorProcessed.Dilate(5);
            this.ColorProcessed = this.ColorProcessed.Erode(5);

            MCvMoments moment = this.ColorProcessed.GetMoments(true);
            this.gravityCenter = new PointF(((float)moment.m10 / (float)moment.m00),(float)(moment.m01 / (float)moment.m00));
        }

        public Bitmap getBinaryBitmap()
        {
            return this.ColorProcessed.ToBitmap();
        }


 

    }
}
