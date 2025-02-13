using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using uIP.Lib;
using uIP.Lib.DataCarrier;
using uIP.Lib.Script;

namespace uIP.MacroProvider.Resulting.DrawResult
{
    public class uMProvidingDrawResult : UMacroMethodProviderPlugin
    {
        const string Drawing2FormMethodName = "Drawing2Form";
        public uMProvidingDrawResult() : base()
        {
            m_strInternalGivenName = "Drawing2Form Result";
        }

        public override bool Initialize( UDataCarrier[] param )
        {
            if ( m_bOpened )
                return true;

            m_UserQueryOpenedMethods.Add(
                new UMacro( null, m_strCSharpDefClassName, Drawing2FormMethodName, Drawing2Form,
                            null, null,
                            null, null)
            );

            m_MacroControls.Add( "FormTitle", new UScriptControlCarrierMacro( "FormTitle", true, true, true,
                    new UDataCarrierTypeDescription[]
                    {
                        new UDataCarrierTypeDescription(typeof(string), "Title name")
                    },
                    IoctrlGet_FormTitle, IoctrlSet_FormTitle
                )
            );
            m_createMacroDoneFromMethod.Add( Drawing2FormMethodName, MacroShellDoneCall_All );
            m_macroMethodConfigPopup.Add( Drawing2FormMethodName, PopupMacroConfigDialog_All );

            m_bOpened = true;
            return true;
        }

        private bool MacroShellDoneCall_All( string callMethodName, UMacro instance )
        {
            // after create an instance of Macro, call to create Mutable init data
            if ( callMethodName == Drawing2FormMethodName )
            {
                ( ResourceManager.Get( ResourceManager.ScriptEditor ) as Form )?.Invoke(
                    new System.Action( () =>
                    {
                        var form = new FormDisplay() { CoorScale = 0.5 };
                        form.Location = new Point( Screen.PrimaryScreen.WorkingArea.Width, Screen.PrimaryScreen.WorkingArea.Height );
                        form.Show();
                        instance.MutableInitialData = UDataCarrier.MakeOne( form, true );
                        instance.MutableInitialData.fpDataHandler = ResourceManager.InvokeMainThreadFreeResource;
                        //form.Hide();
                    }
                ) );

                //var form = ( ResourceManager.Get( ResourceManager.ScriptEditor ) as Form )?.Invoke( new Func<Form>( () => new FormDisplay() { CoorScale = 0.5 } ) );
            }
            return true;
        }

        private Form PopupMacroConfigDialog_All( string callMethodName, UMacro macroToConf )
        {
            if (callMethodName == Drawing2FormMethodName)
            {
                return new FormEditDisplayFormTitle() { MInstance = macroToConf };
            }
            return null;
        }

        private UDataCarrier[] IoctrlGet_FormTitle( UScriptControlCarrier carrier, UMacro whichMacro, ref bool bRetStatus )
        {
            bRetStatus = false;
            if ( !UDataCarrier.Get<Form>( whichMacro?.MutableInitialData ?? null, null, out var form ) )
                return null;

            bRetStatus = true;
            return UDataCarrier.MakeOneItemArray( string.IsNullOrEmpty( form.Text ) ? "" : string.Copy( form.Text ) );
        }
        private bool IoctrlSet_FormTitle( UScriptControlCarrier carrier, UMacro whichMacro, UDataCarrier[] data )
        {
            if ( !UDataCarrier.Get<Form>( whichMacro?.MutableInitialData ?? null, null, out var form ) )
                return false;
            if ( !UDataCarrier.GetByIndex( data, 0, "", out var title ) )
                return false;

            form.Invoke( new System.Action( () => form.Text = title ) );
            return true;
        }

        private UDataCarrier[] Drawing2Form( UMacro MacroInstance,
                                             UDataCarrier[] PrevPropagationCarrier,
                                             List<UMacroProduceCarrierResult> historyResultCarriers,
                                             List<UMacroProduceCarrierPropagation> historyPropagationCarriers,
                                             List<UMacroProduceCarrierDrawingResult> historyDrawingCarriers,
                                             List<UScriptHistoryCarrier> historyCarrier,
                                         ref bool bStatusCode, ref string strStatusMessage,
                                         ref UDataCarrier[] CurrPropagationCarrier,
                                         ref UDrawingCarriers CurrDrawingCarriers,
                                         ref fpUDataCarrierSetResHandler PropagationCarrierHandler,
                                         ref fpUDataCarrierSetResHandler ResultCarrierHandler )
        {
            if ( (MacroInstance.MutableInitialData?.Data ?? null) is FormDisplay frm && frm != null && MacroInstance.OwnerOfScript != null)
            {
                //if ( !frm.Visible )
                //    frm.Show();
                if ( !frm.EverAdj && ( frm.Location.X >= Screen.PrimaryScreen.WorkingArea.Width || frm.Location.Y >= Screen.PrimaryScreen.WorkingArea.Height) )
                {
                    int x = Screen.PrimaryScreen.WorkingArea.Width / 2 - frm.Width / 2;
                    int y = Screen.PrimaryScreen.WorkingArea.Height / 2 - frm.Height / 2;
                    if ( frm.InvokeRequired )
                        frm.Invoke( new Action<Point>( pt => frm.Location = pt ), new Point( x, y ) );
                    else
                        frm.Location = new Point( x, y );
                    frm.EverAdj = true;
                }

                var drawRs = MacroInstance.OwnerOfScript.DrawingResultCarriers ?? null;
                if (drawRs != null)
                {
                    int resW = 0, resH = 0;
                    UMacroProduceCarrierDrawingResult which = null;
                    foreach ( var c in drawRs )
                    {
                        if ( c == null ) continue;
                        if (c.BackgroundW > 0 && c.BackgroundH > 0)
                        {
                            resW = c.BackgroundW;
                            resH = c.BackgroundH;
                            which = c;
                            break;
                        }
                    }
                    // got image resolution
                    if ( resW > 0 && resH > 0 )
                    {
                        double zoom = frm.CoorScale;
                        int iw = Convert.ToInt32( zoom * resW );
                        int ih = Convert.ToInt32( zoom * resH );
                        Bitmap b = new Bitmap( iw, ih, System.Drawing.Imaging.PixelFormat.Format24bppRgb );
                        try
                        {
                            foreach(var drw in drawRs )
                            {
                                if ( drw == null ) continue;
                                UDrawingCarriers.Drawing( b, drw.Data, zoom, zoom );
                            }
                        }
                        catch { b?.Dispose(); b = null; }
                        // draw to form
                        if ( b != null ) frm.DrawBitmap( b );
                    }
                }
            }
            bStatusCode = true;
            return null;
        }

    }
}
