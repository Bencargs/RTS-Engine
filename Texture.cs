using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace RTSEngine
{
    class Texture
    {
        private Bitmap image;

        public Texture()
        {
        }

        public Bitmap loadImage(string mapName, int type)
        {          
            try
            {
                switch (type)
                {
                    case 0:
                        heightmap(mapName);
                        break;
                    case 1:
                        colourImage();
                        break;
                    case 2:
                        alphaImage();
                        break;
                }
            }
            catch
            {
                System.Console.WriteLine("Could not load map, " + mapName);
                throw new Exception("Could not load map, " + mapName);
            }
            return image;
        }

        void heightmap(string mapName)
        {
            FileStream heightmapStream = new FileStream(@".\Maps\\" + mapName + "\\heightmap.bmp", FileMode.Open);
            FileStream mapStream = new FileStream(@".\Maps\\" + mapName + "\\map.bmp", FileMode.Open);
            Bitmap colourImage = new Bitmap(mapStream);
            Bitmap heightmapImage = new Bitmap(heightmapStream);
            image = new Bitmap(heightmapImage.Width, heightmapImage.Height, PixelFormat.Format32bppArgb);
            heightmapStream.Close();
            mapStream.Close();

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    //assume heightmap is greyscale, so only take red value (should all be the same)
                    //map image is combination of heightmap file in alpha channel and colour image
                    image.SetPixel(x, y, Color.FromArgb(heightmapImage.GetPixel(x, y).R, colourImage.GetPixel(x, y)));
                }
            }
            heightmapImage.Dispose();
            colourImage.Dispose();
        }

        void colourImage()
        {
        }

        void alphaImage()
        {
        }
    }
}