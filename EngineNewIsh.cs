using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Tao.OpenGl;
using Tao.Platform.Windows;

namespace RTSEngine
{
    public class Engine : Form
    {
        // --- Fields ---
        #region Private Fields
        private IntPtr DC;                                              // Private GDI Device Context
        private IntPtr RC;                                              // Permanent Rendering Context
        private PrivateFontCollection fonts;
        private uint[] textures;
        private byte[,] heightmap;
        private bool[] keys;// = new bool[256];                             // Array Used For The Keyboard Routine
        private bool active;// = true;                                      // Window Active Flag, Set To True By Default
        private bool fullscreen;// = true;                                  // Fullscreen Flag, Set To Fullscreen Mode By Default
        private bool finished;// = false;                                       // Bool Variable To Exit Main Loop
        private uint drawType;

        private const int STEP_SIZE = 16;                                       // Width And Height Of Each Quad (NEW)
        private const int HEIGHT_RATIO = 5;                                // Ratio That The Y Is Scaled According To The X And Z (NEW)
        //private bool bRender = true;                                     // Polygon Flag Set To TRUE By Default (NEW)
        //private byte[] heightMap = new byte[MAP_SIZE * MAP_SIZE];        // Holds The Height Map Data (NEW)
        private Camera camera;
        private FPS fps;

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
        #endregion

        // --- Constructors & Destructors ---
        public Engine()
        {
            this.CreateParams.ClassStyle = this.CreateParams.ClassStyle |       // Redraw On Size, And Own DC For Window.
                User.CS_HREDRAW | User.CS_VREDRAW | User.CS_OWNDC;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);            // No Need To Erase Form Background
            this.SetStyle(ControlStyles.DoubleBuffer, true);                    // Buffer Control
            this.SetStyle(ControlStyles.Opaque, true);                          // No Need To Draw Form Background
            this.SetStyle(ControlStyles.ResizeRedraw, true);                    // Redraw On Resize
            this.SetStyle(ControlStyles.UserPaint, true);                       // We'll Handle Painting Ourselves

            this.Activated += new EventHandler(this.Form_Activated);            // On Activate Event Call Form_Activated
            this.Closing += new CancelEventHandler(this.Form_Closing);          // On Closing Event Call Form_Closing
            this.Deactivate += new EventHandler(this.Form_Deactivate);          // On Deactivate Event Call Form_Deactivate
            //this.KeyDown += new KeyEventHandler(this.Form_KeyDown);             // On KeyDown Event Call Form_KeyDown
            //this.KeyUp += new KeyEventHandler(this.Form_KeyUp);                 // On KeyUp Event Call Form_KeyUp
            this.KeyPress += new KeyPressEventHandler(keyPress);
            //this.MouseDown += new MouseEventHandler(this.Form_MouseDown);       // On MouseDown Even Call Form_MouseDown
            this.MouseWheel += new MouseEventHandler(mouseFunc);
            this.Resize += new EventHandler(this.Form_Resize);                  // On Resize Event Call Form_Resize

            fonts = new PrivateFontCollection();
            textures = new uint[1];//size to imageResources size
            keys = new bool[256];                             // Array Used For The Keyboard Routine
            active = true;                                      // Window Active Flag, Set To True By Default
            fullscreen = false;                                  // Fullscreen Flag, Set To Fullscreen Mode By Default
            finished = false;
        }

        public static void Main()
        {
            mainLoop();
        }

