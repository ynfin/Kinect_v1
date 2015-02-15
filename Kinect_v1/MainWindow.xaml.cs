using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Security.AccessControl;
using System.Threading.Tasks;
using System.Windows.Controls;
using Kinect_v1.Model;
using Microsoft.Kinect;
using Brushes = System.Drawing.Brushes;
using Point = System.Drawing.Point;

namespace Kinect_v1
{
    public partial class MainWindow
    {
        private const int MapDepthToByte = 8000 / 256;

        private Logger _logger = new Logger();
        private Model.ImageModel _imageModel = new Model.ImageModel();
        private Model.FpsCounter _fpsCounter = new Model.FpsCounter(30);
        private Model.FpsCounter _fpsCounterColor = new Model.FpsCounter(30);
        private Model.FpsCounter _fpsCounterDepth = new Model.FpsCounter(30);

        private KinectSensor _kinectSensor;

        private MultiSourceFrameReader _multiFrameReader;
        private ColorFrameReader _colorFrameReader;
        private DepthFrameReader _depthFrameReader;

        private FrameDescription _depthFrameDescription;
        private FrameDescription _colorFrameDescription;

        private WriteableBitmap _depthBitmap;
        private WriteableBitmap _colorBitmap;
        private WriteableBitmap _binBitmap;

        private ImageSource _binaryImageSource = null;

        enum DisplaySource { ColorStream, DepthStream, InfraredStream, BinaryStream };
        DisplaySource _source = DisplaySource.ColorStream;

        private byte[] _depthPixels;
        private byte[] _rawColorPix;
        private ushort[] _rawDepthPix;

        private CameraSpacePoint[] _cameraSpacePoints;

        private TimeSpan _colorFrameCaptureTimeSpan;
        private TimeSpan DepthFrameCaptureTimeSpan;

        public bool DepthSet;
        public bool ColorSet;

        private int[] rawCoords = new int[]{0,0,0}; //raw 2D colorspace coords
        private int[] mapCoords = new int[]{0,0,0}; //mapped to 3D cameraspace coords

        private TaskScheduler _scheduler;

        private int taskCounter = 0;

        private bool _recordEnabled;

        public MainWindow()
        {
            _kinectSensor = KinectSensor.GetDefault();

            // streams
            _multiFrameReader = _kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color);
            _depthFrameReader = _kinectSensor.DepthFrameSource.OpenReader();
            _colorFrameReader = _kinectSensor.ColorFrameSource.OpenReader();

            // handler
            _multiFrameReader.MultiSourceFrameArrived += Reader_MultiFrameReader;
            _colorFrameReader.FrameArrived += Reader_ColorFrameReader;
            _depthFrameReader.FrameArrived += Reader_DepthFrameReader;

