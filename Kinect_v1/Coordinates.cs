

namespace Kinect_v1
{
    class Coordinates
    {
        private int[] cameraCoordinateInts;
        private float[] cameraCoordinateFloat;
        private int[] colorCoordinateInts;

        public Coordinates(int[] colorCoords2D)
        {
            colorCoordinateInts = colorCoords2D;
        }

        public void setCameraCoords(float[] camCoords3D )
        {
            cameraCoordinateFloat = camCoords3D;
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
