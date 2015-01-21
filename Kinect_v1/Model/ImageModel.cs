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

        Hsv lowerColorThreshold = new Hsv(100, 100, 100);
        Hsv upperColorThreshold = new Hsv(200, 200, 200);

        public Image<Hsv,Byte> BitmapToColorImage(WriteableBitmap inputFrame)
        {
            Bitmap bmp;
            using (MemoryStream bitmapstream = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)inputFrame));
                encoder.Save(bitmapstream);
                bmp = new Bitmap(bitmapstream);
                return new Image<Hsv, Byte>(bmp);
            }
            //return new Image<Hsv, Byte>(bmp);
        }

  
        public Image<Gray, Byte> BitmapToGrayscaleImage(WriteableBitmap inputFrame)
        {
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(inputFrame));
            MemoryStream ms = new MemoryStream();
            encoder.Save(ms);
            return new Image<Gray, Byte>(new Bitmap(ms));
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
            //this.ColorProcessed = this.ColorImage.InRange(this.lowerColorThreshold, this.upperColorThreshold);
            //this.ColorProcessed = this.ColorProcessed.Dilate(5);
            //this.ColorProcessed = this.ColorProcessed.Erode(5);

            this.ColorProcessed = ColorImage.Convert<Gray, Byte>();
            this.ColorProcessed.ToBitmap().Save(@"C:\Users\Kinect\Pictures\fromImageModel.bmp");
            System.Diagnostics.Debug.WriteLine("saved");
            //MCvMoments moment = this.ColorProcessed.GetMoments(true);
            //this.gravityCenter = new PointF(((float)moment.m10 / (float)moment.m00),(float)(moment.m01 / (float)moment.m00));
        }

        public Bitmap getBinaryBitmap()
        {
            return this.ColorProcessed.ToBitmap();
        }

        public Bitmap getColorBitmap()
        {
            return this.ColorImage.ToBitmap();
        }

        public void recieveWritableColorBitmap(WriteableBitmap inWBMP)
        {
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(inWBMP));
           
            using (MemoryStream ms = new MemoryStream())
            {
                encoder.Save(ms);
                Bitmap b=new Bitmap(ms);
                ColorImage = new Image<Hsv, Byte>(b);
            }     
        }

        public void setColorFrame(WriteableBitmap rawInput)
        {
            System.Diagnostics.Debug.WriteLine("color is started");
            this.ColorImage = BitmapToColorImage(rawInput);
            System.Diagnostics.Debug.WriteLine("color is set");
        }
    }
}
