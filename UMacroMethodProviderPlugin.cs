using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading;
using System.IO;
using System.Xml;
using System.Windows.Forms;
using uIP.Lib.Utility;

namespace uIP.Lib.Script
{
    /// <summary>
    /// 外掛基礎 class: 提供 script 中 macro 所要執行的 method
    /// - 目前並無提供不同的 Domain 進行 Assembly 掛載操作, 因此無法進行卸載. 也就是直接載入主要的 doamin 進行操作
    /// + 初始化
    ///   - Initialize: 為虛擬函式, 繼承的 class 必須 implement. 如果沒有相關於其他 Assembly 則執行成功結束要將 m_bOpened 設定為 true
    ///   - InitializedDone1stChance: 第一次呼叫所有外掛皆已載入, 繼承者要呼叫 base 進行設定 m_EnvLoadedPluginInstances 讓所有外掛知道有哪些其他外外都已經載入
    ///   - InitializedDone2ndChance: 第二次呼叫所有外掛皆已載入
    /// + 繼承的 class 必要填入資訊
    ///   - m_UserQueryOpenedMethods: 開放的 method 基礎描述
    ///     - UMacro->Invisible: 在 script 的可視度 (default: false)
    ///     - UMacro->ConfigFirst: 是否有內部參數要進行設定 (default: false)
    ///     - UMacro->ConfigDone: 是否已經對內部參數完成設定 (default: true)
    ///   - m_createMacroDoneFromMethod: 產生基礎的 macro 後, 要進行的額外設定, 有 macro 的殼接下來要填充額外的內容物
    ///   - m_genImmutableParamFromMethod: 要產生 macro 的 method 前, 用來呼叫生產出 immutable parameter
    ///   - m_genVariableParamFromMethod: 要產生 macro 的 method 前, 用來呼叫生產出 variable parameter
    ///   - m_macroMethodConfigPopup: 針對 macro 對應的 method 彈出的設定視窗
    ///   - m_macroMethodSettingsGet: 針對 macro method 取得 settings
    ///   - m_macroMethodSettingsSet: 針對 macro method 寫入 settings
    ///   - 呼叫 AutoBindingMacroPredefineCtrl() 會自動搜尋以下條件, Method 是屬於不公開
    ///     - CreateMacro{MethodName}DoneCall: 產生 UMacro 後會自動呼叫 <see cref="fpMacroShellDoneCall"/>
    ///     - Gen{MethodName}ImmutableParams: 要產生一個新的 UMacro 時, 用來產生 immutable parameters <see cref="fpGenMacroImmutableParam"/>
    ///     - Config{MethodName}ImmutableParams: 用來設定 macro 的 immutable parameters <see cref="fpConfigMacroImmuParamSet"/>
    ///     - Gen{MethodName}VariableParams: 要產生一個新的 UMacro 時, 用來產生 variable parameters <see cref="fpGenMacroVariableParam"/>
    ///     - Config{MethodName}VariableParams: 用來設定 macro 的 variable parameters <see cref="fpConfigMacroVarParamSet"/>
    ///     - Popup{MethodName}Config: 用來設定已經產生 UMacro 內部參數 <see cref="fpPopupMacroConfigDialog"/>
    ///     - Gen{MethodName}ConfigControl: 用來產生一個 window control 的 UMacro 設定 GUI <see cref="fpGetMacroConfig"/>
    ///     - Get{MethodName}MethodSettings: 用來取得 UMacro 的內部設定 <see cref="fpGetMacroMethodSettings"/>
    ///     - Set{MethodName}MethodSettings: 用來設定 UMacro 內部參數 <see cref="fpSetMacroMethodSettings"/>
    /// + 參數設定
    ///   - m_PluginClassControls: 針對 class 的 ioctl, 若是要儲存的參數 CanStore 要設定為 true; 禁用 Dictionary輸出
    ///   - m_MacroControls: 針對 macro 的 method 的 ioctl, 若是要儲存的參數 CanStore 要設定為 true; 禁用 Dictionary輸出
    /// </summary>
    public abstract class UMacroMethodProviderPlugin
    {
        public const string PredefMacroIoctl_SetupMacro = "SetupMacro";
        public const string PredefMacroIoctl_MacroImmuParam = "MacroImmuParam";
        public const string PredefMacroIoctl_MacroVarParam = "MacroVarParam";
        public const string PredefMacroIoctl_MacroMethodSettings = "MacroMethodSettings";
        public const string PredefMacroIoctl_SetupImmParam = "SetupImmParam";
        public const string PredefMacroIoctl_SetupVarParam = "SetupVarParam";

        public const string PredefClassIoctl_ParamGUI = "ParamGUI";

        public const string SettingsNodeName = "Settings";
        public const string MethodNodeName = "NameOfMethod";
        public const string ParametersNodeName = "Parameters";
        public readonly string Path2XmlRootNode = $"//{SettingsNodeName}";
        public readonly string Path2XmlMethosNameNode = $"//{SettingsNodeName}/{MethodNodeName}";
        public readonly string Path2XmlParametersNode = $"//{SettingsNodeName}/{ParametersNodeName}";

        private static string _strPluginClassParamDescFileName = "PluginClassDescription.xml";
        /// <summary>
        /// 針對 class 本身用來毒也的設定檔案名稱
        /// </summary>
        public static string PluginClassParamDescFileName => _strPluginClassParamDescFileName;

        /// <summary>
        /// 必要設定, 在這個 class 初始化後必須設定為 true, 之後針對這個 class 操作才會成功
        /// </summary>
        protected bool m_bOpened = false;

        /// <summary>
        /// 配合 UI 來取得 class 所開放之 method 在 class constructor 時就必須設定
        /// </summary>
        protected List<UMacro> m_UserQueryOpenedMethods = new List<UMacro>();

        /// <summary>
        /// 同步的 Mux 用來保護產生的 Macro List
        /// </summary>
        protected object m_hSyncCreatedMacroList = new object();

        /// <summary>
        /// provide a list for descendent to store UMacro called by UScript and need handling by this class itself
        /// </summary>
        protected List<UMacro> m_CreatedMacros = new List<UMacro>();

        /// <summary>
        /// 定義可設定之參數，並分成兩類
        /// - Plugin Class本身
        /// - UMacro 對應到 Opened Method
        /// [class itself] define which parameters that can be configured/ controlled to the class instance
        /// </summary>
        //protected List<UScriptControlCarrierPluginClass> m_PluginClassControls = new List<UScriptControlCarrierPluginClass>();
        protected Dictionary<string, UScriptControlCarrierPluginClass> m_PluginClassControls = new Dictionary<string, UScriptControlCarrierPluginClass>();

        /// <summary>
        /// [macro] define all parameters that can be configured/ controlled to opened method and can be filter by method name
        /// </summary>
        // protected List<UScriptControlCarrierMacro> m_MacroControls = new List<UScriptControlCarrierMacro>();
        protected Dictionary<string, UScriptControlCarrierMacro> m_MacroControls = new Dictionary<string, UScriptControlCarrierMacro>();

        /// <summary>
        /// Log 介面
        /// </summary>
        protected fpLogMessage m_fpLog = null;

        /// <summary>
        /// 目前環境中所載入的所有此類型的class instance
        /// all plugin class instances and only be read
        /// </summary>
        protected List<UMacroMethodProviderPlugin> m_EnvLoadedPluginInstances = null; // Read-only

        /* 紀錄系統相關資訊
         */
        /// <summary>
        /// c# defined the full type name of this class
        /// </summary>
        protected string m_strCSharpDefClassName = null;
        /// <summary>
        /// designer given name
        /// </summary>
        protected string m_strInternalGivenName = null;
        /// <summary>
        /// inside which assembly
        /// </summary>
        protected Assembly m_refAssemblyOwner = null;

        /// <summary>
        /// 語系支援
        /// </summary>
        protected string m_strLanguageCode = "en-us";

        /// <summary>
        /// 權限控制: Group level
        /// </summary>
        protected int m_nGroupLvl = 0;
        /// <summary>
        /// 權限控制: User level
        /// </summary>
        protected int m_nUserLvl = 0;

