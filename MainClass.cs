using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace RTSEngine
{
    public class MainForm : Form
    {
        //private Engine engine;
        public PrivateFontCollection fontsCollection = new PrivateFontCollection();

        [STAThread]
        static void Main()
        {
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
//            Application.Run(new Engine());//runs on current thread...
            Application.Run(new City());//runs on current thread...

            /*
            MainForm form = new MainForm();

            while ((!form.engine.finished) && (!form.IsDisposed))
            {
                form.Refresh();
                Application.DoEvents();
            }
            form.Dispose();
            */
        }

        public MainForm()
        {
            //this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            //this.ClientSize = new System.Drawing.Size(640, 480);
            //this.Name = "MainForm";
            //this.Text = "RTS Engine";
            loadFont();
            //engine.Parent = this;
            //engine.Dock = DockStyle.Fill; // Will fill whole form
            //this.Show();
        }

        public void errorHandler(string className, string functionName, string resource)
        {
            MessageBox.Show("Exception in " + className + " Class." + Environment.NewLine +
                "Inside " + functionName + "() Function." + Environment.NewLine +
                "Could not load " + resource + " Resources.", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //engine.finished = true;
        }

        private void loadFont()
        {
            try
            {
                fontsCollection.AddFontFile(@".\Fonts\28 Days Later.ttf");
                //as with textures, use a directory search, for mods
                //Font gameFont = new Font(fontsCollection.Families[0], 12);
            }
            catch
            {
                errorHandler(this.Name, MethodBase.GetCurrentMethod().Name, "Font");
            }
        }
    }
}
