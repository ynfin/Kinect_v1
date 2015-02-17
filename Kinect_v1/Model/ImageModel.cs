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

        private float _gravCenterX;
        private float _gravCenterY;

        Hsv _lowerColorThreshold = new Hsv(88, 2, 250);
        Hsv _upperColorThreshold = new Hsv(92, 6, 254);

        MCvMoments moments = new MCvMoments();

        public bool fetchNextBinaryImage = false;
        public bool newBinaryReady = false;

        Image<Hsv, byte> imageHsv;
        Image<Gray, byte> imageGray;

        public ImageModel()
        {
            imageHsv = new Image<Hsv, byte>(1920,1080);
            imageGray = new Image<Gray, byte>(1920, 1080);
        }

        // PROCESSORS
        public int[] ProcesscolorReturnCoords()
        {
            int[] coordInts = new int[2];

                using (imageHsv)
                {
                    imageHsv = _colorImageBgra.Convert<Hsv, byte>(); // ~37 ms
                    using (imageGray)
                    {
                        imageGray = imageHsv.InRange(_lowerColorThreshold, _upperColorThreshold); // ~30 ms

                        moments = imageGray.GetMoments(true);
                        coordInts[0] = (int)(moments.GravityCenter.x);
                        coordInts[1] = (int)(moments.GravityCenter.y);
                    }
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
                _lowerColorThreshold = new Hsv(hsvInts[0] - 5, hsvInts[1] - 10, hsvInts[2] - 10);
                _upperColorThreshold = new Hsv(hsvInts[0] + 5, hsvInts[1] + 10, hsvInts[2] + 10);
            }
            return hsvInts;
        }

        public PointF getGravityCenter()
        {
            return new PointF(_gravCenterX, _gravCenterY);
        }


        public void drawCrosshairOnGrayImage(float x, float y)
        {
            _colorProcessed.Draw(new Cross2DF(new PointF(x, y), 60, 60), new Gray(125), 3);
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
