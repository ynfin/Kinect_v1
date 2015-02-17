
using Point = System.Drawing.Point;

namespace Kinect_v1
{
    class Coordinates
    {
        private int[] cameraCoordinateInts;
        private float[] cameraCoordinateFloat;
        private int[] colorCoordinateInts;

        public Coordinates()
        {
            colorCoordinateInts = new int[] { -1, -1 ,-1 };
            cameraCoordinateFloat = new float[] { -1, -1 };
            cameraCoordinateInts = new int[] { -1, -1, -1 };
        }

        public Coordinates(int[] colorCoords2D)
        {
            colorCoordinateInts = colorCoords2D;
        }

        public void setCameraCoords(float[] camCoords3D )
        {
            cameraCoordinateFloat = camCoords3D;
        }


        public Point getColorCoordinatesPoint()
        {
            return new Point(colorCoordinateInts[0], colorCoordinateInts[1]);
        }

        public int[] getCamCoords()
        {
            cameraCoordinateInts[0] = (int)(cameraCoordinateFloat[0] * 1000);
            cameraCoordinateInts[1] = (int)(cameraCoordinateFloat[1] * 1000);
            cameraCoordinateInts[2] = (int)(cameraCoordinateFloat[2] * 1000);

            return cameraCoordinateInts;
        }
        
        public float[] getCamCoordsFloat()
        {
            return cameraCoordinateFloat;
        }

        public int[] GetColorCoordinatesInts()
        {
            return colorCoordinateInts;
        }
    }
}