        /*
         * 配合一些 method 要進行的 delegate
         */
        /// <summary>
        /// 產生實際 UMacro instance 後要執行的 delegate call
        /// - key: Method Name of UMacro
        /// - value: type <see cref="fpMacroShellDoneCall"/>
        /// </summary>
        protected Dictionary<string, fpMacroShellDoneCall> m_createMacroDoneFromMethod = new Dictionary< string, fpMacroShellDoneCall >();
        /// <summary>
        /// 要產生 UMacro 時, 預先要執行來產生 Immutable parameter
        /// - Key: Method name of UMacro
        /// - Value: type <see cref="fpGenMacroImmutableParam"/>
        /// </summary>
        protected Dictionary<string, fpGenMacroImmutableParam> m_genImmutableParamFromMethod = new Dictionary< string, fpGenMacroImmutableParam >();

        /// <summary>
        /// 針對 macro 的 immutable parameter 進行設定
        /// - Key: Method name of UMacro
        /// - Value: type <see cref="fpConfigMacroImmuParamSet"/>
        /// </summary>
        protected Dictionary<string, fpConfigMacroImmuParamSet> m_macroImmutableParamConfMethod = new Dictionary< string, fpConfigMacroImmuParamSet >();

        /// <summary>
        /// 要產生 UMacro 時, 預先要執行來產生 Variable parameter
        /// - Key: Method name of UMacro
        /// - Value: type <see cref="fpGenMacroVariableParam"/>
        /// </summary>
        protected Dictionary<string, fpGenMacroVariableParam> m_genVariableParamFromMethod = new Dictionary< string, fpGenMacroVariableParam >();
        /// <summary>
        /// 針對 macro 的 variable parameter 進行設定
        /// - Key: Method name of UMacro
        /// - Value: type <see cref="fpConfigMacroVarParamSet"/>
        /// </summary>
        protected Dictionary<string, fpConfigMacroVarParamSet> m_macroVariableParamConfMethod = new Dictionary< string, fpConfigMacroVarParamSet >();
        /// <summary>
        /// 用來針對已經產生的 UMacro instance 的設定
        /// - Key: Method name of UMacro
        /// - Value: type <see cref="fpPopupMacroConfigDialog"/>
        /// </summary>
        protected Dictionary<string, fpPopupMacroConfigDialog> m_macroMethodConfigPopup = new Dictionary< string, fpPopupMacroConfigDialog >();
        /// <summary>
        /// 用來針對已經產生的 UMacro instance 呼叫產生一個 window control, 由呼叫端管理
        /// - Key: Method name of UMacro
        /// - Value: type <see cref="fpGetMacroConfig"/>
        /// </summary>
        protected Dictionary<string, fpGetMacroConfig> m_macroMethodConfigFormGet = new Dictionary< string, fpGetMacroConfig >();
        /// <summary>
        /// 用來針對已經產生的 UMacro instance 呼叫回傳設定參數
        /// - Key: Method name of UMacro
        /// - Value: type <see cref="fpGetMacroMethodSettings"/>
        /// </summary>
        protected Dictionary<string, fpGetMacroMethodSettings> m_macroMethodSettingsGet = new Dictionary< string, fpGetMacroMethodSettings >();
        /// <summary>
        /// 用來針對已經產生的 UMacro instance 進行參數設定
        /// - Key: Method name of UMacro
        /// - Value: type <see cref="fpSetMacroMethodSettings"/>
        /// </summary>
        protected Dictionary< string, fpSetMacroMethodSettings > m_macroMethodSettingsSet =
            new Dictionary< string, fpSetMacroMethodSettings >();

        #region [Public Properties]

        /// <summary>
        /// 檢查這個 class 是否已經可以啟用操作
        /// </summary>
        public bool IsOpened => m_bOpened;

        /// <summary>
        /// 回傳可供調整這個 class instance 的 ioctl 列表
        /// </summary>
        public Dictionary<string, UScriptControlCarrierPluginClass> PluginClassControlList => m_PluginClassControls;

        /// <summary>
        /// 回傳可針對 UMacro 中的 Method 進行 ioctl 列表
        /// </summary>
        public Dictionary<string, UScriptControlCarrierMacro> MacroControlList => m_MacroControls;

        /// <summary>
        /// 回報可供使用的 Method, 在初始化或是建構子時進行設定
        /// </summary>
        public List<UMacro> UserQueriedMethodList => m_UserQueryOpenedMethods;

        /// <summary>
        /// 回傳已經產生的 UMacro instance, 由這個 class 進行資源維護
        /// </summary>
        public List<UMacro> CreatedMacros => m_CreatedMacros;

        /// <summary>
        /// 設定或是取得 log 的 delegate
        /// </summary>
        public fpLogMessage fpLog
        {
            get => m_fpLog;
            set => m_fpLog = value;
        }

        /// <summary>
        /// 用來給定 CSharp class 的 全名, 如果是由 plugin service 載入則會自動設定為最終 class 的全名
        /// </summary>
        public string NameOfCSharpDefClass
        {
            get => m_strCSharpDefClassName;
            set => m_strCSharpDefClassName = string.IsNullOrEmpty( value ) ? null : string.Copy( value );
        }
        /// <summary>
        /// 用來在設計時給定的名稱, 由 plugin service 載入時沒有給值時, 會使用 class 的全名
        /// </summary>
        public string GivenName
        {
            get => m_strInternalGivenName;
            set => m_strInternalGivenName = string.IsNullOrEmpty( value ) ? null : string.Copy( value );
        }
        /// <summary>
        /// 用來儲存 Assembly, 用來標示由哪個 assembly 中找到的 instance
        /// </summary>
        public Assembly OwnerAssembly
        {
            get => m_refAssemblyOwner;
            set { if ( m_refAssemblyOwner == null ) { m_refAssemblyOwner = value; } }
        }

        #endregion

        /// <summary>
        /// 針對產生的 Macro 預設的 IOCtl
        /// + PredefMacroIoctl_SetupMacro
        ///   - GET: 回傳一個 Control 可供設定 Macro 的 method 參數 -> child override "DefaultParamsGet_SetupMacro()"
        ///   - SET: 彈出一個 Dialog 可供設定 Macro 的 method 參數 -> child override "DefaultParamsSet_SetupMacro()"
        /// + PredefMacroIoctl_MacroImmuParam
        ///   - SET: 設定 Macro 中 method 的不可變動的參數
        /// + PredefMacroIoctl_MacroVarParam
        ///   - SET: 設定 Macro 中 method 的可變動參數
        /// 針對 class 本身預設的 IOCtl
        /// + PredefClassIoctl_ParamGUI
        ///   - GET: 回傳一個 Control 可供設定 -> child override "ClassConfigGUIGet()"
        ///   - SET: 彈出一個 Dialog 進行設定 -> child override "ClassConfigGUIPopup()"
        /// </summary>
        public UMacroMethodProviderPlugin()
        {
            // default GET/ SET Support
            m_MacroControls.Add( PredefMacroIoctl_SetupMacro, new UScriptControlCarrierMacro( PredefMacroIoctl_SetupMacro, true, true, false, null,
                DefaultParamsGet_SetupMacro, DefaultParamsSet_SetupMacro ) );
            m_MacroControls.Add( PredefMacroIoctl_MacroImmuParam, new UScriptControlCarrierMacro( PredefMacroIoctl_MacroImmuParam, false, true, false,
                null, null, SetMacroImmutable ) );
            m_MacroControls.Add( PredefMacroIoctl_MacroVarParam, new UScriptControlCarrierMacro( PredefMacroIoctl_MacroVarParam, false, true, false,
                null, null, SetMacroVariable ) );
            m_MacroControls.Add( PredefMacroIoctl_MacroMethodSettings, new UScriptControlCarrierMacro( PredefMacroIoctl_MacroMethodSettings, true, true, true,
                null, MacroIoctl_GetMacroMethodSettings, MacroIoctl_SetMacroMethodSettings ) );
            m_MacroControls.Add( PredefMacroIoctl_SetupImmParam, new UScriptControlCarrierMacro( PredefMacroIoctl_SetupImmParam, false, true, false,
                null, null, MacroIoctl_SetupMacroImmParam ) );
            m_MacroControls.Add( PredefMacroIoctl_SetupVarParam, new UScriptControlCarrierMacro( PredefMacroIoctl_SetupVarParam, false, true, false,
                null, null, MacroIoctl_SetupMacroVarParam ) );
            m_PluginClassControls.Add( PredefClassIoctl_ParamGUI, new UScriptControlCarrierPluginClass( PredefClassIoctl_ParamGUI, true, true,
                false, null, ClassConfigGUIGet, ClassConfigGUIPopup ) );
        }