            // framedescriptions
            _depthFrameDescription = _kinectSensor.DepthFrameSource.FrameDescription;
            _colorFrameDescription = _kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            //bitmaps
            _colorBitmap = new WriteableBitmap(_colorFrameDescription.Width, _colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
            _depthBitmap = new WriteableBitmap(_depthFrameDescription.Width, _depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
            _binBitmap = new WriteableBitmap(_depthFrameDescription.Width, _depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

            // raw arrays
            _depthPixels = new byte[_depthFrameDescription.Width * _depthFrameDescription.Height];
            _rawColorPix = new byte[_colorFrameDescription.Width * _colorFrameDescription.Height * 4];
            _rawDepthPix = new ushort[_depthFrameDescription.Width * _depthFrameDescription.Height];

            // spaces
            _cameraSpacePoints = new CameraSpacePoint[_colorFrameDescription.Width * _colorFrameDescription.Height];

            //threading
            _scheduler = TaskScheduler.FromCurrentSynchronizationContext();

            // startup

            _kinectSensor.Open();
            DataContext = this;
            InitializeComponent();
        }

        private void Reader_ColorFrameReader(object sender, ColorFrameArrivedEventArgs e)
        {
            FpsDisplay2.Text = "C: " + _fpsCounterColor.Tick();
        }

        private void Reader_DepthFrameReader(object sender, DepthFrameArrivedEventArgs e)
        {
            FpsDisplay3.Text = "D: " + _fpsCounterDepth.Tick();
        }


        private void Reader_MultiFrameReader(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();
            //DepthFrameCaptureTimeSpan = depthFrame.RelativeTime;
            //ColorFrameCaptureTimeSpan = colorFrame.RelativeTime;

            using (ColorFrame colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    _colorFrameDescription = colorFrame.FrameDescription;
                    _colorFrameCaptureTimeSpan = colorFrame.RelativeTime;
                    
                    _colorBitmap.Lock();

                    // verify data and write the new color frame data to the display bitmap
                    if ((_colorFrameDescription.Width == _colorBitmap.PixelWidth) && (_colorFrameDescription.Height == _colorBitmap.PixelHeight))
                    {
                        colorFrame.CopyConvertedFrameDataToIntPtr(_colorBitmap.BackBuffer, (uint)(_colorFrameDescription.Width * _colorFrameDescription.Height * 4), ColorImageFormat.Bgra);
                        _colorBitmap.AddDirtyRect(new Int32Rect(0, 0, _colorBitmap.PixelWidth, _colorBitmap.PixelHeight));

                        // draw on the bitmap
                        Point[] p = {new Point(700, 700), new Point(200, 200)};
                        p[0] = new Point(rawCoords[0], rawCoords[1]);
                        drawCoordsOnColorBitmap(p,64);
                           
                        // storage for imagemodel
                        colorFrame.CopyConvertedFrameDataToArray(_rawColorPix, ColorImageFormat.Bgra);
                        _imageModel.createColorImage(_rawColorPix, _colorFrameDescription.Width, _colorFrameDescription.Height);

                        ColorSet = true;
                    }
                    _colorBitmap.Unlock();
                }
            }


            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    DepthFrameCaptureTimeSpan = depthFrame.RelativeTime;

                    // the fastest way to process the body index data is to directly access the underlying buffer
                    using (KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((_depthFrameDescription.Width * _depthFrameDescription.Height) == (depthBuffer.Size / _depthFrameDescription.BytesPerPixel)) &&
                            (_depthFrameDescription.Width == _depthBitmap.PixelWidth) && (_depthFrameDescription.Height == _depthBitmap.PixelHeight))
                        {
                            //ushort maxDepth = ushort.MaxValue;
                            ushort maxDepth = depthFrame.DepthMaxReliableDistance;

                            ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
                RenderDepthPixels();

            if (DepthSet && ColorSet)
            {
                
                taskCounter ++;
                Task.Factory.StartNew<int[]>(() => ProcessingMethod()).ContinueWith((i) => UpdateCoords(i.Result), _scheduler);
            }
               
        }


        private void drawCoordsOnColorBitmap(Point[] coordPointArray, int sizeD)
        {
            // inside bounds check
            foreach (var coordPoint in coordPointArray)
            {
                if (coordPoint.X <= sizeD/2 || coordPoint.X >= _colorFrameDescription.Width-sizeD/2 ||
                    coordPoint.Y <= sizeD/2 || coordPoint.Y >= _colorFrameDescription.Height-sizeD/2)
                    return;  
            }
            
                
            // draw on bitmap
            var tempBitmap = new Bitmap(_colorBitmap.PixelWidth,
                _colorBitmap.PixelHeight,
                _colorBitmap.BackBufferStride,
                System.Drawing.Imaging.PixelFormat.Format32bppRgb,
                _colorBitmap.BackBuffer);

            using (var bitmapGraphics = Graphics.FromImage(tempBitmap))
            {
                bitmapGraphics.SmoothingMode = SmoothingMode.HighSpeed;
                bitmapGraphics.InterpolationMode = InterpolationMode.Low;
                bitmapGraphics.CompositingMode = CompositingMode.SourceOver;
                bitmapGraphics.CompositingQuality = CompositingQuality.HighSpeed;

                bool colordrawn = false;
                foreach (var coordPoint in coordPointArray)
                {
                    if (colordrawn)
                    {
                        bitmapGraphics.DrawIcon(new Icon("yellowmarker.ico", new System.Drawing.Size(sizeD, sizeD)),
                           new Rectangle(new Point(coordPoint.X - sizeD / 2, coordPoint.Y - sizeD / 2), new System.Drawing.Size(sizeD, sizeD)));

                        bitmapGraphics.FillRectangle(new SolidBrush(System.Drawing.Color.FromArgb(150, 255, 255, 0)), new RectangleF(new Point(coordPoint.X + (sizeD / 2) + 10, coordPoint.Y - sizeD / 2 - 32), new System.Drawing.Size(150, 28)));
                        bitmapGraphics.DrawString("colorspace", new Font(new System.Drawing.FontFamily("Arial"), 16), Brushes.Black, new RectangleF(new Point(coordPoint.X + (sizeD / 2) + 10, coordPoint.Y - sizeD / 2 - 35), new System.Drawing.Size(200, 30)));
                    }
                    else
                    {
                        bitmapGraphics.DrawIcon(new Icon("redmarker.ico", new System.Drawing.Size(sizeD, sizeD)),
                            new Rectangle(new Point(coordPoint.X - sizeD / 2, coordPoint.Y - sizeD / 2), new System.Drawing.Size(sizeD, sizeD)));
                        
                        bitmapGraphics.FillRectangle(new SolidBrush(System.Drawing.Color.FromArgb(150, 255, 0, 0)), new RectangleF(new Point(coordPoint.X + (sizeD / 2) + 10, coordPoint.Y - sizeD / 2 - 32), new System.Drawing.Size(180, 28)));
                        bitmapGraphics.DrawString("cameraspace", new Font(new System.Drawing.FontFamily("Arial"), 16), Brushes.Black, new RectangleF(new Point(coordPoint.X + (sizeD / 2) + 10, coordPoint.Y - sizeD / 2 - 35), new System.Drawing.Size(200, 30)));
                        
                        colordrawn = true;
                    }        
                }   
            }
            tempBitmap.Dispose();
        }

       
        private int[] ProcessingMethod()
        {
            //System.Threading.Thread.Sleep(1000);
            return _imageModel.ProcesscolorReturnCoords();
        }

        private void UpdateCoords(int[] inputInts)
        {
            
            if (_recordEnabled)
                appendDataToLogg(inputInts, _colorFrameCaptureTimeSpan);

            // ----------------
            rawCoords = inputInts;
            DepthSet = false;
            ColorSet = false;

            if (_imageModel.newBinaryReady)
                _binBitmap =  writableBitmapFromBitmap(_imageModel.GetBinaryBitmap());

            loggerDisplay.Text = (_colorFrameCaptureTimeSpan).ToString();
            FpsDisplay.Text = "CV: " + _fpsCounter.Tick();

            taskCounter--;
        }

        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            ushort* frameData = (ushort*)depthFrameData;

            for (int i = 0; i < (int)(depthFrameDataSize / _depthFrameDescription.BytesPerPixel); ++i)
            {
                ushort depth = frameData[i];
                _depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
            _imageModel.createGrayImage(_depthPixels, _depthFrameDescription.Width, _depthFrameDescription.Height);
            DepthSet = true;
        }


        private void mapWithGivenCoords(Point unmappedPoint)
        {
            // declare the unmapped coords
           
            _kinectSensor.CoordinateMapper.MapColorFrameToCameraSpace(_rawDepthPix,_cameraSpacePoints);
        
        }

        private void appendDataToLogg(int[] coordInts, TimeSpan time)
        {
            // convert input to z,y,x,time format
            _logger.appendLogline(coordInts,time);
        }

        private void RenderDepthPixels()
        {
            _depthBitmap.WritePixels(
                new Int32Rect(0, 0, _depthBitmap.PixelWidth, _depthBitmap.PixelHeight),
                _depthPixels,
                _depthBitmap.PixelWidth,
                0);
        }

        private WriteableBitmap writableBitmapFromBitmap(Bitmap bmp)
        {   
            // GDI HANDLE LEAK
            BitmapSource b = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height));
            WriteableBitmap wb = new WriteableBitmap(b);
            
            return wb;
        }