        // --- Entry Point ---
        public void mainLoop()
        {                                            // Windowed Mode
            createGLWindow("RTS Engine", 640, 480, 16, fullscreen);
            while (!finished)
            {                                                      // Loop That Runs While done = false
                Application.DoEvents();                                         // Process Events
                if (active)
                {
                    drawGLScene();
                }
                //drawGLScene();
                else
                {
                    Gdi.SwapBuffers(DC);
                }
                //if (keys[(int)Keys.F1])
                //{                                   // Is F1 Being Pressed?
                //    keys[(int)Keys.F1] = false;                            // If So Make Key false
                //    KillGLWindow();                                         // Kill Our Current Window
                //    fullscreen = !fullscreen;                               // Toggle Fullscreen / Windowed Mode
                //    // Recreate Our OpenGL Window
                //    if (!CreateGLWindow("NeHe & Ben Humphrey's Height Map Tutorial", 640, 480, 16, fullscreen))
                //    {
                //        return;                                             // Quit If Window Was Not Created
                //    }
                //    done = false;                                           // We're Not Done Yet
                //}
            }
            // Shutdown
            killGLWindow();                                                     // Kill The Window
        }

        // --- Private Static Methods ---
        private void createGLWindow(string title, int width, int height, int bits, bool fullscreenflag)
        {
            int pixelFormat;                                                    // Holds The Results After Searching For A Match
            fullscreen = fullscreenflag;                                        // Set The Global Fullscreen Flag

            // This Forces A Swap
            GC.Collect();                                                       // Request A Collection
            Kernel.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);