        #region [Default: GET/ SET]

        protected bool DefaultParamsSet_SetupMacro( UScriptControlCarrier carrier, UMacro whichMacro, UDataCarrier[] data )
        {
            if ( whichMacro == null || !m_macroMethodConfigPopup.ContainsKey( whichMacro.MethodName ) ) return false;
            Form frm = m_macroMethodConfigPopup[ whichMacro.MethodName ]( whichMacro.MethodName, whichMacro );
            frm?.ShowDialog();
            frm?.Dispose();
            return true;
        }

        protected UDataCarrier[] DefaultParamsGet_SetupMacro( UScriptControlCarrier carrier, UMacro whichMacro, ref bool bRetStatus )
        {
            bRetStatus = false;
            if ( whichMacro == null || !m_macroMethodConfigFormGet.ContainsKey( whichMacro.MethodName ) ) return null;
            bRetStatus = true;
            Control wc = m_macroMethodConfigFormGet[ whichMacro.MethodName ]( whichMacro.MethodName, whichMacro );
            return wc == null ? null : UDataCarrier.MakeOneItemArray( wc );
        }

        private bool MacroIoctl_SetupMacroImmParam( UScriptControlCarrier carrier, UMacro whichMacro,
            UDataCarrier[] data )
        {
            if ( whichMacro == null || !m_macroImmutableParamConfMethod.ContainsKey( whichMacro.MethodName ) ) return false;

            m_macroImmutableParamConfMethod[whichMacro.MethodName]?.Invoke( whichMacro );

            return true;
        }

        private bool MacroIoctl_SetupMacroVarParam( UScriptControlCarrier carrier, UMacro whichMacro,
            UDataCarrier[] data )
        {
            if ( whichMacro == null || !m_macroVariableParamConfMethod.ContainsKey( whichMacro.MethodName ) ) return false;

            m_macroVariableParamConfMethod[whichMacro.MethodName]?.Invoke( whichMacro );

            return true;
        }

        private UDataCarrier[] MacroIoctl_GetMacroMethodSettings( UScriptControlCarrier carrier, UMacro whichMacro,
            ref bool bRetStatus )
        {
            bRetStatus = false;
            if ( whichMacro == null || !m_macroMethodSettingsGet.ContainsKey( whichMacro.MethodName ) ) return null;
            object repo = null;
            Type t = null;
            bRetStatus = m_macroMethodSettingsGet[ whichMacro.MethodName ]?.Invoke( whichMacro, out repo, out t ) ?? true; // force not invoke return true
            return UDataCarrier.MakeOneItemArray( repo, t );
        }
        private bool MacroIoctl_SetMacroMethodSettings( UScriptControlCarrier carrier, UMacro whichMacro, UDataCarrier[] data )
        {
            if ( whichMacro == null || !m_macroMethodSettingsSet.ContainsKey( whichMacro.MethodName ) || data == null || data.Length <= 0 ) return false;

            return m_macroMethodSettingsSet[ whichMacro.MethodName ]?.Invoke( whichMacro, data[0].Data ) ?? false;
        }

        #endregion

        #region [Utility: 查詢資訊]

        public bool ContainMethod(string nameOfMethod)
        {
            if ( !m_bOpened ) return false;
            if ( m_UserQueryOpenedMethods == null ) return false;
            for(int i =0 ; i < m_UserQueryOpenedMethods.Count ; i++ )
            {
                if ( m_UserQueryOpenedMethods[ i ] == null ) continue;
                if ( m_UserQueryOpenedMethods[ i ].MethodName == nameOfMethod )
                    return true;
            }
            return false;
        }

        public UMacro QueryOpenedMethod( string name )
        {
            if ( !m_bOpened ) return null;
            if ( String.IsNullOrEmpty( name ) ) return null;
            if ( m_UserQueryOpenedMethods == null || m_UserQueryOpenedMethods.Count <= 0 ) return null;

            for ( int i = 0 ; i < m_UserQueryOpenedMethods.Count ; i++ )
            {
                if ( m_UserQueryOpenedMethods[ i ] == null ) continue;
                if ( m_UserQueryOpenedMethods[ i ].MethodName == name )
                    return m_UserQueryOpenedMethods[ i ];
            }

            return null;
        }

        public UScriptControlCarrierPluginClass QueryClassControls( string name )
        {
            if ( !m_bOpened ) return null;
            if ( String.IsNullOrEmpty( name ) ) return null;
            if ( m_PluginClassControls == null || m_PluginClassControls.Count <= 0 ) return null;

            if ( m_PluginClassControls.TryGetValue( name, out var ctrl ) ) return ctrl;

            return null;
        }

        public UScriptControlCarrierMacro QueryMacroControls( string name )
        {
            if ( !m_bOpened ) return null;
            if ( String.IsNullOrEmpty( name ) ) return null;
            if ( m_MacroControls == null || m_MacroControls.Count <= 0 ) return null;

            if ( m_MacroControls.TryGetValue( name, out var ctrl ) ) return ctrl;

            return null;
        }

        #endregion

