using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using uIP.Lib;
using uIP.Lib.Script;

namespace uIP.MacroProvider.Resulting.DrawResult
{
    public partial class FormEditDisplayFormTitle : Form
    {
        public FormEditDisplayFormTitle()
        {
            InitializeComponent();
        }

        internal UMacro m_Macro = null;
        public UMacro MInstance
        {
            get => m_Macro;
            set
            {
                if ( UDataCarrier.Get<Form>( value?.MutableInitialData ?? null, null, out var form ) )
                {
                    textBox_title.Text = form.Text;
                }
                m_Macro = value;

                if ( m_Macro != null )
                    label_scriptName.Text = m_Macro.OwnerOfScript?.NameOfId ?? "";
            }
        }

        private void button_apply_Click( object sender, EventArgs e )
        {
            if ( string.IsNullOrEmpty( textBox_title.Text ) || m_Macro == null )
                return;
            if ( UDataCarrier.Get<Form>( m_Macro?.MutableInitialData ?? null, null, out var form ) )
            {
                form.Text = string.Copy( textBox_title.Text );
            }
        }
    }
}
