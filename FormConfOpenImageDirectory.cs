ing System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using uIP.Lib;
using uIP.Lib.Script;


namespace uIP.MacroProvider.StreamIO.ImageFileLoader
{
    public partial class FormConfOpenImageDirectory : Form
    {
        internal static string LatestBrowerFolder { get; set; } = "";
        public string PickedDir { get { return string.IsNullOrEmpty(textBox_pickedDir.Text) ? "" : string.Copy(textBox_pickedDir.Text); } }
        public UMacro MacroInstance { get; set; }
        public FormConfOpenImageDirectory()
        {
            InitializeComponent();
        }

        private void button_pickFolder_Click( object sender, EventArgs e )
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            if ( !string.IsNullOrEmpty( PickedDir ) && Directory.Exists( PickedDir ) )
                dlg.SelectedPath = PickedDir;
            else if ( !string.IsNullOrEmpty( LatestBrowerFolder ) && Directory.Exists( LatestBrowerFolder ) )
                dlg.SelectedPath = LatestBrowerFolder;
            if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedPath) )
            {
                textBox_pickedDir.Text = string.Copy( dlg.SelectedPath );

                LatestBrowerFolder = Path.GetDirectoryName( dlg.SelectedPath );
            }
            dlg.Dispose();
        }

        private void button_apply_Click( object sender, EventArgs e )
        {
            var got = uMProvidImageLoader.MakeOpenImageInitData( textBox_pickedDir.Text );
            if ( got != null && MacroInstance != null )
                MacroInstance.MutableInitialData = got;
        }

        private void FormConfOpenImageDirectory_Shown( object sender, EventArgs e )
        {
            if ( MacroInstance == null || MacroInstance.MutableInitialData == null ) return;
            if ( MacroInstance.MutableInitialData.Data is UDataCarrier[] container && container != null)
            {
                if ( container.Length > ( int )uMProvidImageLoader.OpenImageIndex.SearchDir && container[ ( int )uMProvidImageLoader.OpenImageIndex.SearchDir ].Data is string path )
                {
                    if ( !string.IsNullOrEmpty( path ) ) textBox_pickedDir.Text = path;
                }
            }
        }

        private void button_ok_Click(object sender, EventArgs e)
        {

        }

        private void textBox_pickedDir_TextChanged(object sender, EventArgs e)
        {

        }

        private void FormConfOpenImageDirectory_Load(object sender, EventArgs e)
        {

        }
    }
}