        #region [針對 class 本身的操作]
        /// <summary>
        /// Initialize method must do(class 的初始化)
        /// 1. initialize its resource
        /// 2. update opened method infromation list
        /// </summary>
        /// <param name="param">[0]: a path of folder for this to R/W initial data, 
        ///                     [1]: env specified working RW path</param>
        /// <returns></returns>
        public abstract bool Initialize( UDataCarrier[] param );
        /// <summary>
        /// 所有初始化完成後呼叫，若是對其他的plugin class有相依性，則可以在此method在進行呼叫
        /// </summary>
        /// <param name="envLoadedClasses">由管理端傳入系統目前載入此類的instance</param>
        public virtual void InitializedDone1stChance( List<UMacroMethodProviderPlugin> envLoadedClasses ) { m_EnvLoadedPluginInstances = envLoadedClasses; }
        public virtual void InitializedDone2ndChance() { }
        /// <summary>
        /// The Close will do the following, if need to do others, override this.
        /// 1. set "open" flag to false
        /// 2. clear the created macro info and iternal ID if implemented IDispose
        /// </summary>
        public virtual void Close()
        {
            m_bOpened = false;

            if ( m_CreatedMacros != null )
            {
                Monitor.Enter( m_hSyncCreatedMacroList );
                try
                {
                    for ( int i = 0 ; i < m_CreatedMacros.Count ; i++ )
                    {
                        if ( m_CreatedMacros[ i ] != null )
                        {
                            // disable calling
                            m_CreatedMacros[ i ].IsAbleToCall( false );
                            // handle its carrier
                            m_CreatedMacros[ i ].MutableInitialData?.Dispose();
                            UDataCarrier.FreeByIDispose( m_CreatedMacros[ i ].ParameterCarrierImmutable );
                            UDataCarrier.FreeByIDispose( m_CreatedMacros[ i ].ParameterCarrierVariable );
                        }
                    }
                    m_CreatedMacros.Clear();
                    m_CreatedMacros = null;
                }
                finally { Monitor.Exit( m_hSyncCreatedMacroList ); }
            }

            if ( m_ConfigGUIControl != null) {
                m_ConfigGUIControl.Dispose();
                m_ConfigGUIControl = null;
            }
        }
        /// <summary>
        /// This will access to m_PluginClassControls configuration list.
        /// Put param "id" with string to descript which parameter name in list accessing.
        /// Configuration data is in param "data" with acceptable types and counts.
        /// </summary>
        /// <param name="id">[id.Data]: name to set in string type</param>
        /// <param name="data">acceptable data</param>
        /// <returns>true:ok, false:not available</returns>
        public virtual bool SetClassControl( UDataCarrier id, UDataCarrier[] data )
        {
            // Fix id querying using string, if need change, override it
            if ( !id.IsTypeMatching<string>() )
                return false;

            bool bRet = false;
            string strCast = ( string ) id.Data;
            if ( String.IsNullOrEmpty( strCast ) )
                return false;

            if ( !m_PluginClassControls.TryGetValue( strCast, out var ctrl ) || ctrl == null )
                return false;

            if ( !ctrl.CanSet )
                return false;

            if ( ctrl.SetParam == null )
                return false;

            // call to set ignore type checking
            bRet = ctrl.SetParam(ctrl, data);

            return bRet;
        }
        /// <summary>
        /// Get the setting of given "id" name in m_PluginClassControls list.
        /// </summary>
        /// <param name="id">name to get in string type</param>
        /// <param name="bRetStatus">return the function status. true:ok, false:NG</param>
        /// <returns>return the configuration</returns>
        public virtual UDataCarrier[] GetClassControl( UDataCarrier id, ref bool bRetStatus )
        {
            // Fix id querying using string, if need change, override it
            bRetStatus = false;
            if ( !id.IsTypeMatching<string>() )
                return null;

            string strCast = ( string ) id.Data;
            if ( String.IsNullOrEmpty( strCast ) )
                return null;

            if ( !m_PluginClassControls.TryGetValue( strCast, out var ctrl ) || ctrl == null )
                return null;

            if ( !ctrl.CanGet )
                return null;

            if ( ctrl.GetParam == null )
                return null;

            return ctrl.GetParam(ctrl, ref bRetStatus);
        }
        /// <summary>
        /// Query the configuration data type with "id" name in m_PluginClassControls list.
        /// </summary>
        /// <param name="id">[id.Data]: name to query in string type</param>
        /// <param name="bRetStatus">return the function status. true:ok, false:NG</param>
        /// <returns>return the configuration type of each item</returns>
        public virtual UDataCarrierTypeDescription[] GetClassControlDescription( UDataCarrier id, ref bool bRetStatus )
        {
            // Fix id querying using string, if need change, override it
            bRetStatus = false;
            if ( !id.IsTypeMatching<string>() )
                return null;

            string strCast = ( string ) id.Data;
            if ( String.IsNullOrEmpty( strCast ) )
                return null;

            if ( !m_PluginClassControls.TryGetValue( strCast, out var ctrl ) || ctrl == null )
                return null;

            return ctrl.DataTypes;
        }
        /// <summary>
        /// Leave an interface to support multi-language.
        /// </summary>
        /// <param name="code">language name</param>
        public virtual void ChangeLanguage( string code )
        {
            m_strLanguageCode = String.IsNullOrEmpty( code ) ? m_strLanguageCode : String.Copy( code );
        }
        /// <summary>
        /// Leave an interface to support access right
        /// </summary>
        /// <param name="lvl">current right</param>
        public virtual void ChangeAccessLvl( int gl, int ul )
        {
            m_nGroupLvl = gl;
            m_nUserLvl = ul;
        }

        protected Control m_ConfigGUIControl = null;
        protected virtual bool ClassConfigGUIPopup( UScriptControlCarrier carrier, UDataCarrier[] data )
        {
            return false;
        }
        protected virtual UDataCarrier[] ClassConfigGUIGet( UScriptControlCarrier carrier, ref bool bRetStatus )
        {
            bRetStatus = false;
            return null;
        }
        #endregion

        protected bool WriteToParameterNode(string path2XML, Dictionary<string, string> nodeKVs)
        {
            if ( nodeKVs == null )
                return false;

            try
            {
                var doc = new XmlDocument();
                doc.Load( path2XML );
                var paramNod = doc.SelectSingleNode( Path2XmlParametersNode );

                // not found, create one
                if ( paramNod == null )
                {
                    var rnod = doc.SelectSingleNode( Path2XmlRootNode );
                    if ( rnod == null )
                        return false;
                    paramNod = doc.CreateNode( XmlNodeType.Element, ParametersNodeName, "" );
                    rnod.AppendChild( paramNod );
                }

                // create node and append
                foreach(var kv in nodeKVs )
                {
                    if ( string.IsNullOrEmpty( kv.Key ) )
                        continue;
                    // config node
                    var nod = doc.CreateNode( XmlNodeType.Element, kv.Key, "" );
                    nod.InnerText = kv.Value;
                    // append
                    paramNod.AppendChild( nod );
                }

                // write back
                doc.Save( path2XML );
                return true;
            }
            catch ( Exception e )
            {
                fpLog?.Invoke( eLogMessageType.NORMAL, 0, $"Exception:\n{CommonUtilities.Dump01(e)}" );
                return false;
            }
        }

        protected bool ReadFromParameterNode( Stream s, out Dictionary<string, string> got, params string[] nodeName)
        {
            got = new Dictionary<string, string>();
            if ( nodeName == null || nodeName.Length == 0 )
                return false;

            try
            {
                var doc = new XmlDocument();
                doc.Load(s);

                var paramNod = doc.SelectSingleNode( Path2XmlParametersNode );
                if ( paramNod == null )
                    return false;

                foreach(var name in nodeName)
                {
                    var nod = paramNod.SelectSingleNode( name );
                    if ( nod != null )
                        got.Add( name, nod.InnerText );
                }

                return true;
            }
            catch(Exception e)
            {
                fpLog?.Invoke( eLogMessageType.NORMAL, 0, $"Exception:\n{CommonUtilities.Dump01( e )}" );
                return false;
            }
        }
        protected bool ReadFromParameterNode(string path2XML, out Dictionary<string, string> got, params string[] nodeName)
        {
            got = new Dictionary<string, string>();
            try
            {
                using(var fs = File.Open(path2XML, FileMode.Open, FileAccess.Read))
                {
                    return ReadFromParameterNode( fs, out got, nodeName );
                }
            }
            catch (Exception e)
            {
                fpLog?.Invoke( eLogMessageType.NORMAL, 0, $"Exception:\n{CommonUtilities.Dump01( e )}" );
                return false;
            }
        }

