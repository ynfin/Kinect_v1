using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Diagnostics;

using Microsoft.Kinect;
using Coding4Fun.Kinect;

namespace Kinect_v1
{
    public partial class MainWindow : Window
    {
        private const int MapDepthToByte = 8000 / 256;
        private const float InfraredSourceValueMaximum = (float)ushort.MaxValue;
        private const float InfraredSourceScale = 0.75f;
        private const float InfraredOutputValueMinimum = 0.01f;
        private const float InfraredOutputValueMaximum = 1.0f;

        Model.ImageModel _kinectImageModel1 = new Model.ImageModel();

        private KinectSensor _kinectSensor;

        private DepthFrameReader _depthFrameReader;
        private ColorFrameReader _colorFrameReader;
        private InfraredFrameReader _infraredFrameReader;

        private FrameDescription _depthFrameDescription = null;
        private FrameDescription _colorFrameDescription = null;
        private FrameDescription _infraredFrameDescription = null;

        private WriteableBitmap _depthBitmap = null;
        private WriteableBitmap _colorBitmap = null;
        private WriteableBitmap _infraredBitmap = null;
        private WriteableBitmap _binaryWBitmap = null;

        private Bitmap _binaryBitmap = null;
        private ImageSource _binaryImageSource = null;

        private Stopwatch _fpswatch = new Stopwatch();
        private Stopwatch _timestampWatch = new Stopwatch();
        private long _lastframestamp = 0;

        StreamWriter _logFile;

        enum DisplaySource { ColorStream, DepthStream, InfraredStream, BinaryStream };
        DisplaySource _source = DisplaySource.ColorStream;

        private byte[] _depthPixels = null;

