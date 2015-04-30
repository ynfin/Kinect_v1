using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
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

        private FrameDescription _depthFrameDescription;
        private FrameDescription _colorFrameDescription;

        private DepthFrame _depthframe;
        private ColorFrame _colorframe;

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

        // Intermediate storage for the color to depth mapping
        private DepthSpacePoint[] _colorMappedToDepthPoints = null;
        //private CameraSpacePoint[] colorMappedToCameraPoints = null;
        private ushort[] framed;

        private TimeSpan _colorFrameCaptureTimeSpan;
        private TimeSpan DepthFrameCaptureTimeSpan;

        public bool DepthSet;
        public bool ColorSet;

        //private int[] rawCoords = new int[] { 0, 0, 0 }; //raw 2D colorspace coords
        //private int[] mapCoords = new int[] { 0, 0, 0 }; //mapped to 3D cameraspace coords
        private Coordinates _coordinates = new Coordinates();

        private int[] globalret;

        private TaskScheduler _scheduler;

        private int taskCounter = 0;

        private bool _recordEnabled;

        public MainWindow()
        {
            _kinectSensor = KinectSensor.GetDefault();

            // streams
            _multiFrameReader = _kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color);
            _colorFrameReader = _kinectSensor.ColorFrameSource.OpenReader();

            // handler
            _multiFrameReader.MultiSourceFrameArrived += Reader_MultiFrameReader;
            _colorFrameReader.FrameArrived += Reader_ColorFrameReader;

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
            _colorMappedToDepthPoints = new DepthSpacePoint[_colorFrameDescription.Width * _colorFrameDescription.Height];
            framed = new ushort[_depthFrameDescription.Width * _depthFrameDescription.Height];

            //threading
            _scheduler = TaskScheduler.FromCurrentSynchronizationContext();

            globalret = new int[]{0,0};

            // startup

            _kinectSensor.Open();
            DataContext = this;
            InitializeComponent();
        }

        ////////////////////////////////////////////////    STREAM READERS METHODS   /////////////////////////////////////////////////

        private void Reader_ColorFrameReader(object sender, ColorFrameArrivedEventArgs e)
        {
            FpsDisplay2.Text = "C: " + _fpsCounterColor.Tick();
        }

        private void Reader_MultiFrameReader(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            _colorframe = multiSourceFrame.ColorFrameReference.AcquireFrame();
            _depthframe = multiSourceFrame.DepthFrameReference.AcquireFrame();

            //using (ColorFrame colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
            using (_colorframe)
            {
                if (_colorframe != null)
                {
                    // gather frame data
                    _colorFrameDescription = _colorframe.FrameDescription;
                    _colorFrameCaptureTimeSpan = _colorframe.RelativeTime;

                    _colorBitmap.Lock();

                    // verify data and write the new color frame data to the display bitmap
                    if ((_colorFrameDescription.Width == _colorBitmap.PixelWidth) && (_colorFrameDescription.Height == _colorBitmap.PixelHeight))
                    {
                        _colorframe.CopyConvertedFrameDataToIntPtr(_colorBitmap.BackBuffer, (uint)(_colorFrameDescription.Width * _colorFrameDescription.Height * 4), ColorImageFormat.Bgra);
                        _colorBitmap.AddDirtyRect(new Int32Rect(0, 0, _colorBitmap.PixelWidth, _colorBitmap.PixelHeight));

                        // draw captured data on the bitmap
                        //drawCoordsOnColorBitmap(new Point(rawCoords[0],rawCoords[1]), 64);
                        drawCoordsOnColorBitmap(_coordinates.getColorCoordinatesPoint() ,64);

                        IntPtr rc = IntPtr.Zero;
                        // storage for imagemodel
                        //_colorframe.CopyConvertedFrameDataToIntPtr(rc,1920*1080*4,ColorImageFormat.Bgra);
                        _colorframe.CopyConvertedFrameDataToArray(_rawColorPix, ColorImageFormat.Bgra);
                        //_imageModel.createColorImage(rc, _colorFrameDescription.Width, _colorFrameDescription.Height);
                        _imageModel.createColorImage(_rawColorPix, _colorFrameDescription.Width, _colorFrameDescription.Height);

                        ColorSet = true;
                    }
                    _colorBitmap.Unlock();
                }
            }


            bool depthFrameProcessed = false;

                if (_depthframe != null)
                {
                    DepthFrameCaptureTimeSpan = _depthframe.RelativeTime;

                    if (_source == DisplaySource.DepthStream)
                    {
                        // the fastest way to process the body index data is to directly access the underlying buffer
                        using (KinectBuffer depthBuffer = _depthframe.LockImageBuffer())
                        {
                            // verify data and write the color data to the display bitmap
                            if (((_depthFrameDescription.Width * _depthFrameDescription.Height) == (depthBuffer.Size / _depthFrameDescription.BytesPerPixel)) &&
                                (_depthFrameDescription.Width == _depthBitmap.PixelWidth) && (_depthFrameDescription.Height == _depthBitmap.PixelHeight))
                            {
                                //ushort maxDepth = ushort.MaxValue;
                                ushort maxDepth = _depthframe.DepthMaxReliableDistance;

                                ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, _depthframe.DepthMinReliableDistance, maxDepth);
                                depthFrameProcessed = true;
                            }
                        }
                    }
                    else
                        DepthSet = true;
                    
                    // thread will dispose depthframe when done with it
                    Task.Factory.StartNew(() => MapColorToCameraInThread(_depthframe, _kinectSensor)).ContinueWith((i) => updateMappedCameraSpaces(i.Result), _scheduler);
                }


            if (depthFrameProcessed)
                RenderDepthPixels();

            if (DepthSet && ColorSet)
            {
                Task.Factory.StartNew(() => ProcessingMethod()).ContinueWith((i) => UpdateCoords(i.Result), _scheduler);
            }

        }



        ////////////////////////////////////////////////    THREADING  METHODS   /////////////////////////////////////////////////

        //------------------------------------------ MAP depthFrameData.UnderlyingBuffer -> _CameraSpacePoints ----------------------------------

        private CameraSpacePoint[] MapColorToCameraInThread(DepthFrame depthFrameInput, KinectSensor sensor)
        {
            CameraSpacePoint[] csp = new CameraSpacePoint[1920 * 1080];
            using (KinectBuffer depthFrameData = depthFrameInput.LockImageBuffer())
            {
                sensor.CoordinateMapper.MapColorFrameToCameraSpaceUsingIntPtr(depthFrameData.UnderlyingBuffer, depthFrameData.Size, csp);
            }
            return csp;
        }

        private void updateMappedCameraSpaces(CameraSpacePoint[] mappedCamSpacePoints)
        {
            _cameraSpacePoints = mappedCamSpacePoints;
            _depthframe.Dispose();
        }

        
        //------------------------------------------ OPEN CV PROSESSERING -> SET NY GLOBAL DATA ----------------------------------

        long accu = 0;
        int counter = 0;

        private Coordinates ProcessingMethod()
        {
            // store all coordinates into designated class for coordinate storage
            Stopwatch sw = new Stopwatch();

            sw.Start();
            
            Coordinates coordinateStorage = new Coordinates(_imageModel.ProcesscolorReturnCoords());
            float[] mappedCoords = (mapWithGivenCoords(coordinateStorage.GetColorCoordinatesInts()));
            coordinateStorage.setCameraCoords(mappedCoords);
            
            accu += sw.ElapsedMilliseconds;
            sw.Reset();
            counter++;
            if (counter == 1000)
            {
                counter = 0;
                Debug.WriteLine(accu / 1000.0);
                accu = 0;
            }
            
            

            return coordinateStorage;
        }

        private void UpdateCoords(Coordinates inputCoords)
        {

            //Findcoordinatesindepthspace(inputInts.GetColorCoordinatesInts());

            // add to log if recording
            if (_recordEnabled)
                _logger.appendLogline(inputCoords.getCamCoordsFloat(), _colorFrameCaptureTimeSpan);

            // 
            _coordinates = inputCoords;
            DepthSet = false;
            ColorSet = false;

            // get binary bitmap if requested
            if (_imageModel.newBinaryReady)
                _binBitmap = writableBitmapFromBitmap(_imageModel.GetBinaryBitmap());

            _coordinates = inputCoords;
            loggerDisplay.Text = (_colorFrameCaptureTimeSpan).ToString();
            FpsDisplay.Text = "CV: " + _fpsCounter.Tick();

            taskCounter--;
        }

        ////////////////////////////////////////////////    MAPPING METHODS   /////////////////////////////////////////////////

        private float[] mapWithGivenCoords(int[] unmappedPoint2D)
        {
            float[] colorMappedToCamera = new float[3];

            unsafe
            {
                fixed (CameraSpacePoint* colorMappedToCamPointsPointer = _cameraSpacePoints)
                {

                    if (unmappedPoint2D[0] < 0) unmappedPoint2D[0] = 0;
                    if (unmappedPoint2D[1] < 0) unmappedPoint2D[1] = 0;

                    int colorKndex = (unmappedPoint2D[1] * _colorFrameDescription.Width) + unmappedPoint2D[0];

                    colorMappedToCamera[0] = colorMappedToCamPointsPointer[colorKndex].X;
                    colorMappedToCamera[1] = colorMappedToCamPointsPointer[colorKndex].Y;
                    colorMappedToCamera[2] = colorMappedToCamPointsPointer[colorKndex].Z;
                }
            }
            return colorMappedToCamera;
        }


        public int[] Findcoordinatesindepthspace(int[] c)
        {

            int[] ret = new int[2];

            unsafe
            {
                fixed (DepthSpacePoint* colorMappedToDepthPointsPointer = _colorMappedToDepthPoints)
                {
                    int colorKndex = (c[1] * 1920) + c[0];

                    float colorMappedToDepthX = colorMappedToDepthPointsPointer[colorKndex].X;
                    float colorMappedToDepthY = colorMappedToDepthPointsPointer[colorKndex].Y;

                    if (!float.IsNegativeInfinity(colorMappedToDepthX) &&
                        !float.IsNegativeInfinity(colorMappedToDepthY))
                    {
                        // Make sure the depth pixel maps to a valid point in color space
                        int depthX = (int) (colorMappedToDepthX + 0.5f);
                        int depthY = (int) (colorMappedToDepthY + 0.5f);

                        if ((depthX >= 0) && (depthX < _depthFrameDescription.Width) && (depthY >= 0) &&
                            (depthY < _depthFrameDescription.Height))
                        {
                            ret[0] = (int) colorMappedToDepthX;
                            ret[1] = (int) colorMappedToDepthY;
                        }
                    }
                }
            }
            Debug.WriteLine(ret[0]+","+ret[1]);
            globalret = ret;
            return ret;
        }


        private WriteableBitmap writableBitmapFromBitmap(Bitmap bmp)
        {
            // GDI HANDLE LEAK DO NOT AUTOMATE
            BitmapSource b = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height));
            WriteableBitmap wb = new WriteableBitmap(b);

            return wb;
        }


        ////////////////////////////////////////////////    KINECT METHODS   /////////////////////////////////////////////////

        private void RenderDepthPixels()
        {
            // color target pixel white
            _depthPixels[(globalret[1] * 512) + globalret[0]] = byte.MaxValue;

            _depthBitmap.WritePixels(
                new Int32Rect(0, 0, _depthBitmap.PixelWidth, _depthBitmap.PixelHeight),
                _depthPixels,
                _depthBitmap.PixelWidth,
                0);
        }


        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            ushort* frameData = (ushort*)depthFrameData;

            for (int i = 0; i < (int)(depthFrameDataSize / _depthFrameDescription.BytesPerPixel); ++i)
            {
                ushort depth = frameData[i];
                _depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
            //_imageModel.createGrayImage(_depthPixels, _depthFrameDescription.Width, _depthFrameDescription.Height);
            DepthSet = true;
        }

        ////////////////////////////////////////////////    GUI METHODS   /////////////////////////////////////////////////

        private void drawCoordsOnColorBitmap(Point coordPoint, int sizeD)
        {
            // inside bounds check

            if (coordPoint.X <= sizeD / 2 || coordPoint.X >= _colorFrameDescription.Width - sizeD / 2 ||
                coordPoint.Y <= sizeD / 2 || coordPoint.Y >= _colorFrameDescription.Height - sizeD / 2)
                return;

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

                if (!_recordEnabled)
                {
                    bitmapGraphics.DrawIcon(new Icon("yellowmarker.ico", new System.Drawing.Size(sizeD, sizeD)),
                           new Rectangle(new Point(coordPoint.X - sizeD / 2, coordPoint.Y - sizeD / 2), new System.Drawing.Size(sizeD, sizeD)));

                    bitmapGraphics.FillRectangle(new SolidBrush(System.Drawing.Color.FromArgb(150, 255, 255, 0)), new RectangleF(new Point(coordPoint.X + (sizeD / 2) + 10, coordPoint.Y - sizeD / 2 - 32), new System.Drawing.Size(130, 28)));
                    bitmapGraphics.DrawString("colorspace", new Font(new System.Drawing.FontFamily("Arial"), 16), Brushes.Black, new RectangleF(new Point(coordPoint.X + (sizeD / 2) + 10, coordPoint.Y - sizeD / 2 - 35), new System.Drawing.Size(200, 30)));
                }
                else
                {
                    bitmapGraphics.DrawIcon(new Icon("redmarker.ico", new System.Drawing.Size(sizeD, sizeD)),
                           new Rectangle(new Point(coordPoint.X - sizeD / 2, coordPoint.Y - sizeD / 2), new System.Drawing.Size(sizeD, sizeD)));

                    bitmapGraphics.FillRectangle(new SolidBrush(System.Drawing.Color.FromArgb(150, 255, 0, 0)), new RectangleF(new Point(coordPoint.X + (sizeD / 2) + 10, coordPoint.Y - sizeD / 2 - 32), new System.Drawing.Size(130, 28)));
                    bitmapGraphics.DrawString("colorspace", new Font(new System.Drawing.FontFamily("Arial"), 16), Brushes.Black, new RectangleF(new Point(coordPoint.X + (sizeD / 2) + 10, coordPoint.Y - sizeD / 2 - 35), new System.Drawing.Size(200, 30)));
                }
                
                bitmapGraphics.FillRectangle(new SolidBrush(System.Drawing.Color.FromArgb(150, 0, 0, 0)), new RectangleF(new Point(10, 10), new System.Drawing.Size(200, 130)));
                bitmapGraphics.DrawString("X:  " + _coordinates.getCamCoordsFloat()[0].ToString("#0.000"), new Font(new System.Drawing.FontFamily("Arial"), 18), Brushes.LawnGreen, new RectangleF(new Point(10, 10), new System.Drawing.Size(200, 40)));
                bitmapGraphics.DrawString("Y:  " + _coordinates.getCamCoordsFloat()[1].ToString("#0.000"), new Font(new System.Drawing.FontFamily("Arial"), 18), Brushes.LawnGreen, new RectangleF(new Point(10, 50), new System.Drawing.Size(200, 40)));
                bitmapGraphics.DrawString("Z:  " + _coordinates.getCamCoordsFloat()[2].ToString("#0.000"), new Font(new System.Drawing.FontFamily("Arial"), 18), Brushes.LawnGreen, new RectangleF(new Point(10, 90), new System.Drawing.Size(200, 40)));
            }
            tempBitmap.Dispose();
        }

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
            // shut down kinectsensor
            if (_kinectSensor != null)
            {
                _kinectSensor.Close();
                _kinectSensor = null;
            }
            
            // save loggs if recording
            if (_recordEnabled)
                _logger.dumpToFile();

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
            _imageModel.getColorAtPixel((int)p.X, (int)p.Y, true);
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
    }
}