        #region [針對 macro 的操作]
        /// <summary>
        /// 預先設定 immutable parameters
        /// 1. Popup UI to let user config immutable parameters
        /// 2. Need nothing return null
        /// 3. reported immutable parameters must be all the management memory
        /// </summary>
        /// <param name="strMethodNm">plugin class opened method name for reference</param>
        /// <returns></returns>
        public virtual UDataCarrier[] SetupMacroImmutableOnes( string strMethodNm )
        {
            if ( !m_genImmutableParamFromMethod.ContainsKey( strMethodNm ) ) return null;
            return m_genImmutableParamFromMethod[ strMethodNm ]?.Invoke( strMethodNm );
        }
        /// <summary>
        /// 預先設定 variables
        /// 1. Popup UI to let user config variables
        /// 2. Need nothing return null
        /// 3. reported variables must be all the management memory
        /// </summary>
        /// <param name="strMethodNm">class opened method name for reference</param>
        /// <param name="currMacros">
        /// All calling macros of a script.
        /// 1st call( during a step-by-step ) the value is null.
        /// Once the script created, the value is keeping whole macros. All designers can use for searching itself or working with other macros.
        /// </param>
        /// <param name="bRetStatus">report the function status. true:OK, false:NG</param>
        /// <returns>report a macro variables</returns>
        public virtual UDataCarrier[] SetupMacroVariables( string strMethodNm, List<UMacro> currMacros, ref bool bRetStatus )
        {
            if ( !m_genVariableParamFromMethod.ContainsKey( strMethodNm ) || m_genVariableParamFromMethod[strMethodNm] == null )
            {
                bRetStatus = true;
                return null;
            }

            bRetStatus = m_genVariableParamFromMethod[ strMethodNm ].Invoke( strMethodNm, currMacros, out var repo );
            return repo;
        }
        /// <summary>
        /// 如果有需要在 Macro 產生後再次設定 Immutable, 可以使用這個機制進行參數回載至 UI
        /// 亦須要按 Method 名稱進行區分, 可以透過 MethodName 來識別
        /// </summary>
        /// <param name="carrier"></param>
        /// <param name="whichMacro"></param>
        /// <param name="data">null</param>
        /// <returns></returns>
        protected virtual bool SetMacroImmutable( UScriptControlCarrier carrier, UMacro whichMacro, UDataCarrier[] data )
        {
            if ( !m_bOpened ) return false;
            if ( whichMacro == null || !m_CreatedMacros.Contains( whichMacro ) ) return false;
            if ( !UDataCarrier.TypesCheck( data, whichMacro.ImmutableParamTypeDesc ) ) return false;
            whichMacro.ParameterCarrierImmutable = data;
            return true;
        }
        /// <summary>
        /// 如果有需要在 Macro 產生後再次設定 Variable, 可以使用這個機制進行參數回載至 UI
        /// 亦須要按 Method 名稱進行區分, 可以透過 MethodName 來識別
        /// </summary>
        /// <param name="carrier"></param>
        /// <param name="whichMacro"></param>
        /// <param name="data">null</param>
        /// <returns></returns>
        protected virtual bool SetMacroVariable( UScriptControlCarrier carrier, UMacro whichMacro, UDataCarrier[] data )
        {
            if ( !m_bOpened ) return false;
            if ( whichMacro == null || !m_CreatedMacros.Contains( whichMacro ) ) return false;
            if ( !UDataCarrier.TypesCheck( data, whichMacro.ParameterCarrierVariable ) ) return false;
            whichMacro.ParameterCarrierVariable = data;
            return true;
        }
        /// <summary>
        /// create a default UMacro
        /// </summary>
        /// <param name="param">[0].Data: name of Opend Method in list</param>
        /// <param name="immutableOnes">immutable parameters</param>
        /// <param name="variables">variables</param>
        /// <returns>a macro instance</returns>
        protected UMacro CreateDefaultMacro( UDataCarrier[] param, UDataCarrier[] immutableOnes, UDataCarrier[] variables )
        {
            if ( !m_bOpened )
                return null;
            if ( param == null || param.Length < 1 || param[ 0 ] == null )
                return null;

            if ( !param[ 0 ].IsTypeMatching<string>() )
                return null;

            // Convert data
            string strMethodNm = ( string ) param[ 0 ].Data;

            // Prepare data to return
            UMacro ret = null;
            for ( int i = 0 ; i < m_UserQueryOpenedMethods.Count ; i++ )
            {
                if ( m_UserQueryOpenedMethods[ i ] == null ) continue;
                if ( m_UserQueryOpenedMethods[ i ].MethodName == strMethodNm )
                {
                    UMacro tmp = m_UserQueryOpenedMethods[ i ];

                    if ( UDataCarrier.TypesCheck( immutableOnes, tmp.ImmutableParamTypeDesc ) )
                    {
                        ret = new UMacro( this, string.Copy( this.GetType().FullName ?? string.Empty ),
                            String.Copy( strMethodNm ),
                            tmp.fpHandler,
                            null, null, null, null, null )
                        {
                            GivenNameOfOwnerPluginClass = m_strInternalGivenName,
                            ImmutableParamTypeDesc = tmp.ImmutableParamTypeDesc,
                            VariableParamTypeDesc = tmp.VariableParamTypeDesc,
                            PrevPropagationParamTypeDesc = tmp.PrevPropagationParamTypeDesc,
                            RetPropagationParamTypeDesc = tmp.RetPropagationParamTypeDesc,
                            ParameterCarrierImmutable = immutableOnes,
                            ParameterCarrierVariable = variables,
                            Invisible = tmp.Invisible,
                            ConfigFirst = tmp.ConfigFirst,
                            ConfigDone = tmp.ConfigDone
                        };

                        // config after gen
                        if ( m_createMacroDoneFromMethod.ContainsKey( strMethodNm ) &&
                             m_createMacroDoneFromMethod[ strMethodNm ] != null )
                        {
                            if ( !m_createMacroDoneFromMethod[ strMethodNm ]( strMethodNm, ret ) )
                            {
                                // free resources
                                ret.MutableInitialData?.Dispose();
                                UDataCarrier.FreeByIDispose( ret.ParameterCarrierImmutable );
                                UDataCarrier.FreeByIDispose( ret.ParameterCarrierVariable );
                                return null;
                            }
                        }
                    }
                    break;
                }
            }
            return ret;
        }
        /// <summary>
        /// Make a macro according to name of method and use by reading parameter
        /// </summary>
        /// <param name="methodName">name of method</param>
        /// <returns>UMacro instance</returns>
        protected virtual UMacro MakeMacro( string methodName )
        {
            if ( !m_bOpened )
                return null;

            // Prepare data to return
            UMacro ret = null;
            for ( int i = 0; i < m_UserQueryOpenedMethods.Count; i++ ) {
                if ( m_UserQueryOpenedMethods[ i ] == null ) continue;
                if ( m_UserQueryOpenedMethods[ i ].MethodName == methodName ) {
                    UMacro tmp = m_UserQueryOpenedMethods[ i ];

                    ret = new UMacro( this, String.Copy( this.GetType().FullName ),
                                      String.Copy( methodName ),
                                      tmp.fpHandler,
                                      null, null, null, null, null );
                    if ( ret != null ) {
                        ret.GivenNameOfOwnerPluginClass = m_strInternalGivenName;

                        ret.ImmutableParamTypeDesc = tmp.ImmutableParamTypeDesc;
                        ret.VariableParamTypeDesc = tmp.VariableParamTypeDesc;
                        ret.PrevPropagationParamTypeDesc = tmp.PrevPropagationParamTypeDesc;
                        ret.RetPropagationParamTypeDesc = tmp.RetPropagationParamTypeDesc;

                    }

                    // config after gen
                    if ( m_createMacroDoneFromMethod.ContainsKey( methodName ) &&
                         m_createMacroDoneFromMethod[ methodName ] != null )
                    {
                        if ( !m_createMacroDoneFromMethod[ methodName ]( methodName, ret ) )
                        {
                            ret.MutableInitialData?.Dispose();
                            return null;
                        }
                    }

                    break;
                }
            }
            Monitor.Enter( m_hSyncCreatedMacroList );
            try {
                m_CreatedMacros.Add( ret );
            } finally {
                Monitor.Exit( m_hSyncCreatedMacroList );
            }
            return ret;
        }
        /// <summary>
        /// generate macro instance
        /// </summary>
        /// <param name="param">[0].Data: name of Opend Method in list</param>
        /// <param name="immutableOnes">immutable parameters</param>
        /// <param name="variables">variables</param>
        /// <returns>return an instance of macro</returns>
        public virtual UMacro CreateMacroInstance( UDataCarrier[] param, UDataCarrier[] immutableOnes, UDataCarrier[] variables )
        {
            UMacro ret = CreateDefaultMacro( param, immutableOnes, variables );
            if ( ret == null ) return null;

            Monitor.Enter( m_hSyncCreatedMacroList );
            try {
                m_CreatedMacros.Add( ret );
            } finally {
                Monitor.Exit( m_hSyncCreatedMacroList );
            }

            return ret;
        }

