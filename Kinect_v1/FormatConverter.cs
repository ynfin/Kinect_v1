using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;


using Emgu.CV;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Kinect_v1
{
    public class FormatConverter
    {
        // FROM http://www.emgu.com/wiki/index.php/WPF_in_CSharp

        // ---------------------------------------------------------------------------------------------
        /// <summary>
        /// Delete a GDI object
        /// </summary>
        /// <param name="o">The poniter to the GDI object to be deleted</param>
        /// <returns></returns>
        [DllImport("gdi32")]
        public static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// Convert an IImage to a WPF BitmapSource. The result can be used in the Set Property of Image.Source
        /// </summary>
        /// <param name="image">The Emgu CV Image</param>
        /// <returns>The equivalent BitmapSource</returns>

        private static BitmapSource source;
        public static BitmapSource ToBitmapSourceFromBitmap(Bitmap bmpinput)
        {
            IntPtr hBitmap = bmpinput.GetHbitmap();
                try
                {
                    source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bmpinput.Width, bmpinput.Height));
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
                return source;
        }


        /*
        public BitmapSource ToBitmapSourceFAIL(IImage image)
        {
            using (Bitmap source = image.Bitmap)
            {
                IntPtr ptr = source.GetHbitmap(); //obtain the Hbitmap
 
                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
 
                DeleteObject(ptr); //release the HBitmap
                return bs;
            }
        }
         */
        //----------------------------------------------------------------------------------------------
    }
}
