using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
//using OpenGL;
using Tao.OpenGl;
using Tao.Platform.Windows;
using System.Reflection;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace RTSEngine
{
	/// <summary>
	/// Example implementation of the BaseGLControl
	/// </summary>
	public class EngineOld : Form// : BaseGLControl
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>

        private bool finished;
        private Form form;
        private PrivateFontCollection fontsCollection;
        private static IntPtr DC;
        private static IntPtr RC;
        private int heightScale;
        private uint[] textures;
        private int drawMode;
        private byte[,] heightmap;
        private Camera camera;
        private FPS fps;
        private bool init = false;

        private struct Camera
        {
            public float ratio;
            public int minHeight;//always keep this distance from terrain
            public int zoom, zoomOutAt;
            public int cameraX, cameraY, cameraZ;
            public uint lookatX, lookatY, lookatZ;
        }

        private struct FPS
        {
            public int frame;//frames since last check
            public DateTime time;//elapsed time in ms
            public double elapsed;//ms since last check
        }

        private struct Vertex
        {
            public float x;
            public float y;
            public float z;
        }

        private struct TextureCoord
        {
            public float u;
            public float v;
        }

        private struct HeightMap
        {
            public Vertex[] vertices;
            public TextureCoord[] texCoord;
        }

        public static void Nain()
        {
            EngineOld engine = new EngineOld();
            engine.mainLoop();
            Application.Run(engine);
        }

        //Constructor
		public EngineOld()
		{//Initialisation
            //this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            //this.ClientSize = new System.Drawing.Size(640, 480);
            //this.Name = "MainForm";
            //this.Text = "RTS Engine";
            //this.Dock = DockStyle.Fill;

            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);            // No Need To Erase Form Background
            this.SetStyle(ControlStyles.DoubleBuffer, true);                    // Buffer Control
            this.SetStyle(ControlStyles.Opaque, true);                          // No Need To Draw Form Background
            //this.SetStyle(ControlStyles.ResizeRedraw, true);                    // Redraw On Resize
            this.SetStyle(ControlStyles.UserPaint, true);                       // We'll Handle Painting Ourselves

            this.Closing += new CancelEventHandler(this.closeForm);          // On Closing Event Call Form_Closing
			this.KeyPress += new KeyPressEventHandler(keyPress);
            //this.MouseClick += new MouseEventHandler(mouseFunc);
            //this.MouseDown += new MouseEventHandler(mouseFunc);
            //this.MouseMove += new MouseEventHandler(mouseFunc);
            this.MouseWheel += new MouseEventHandler(mouseFunc);

            finished = false;
            textures = new uint[1];//size to imageResources size
            fontsCollection = new PrivateFontCollection();
            drawMode = 0;
            heightScale = 5;

            camera.ratio = this.Width / this.Height;
            //use some initialisation camera function instead
            camera.zoom = 65; camera.zoomOutAt = 150;//when to jump out and follow terrain
            camera.minHeight = 30;
            camera.cameraX = 25;//twist
            camera.cameraY = 10;//-32;//zoom out
            camera.cameraZ = camera.minHeight;
            camera.lookatX = 25;//-22;//twist
            camera.lookatY = 25;//angle (up/down)
            camera.lookatZ = 0;

            //GL.glMatrixMode(GL.GL_PROJECTION);//switch to camera setting perspective
            //GL.glLoadIdentity();//Reset Camera
		}

        ~EngineOld()
        {//never called but good to have
            base.Dispose();
            textures = null;
            heightmap = null;
            finished = true;
            GC.Collect();
        }

        private void closeForm(object sender, CancelEventArgs e)
        {
            finished = true;                                                        // Send A Quit Message
        }

        //Entry Point
        [STAThread]
        public void mainLoop()
        {
            createGLWindow("RTS Engine");

            while (!finished)
            {
                Application.DoEvents();
                DrawScene();
                Gdi.SwapBuffers(DC);
            }
            killGLWindow();
            return;
            //Dispose();
        }

        void createGLWindow(string title)
        {
            int height = 400;
            int width = 600;
            int bpp = 16;
            int pixelFormat;
            form = null;
            GC.Collect();

            Gdi.DEVMODE dmScreenSettings = new Gdi.DEVMODE();
            dmScreenSettings.dmSize = (short)Marshal.SizeOf(dmScreenSettings);
            dmScreenSettings.dmPelsWidth = width;
            dmScreenSettings.dmPelsHeight = height;
            dmScreenSettings.dmBitsPerPel = bpp;
            dmScreenSettings.dmFields = Gdi.DM_BITSPERPEL | Gdi.DM_PELSWIDTH | Gdi.DM_PELSHEIGHT;
            
            form = new EngineOld(); ;
            //form.FormBorderStyle = FormBorderStyle.None;
            form.Width = width;                                                 // Set Window Width
            form.Height = height;                                               // Set Window Height
            form.Text = title;

            Gdi.PIXELFORMATDESCRIPTOR pfd = new Gdi.PIXELFORMATDESCRIPTOR();    // pfd Tells Windows How We Want Things To Be
            pfd.nSize = (short)Marshal.SizeOf(pfd);                            // Size Of This Pixel Format Descriptor
            pfd.nVersion = 1;                                                   // Version Number
            pfd.dwFlags = Gdi.PFD_DRAW_TO_WINDOW |                              // Format Must Support Window
                Gdi.PFD_SUPPORT_OPENGL |                                        // Format Must Support OpenGL
                Gdi.PFD_DOUBLEBUFFER;                                           // Format Must Support Double Buffering
            pfd.iPixelType = (byte)Gdi.PFD_TYPE_RGBA;                          // Request An RGBA Format
            pfd.cColorBits = (byte)bpp;                                       // Select Our Color Depth
            pfd.cRedBits = 0;                                                   // Color Bits Ignored
            pfd.cRedShift = 0;
            pfd.cGreenBits = 0;
            pfd.cGreenShift = 0;
            pfd.cBlueBits = 0;
            pfd.cBlueShift = 0;
            pfd.cAlphaBits = 0;                                                 // No Alpha Buffer
            pfd.cAlphaShift = 0;                                                // Shift Bit Ignored
            pfd.cAccumBits = 0;                                                 // No Accumulation Buffer
            pfd.cAccumRedBits = 0;                                              // Accumulation Bits Ignored
            pfd.cAccumGreenBits = 0;
            pfd.cAccumBlueBits = 0;
            pfd.cAccumAlphaBits = 0;
            pfd.cDepthBits = 16;                                                // 16Bit Z-Buffer (Depth Buffer)
            pfd.cStencilBits = 0;                                               // No Stencil Buffer
            pfd.cAuxBuffers = 0;                                                // No Auxiliary Buffer
            pfd.iLayerType = (byte)Gdi.PFD_MAIN_PLANE;                         // Main Drawing Layer
            pfd.bReserved = 0;                                                  // Reserved
            pfd.dwLayerMask = 0;                                                // Layer Masks Ignored
            pfd.dwVisibleMask = 0;
            pfd.dwDamageMask = 0;

            DC = User.GetDC(form.Handle);                                      // Attempt To Get A Device Context
            if (DC == IntPtr.Zero)
            {                                            // Did We Get A Device Context?
                killGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Create A GL Device Context.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                //**errorHandler
            }

            // Attempt To Find An Appropriate Pixel Format
            pixelFormat = Gdi.ChoosePixelFormat(DC, ref pfd);
            if (pixelFormat == 0)
            {                                              // Did Windows Find A Matching Pixel Format?
                killGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Find A Suitable PixelFormat.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (!Gdi.SetPixelFormat(DC, pixelFormat, ref pfd))
            {                // Are We Able To Set The Pixel Format?
                killGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Set The PixelFormat.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            RC = Wgl.wglCreateContext(DC);                                    // Attempt To Get The Rendering Context
            if (RC == IntPtr.Zero)
            {                                            // Are We Able To Get A Rendering Context?
                killGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Create A GL Rendering Context.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (!Wgl.wglMakeCurrent(DC, RC))
            {                                 // Try To Activate The Rendering Context
                killGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Activate The GL Rendering Context.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            form.Show();                                                        // Show The Window
            //form.TopMost = true;                                                // Topmost Window
            form.Focus();                                                       // Focus The Window

            InitLayout();
        }

        void killGLWindow()
        {
            if (RC != IntPtr.Zero)
            {                                            // Do We Have A Rendering Context?
                if (!Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero))
                {             // Are We Able To Release The DC and RC Contexts?
                    MessageBox.Show("Release Of DC And RC Failed.", "SHUTDOWN ERROR",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                if (!Wgl.wglDeleteContext(RC))
                {                                // Are We Able To Delete The RC?
                    MessageBox.Show("Release Rendering Context Failed.", "SHUTDOWN ERROR",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                RC = IntPtr.Zero;                                              // Set RC To Null

            }

            if (DC != IntPtr.Zero)
            {                                            // Do We Have A Device Context?
                if (form != null && !form.IsDisposed)
                {                          // Do We Have A Window?
                    if (form.Handle != IntPtr.Zero)
                    {                            // Do We Have A Window Handle?
                        if (!User.ReleaseDC(form.Handle, DC))
                        {                 // Are We Able To Release The DC?
                            MessageBox.Show("Release Device Context Failed.", "SHUTDOWN ERROR",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }

                DC = IntPtr.Zero;                                              // Set DC To Null
            }

            if (form != null)
            {                                                  // Do We Have A Windows Form?
                form.Hide();                                                    // Hide The Window
                form.Close();                                                   // Close The Form
                form = null;                                                    // Set form To Null
            }
        }

        public void errorHandler(string className, string functionName, string resource)
        {
            MessageBox.Show("Exception in " + className + " Class." + Environment.NewLine +
                "Inside " + functionName + "() Function." + Environment.NewLine +
                "Could not load " + resource + " Resources.", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            finished = true;
        }

        //Initialise GL window
        //protected override void  InitLayout()
        private void InitLayout()
        {
            Gl.glShadeModel(Gl.GL_SMOOTH);								// enable smooth shading
            Gl.glClearColor(0.0f, 0.0f, 0.0f, 0.5f);					// black background
            Gl.glClearDepth(1.0f);										// depth buffer setup
            Gl.glEnable(Gl.GL_DEPTH_TEST);								// enables depth testing
            Gl.glDepthFunc(Gl.GL_LEQUAL);								// type of depth testing
            //GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE);						// Select The Type Of Blending (transperancy)
            Gl.glEnable(Gl.GL_BLEND);
            Gl.glEnable(Gl.GL_POLYGON_SMOOTH);
            //GL.glEnable(GL.GL_CULL_FACE);
            Gl.glCullFace(Gl.GL_BACK);
            Gl.glEnable(Gl.GL_TEXTURE_2D);


            //Tao.OpenGl.Gl.glGenBuffersARB
            //GL.glGenBuffersARB(1, _terview._vertex_buffer);
            //GL.glGenLists(2);


            loadTextures();
            loadHeightmap();
            loadFont();
        }

        private void loadTextures()
        {
            int imageResources = 2;//count how many images in maps folder
            Bitmap[] image = new Bitmap[imageResources];

            try
            {
                image[0] = new Bitmap(@".\Maps\Forrest01\Map.bmp");
                Gl.glGenTextures(imageResources, textures);

                for (int i = 0; i < imageResources; i++)
                {
                    if (image[i] != null)
                    {
                        image[i].RotateFlip(RotateFlipType.RotateNoneFlipY);
                        System.Drawing.Imaging.BitmapData bitmapdata;
                        Rectangle rect = new Rectangle(0, 0, image[i].Width, image[i].Height);
                        bitmapdata = image[i].LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                        Gl.glBindTexture(Gl.GL_TEXTURE_2D, textures[i]);
                        Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MIN_FILTER, (int)Gl.GL_LINEAR);		// Linear Filtering
                        Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MAG_FILTER, (int)Gl.GL_LINEAR);//why is this repeated?
                        Gl.glTexParameterf(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_S, Gl.GL_REPEAT);
                        Gl.glTexParameterf(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_T, Gl.GL_REPEAT);
                        //repeated to repeat texture instead of clamping texture to edge of shape
                        Gl.glTexImage2D(Gl.GL_TEXTURE_2D, 0, (int)Gl.GL_RGB8, image[i].Width, image[i].Height,
                            0, Gl.GL_BGR_EXT, Gl.GL_UNSIGNED_BYTE, bitmapdata.Scan0);// Linear Filtering
                        image[i].UnlockBits(bitmapdata);
                        image[i].Dispose();
                    }
                }
            }
            catch
            {
                errorHandler(this.ToString(), MethodBase.GetCurrentMethod().Name, "Image");
            }
        }

        private void loadHeightmap()
        {
            try
            {
                Bitmap rawImage = new Bitmap(@".\Maps\Forrest01\Heightmap.bmp");
                heightmap = new byte[rawImage.Width, rawImage.Height];
                byte heightVal;

                for (int y = 0; y < rawImage.Height; y++)
                {
                    for (int x = 0; x < rawImage.Width; x++)
                    {
                        heightVal = (byte)((rawImage.GetPixel(x, y).R + rawImage.GetPixel(x, y).G + rawImage.GetPixel(x, y).B) / 3);
                        heightmap[x, y] = heightVal;
                    }
                }
                rawImage.Dispose();
            }
            catch
            {
                errorHandler(this.Name, MethodBase.GetCurrentMethod().Name, "Image");
            }
        }

        private void loadFont()
        {
            try
            {
                fontsCollection.AddFontFile(@".\Fonts\28 Days Later.ttf");
                //gameFont = new Font(fontsCollection.Families[0], 24);
                //as with textures, use a directory search, for mods
                //Font gameFont = new Font(fontsCollection.Families[0], 12);
            }
            catch
            {
                errorHandler(this.Name, MethodBase.GetCurrentMethod().Name, "Font");
            }
        }

        private void setCamera()
        {
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glLoadIdentity();//Reset Camera again
            Glu.gluLookAt(camera.cameraX, camera.cameraY, camera.cameraZ + camera.zoom / heightScale,
            camera.lookatX, camera.lookatY, camera.lookatZ,
            0.0f, 1.0f, 0.0f);
        }

        private void segmentMap(int middleSegment)
        {//keep a state of prev to currnt, dont load as many segmns (as most movemnt sequntl)
            //divides map into 16x16 segments, so that a chunck of segments are loaded at once
            //rather than loading pixel by pixel of new area
            //or having entire map in memory

        }

        private void heightMap()
        {
            for (int y = 0; y < heightmap.GetLength(1) - 1; y++)
            {
                for (int x = 0; x < heightmap.GetLength(0) - 1; x++)
                {
                    if (drawMode == 0)
                        Gl.glBegin(Gl.GL_TRIANGLE_STRIP);
                    else
                        Gl.glBegin(Gl.GL_LINES);
                    Gl.glVertex3f(x, y, heightmap[x, y]); Gl.glTexCoord2f(0.0f, 0.0f);
                    Gl.glVertex3f(x, y + 1, heightmap[x, y + 1]); Gl.glTexCoord2f(0.0f, 1.0f);
                    Gl.glVertex3f(x + 1, y, heightmap[x + 1, y]); Gl.glTexCoord2f(1.0f, 0.0f);
                    Gl.glVertex3f(x + 1, y + 1, heightmap[x + 1, y + 1]); Gl.glTexCoord2f(1.0f, 1.0f);
                    Gl.glEnd();
                }
            }
        }

        private void heightMapS()
        {
            int x, y, drawToX, drawToY;
            //cant bind textures inbetween glStart, glEnd
            //GL.glColor3f(0.3f, 1.0f, 0.3f);//!!! remove when better texture
            if (drawMode == 0)
                Gl.glBegin(Gl.GL_TRIANGLES);//~32fps
            else
                //GL.glBegin(GL.GL_TRIANGLE_FAN);//~4fps
                //GL.glBegin(GL.GL_TRIANGLE_STRIP);//31.5fps
                Gl.glBegin(Gl.GL_LINES);
            //GL.glDrawElements
            //GL.glDrawArrays



            //**instead of drawing heightmap one triangle at a time
            //load entire heightmap into an array
            //** for dynamic terrain use vertex buffer object (??)

            y = camera.cameraY - 30;
            drawToY = camera.cameraY + 60;
            while (y < drawToY)
            {
                if (y < 0)
                    y = 0;
                else if (drawToY > heightmap.GetLength(1) - 1)
                    drawToY = heightmap.GetLength(1) - 1;
                x = camera.cameraX - 30;
                drawToX = camera.cameraX + 30;
                while (x < drawToX)
                {
                    if (x < 0)
                        x = 0;
                    else if (drawToX > heightmap.GetLength(0) - 1)
                        drawToX = heightmap.GetLength(0) - 1;

                    Gl.glNormal3f(0.0f, 1.0f, 0.0f);
                    Gl.glVertex3i(x, y, heightmap[x, y]); Gl.glTexCoord2f(0.0f, 0.0f);
                    Gl.glVertex3i(x + 1, y, heightmap[x + 1, y]); Gl.glTexCoord2f(1.0f, 0.0f);
                    Gl.glVertex3i(x, y + 1, heightmap[x, y + 1]); Gl.glTexCoord2f(0.0f, 1.0f);

                    Gl.glVertex3i(x + 1, y, heightmap[x + 1, y]); Gl.glTexCoord2f(1.0f, 0.0f);
                    Gl.glVertex3i(x + 1, y + 1, heightmap[x + 1, y + 1]); Gl.glTexCoord2f(1.0f, 1.0f);
                    Gl.glVertex3i(x, y + 1, heightmap[x, y + 1]); Gl.glTexCoord2f(0.0f, 1.0f);
                    x++;
                }
                y++;
            }
            Gl.glEnd();
        }

        private void calcFps()
        {
            fps.frame++;
            fps.elapsed = (DateTime.Now - fps.time).TotalMilliseconds;
            if (fps.elapsed > 1000)
            {//if more than 1 second
                Console.WriteLine(fps.frame / (fps.elapsed / 1000));
                fps.time = DateTime.Now;
                fps.frame = 0;
            }
        }

		/// <summary>
		/// Override OnPaint to draw our gl scene
		/// </summary>
        //protected override void  OnPaint(PaintEventArgs e)
        private void DrawScene()
		{//Draw scene
            //if((DC == 0 || RC == 0) || (DC == RC))
            //    return;//dont know what this does
            //Wgl.wglMakeCurrent(DC,RC);
            if (init == false)
            {//some reason first initialisation doesnt stick
                init = true;
                InitLayout();
            }


            //Just clear the screen


            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);
            setCamera();

            Gl.glPushMatrix();
            Gl.glScalef(1.0f, 1.0f, 1.0f / heightScale);

            heightMap();
            calcFps();
 
            Gl.glPopMatrix();
            Wgl.wglSwapBuffers(DC);
			Gl.glFlush ();													// Flush The GL Rendering Pipeline
		}

        private void mouseFunc(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {//push forward on wheel
                moveCamera(4);
            }
            else if (e.Delta < 0)
            {
                moveCamera(5);
            }
        }

        private void moveCamera(byte camType)
        {
            int curHeight = heightmap[camera.cameraX, camera.cameraY];
            switch (camType)
            {
                case 0://X scroll Left
                    if (camera.cameraX > 15)
                    {
                        camera.cameraX--;
                        camera.lookatX--;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = curHeight / heightScale;
                        }
                    }
                    break;
                case 1://X scroll Right
                    if (camera.cameraX + 15 < heightmap.GetLength(0)-1)
                    {
                        camera.cameraX++;
                        camera.lookatX++;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = curHeight / heightScale;
                        }
                    }
                    break;
                case 2://Y scroll Down
                    if (camera.cameraY > 0)
                    {
                        camera.cameraY--;
                        camera.lookatY--;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = curHeight / heightScale;
                        }
                    }
                    break;
                case 3://Y scroll Up
                    if (camera.cameraY + 15 < heightmap.GetLength(1)-1)
                    {
                        camera.cameraY++;
                        camera.lookatY++;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = curHeight / heightScale;
                        }
                    }
                    break;
                case 4://Zoom in
                    if (camera.zoom > 15)
                    {
                        camera.zoom -= 10;
                        camera.lookatY += 4;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                    }
                    break;
                case 5://Zoom out
                    if (camera.zoom < 75)
                    {
                        camera.zoom += 10;
                        camera.lookatY -= 4;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                    }
                    break;
            }
        }

		/// <summary>
		/// Handle keys, specifically escape
		/// </summary>
		private void keyPress(object sender, KeyPressEventArgs e)
		{
            char c = char.ToUpper( e.KeyChar);
            switch (c)
            {
                case (char)Keys.Escape:
                    finished = true;
                    break;
                case (char)Keys.A:
                    moveCamera(0);
                    break;
                case (char)Keys.D:
                    moveCamera(1);
                    break;
                case (char)Keys.S:
                    moveCamera(2);
                    break;
                case (char)Keys.W:
                    moveCamera(3);
                    break;
                case (char)Keys.I:
                    drawMode = 0;
                    break;
                case (char)Keys.K:
                    drawMode = 1;
                    break;
            }
		}
	}
}
