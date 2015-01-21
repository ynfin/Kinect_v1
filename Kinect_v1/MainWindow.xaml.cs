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
using Microsoft.Kinect;
using System.Drawing;
using System.IO;
using System.Threading;

namespace Kinect_v1
{
    public partial class MainWindow : Window
    {
        private const int MapDepthToByte = 8000 / 256;
        private const float InfraredSourceValueMaximum = (float)ushort.MaxValue;
        private const float InfraredSourceScale = 0.75f;
        private const float InfraredOutputValueMinimum = 0.01f;
        private const float InfraredOutputValueMaximum = 1.0f;

        Model.ImageModel KinectImageModel_1 = new Model.ImageModel();

        private KinectSensor kinectSensor = null;

        private DepthFrameReader depthFrameReader = null;
        private ColorFrameReader colorFrameReader = null;
        private InfraredFrameReader infraredFrameReader = null;

        private FrameDescription depthFrameDescription = null;
        private FrameDescription colorFrameDescription = null;
        private FrameDescription infraredFrameDescription = null;

        private WriteableBitmap depthBitmap = null;
        private WriteableBitmap colorBitmap = null;
        private WriteableBitmap infraredBitmap = null;
        private WriteableBitmap binaryWBitmap = null;

        private WriteableBitmap binaryBitmap2 = null;

        private Bitmap binaryBitmap = null;
        private ImageSource binaryImageSource = null;

        // threads
        Thread thread;
        Action action;

        private int framesounter = 0;

        enum DisplaySource{ColorStream,DepthStream,InfraredStream,BinaryStream};
        DisplaySource source = DisplaySource.ColorStream;

        private byte[] depthPixels = null;

        public MainWindow()
        {
            this.kinectSensor = KinectSensor.GetDefault();

            // streams
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();
            this.infraredFrameReader = this.kinectSensor.InfraredFrameSource.OpenReader();

            // handlers
            this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
            this.infraredFrameReader.FrameArrived += this.Reader_InfraredFrameArrived;

            //infrared
            this.infraredFrameDescription = this.kinectSensor.InfraredFrameSource.FrameDescription;
            this.infraredBitmap = new WriteableBitmap(this.infraredFrameDescription.Width, this.infraredFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray32Float, null);

            //color
            this.colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // depth
            this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
            this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
            
            // processed
            this.binaryWBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
            this.binaryBitmap2 = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

            
            
            // general
            this.kinectSensor.Open();
            this.DataContext = this;
            this.InitializeComponent();

           
           
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
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            ushort maxDepth = ushort.MaxValue;
                            //maxDepth = depthFrame.DepthMaxReliableDistance;

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
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
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this.colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                                this.colorBitmap.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                        }

                     
                        this.colorBitmap.Unlock();

                        binaryBitmap2 = colorBitmap.Clone();

                        framesounter++;

                        thread = new Thread(() => saveBitmap(this.binaryBitmap2, framesounter));
                        thread.Start();
                        thread.Priority = ThreadPriority.Highest;




                        if (framesounter > 100)
                        {
                            
                            //System.Diagnostics.Debug.WriteLine("Color Ready");
                            //KinectImageModel_1.recieveWritableColorBitmap(this.colorBitmap);
                            //KinectImageModel_1.triggerProcessing();
                            //binaryBitmap = KinectImageModel_1.getColorBitmap();   
                            
                            framesounter = 0;
                        }
                        
                    }
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
                        if (((this.infraredFrameDescription.Width * this.infraredFrameDescription.Height) == (infraredBuffer.Size / this.infraredFrameDescription.BytesPerPixel)) &&
                            (this.infraredFrameDescription.Width == this.infraredBitmap.PixelWidth) && (this.infraredFrameDescription.Height == this.infraredBitmap.PixelHeight))
                        {
                            this.ProcessInfraredFrameData(infraredBuffer.UnderlyingBuffer, infraredBuffer.Size);
                        }
                    }
                }
            }
        }

        private unsafe void ProcessInfraredFrameData(IntPtr infraredFrameData, uint infraredFrameDataSize)
        {
            // infrared frame data is a 16 bit value
            ushort* frameData = (ushort*)infraredFrameData;

            // lock the target bitmap
            this.infraredBitmap.Lock();

            // get the pointer to the bitmap's back buffer
            float* backBuffer = (float*)this.infraredBitmap.BackBuffer;

            // process the infrared data
            for (int i = 0; i < (int)(infraredFrameDataSize / this.infraredFrameDescription.BytesPerPixel); ++i)
            {
                // since we are displaying the image as a normalized grey scale image, we need to convert from
                // the ushort data (as provided by the InfraredFrame) to a value from [InfraredOutputValueMinimum, InfraredOutputValueMaximum]
                backBuffer[i] = Math.Min(InfraredOutputValueMaximum, (((float)frameData[i] / InfraredSourceValueMaximum * InfraredSourceScale) * (1.0f - InfraredOutputValueMinimum)) + InfraredOutputValueMinimum);
            }

            // mark the entire bitmap as needing to be drawn
            this.infraredBitmap.AddDirtyRect(new Int32Rect(0, 0, this.infraredBitmap.PixelWidth, this.infraredBitmap.PixelHeight));

            // unlock the bitmap
            this.infraredBitmap.Unlock();
        }

        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            ushort* frameData = (ushort*)depthFrameData;

            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                ushort depth = frameData[i];
                this.depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
        }

        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);
        }

        private static WriteableBitmap ImageSourceFromBitmap(Bitmap bmp)
        {
            System.Windows.Media.Imaging.BitmapSource b = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height));
            WriteableBitmap wb = new WriteableBitmap(b);
            return wb;
        }


        public ImageSource DepthSource
        {
            get
            {
                if (source == DisplaySource.InfraredStream)
                    return this.infraredBitmap;

                else if (source == DisplaySource.DepthStream)
                    return this.depthBitmap;

                else if (source == DisplaySource.BinaryStream)
                    return binaryImageSource;
                else
                    return this.colorBitmap;
            }       
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.depthFrameReader != null)
            {
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }

            if (this.colorFrameReader != null)
            {
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }

            if (this.infraredFrameReader != null)
            {
                this.infraredFrameReader.Dispose();
                this.infraredFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            this.source = DisplaySource.ColorStream;
            BindingOperations.GetBindingExpressionBase(displayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        private void IR_Click(object sender, RoutedEventArgs e)
        {
            this.source = DisplaySource.InfraredStream;
            BindingOperations.GetBindingExpressionBase(displayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        private void Depth_Click(object sender, RoutedEventArgs e)
        {
            this.source = DisplaySource.DepthStream;
            BindingOperations.GetBindingExpressionBase(displayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }

        private void Binary_Click(object sender, RoutedEventArgs e)
        {
            this.source = DisplaySource.BinaryStream;
            BindingOperations.GetBindingExpressionBase(displayScreen, System.Windows.Controls.Image.SourceProperty).UpdateTarget();
        }




        public void saveBitmap(WriteableBitmap bmp, int framecount)
        {
            string filename = @"C:\Users\Kinect\Pictures\pic" + framecount + ".png";

            if (filename != string.Empty)
            {
                using (FileStream stream = new FileStream(filename, FileMode.Create))
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmp));
                    encoder.Save(stream);
                    stream.Close();
                    System.Diagnostics.Debug.WriteLine("Thread ran!");
                }
            }
        }
    
        
    
    
    }
}
