using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Collections;
using System.Collections.Generic;

namespace Kinect_v1.Model
{
    class ImageModel
    {

        // Color related data
        private Image<Bgra, Byte> _colorImageBgra;// = new Image<Bgra, byte>("null_large_color.png");
        private Image<Gray, Byte> _colorProcessed;// = new Image<Gray, byte>(1920, 1080, new Gray(100));
        private Image<Gray, Byte> _depthImage;// = new Image<Gray, byte>("null_small.png");

        MCvScalar _lowerColorScalar = new MCvScalar(110-2,246-20,173-20);
        MCvScalar _upperColorScalar = new MCvScalar(110+2,246+20,173+20);

        MCvMoments moments = new MCvMoments();

        public bool fetchNextBinaryImage = false;
        public bool newBinaryReady = false;

        Image<Bgr, byte> imageBgr;
        Image<Hsv, byte> imageHsv;
        Image<Gray, byte> imageGray;

        IntPtr cvt = IntPtr.Zero;
        MCvMoments mom = new MCvMoments();

        public ImageModel()
        {
            imageBgr = new Image<Bgr, byte>(1920, 1080);
            imageHsv = new Image<Hsv, byte>(1920,1080);
            imageGray = new Image<Gray, byte>(1920, 1080);
        }

        private double getNormCentralMoment(int xOrder, int yOrder)
        {
            return CvInvoke.cvGetNormalizedCentralMoment(ref mom, xOrder, yOrder);
        }

        // PROCESSORS
        public int[] ProcesscolorReturnCoords()
        {
            int[] coordInts = new int[2];

            // using CVInvoke to gain major speed increases
            CvInvoke.cvCvtColor(_colorImageBgra.Ptr,imageBgr.Ptr,Emgu.CV.CvEnum.COLOR_CONVERSION.BGRA2BGR);
            CvInvoke.cvCvtColor(imageBgr.Ptr, imageHsv.Ptr, Emgu.CV.CvEnum.COLOR_CONVERSION.BGR2HSV);
            CvInvoke.cvInRangeS(imageHsv.Ptr,_lowerColorScalar,_upperColorScalar,imageGray.Ptr);
            //CvInvoke.cvSmooth(imageGray.Ptr, imageGray.Ptr, Emgu.CV.CvEnum.SMOOTH_TYPE.CV_GAUSSIAN, 9, 0, 0, 0);
            //CvInvoke.cvErode(imageGray.Ptr, imageGray.Ptr, IntPtr.Zero, 1);

            CvInvoke.cvMoments(imageGray.Ptr,ref mom,1);
            coordInts[0] = (int)(mom.m10 / mom.m00);
            coordInts[1] = (int)(mom.m01 / mom.m00);

            //Debug.WriteLine("m00: " + mom.m00 + " m01: " + mom.m01 + " m10: " + mom.m10);

            //moments = imageGray.GetMoments(true);
            //coordInts[0] = (int)(moments.GravityCenter.x);
            //coordInts[1] = (int)(moments.GravityCenter.y);

            if (fetchNextBinaryImage)
            {
                _colorProcessed = imageGray;
                newBinaryReady = true;
                fetchNextBinaryImage = false;
            }

            return coordInts;
        }


        public int[] getColorAtPixel(int x, int y, bool setcoloralso)
        {
            int[] hsvInts = new[] {0, 0, 0};

            using (Image<Hsv, byte> im = _colorImageBgra.Convert<Hsv, byte>())
            {
                hsvInts[0] = im.Data[y, x, 0]; //Read to the Red Spectrum
                hsvInts[1] = im.Data[y, x, 1]; //Read to the Green Spectrum
                hsvInts[2] = im.Data[y, x, 2]; //Read to the BlueSpectrum
            }

            if (setcoloralso)
            {
                _lowerColorScalar = new MCvScalar(hsvInts[0] - 5, hsvInts[1] - 10, hsvInts[2] - 10);
                _upperColorScalar = new MCvScalar(hsvInts[0] + 5, hsvInts[1] + 10, hsvInts[2] + 10);
                Debug.WriteLine("Color reference set to: [" + hsvInts[0] + "," + hsvInts[1] + "," + hsvInts[2] + "]");
            }
            return hsvInts;
        }

        // GETTERS AND SETTERS
        // setters

        public void createGrayImage(byte[] pixels, int width, int height)
        {
            using (Image<Gray, byte> im = new Image<Gray, byte>(width, height))
            {
                im.Bytes = pixels;
                _depthImage = im.Clone();
            }
        }

        public void createColorImage(byte[] pixels, int width, int height)
        {
            // kan det brukes pointer her???
            using (Image<Bgra, byte> im = new Image<Bgra, byte>(width, height))
            {
                im.Bytes = pixels;
                _colorImageBgra = im.Clone();
            }
        }

        //getters
        public Bitmap GetBinaryBitmap()
        {
            newBinaryReady = false;
            return _colorProcessed.ToBitmap();
        }

        public Bitmap GetDepthBitmap()
        {
            return _depthImage.ToBitmap();
        }

        public Bitmap GetColorBitmap()
        {
            return _colorImageBgra.ToBitmap();
        }




    }
}