        /// <summary>
        /// Notify plugin class to recycle resources, such as c/c++ dll tool instance.
        /// If need to handle resources, this function must be overrided or implement IDispose.
        /// </summary>
        /// <param name="macro">macro of script</param>
        public virtual void RecycleMacro( UMacro macro )
        {
            if ( !m_bOpened ) return;

            bool bRmvSucc = false;

            Monitor.Enter( m_hSyncCreatedMacroList );
            try
            {
                macro.IsAbleToCall( false );
                if ( m_CreatedMacros.Remove( macro ) )
                {
                    // resource manage
                    macro.MutableInitialData?.Dispose();
                    UDataCarrier.FreeByIDispose( macro.ParameterCarrierImmutable );
                    UDataCarrier.FreeByIDispose( macro.ParameterCarrierVariable );
                    bRmvSucc = true;
                }
                else
                {
                    fpLog?.Invoke(eLogMessageType.NORMAL, 0,
                        $"[{this.GetType().FullName}] Remove {macro.MethodName} fail in list!" );
                }
            }
            finally
            {
                Monitor.Exit( m_hSyncCreatedMacroList );
            }

            if ( bRmvSucc )
                macro.DetachFromOwner();
        }
        /// <summary>
        /// Set data to macro of a script. Usually control the data of macro.
        /// Only accept one-by-one. If need to configure array item, we may combine two.
        /// One is configuring the index. Following by actual data set.
        /// </summary>
        /// <param name="macro">created macro info</param>
        /// <param name="id">[id.Data]: name in string to search</param>
        /// <param name="data">configuration data set</param>
        /// <returns>true:OK, false:NG</returns>
        public virtual bool SetMacroControl( UMacro macro, UDataCarrier id, UDataCarrier[] data )
        {
            if ( id == null || !id.IsTypeMatching<string>() )
                return false;

            string strCast = ( string ) id.Data;
            if ( String.IsNullOrEmpty( strCast ) ) return false;

            if ( !m_MacroControls.TryGetValue( strCast, out var ctrl ) || ctrl == null )
                return false;

            if ( !ctrl.CanSet )
                return false;

            if ( ctrl.SetParam == null )
                return false;

            return ctrl.SetParam( ctrl, macro, data );
        }
        /// <summary>
        /// Get data from macro of a script.
        /// If need specified data and cannot complete in one, we can considerate two calls.
        /// One call "SET" something, following by "GET". Of course, sync must be concerned.
        /// </summary>
        /// <param name="macro">created macro info</param>
        /// <param name="id">name in string to serach</param>
        /// <param name="bRetStatus">return the status. true:OK, false:NG</param>
        /// <returns>reported carrier</returns>
        public virtual UDataCarrier[] GetMacroControl( UMacro macro, UDataCarrier id, ref bool bRetStatus )
        {
            bRetStatus = false;
            if ( id == null || !id.IsTypeMatching<string>() )
                return null;

            string strCast = ( string ) id.Data;
            if ( String.IsNullOrEmpty( strCast ) ) return null;

            if ( !m_MacroControls.TryGetValue( strCast, out var ctrl ) || ctrl == null )
                return null;
            if ( !ctrl.CanGet )
                return null;
            if ( ctrl.GetParam == null )
                return null;

            return ctrl.GetParam( ctrl, macro, ref bRetStatus );
        }
        /// <summary>
        /// Get specified name "id" reporting data types.
        /// </summary>
        /// <param name="id">[id.Data]: name in string to search</param>
        /// <param name="bRetStatus">return the status. true:OK, false:NG</param>
        /// <returns>report the types description</returns>
        public virtual UDataCarrierTypeDescription[] GetMacroControlTypeDescription( UDataCarrier id, ref bool bRetStatus )
        {
            bRetStatus = false;
            if ( id == null || !id.IsTypeMatching<string>() )
                return null;

            string strCast = ( string ) id.Data;
            if ( String.IsNullOrEmpty( strCast ) ) return null;

            if (!m_MacroControls .TryGetValue(strCast, out var ctrl) || ctrl == null ) return null;
            return ctrl.DataTypes;
        }
        /// <summary>
        /// Get the all configurable names and its types info of a opened method.
        /// </summary>
        /// <param name="macro">created macro info</param>
        /// <returns>report the GET/SET info dictionary</returns>
        public virtual Dictionary<string, UScriptControlCarrierMacro> GetMethodsForMacro( UMacro macro )
        {
            return m_MacroControls;
        }

        /// <summary>
        /// 1. Read immutable parameters if need
        /// 2. Read variables if need
        /// 3. Read settings
        /// </summary>
        /// <param name="macro">new an instance of macro call info</param>
        /// <param name="pathOfFolder">path to reload parameters</param>
        /// <param name="cfgFile">settings file name</param>
        /// <returns></returns>
        public virtual bool ReadMacroSettings( ref UMacro macro, string pathOfFolder, string cfgFile )
        {
            return ReadMacroSettingsCommon( ref macro, pathOfFolder, cfgFile );
        }
        /// <summary>
        /// Read settings from a folder path and update the settings to an existing macro function without re-creating instance.
        /// Just load the settings.
        /// </summary>
        /// <param name="macro">existing macro</param>
        /// <param name="pathOfFolder">path for the macro function to read settings</param>
        /// <param name="cfgFile">file name to read</param>
        /// <returns></returns>
        public virtual bool UpdateMacroSettings( UMacro macro, string pathOfFolder, string cfgFile )
        {
            if ( macro == null ) return false;

            XmlDocument doc = new XmlDocument();
            doc.Load( Path.Combine( pathOfFolder, cfgFile ) );

            XmlNode method = doc.SelectSingleNode( Path2XmlMethosNameNode );
            if ( method == null || String.IsNullOrEmpty( method.InnerText ) )
                return false;

            XmlNode param = doc.SelectSingleNode( Path2XmlParametersNode );
            ReadMacroSettings( macro, param, true, true );

            return true;
        }

        /// <summary>
        /// 1. Write immutable parameters if need
        /// 2 .Write variables if need
        /// 3. Write settings
        /// </summary>
        /// <param name="macro">macro instance</param>
        /// <param name="pathOfFolder">path for the macro to store parameters info including images...</param>
        /// <param name="cfgFile">configuration file name</param>
        /// <returns></returns>
        public virtual bool WriteMacroSettings( UMacro macro, string pathOfFolder, string cfgFile )
        {
            return WriteMacroSettingsCommon( macro, pathOfFolder, cfgFile );
        }

        /// <summary>
        /// Make a copy from "reference". The content must be totally the same.
        /// This is used by multi-threading in async model or make a template.
        /// </summary>
        /// <param name="reference">for plugin class to refer</param>
        /// <returns>created same content macro instance</returns>
        public virtual UMacro ReproduceMacro( UMacro reference )
        {
            if ( !m_bOpened || reference == null )
                return null;
            if ( !m_CreatedMacros.Contains( reference ) )
                return null;


            UDataCarrier[] nm = UDataCarrier.MakeOneItemArray<string>( reference.MethodName );
            UMacro c = CreateMacroInstance( nm, reference.ParameterCarrierImmutable, reference.ParameterCarrierVariable );
            if ( c != null )
            {
                c.ConfigDone = true;
            }

            return c;
        }

        #region [Macro 參數寫成 xml]

        protected UDataCarrier[] ReadMacroImmutableOnes( string methodNm, XmlNode nod )
        {
            if ( nod == null ) return null;

            UDataCarrier[] ret = null;

            XmlNode n = nod.SelectSingleNode( String.Format( "{0}_ImmutableOnes", methodNm ) );
            if ( n != null )
            {
                UDataCarrier[] predef = null;
                string[] dummy = null;

                MemoryStream ms = new MemoryStream( Encoding.UTF8.GetBytes( n.InnerText ) );
                if ( UDataCarrier.ReadXml( ms, new Assembly[] { m_refAssemblyOwner }, ref predef, ref dummy ) )
                    ret = predef;
                ms.Dispose(); ms = null;
            }

            return ret;
        }

        protected UDataCarrier[] ReadMacroVariables( string methodNm, XmlNode nod )
        {
            if ( nod == null ) return null;

            UDataCarrier[] ret = null;

            XmlNode n = nod.SelectSingleNode( String.Format( "{0}_Variables", methodNm ) );
            if ( n != null )
            {
                UDataCarrier[] predef = null;
                string[] dummy = null;

                MemoryStream ms = new MemoryStream( Encoding.UTF8.GetBytes( n.InnerText ) );
                if ( UDataCarrier.ReadXml( ms, new Assembly[] { m_refAssemblyOwner }, ref predef, ref dummy ) )
                    ret = predef;
                ms.Dispose(); ms = null;
            }

            return ret;
        }

