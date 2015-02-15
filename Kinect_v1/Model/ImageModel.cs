using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.Structure;

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

        // PROCESSORS
        public void Processcolor()
        {

            using (Image<Hsv, byte> im = _colorImageBgra.Convert<Hsv, byte>())
            {
                using (Image<Gray, byte> t_img = im.InRange(_lowerColorThreshold, _upperColorThreshold))
                {

                    moments = t_img.GetMoments(true);
                    _gravCenterX = (float)moments.GravityCenter.x;
                    _gravCenterY = (float)moments.GravityCenter.y;
                    _colorProcessed = t_img.Clone();

                }
            }



            //_colorProcessed = _colorImageBgra.Convert<Gray, Byte>();

            // Segmentation
            //_colorImageHsv = _colorImageBgra.Convert<Hsv, byte>();
            //_colorProcessed = _colorImageHsv.InRange(_lowerColorThreshold, _upperColorThreshold);

            // Get moments
            //moments = _colorProcessed.GetMoments(true);
            //_gravCenterX = (float)moments.GravityCenter.x;
            //_gravCenterY = (float)moments.GravityCenter.y;
        }


        public int[] ProcesscolorReturnCoords()
        {
            int[] coordInts = new int[3];

            using (Image<Hsv, byte> im = _colorImageBgra.Convert<Hsv, byte>())
            {
                using (Image<Gray, byte> gim = im.InRange(_lowerColorThreshold, _upperColorThreshold))
                {
                    moments = gim.GetMoments(true);
                    coordInts[0] = (int)moments.GravityCenter.x;
                    coordInts[1] = (int)moments.GravityCenter.y;
                    coordInts[2] = -1;

                    if (fetchNextBinaryImage)
                    {
                        _colorProcessed = gim.Clone();
                        fetchNextBinaryImage = false;
                        newBinaryReady = true;
                    }
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
