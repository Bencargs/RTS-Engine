using System;
using System.Drawing;
using System.Windows.Forms;
using OpenGL;

namespace RTSEngine
{
	/// <summary>
	/// Example form to contain the example implementation of BaseGLControl
	/// </summary>
	class MainForm : System.Windows.Forms.Form
	{
		Engine glControl = new Engine();		//Example implementation

		static Form _this = null;
		/// <summary>
		/// Singleton for accessing our application
		/// </summary>
		public static Form App
		{
			get
			{
				if(_this == null)
					_this = new MainForm();
				return _this;
			}
		}

		public MainForm()
		{
			InitializeComponent();
			glControl.Location = new Point(0,0);	//Position control at 0
			glControl.Dock = DockStyle.Fill;		//Dock to fill form
			glControl.Visible = true;

			
			this.Load += new EventHandler(MainForm_Load);	//Add load handler to create timer
			this.Closing += new System.ComponentModel.CancelEventHandler(MainForm_Closing);
			this.Controls.Add(glControl);
		}
		void InitializeComponent() {
			// 
			// MainForm
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(640, 480);
			this.Name = "MainForm";
			this.Text = "RTS Engine";
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
		}
		
		/// <summary>
		/// When the form loads create a refresh timer
		/// </summary>
		private void MainForm_Load(object sender, EventArgs e)
		{
		}

		/// <summary>
		/// When the timer fires, refresh control
		/// </summary>
		private void updateTimer_Tick(object sender, EventArgs e)
		{
			glControl.Invalidate();
		}

		/// <summary>
		/// When the form closes, close the refresh timer
		/// </summary>
		private void MainForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
		}

		[STAThread]
		public static void Main(string[] args)
		{
			MainForm form = (MainForm)MainForm.App;
			form.FormBorderStyle = FormBorderStyle.None;
			form.Location = new Point(0,0);
			form.Size = Screen.PrimaryScreen.Bounds.Size;
			Application.Run(form);
		}
	}			
}