        public MainWindow()
        {
            _kinectSensor = KinectSensor.GetDefault();

            // streams
            _depthFrameReader = this._kinectSensor.DepthFrameSource.OpenReader();
            _colorFrameReader = this._kinectSensor.ColorFrameSource.OpenReader();
            _infraredFrameReader = this._kinectSensor.InfraredFrameSource.OpenReader();

            // handlers
            this._depthFrameReader.FrameArrived += this.Reader_FrameArrived;
            this._colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
            this._infraredFrameReader.FrameArrived += this.Reader_InfraredFrameArrived;

            //infrared
            this._infraredFrameDescription = this._kinectSensor.InfraredFrameSource.FrameDescription;
            this._infraredBitmap = new WriteableBitmap(this._infraredFrameDescription.Width, this._infraredFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray32Float, null);

            //color
            this._colorFrameDescription = this._kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            this._colorBitmap = new WriteableBitmap(_colorFrameDescription.Width, _colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // depth
            this._depthFrameDescription = this._kinectSensor.DepthFrameSource.FrameDescription;
            this._depthPixels = new byte[this._depthFrameDescription.Width * this._depthFrameDescription.Height];
            this._depthBitmap = new WriteableBitmap(this._depthFrameDescription.Width, this._depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

            // processed
            this._binaryWBitmap = new WriteableBitmap(this._depthFrameDescription.Width, this._depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

            //diagnostics
            _timestampWatch.Start();
            _fpswatch.Start();

            // logger
            _logFile = new StreamWriter(@"C:\Users\Yngve\Documents\" + DateTime.Now.Second+ "_logFile2.txt", true);
            
            //threading
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;


            // general
            this._kinectSensor.Open();
            this.DataContext = this;
            this.InitializeComponent();
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // run all background tasks here
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //update ui once worker complete his work
        }



        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this._depthFrameDescription.Width * this._depthFrameDescription.Height) == (depthBuffer.Size / this._depthFrameDescription.BytesPerPixel)) &&
                            (this._depthFrameDescription.Width == this._depthBitmap.PixelWidth) && (this._depthFrameDescription.Height == this._depthBitmap.PixelHeight))
                        {
                            ushort maxDepth = ushort.MaxValue;
                            //maxDepth = depthFrame.DepthMaxReliableDistance;

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                    // Process the frame




                    //display stopwatch
                    if (_source == DisplaySource.DepthStream)
                    {
                        FpsDisplay.Text = (1000 / _fpswatch.ElapsedMilliseconds).ToString();
                        _fpswatch.Restart();
                    }
                }
            }

            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();
            }
        }


        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // ColorFrame is IDisposable
            //using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this._colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this._colorBitmap.PixelWidth) && (colorFrameDescription.Height == this._colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                                this._colorBitmap.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            this._colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this._colorBitmap.PixelWidth, this._colorBitmap.PixelHeight));
                        }
                        this._colorBitmap.Unlock();

                        // process colorframe
                        //KinectImageModel_1.setColorFrame(GetBitmapFromBitmapSource(colorBitmap));
                        //KinectImageModel_1.triggerProcessing();
                        //binaryBitmap = KinectImageModel_1.getBinaryBitmap();


                    }

                    //display stopwatch
                    if (_source == DisplaySource.ColorStream)
                    {
                        FpsDisplay.Text = (1000 / _fpswatch.ElapsedMilliseconds).ToString();
                        _fpswatch.Restart();
                    }
                    //logFrame('c');

                }
            }
        }

        private void Reader_InfraredFrameArrived(object sender, InfraredFrameArrivedEventArgs e)
        {
            // InfraredFrame is IDisposable
            using (InfraredFrame infraredFrame = e.FrameReference.AcquireFrame())
            {
                if (infraredFrame != null)
                {
                    // the fastest way to process the infrared frame data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer infraredBuffer = infraredFrame.LockImageBuffer())
                    {
                        // verify data and write the new infrared frame data to the display bitmap
                        if (((this._infraredFrameDescription.Width * this._infraredFrameDescription.Height) == (infraredBuffer.Size / this._infraredFrameDescription.BytesPerPixel)) &&
                            (this._infraredFrameDescription.Width == this._infraredBitmap.PixelWidth) && (this._infraredFrameDescription.Height == this._infraredBitmap.PixelHeight))
                        {
                            this.ProcessInfraredFrameData(infraredBuffer.UnderlyingBuffer, infraredBuffer.Size);
                        }
                    }

                    //display stopwatch
                    if (_source == DisplaySource.InfraredStream)
                    {
                        FpsDisplay.Text = (1000 / _fpswatch.ElapsedMilliseconds).ToString();
                        _fpswatch.Restart();
                    }
                }
            }
        }

        private unsafe void ProcessInfraredFrameData(IntPtr infraredFrameData, uint infraredFrameDataSize)
        {
            // infrared frame data is a 16 bit value
            ushort* frameData = (ushort*)infraredFrameData;

            // lock the target bitmap
            this._infraredBitmap.Lock();

            // get the pointer to the bitmap's back buffer
            float* backBuffer = (float*)this._infraredBitmap.BackBuffer;

            // process the infrared data
            for (int i = 0; i < (int)(infraredFrameDataSize / this._infraredFrameDescription.BytesPerPixel); ++i)
            {
                // since we are displaying the image as a normalized grey scale image, we need to convert from
                // the ushort data (as provided by the InfraredFrame) to a value from [InfraredOutputValueMinimum, InfraredOutputValueMaximum]
                backBuffer[i] = Math.Min(InfraredOutputValueMaximum, (((float)frameData[i] / InfraredSourceValueMaximum * InfraredSourceScale) * (1.0f - InfraredOutputValueMinimum)) + InfraredOutputValueMinimum);
            }

            // mark the entire bitmap as needing to be drawn
            this._infraredBitmap.AddDirtyRect(new Int32Rect(0, 0, this._infraredBitmap.PixelWidth, this._infraredBitmap.PixelHeight));

            // unlock the bitmap
            this._infraredBitmap.Unlock();
        }

        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            ushort* frameData = (ushort*)depthFrameData;

            for (int i = 0; i < (int)(depthFrameDataSize / this._depthFrameDescription.BytesPerPixel); ++i)
            {
                ushort depth = frameData[i];
                this._depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
        }

        private void RenderDepthPixels()
        {
            this._depthBitmap.WritePixels(
                new Int32Rect(0, 0, this._depthBitmap.PixelWidth, this._depthBitmap.PixelHeight),
                this._depthPixels,
                this._depthBitmap.PixelWidth,
                0);
        }

        /*
        private static WriteableBitmap ImageSourceFromBitmap(Bitmap bmp)
        {
            System.Windows.Media.Imaging.BitmapSource b = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height));
            WriteableBitmap wb = new WriteableBitmap(b);
            return wb;
        }
         */

        public ImageSource DepthSource
        {
            get
            {
                if (_source == DisplaySource.InfraredStream)
                    return this._infraredBitmap;

                else if (_source == DisplaySource.DepthStream)
                    return this._depthBitmap;

                else if (_source == DisplaySource.BinaryStream)
                    return _binaryImageSource;
                else
                    return this._colorBitmap;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this._depthFrameReader != null)
            {
                this._depthFrameReader.Dispose();
                this._depthFrameReader = null;
            }

            if (this._colorFrameReader != null)
            {
                this._colorFrameReader.Dispose();
                this._colorFrameReader = null;
            }

            if (this._infraredFrameReader != null)
            {
                this._infraredFrameReader.Dispose();
                this._infraredFrameReader = null;
            }

            if (this._kinectSensor != null)
            {
                this._kinectSensor.Close();
                this._kinectSensor = null;
            }

            //general closings
            _logFile.Close();

        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            this._source = DisplaySource.ColorStream;
            BindingOperations.GetBindingExpressionBase(DisplayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        private void IR_Click(object sender, RoutedEventArgs e)
        {
            this._source = DisplaySource.InfraredStream;
            BindingOperations.GetBindingExpressionBase(DisplayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        private void Depth_Click(object sender, RoutedEventArgs e)
        {
            this._source = DisplaySource.DepthStream;
            BindingOperations.GetBindingExpressionBase(DisplayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        private void Binary_Click(object sender, RoutedEventArgs e)
        {
            this._source = DisplaySource.BinaryStream;
            BindingOperations.GetBindingExpressionBase(DisplayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        public void LogFrame(char id)
        {
            long now = _timestampWatch.ElapsedMilliseconds;
            long delta = now - _lastframestamp;
            long fps = 1000/delta;
            _logFile.WriteLine(id + " - " + now.ToString() + " - " + _lastframestamp.ToString() + " - " + delta.ToString() + " - " + fps.ToString());
            _lastframestamp = now;
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
      

        //System.Diagnostics.Debug.WriteLine("Color Ready");
        //KinectImageModel_1.recieveWritableColorBitmap(this.colorBitmap);
        //KinectImageModel_1.triggerProcessing();
        //binaryBitmap = KinectImageModel_1.getColorBitmap();   



    }
}
