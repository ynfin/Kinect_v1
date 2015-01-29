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
        Image<Hsv, Byte> _colorImage = null;
        Image<Gray, Byte> _colorProcessed = null;
        PointF _gravityCenter = new PointF(-1,-1);

        Image<Gray, Byte> _depthImage = null;
        Image<Gray, Byte> _infraredImage = null;

        Hsv _lowerColorThreshold = new Hsv(100, 100, 100);
        Hsv _upperColorThreshold = new Hsv(200, 200, 200);


        public void SetColorFrame(Bitmap rawInput)
        {
            this._colorImage = new Image<Hsv, byte>(rawInput);
        }
       
        public void SetDepthFrame(Bitmap rawInput)
        {
            this._depthImage = new Image<Gray, byte>(rawInput);
            // add the kinect formatted frame here as well, in order to map everything after locating colorcoordinates.
        }

        public void SetInfraredFrame(Bitmap rawInput)
        {
            this._infraredImage = new Image<Gray, byte>(rawInput);
        }

        public void TriggerProcessing()
        {
            ProcessColorImage();
        }

        public void ProcessColorImage()
        {
            //this.ColorProcessed = this.ColorImage.InRange(this.lowerColorThreshold, this.upperColorThreshold);
            //this.ColorProcessed = this.ColorProcessed.Dilate(5);
            //this.ColorProcessed = this.ColorProcessed.Erode(5);

            this._colorProcessed = _colorImage.Convert<Gray, Byte>();
            //this.ColorProcessed.ToBitmap().Save(@"C:\Users\Kinect\Pictures\fromImageModel.bmp");
            //System.Diagnostics.Debug.WriteLine("saved");
            //MCvMoments moment = this.ColorProcessed.GetMoments(true);
            //this.gravityCenter = new PointF(((float)moment.m10 / (float)moment.m00),(float)(moment.m01 / (float)moment.m00));
        }

        public Bitmap GetBinaryBitmap()
        {
            return this._colorProcessed.ToBitmap();
        }

        public Bitmap GetColorBitmap()
        {
            return this._colorImage.ToBitmap();
        }

        public void RecieveWritableColorBitmap(WriteableBitmap inWbmp)
        {
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(inWbmp));
           
            using (MemoryStream ms = new MemoryStream())
            {
                encoder.Save(ms);
                Bitmap b=new Bitmap(ms);
                _colorImage = new Image<Hsv, Byte>(b);
            }     
        }

        
    }
}