            if (fullscreen)
            {                                                    // Attempt Fullscreen Mode?
                Gdi.DEVMODE dmScreenSettings = new Gdi.DEVMODE();               // Device Mode
                // Size Of The Devmode Structure
                dmScreenSettings.dmSize = (short)Marshal.SizeOf(dmScreenSettings);
                dmScreenSettings.dmPelsWidth = width;                           // Selected Screen Width
                dmScreenSettings.dmPelsHeight = height;                         // Selected Screen Height
                dmScreenSettings.dmBitsPerPel = bits;                           // Selected Bits Per Pixel
                dmScreenSettings.dmFields = Gdi.DM_BITSPERPEL | Gdi.DM_PELSWIDTH | Gdi.DM_PELSHEIGHT;

                // Try To Set Selected Mode And Get Results.  NOTE: CDS_FULLSCREEN Gets Rid Of Start Bar.
                if (User.ChangeDisplaySettings(ref dmScreenSettings, User.CDS_FULLSCREEN) != User.DISP_CHANGE_SUCCESSFUL)
                {
                    // If The Mode Fails, Offer Two Options.  Quit Or Use Windowed Mode.
                    if (MessageBox.Show("The Requested Fullscreen Mode Is Not Supported By\nYour Video Card.  Use Windowed Mode Instead?", "NeHe GL",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                    {
                        fullscreen = false;                                     // Windowed Mode Selected.  Fullscreen = false
                    }
                    else
                    {
                        // Pop up A Message Box Lessing User Know The Program Is Closing.
                        MessageBox.Show("Program Will Now Close.", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Stop);                                         // Return false
                    }
                }
            }

            if (fullscreen)
            {                                                    // Are We Still In Fullscreen Mode?
                this.FormBorderStyle = FormBorderStyle.None;                    // No Border                                                 // Hide Mouse Pointer
            }
            else
            {                                                              // If Windowed
                this.FormBorderStyle = FormBorderStyle.Sizable;                 // Sizable                                                 // Show Mouse Pointer
            }

            this.Width = width;                                                 // Set Window Width
            this.Height = height;                                               // Set Window Height
            this.Text = title;                                                  // Set Window Title

            Gdi.PIXELFORMATDESCRIPTOR pfd = new Gdi.PIXELFORMATDESCRIPTOR();    // pfd Tells Windows How We Want Things To Be
            pfd.nSize = (short)Marshal.SizeOf(pfd);                            // Size Of This Pixel Format Descriptor
            pfd.nVersion = 1;                                                   // Version Number
            pfd.dwFlags = Gdi.PFD_DRAW_TO_WINDOW |                              // Format Must Support Window
                Gdi.PFD_SUPPORT_OPENGL |                                        // Format Must Support OpenGL
                Gdi.PFD_DOUBLEBUFFER;                                           // Format Must Support Double Buffering
            pfd.iPixelType = (byte)Gdi.PFD_TYPE_RGBA;                          // Request An RGBA Format
            pfd.cColorBits = (byte)bits;                                       // Select Our Color Depth
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

            DC = User.GetDC(this.Handle);                                      // Attempt To Get A Device Context
            if (DC == IntPtr.Zero)
            {                                            // Did We Get A Device Context?
                killGLWindow();                                                 // Reset The Display
                errorHandler(this.ToString(), MethodBase.GetCurrentMethod().Name, "OpenGL Device Context");
                MessageBox.Show("Can't Create A GL Device Context.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            pixelFormat = Gdi.ChoosePixelFormat(DC, ref pfd);                  // Attempt To Find An Appropriate Pixel Format
            if (pixelFormat == 0)
            {                                              // Did Windows Find A Matching Pixel Format?
                killGLWindow();                                                 // Reset The Display
                errorHandler(this.ToString(), MethodBase.GetCurrentMethod().Name, "Pixel Format");
                MessageBox.Show("Can't Find A Suitable PixelFormat.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (!Gdi.SetPixelFormat(DC, pixelFormat, ref pfd))
            {                // Are We Able To Set The Pixel Format?
                killGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Set The PixelFormat.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                errorHandler(this.ToString(), MethodBase.GetCurrentMethod().Name, "Pixel Format");
            }

            RC = Wgl.wglCreateContext(DC);                                    // Attempt To Get The Rendering Context
            if (RC == IntPtr.Zero)
            {                                            // Are We Able To Get A Rendering Context?
                killGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Create A GL Rendering Context.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                errorHandler(this.ToString(), MethodBase.GetCurrentMethod().Name, "GL Rendering Context");
            }

            if (!Wgl.wglMakeCurrent(DC, RC))
            {                                 // Try To Activate The Rendering Context
                killGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Activate The GL Rendering Context.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                errorHandler(this.ToString(), MethodBase.GetCurrentMethod().Name, "GL Rendering Context");
            }

            this.Show();                                                        // Show The Window
            //this.TopMost = true;                                                // Topmost Window
            this.Focus();                                                       // Focus The Window

            //resizeGLScene(width, height);                                       // Set Up Our Perspective GL Screen

            initGL();
        }

        private void initGL()
        {                                          // All Setup For OpenGL Goes Here
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

            //LoadRawFile("NeHe.Lesson34.Terrain.raw", MAP_SIZE * MAP_SIZE, ref heightMap);
            loadCamera();
            loadTextures();
            loadHeightmap();
            loadFont();
        }

        public void errorHandler(string className, string functionName, string resource)
        {
            MessageBox.Show("Exception in " + className + " Class." + Environment.NewLine +
                "Inside " + functionName + "() Function." + Environment.NewLine +
                "Could not load " + resource + " Resources.", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            finished = true;
        }

        private void loadCamera()
        {
            //load from file
            camera.ratio = this.Width / this.Height;
            camera.zoom = 65; camera.zoomOutAt = 150;//when to jump out and follow terrain
            camera.minHeight = 30;
            camera.cameraX = 25;//twist
            camera.cameraY = 10;//-32;//zoom out
            camera.cameraZ = camera.minHeight;
            camera.lookatX = 25;//-22;//twist
            camera.lookatY = 25;//angle (up/down)
            camera.lookatZ = 0;
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
                errorHandler(this.Name, MethodBase.GetCurrentMethod().Name, "HeightMap");
            }
        }

        private void loadFont()
        {
            try
            {
                fonts.AddFontFile(@".\Fonts\28 Days Later.ttf");
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
            Glu.gluLookAt(camera.cameraX, camera.cameraY, camera.cameraZ + camera.zoom / HEIGHT_RATIO,
            camera.lookatX, camera.lookatY, camera.lookatZ,
            0.0f, 1.0f, 0.0f);
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

        private void heightMap()
        {
            for (int y = 0; y < heightmap.GetLength(1) - 1; y++)
            {
                for (int x = 0; x < heightmap.GetLength(0) - 1; x++)
                {
                    if (drawType == 0)
                        Gl.glBegin(Gl.GL_TRIANGLES);
                        //Gl.glBegin(Gl.GL_TRIANGLE_STRIP);
                    else
                        Gl.glBegin(Gl.GL_LINES);
                    Gl.glVertex3f(x, heightmap[x, y], y); Gl.glTexCoord2f(0.0f, 0.0f);
                    Gl.glVertex3f(x, heightmap[x, y + 1], y+1); Gl.glTexCoord2f(0.0f, 1.0f);
                    Gl.glVertex3f(x + 1, heightmap[x + 1, y], y); Gl.glTexCoord2f(1.0f, 0.0f);
                    Gl.glVertex3f(x + 1, heightmap[x + 1, y + 1], y+1); Gl.glTexCoord2f(1.0f, 1.0f);
                    Gl.glEnd();
                }
            }
        }

        #region bool LoadRawFile(string name, int size, ref byte[] heightMap)
        /// <summary>
        ///     Read data from file.
        /// </summary>
        /// <param name="name">
        ///     Name of file where data resides.
        /// </param>
        /// <param name="size">
        ///     Size of file to be read.
        /// </param>
        /// <param name="heightMap">
        ///     Where data is put when read.
        /// </param>
        /// <returns>
        ///     Returns <c>true</c> if success, <c>false</c> failure.
        /// </returns>
        private bool LoadRawFile(string name, int size, ref byte[] heightMap) {
            if(name == null || name == string.Empty) {                          // Make Sure A Filename Was Given
                return false;                                                   // If Not Return false
            }

            string fileName1 = string.Format("Data{0}{1}",                      // Look For Data\Filename
                Path.DirectorySeparatorChar, name);
            string fileName2 = string.Format("{0}{1}{0}{1}Data{1}{2}",          // Look For ..\..\Data\Filename
                "..", Path.DirectorySeparatorChar, name);

            // Make Sure The File Exists In One Of The Usual Directories
            if(!File.Exists(name) && !File.Exists(fileName1) && !File.Exists(fileName2)) {
                MessageBox.Show("Can't Find The Height Map!", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return false;                                                   // File Could Not Be Found
            }

            if(File.Exists(fileName1)) {                                        // Does The File Exist Here?
                name = fileName1;                                               // Set To Correct File Path
            }
            else if(File.Exists(fileName2)) {                                   // Does The File Exist Here?
                name = fileName2;                                               // Set To Correct File Path
            }

            // Open The File In Read / Binary Mode
            using(FileStream fs = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                BinaryReader r = new BinaryReader(fs);
                heightMap = r.ReadBytes(size);
            }
            return true;                                                        // Found And Loaded Data In File
        }
        #endregion bool LoadRawFile(string name, int size, ref byte[] heightMap)

        private void drawGLScene()
        {
            /*
            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);
            setCamera();
            Gl.glScalef(1.0f, 1.0f, 1.0f / HEIGHT_RATIO);
            heightMap();
            calcFps();
            */

            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);        // Clear Screen And Depth Buffer
            Gl.glLoadIdentity();                                                // Reset The Current Modelview Matrix
            Gl.glTranslatef(-1.5f, 0, -6);                                      // Move Left 1.5 Units And Into The Screen 6.0
            Gl.glBegin(Gl.GL_TRIANGLES);                                        // Drawing Using Triangles
            Gl.glColor3f(1, 0, 0);                                          // Set The Color To Red
            Gl.glVertex3f(0, 1, 0);                                         // Top
            Gl.glColor3f(0, 1, 0);                                          // Set The Color To Green
            Gl.glVertex3f(-1, -1, 0);                                       // Bottom Left
            Gl.glColor3f(0, 0, 1);                                          // Set The Color To Blue
            Gl.glVertex3f(1, -1, 0);                                        // Bottom Right
            Gl.glEnd();                                                         // Finished Drawing The Triangle
            Gl.glTranslatef(3, 0, 0);                                           // Move Right 3 Units
            Gl.glColor3f(0.5f, 0.5f, 1);                                        // Set The Color To Blue One Time Only
            Gl.glBegin(Gl.GL_QUADS);                                            // Draw A Quad
            Gl.glVertex3f(-1, 1, 0);                                        // Top Left
            Gl.glVertex3f(1, 1, 0);                                         // Top Right
            Gl.glVertex3f(1, -1, 0);                                        // Bottom Right
            Gl.glVertex3f(-1, -1, 0);                                       // Bottom Left
            Gl.glEnd();
        }

        /*
        #region int getHeight(byte[] heightMap, int x, int y)
        private int getHeight(byte[] heightMap, int x, int y)
        {          // This Returns The Height From A Height Map Index
            x = x % MAP_SIZE;                                                   // Error Check Our x Value
            y = y % MAP_SIZE;                                                   // Error Check Our y Value

            return heightMap[x + (y * MAP_SIZE)];                               // Index Into Our Height Array And Return The Height
        }
        #endregion
        */

        #region killGLWindow()
        private void killGLWindow()
        {
            if (fullscreen)
            {                                                    // Are We In Fullscreen Mode?
                User.ChangeDisplaySettings(IntPtr.Zero, 0);                     // If So, Switch Back To The Desktop                                                  // Show Mouse Pointer
            }

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
                if (this != null && !this.IsDisposed)
                {                          // Do We Have A Window?
                    if (this.Handle != IntPtr.Zero)
                    {                            // Do We Have A Window Handle?
                        if (!User.ReleaseDC(this.Handle, DC))
                        {                 // Are We Able To Release The DC?
                            MessageBox.Show("Release Device Context Failed.", "SHUTDOWN ERROR",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }

                DC = IntPtr.Zero;                                              // Set DC To Null
            }

            if (this != null)
            {                                                  // Do We Have A Windows Form?
                this.Hide();                                                    // Hide The Window
                this.Close();                                                   // Close The Form
                //this = null;                                                    // Set form To Null
            }
        }
        #endregion

        /*
        #region bool renderHeightMap(byte[] heightMap)
        private bool renderHeightMap(byte[] heightMap) {                 // This Renders The Height Map As Quads
            int X, Y;                                                           // Create Some Variables To Walk The Array With.
            int x, y, z;                                                        // Create Some Variables For Readability

            if(heightMap == null) {                                             // Make Sure Our Height Data Is Valid
                return false;
            }

            if(drawType) {                                                       // What We Want To Render
                Gl.glBegin(Gl.GL_QUADS);                                        // Render Polygons
            }
            else {
                Gl.glBegin(Gl.GL_LINES);                                        // Render Lines Instead
            }

            for(X = 0; X < (MAP_SIZE - STEP_SIZE); X += STEP_SIZE) {
                for (Y = 0; Y < (MAP_SIZE-STEP_SIZE); Y += STEP_SIZE) {
                    // Get The (X, Y, Z) Value For The Bottom Left Vertex
                    x = X;
                    y = GetHeight(heightMap, X, Y);
                    z = Y;

                    SetVertexColor(heightMap, x, z);                            // Set The Color Value Of The Current Vertex
                    Gl.glVertex3i(x, y, z);                                     // Send This Vertex To OpenGL To Be Rendered (Integer Points Are Faster)

                    // Get The (X, Y, Z) Value For The Top Left Vertex
                    x = X;
                    y = GetHeight(heightMap, X, Y + STEP_SIZE);
                    z = Y + STEP_SIZE;

                    SetVertexColor(heightMap, x, z);                            // Set The Color Value Of The Current Vertex
                    Gl.glVertex3i(x, y, z);                                     // Send This Vertex To OpenGL To Be Rendered

                    // Get The (X, Y, Z) Value For The Top Right Vertex
                    x = X + STEP_SIZE;
                    y = GetHeight(heightMap, X + STEP_SIZE, Y + STEP_SIZE);
                    z = Y + STEP_SIZE;

                    SetVertexColor(heightMap, x, z);                            // Set The Color Value Of The Current Vertex
                    Gl.glVertex3i(x, y, z);                                     // Send This Vertex To OpenGL To Be Rendered

                    // Get The (X, Y, Z) Value For The Bottom Right Vertex
                    x = X + STEP_SIZE;
                    y = GetHeight(heightMap, X + STEP_SIZE, Y);
                    z = Y;

                    SetVertexColor(heightMap, x, z);                            // Set The Color Value Of The Current Vertex
                    Gl.glVertex3i(x, y, z);                                     // Send This Vertex To OpenGL To Be Rendered
                }
            }
            Gl.glEnd();
            Gl.glColor4f(1, 1, 1, 1);                                           // Reset The Color
            return true;                                                        // All Good
        }
        #endregion
        */

        /*
        #region SetVertexColor(byte[] heightMap, int x, int y)
        /// <summary>
        ///     Sets the color value for a particular index, depending on the height index.
        /// </summary>
        /// <param name="heightMap">
        ///     Height map data.
        /// </param>
        /// <param name="x">
        ///     X coordinate value.
        /// </param>
        /// <param name="y">
        ///     Y coordinate value.
        /// </param>
        private void SetVertexColor(byte[] heightMap, int x, int y) {
            float fColor = -0.15f + (GetHeight(heightMap, x, y ) / 256.0f);
            Gl.glColor3f(0, 0, fColor);                                         // Assign This Blue Shade To The Current Vertex
        }
        #endregion SetVertexColor(byte[] heightMap, int x, int y)
        */
 
        private void resizeGLScene(int width, int height)
        {
            if (height == 0)
            {                                                   // Prevent A Divide By Zero...
                height = 1;                                                     // By Making Height Equal To One
            }
            Gl.glViewport(0, 0, width, height);                                 // Reset The Current Viewport                                             // Reset The Modelview Matrix
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
                            camera.cameraZ = curHeight / HEIGHT_RATIO;
                        }
                    }
                    break;
                case 1://X scroll Right
                    if (camera.cameraX + 15 < heightmap.GetLength(0) - 1)
                    {
                        camera.cameraX++;
                        camera.lookatX++;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = curHeight / HEIGHT_RATIO;
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
                            camera.cameraZ = curHeight / HEIGHT_RATIO;
                        }
                    }
                    break;
                case 3://Y scroll Up
                    if (camera.cameraY + 15 < heightmap.GetLength(1) - 1)
                    {
                        camera.cameraY++;
                        camera.lookatY++;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = curHeight / HEIGHT_RATIO;
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

        private void keyPress(object sender, KeyPressEventArgs e)
        {
            char c = char.ToUpper(e.KeyChar);
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
                    drawType = 0;
                    break;
                case (char)Keys.K:
                    drawType = 1;
                    break;
            }
        }

        // --- Private Instance Event Handlers ---
        #region Form_Activated
        /// <summary>
        ///     Handles the form's activated event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        private void Form_Activated(object sender, EventArgs e) {
            active = true;                                                      // Program Is Active
        }
        #endregion Form_Activated

        #region Form_Closing(object sender, CancelEventArgs e)
        /// <summary>
        ///     Handles the form's closing event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        private void Form_Closing(object sender, CancelEventArgs e) {
            finished = true;                                                        // Send A Quit Message
        }
        #endregion Form_Closing(object sender, CancelEventArgs e)

        private void Form_Deactivate(object sender, EventArgs e)
        {
            active = false;                                                     // Program Is No Longer Active
        }

        #region Form_Resize(object sender, EventArgs e)
        /// <summary>
        ///     Handles the form's resize event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        private void Form_Resize(object sender, EventArgs e) {
            resizeGLScene(this.Width, this.Height);                             // Resize The OpenGL Window
        }
        #endregion Form_Resize(object sender, EventArgs e)
    }
}
