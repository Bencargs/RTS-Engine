using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace RTSEngine
{
    class EngineMain
    {
        [STAThread]
        public static void Main()
        {
            Engine engine = new Engine();
            engine.mainLoop();
        }
    }
}