        protected void ReadMacroSettings( UMacro macro, XmlNode nod, bool bReadImmutable, bool bReadVariables )
        {
            if ( nod == null || macro == null ) return;

            if ( bReadImmutable )
            {
                UDataCarrier[] immutable = ReadMacroImmutableOnes( macro.MethodName, nod );
                if ( immutable != null ) macro.ParameterCarrierImmutable = immutable;
            }
            if ( bReadVariables )
            {
                UDataCarrier[] variables = ReadMacroVariables( macro.MethodName, nod );
                if ( variables != null ) macro.ParameterCarrierVariable = variables;
            }

            foreach ( var kv in m_MacroControls )
            {
                if ( kv.Value == null ) continue;
                if ( !kv.Value.CanStore || !kv.Value.CanSet || kv.Value.SetParam == null )
                    continue;

                XmlNode n = nod.SelectSingleNode( $"{macro.MethodName}_{kv.Value.Name}" );
                if ( n == null || String.IsNullOrEmpty( n.InnerText ) )
                    continue;

                UDataCarrier[] dat = null;
                string[] dummy = null;

                MemoryStream ms = new MemoryStream( Encoding.UTF8.GetBytes( n.InnerText ) );
                if ( UDataCarrier.ReadXml( ms, new Assembly[] { m_refAssemblyOwner }, ref dat, ref dummy ) )
                {
                    if ( !kv.Value.SetParam( kv.Value, macro, dat ) )
                        fpLog?.Invoke( eLogMessageType.NORMAL, 0, $"[{macro.OwnerOfScript?.NameOfId ?? ""}->{macro.MethodName}] call set \"{kv.Value.Name}\" fail" );
                }
                ms.Dispose();
            }
        }
        protected bool ReadMacroSettingsCommon( ref UMacro macro, string pathOfFolder, string cfgFile, bool bReadImmutable = true, bool bReadVariables = true )
        {
            XmlDocument doc = new XmlDocument();
            doc.Load( Path.Combine( pathOfFolder, cfgFile ) );

            XmlNode method = doc.SelectSingleNode( Path2XmlMethosNameNode );
            if ( method == null || String.IsNullOrEmpty( method.InnerText ) )
                return false;

            UMacro m = MakeMacro( method.InnerText.Trim() );
            if ( m == null )
                return false;

            XmlNode param = doc.SelectSingleNode( Path2XmlParametersNode );
            ReadMacroSettings( m, param, bReadImmutable, bReadVariables );

            // add to list to manage it
            Monitor.Enter( m_hSyncCreatedMacroList );
            try {
                m_CreatedMacros.Add( m );
            } finally {
                Monitor.Exit( m_hSyncCreatedMacroList );
            }

            macro = m;

            return true;
        }

        protected void WriteMacroImmutableOnes( UMacro macro, XmlTextWriter tw )
        {
            if ( macro == null || tw == null ) return;
            if ( macro.ParameterCarrierImmutable == null ) tw.WriteElementString( String.Format( "{0}_ImmutableOnes", macro.MethodName ), "" );
            else
            {
                MemoryStream ms = new MemoryStream();
                if ( UDataCarrier.WriteXml( macro.ParameterCarrierImmutable, null, ms ) )
                    tw.WriteElementString( String.Format( "{0}_ImmutableOnes", macro.MethodName ), Encoding.UTF8.GetString( ms.ToArray() ) );
                else
                    fpLog?.Invoke(eLogMessageType.NORMAL, 0, $"[{macro.OwnerOfScript?.NameOfId??""}->{macro.MethodName}] write immutable fail." );
                ms.Dispose(); ms = null;
            }
        }

        protected void WriteMacroVariables( UMacro macro, XmlTextWriter tw )
        {
            if ( macro == null || tw == null ) return;
            if ( macro.ParameterCarrierVariable == null ) tw.WriteElementString( String.Format( "{0}_Variables", macro.MethodName ), "" );
            else
            {
                MemoryStream ms = new MemoryStream();
                if ( UDataCarrier.WriteXml( macro.ParameterCarrierVariable, null, ms ) )
                    tw.WriteElementString( String.Format( "{0}_Variables", macro.MethodName ), Encoding.UTF8.GetString( ms.ToArray() ) );
                else
                    fpLog?.Invoke( eLogMessageType.NORMAL, 0, $"[{macro.OwnerOfScript?.NameOfId ?? ""}->{macro.MethodName}] write variables fail." );
                ms.Dispose(); ms = null;
            }
        }

        protected void WriteMacroSettings( UMacro macro, XmlTextWriter tw, bool bWriteImmutable, bool bWriteVariables )
        {
            if ( macro == null || tw == null ) return;

            // write immutable parameters
            if ( bWriteImmutable )
                WriteMacroImmutableOnes( macro, tw );
            // write variables
            if ( bWriteVariables )
                WriteMacroVariables( macro, tw );
            // write parameters
            foreach ( var kv in m_MacroControls )
            {
                if ( kv.Value == null || !kv.Value.CanStore || !kv.Value.CanGet || kv.Value.GetParam == null )
                    continue;
                bool stat = false;
                var dat = kv.Value.GetParam( kv.Value, macro, ref stat );
                if ( !stat )
                {
                    fpLog?.Invoke( eLogMessageType.NORMAL, 0, $"[{macro.OwnerOfScript?.NameOfId}->{macro.MethodName}] get \"{kv.Value.Name}\" fail." );
                    continue;
                }

                MemoryStream ms = new MemoryStream();
                if ( UDataCarrier.WriteXml( dat, null, ms ) )
                    tw.WriteElementString( $"{macro.MethodName}_{kv.Value.Name}", Encoding.UTF8.GetString( ms.ToArray() ) );
                else
                    fpLog?.Invoke( eLogMessageType.NORMAL, 0, $"[{macro.OwnerOfScript?.NameOfId}->{macro.MethodName}] write \"{kv.Value.Name}\" fail." );
                ms.Dispose(); ms = null;
            }
        }
        protected bool WriteMacroSettingsCommon( UMacro macro, string pathOfFolder, string cfgFile, bool bWriteImmutable = true, bool bWriteVariables = true )
        {
            string filpath = Path.Combine( pathOfFolder, cfgFile );
            try {
                using(Stream ws = File.Open(filpath, FileMode.Create)) {
                    XmlTextWriter xw = new XmlTextWriter( ws, Encoding.UTF8 );
                    xw.Formatting = Formatting.Indented;

                    xw.WriteStartDocument();
                    xw.WriteStartElement( SettingsNodeName );
                    xw.WriteElementString( MethodNodeName, macro.MethodName );

                    xw.WriteStartElement( ParametersNodeName );

                    WriteMacroSettings( macro, xw, bWriteImmutable, bWriteVariables );

                    xw.WriteEndElement();

                    xw.WriteEndElement();
                    xw.WriteEndDocument();

                    xw.Flush();
                    xw.Close();
                }
            }catch { return false; }

            return true;
        }

        #endregion

        #endregion