        /*
        public WriteableBitmap ByteArrayToImage(Byte[] BArray)
        {

            var width = 100;
            var height = 100;
            var dpiX = 96d;
            var dpiY = 96d;
            var pixelFormat = PixelFormats.Pbgra32;
            var bytesPerPixel = (pixelFormat.BitsPerPixel + 7) / 8;
            var stride = bytesPerPixel * width;

            var bitmap = BitmapImage.Create(width, height, dpiX, dpiY, pixelFormat, null, BArray, stride);
            WriteableBitmap wbtmMap = new WriteableBitmap(BitmapFactory.ConvertToPbgra32Format(bitmap));
            return wbtmMap;
        }
         */

        public ImageSource DepthSource
        {
            get
            {
                switch (_source)
                {
                    case DisplaySource.DepthStream:
                        return _depthBitmap;
                    case DisplaySource.BinaryStream:
                        return _binBitmap;
                    default:
                        return _colorBitmap;
                }
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_kinectSensor != null)
            {
                _kinectSensor.Close();
                _kinectSensor = null;
            }

        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            buttonColor.BorderBrush = System.Windows.Media.Brushes.Red;
            buttonDepth.BorderBrush = System.Windows.Media.Brushes.White;

            _source = DisplaySource.ColorStream;
            BindingOperations.GetBindingExpressionBase(DisplayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        private void Depth_Click(object sender, RoutedEventArgs e)
        {
            buttonDepth.BorderBrush = System.Windows.Media.Brushes.Red;
            buttonColor.BorderBrush = System.Windows.Media.Brushes.White;

            _source = DisplaySource.DepthStream;
            BindingOperations.GetBindingExpressionBase(DisplayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        private void Binary_Click(object sender, RoutedEventArgs e)
        {
            buttonColor.BorderBrush = System.Windows.Media.Brushes.White;
            buttonDepth.BorderBrush = System.Windows.Media.Brushes.White;

            _imageModel.fetchNextBinaryImage = true;
            _source = DisplaySource.BinaryStream;
            BindingOperations.GetBindingExpressionBase(DisplayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        private void DisplayScreen_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(DisplayScreen);
            Debug.WriteLine("looking at " + p.X +", "+ p.Y);
            _imageModel.getColorAtPixel((int) p.X, (int) p.Y, true);
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            _recordEnabled = !_recordEnabled;

            if (_recordEnabled)
            {
                _logger.newLoggerFile();
                RecordButton.Background = System.Windows.Media.Brushes.Red;
                RecordButton.Content = "Recording";
            }
            else
            {
                _logger.dumpToFile();
                _logger.clear();
                RecordButton.Background = System.Windows.Media.Brushes.SlateGray;
                RecordButton.Content = "Record";
            }
                
            
        
        }


        /*
        public static Bitmap GetBitmapFromBitmapSource(BitmapSource bSource)
        {
            Bitmap bmp;
            using (MemoryStream ms = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bSource));
                enc.Save(ms);
                bmp = new Bitmap(ms);
            }
            return bmp;
        }
        */


    }
}
