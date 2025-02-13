using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace uIP.MacroProvider.Resulting.DrawResult
{
    public partial class FormDisplay : Form
    {
        public double Zoom { get; set; } = 1.0;
        public double CoorScale { get; set; } = 1.0;
        public bool EverAdj { get; set; } = false;

        public FormDisplay()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing )
        {
            pictureBox_image.Image?.Dispose();
            pictureBox_image.Image = null;

            if ( disposing && ( components != null ) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        public void DrawBitmap(Bitmap b)
        {
            if (pictureBox_image.InvokeRequired)
            {
                BeginInvoke( new Action<Bitmap>( DrawBitmap ), b );
                return;
            }

            pictureBox_image.Image?.Dispose();
            pictureBox_image.Image = null;
            if ( b == null )
                return;

            Bitmap dst = null;
            if ( Zoom == 1.0 || Zoom == 0 )
                dst = b;
            else
            {
                try
                {
                    int rw = Convert.ToInt32( Zoom * b.Width );
                    int rh = Convert.ToInt32( Zoom * b.Height );
                    dst = new Bitmap( rw, rh, System.Drawing.Imaging.PixelFormat.Format24bppRgb );
                    using ( var g = Graphics.FromImage( dst ) )
                    {
                        g.DrawImage( b, new Rectangle( 0, 0, rw, rh ) );
                    }
                }
                catch { b.Dispose(); dst?.Dispose(); dst = null; }
            }

            if (dst != null)
            {
                if (pictureBox_image.Width != dst.Width || pictureBox_image.Height != dst.Height)
                {
                    pictureBox_image.Width = dst.Width;
                    pictureBox_image.Height = dst.Height;
                }
                pictureBox_image.Image = dst;
            }
        }

        private void FormDisplay_FormClosing( object sender, FormClosingEventArgs e )
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                this.Hide();
                e.Cancel = true;
            }
        }
    }
}