        #region [操作存取 class 的設定]
        // Write
        public virtual bool CanWritePluginClassSettings()
        {
            return ( m_PluginClassControls != null && m_PluginClassControls.Count > 0 );
        }
        public virtual void WritePluginClassSettingsBegin() { }
        public virtual bool WritePluginClassSettings( string pathDir )
        {
            if ( !Directory.Exists( pathDir ) ) return false;

            bool bOpenSucc = false;
            bool bWrstat = false;
            using ( Stream ws = File.Open( String.Format( @"{0}\{1}", pathDir, PluginClassParamDescFileName ), FileMode.Create ) )
            {
                bOpenSucc = ws != null;

                if ( bOpenSucc )
                    bWrstat = WritePluginClassConfigurations( ws, false );

                if ( bOpenSucc )
                {
                    try
                    {
                        ws.Flush();
                        ws.Close();
                    }
                    catch { }
                }
            }

            return bOpenSucc && bWrstat;
        }
        public virtual void WritePluginClassSettingsEnd() { }
        // Read
        public virtual bool CanReadPluginClassSettings()
        {
            return ( m_PluginClassControls != null && m_PluginClassControls.Count > 0 );
        }
        public virtual void ReadPluginClassSettingsBegin() { }
        public virtual bool ReadPluginClassSettings( string pathDir )
        {
            if ( !Directory.Exists( pathDir ) ) return false;

            bool bOpenStat = false;
            bool bRdStat = false;
            using ( Stream rs = File.Open( String.Format( @"{0}\{1}", pathDir, PluginClassParamDescFileName ), FileMode.Open ) )
            {
                bOpenStat = rs != null;
                if ( bOpenStat )
                    bRdStat = ReadPluginClassConfigurations( rs, String.Format( "//{0}", m_strCSharpDefClassName ) );

                if ( bOpenStat )
                {
                    try { rs.Close(); }
                    catch { }
                }
            }
            return bOpenStat && bRdStat;
        }
        public virtual void ReadPluginClassSettingsEnd() { }

        protected bool WritePluginClassConfigurations( Stream ws, bool isNode )
        {
            if ( ws == null ) return false;

            XmlTextWriter tw = new XmlTextWriter( ws, Encoding.UTF8 );
            if ( tw == null ) return false;

            tw.Formatting = Formatting.Indented;

            if ( !isNode ) tw.WriteStartDocument(); // start doc node
            tw.WriteStartElement( m_strCSharpDefClassName ); // class full name as root node

            if ( m_PluginClassControls != null && m_PluginClassControls.Count > 0 )
            {
                // get each parameter that can be stored.
                foreach ( var kv in m_PluginClassControls )
                {
                    if ( kv.Value == null || !kv.Value.CanStore || !kv.Value.CanGet || kv.Value.GetParam == null )
                        continue;

                    // call delegate function to get
                    bool retStatus = false;
                    UDataCarrier[] ret = kv.Value.GetParam( kv.Value, ref retStatus );
                    if ( !retStatus || ret == null || ret.Length <= 0 ) continue;

                    // write to memory stream
                    MemoryStream ms = new MemoryStream();
                    byte[] conv = null;
                    if ( UDataCarrier.WriteXml( ret, null, ms ) )
                        conv = ms.ToArray();

                    ms.Dispose();
                    ms = null;

                    if ( conv == null || conv.Length <= 0 ) continue;

                    // converting to string & write a node
                    tw.WriteElementString( kv.Value.Name, Encoding.UTF8.GetString( conv ) );
                }
            }

            tw.WriteEndElement(); // end root node
            if ( !isNode ) tw.WriteEndDocument(); // end doc

            tw.Flush();
            tw.Close();
            tw = null;

            return true;
        }

        protected bool ReadPluginClassConfigurations( Stream rs, string xmlRootPath )
        {
            if ( rs == null || String.IsNullOrEmpty( xmlRootPath ) ) return false;
            if ( m_PluginClassControls == null || m_PluginClassControls.Count <= 0 ) return true;

            XmlDocument doc = new XmlDocument();
            doc.Load( rs );

            XmlNode rootNd = doc.SelectSingleNode( xmlRootPath );

            if ( rootNd == null ) return false;

            foreach ( var kv in m_PluginClassControls )
            {
                if ( kv.Value == null || !kv.Value.CanStore || !kv.Value.CanSet || kv.Value.SetParam == null )
                    continue;

                XmlNode subNd = rootNd.SelectSingleNode( kv.Value.Name );
                if ( subNd == null ) continue;
                if ( String.IsNullOrEmpty( subNd.InnerText ) ) continue;

                byte[] dat = Encoding.UTF8.GetBytes( subNd.InnerText );
                if ( dat == null || dat.Length <= 0 ) continue;

                UDataCarrier[] retParams = null;
                string[] retAddiInfo = null;
                MemoryStream ms = new MemoryStream( dat );
                bool stat = UDataCarrier.ReadXml( ms, AppDomain.CurrentDomain.GetAssemblies(), ref retParams, ref retAddiInfo );
                ms.Dispose(); ms = null;
                if ( !stat )
                {
                    fpLog?.Invoke( eLogMessageType.NORMAL, 0, $"[UMacroMethodProviderPlugin::ReadPluginClassConfigurations] read item {kv.Value.Name} from xml data error." );
                    continue;
                }

                if ( !kv.Value.SetParam( kv.Value, retParams ) )
                    fpLog?.Invoke( eLogMessageType.NORMAL, 0, $"[UMacroMethodProviderPlugin::ReadPluginClassConfigurations] read item {kv.Value.Name} success but call set error." );

            }

            return true;
        }

        private static void AddDelegate< T >( Dictionary< string, T > dd, string keyName, object inst, string mn ) where T : class
        {
            if ( dd == null || string.IsNullOrEmpty( keyName ) || string.IsNullOrEmpty( mn ) ) return;
            if ( dd.ContainsKey( keyName ) ) return;

            try
            {
                T d = Delegate.CreateDelegate( typeof( T ), inst, mn ) as T;
                dd.Add( keyName, d );
            } catch ( Exception e )
            {
                Console.WriteLine(e);
            }
        }

        protected void AutoBindingMacroPredefineCtrl( object classInst )
        {
            // config method first
            if ( classInst == null ) return;
            Type tp = classInst.GetType();
            foreach ( var mm in m_UserQueryOpenedMethods )
            {
                if (string.IsNullOrEmpty( mm.MethodName )) continue;
                MethodInfo minfo = null;

                // search CreateMacro{MethodName}DoneCall
                minfo = tp.GetMethod( $"CreateMacro{mm.MethodName}DoneCall", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                AddDelegate( m_createMacroDoneFromMethod, mm.MethodName, classInst, minfo?.Name??"" );

                // search Gen{MethodName}ImmutableParams
                minfo = tp.GetMethod( $"Gen{mm.MethodName}ImmutableParams", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                AddDelegate( m_genImmutableParamFromMethod, mm.MethodName, classInst, minfo?.Name??"" );

                // search Config{MethodName}ImmutableParams
                minfo = tp.GetMethod( $"Config{mm.MethodName}ImmutableParams", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                AddDelegate( m_macroImmutableParamConfMethod, mm.MethodName, classInst, minfo?.Name??"" );

                // search Gen{MethodName}VariableParams
                minfo = tp.GetMethod( $"Gen{mm.MethodName}VariableParams", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                AddDelegate( m_genVariableParamFromMethod, mm.MethodName, classInst, minfo?.Name??"" );

                // search Config{MethodName}VariableParams
                minfo = tp.GetMethod( $"Config{mm.MethodName}VariableParams", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                AddDelegate( m_macroVariableParamConfMethod, mm.MethodName, classInst, minfo?.Name??"" );

                // search Popup{MethodName}Config
                minfo = tp.GetMethod( $"Popup{mm.MethodName}Config", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                AddDelegate( m_macroMethodConfigPopup, mm.MethodName, classInst, minfo?.Name??"" );

                // search Gen{MethodName}ConfigControl
                minfo = tp.GetMethod( $"Gen{mm.MethodName}ConfigControl", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                AddDelegate( m_macroMethodConfigFormGet, mm.MethodName, classInst, minfo?.Name??"" );

                // search Get{MethodName}MethodSettings
                minfo = tp.GetMethod( $"Get{mm.MethodName}MethodSettings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                AddDelegate( m_macroMethodSettingsGet, mm.MethodName, classInst, minfo?.Name??"" );

                // search Set{MethodName}MethodSettings
                minfo = tp.GetMethod( $"Set{mm.MethodName}MethodSettings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                AddDelegate( m_macroMethodSettingsSet, mm.MethodName, classInst, minfo?.Name??"" );
            }
        }

        #endregion
    }
}
