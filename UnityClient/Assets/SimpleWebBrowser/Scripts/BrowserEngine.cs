using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MessageLibrary;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SimpleWebBrowser
{
    public interface IDynamicRequestHandler {
        string Request(string url, string query);
    }
    public class BrowserEngine
    {
        private SharedTextureBuffer _mainTexArray;
        private MessageReader _inCommServer;
        private MessageWriter _outCommServer;
        private Process _pluginProcess;
        private string _localhostname;
        public Texture2D BrowserTexture;
        private bool StopRequestFlag;
        public bool Initialized;
        private bool _needToRunOnce;
        private string _runOnceJS = "";
        //Image buffer
        private bool _connected;
        private Thread _pollthread;
        private string StreamingAssetPath;
        internal IDynamicRequestHandler dynamicRequestHandler;

        #region Status events

        public delegate void PageLoaded(string url);

        public event PageLoaded OnPageLoaded;
        public event Action<Texture2D> OnTextureObjectUpdated;

        #endregion


        #region Settings

        public int kWidth = 512;
        public int kHeight = 512;

        private string _sharedFileName;

        //comm files
        private string _inCommFile;
        private string _outCommFile;

        private string _initialURL;
        private bool _enableWebRTC;
        private bool _enableGPU;

        #endregion

        #region Dialogs

        public delegate void JavaScriptDialog(string message, string prompt, DialogEventType type);

        public event JavaScriptDialog OnJavaScriptDialog;

        #endregion

        #region JSQuery

        public delegate void JavaScriptQuery(string message);

        public event JavaScriptQuery OnJavaScriptQuery;
        public string StreamingResourceName;

        #endregion



        #region Init

        private void BackgroundPollThread() {
            while (!StopRequestFlag) {
                SendPing();
                Thread.Sleep(1000);
            }
        }

        public string RedirectLocalhost(string path) {
            return path.Replace("localhost", _localhostname);
        }
        public IEnumerator InitPlugin(int width, int height, string sharedfilename,string initialURL,bool enableWebRTC,bool enableGPU) {
            _localhostname = WWW.EscapeURL(sharedfilename);
            initialURL = RedirectLocalhost(initialURL);
            _pollthread=new Thread(BackgroundPollThread);
            _pollthread.Start();
            //Initialization (for now) requires a predefined path to PluginServer,
            //so change this section if you move the folder
            //Also change the path in deployment script.
            StreamingAssetPath = Application.streamingAssetsPath;
#if UNITY_EDITOR_64
         string PluginServerPath = Path.Combine(Application.dataPath,@"SimpleWebBrowser\PluginServer\x64");
#else
#if UNITY_EDITOR_32
            string PluginServerPath = Application.dataPath + @"\SimpleWebBrowser\PluginServer\x86";
#else
        //HACK
        string AssemblyPath=System.Reflection.Assembly.GetExecutingAssembly().Location;
        //log this for error handling
        Debug.Log("Assembly path:"+AssemblyPath);

        AssemblyPath = Path.GetDirectoryName(AssemblyPath); //Managed
      
        AssemblyPath = Directory.GetParent(AssemblyPath).FullName; //<project>_Data
        AssemblyPath = Directory.GetParent(AssemblyPath).FullName;//required

        string PluginServerPath=AssemblyPath+@"\PluginServer";
        
#endif
#endif



            Debug.Log("Starting server from:" + PluginServerPath);

            kWidth = width;
            kHeight = height;



            _sharedFileName = sharedfilename;

            //randoms
            Guid inID = Guid.NewGuid();
            _outCommFile = inID.ToString();

            Guid outID = Guid.NewGuid();
            _inCommFile = outID.ToString();

            _initialURL = initialURL;
            _enableWebRTC = enableWebRTC;
            _enableGPU = enableGPU;

            if (BrowserTexture == null) {
                BrowserTexture = new Texture2D(kWidth, kHeight, TextureFormat.BGRA32, false, true);
                if(OnTextureObjectUpdated!=null)
                    OnTextureObjectUpdated(BrowserTexture);
            }

            string args = BuildParamsString();


           _connected = false;
            _inCommServer = null;
            _outCommServer = null;

            var pluginpath = Path.Combine(PluginServerPath, @"SharedPluginServer.exe");
            while (!_connected)
            {
                try
                {
                    _pluginProcess = new Process {
                        StartInfo = new ProcessStartInfo {
                            WorkingDirectory = PluginServerPath,
                            FileName = pluginpath,
                            Arguments = args

                        }
                    };
                    _pluginProcess.Start();
                    Initialized = false;
                }
                catch (Exception ex)
                {
                    //log the file
                    Debug.Log("FAILED TO START SERVER FROM:" + PluginServerPath + @"\SharedPluginServer.exe");
                    throw;
                }
                yield return new WaitForSeconds(1.0f);
                bool isReady = false;
                while (!isReady) {
                    try {
                        isReady = _pluginProcess.WaitForInputIdle(0);
                    }
                    catch (Exception e) {
                        Debug.LogException(e);
                        if (_pluginProcess.HasExited)
                            break;
                    }
                    yield return new WaitForSeconds(0.5f);
                }
                MessageReader inserv = null;
                MessageWriter outserv = null;
                try {
                    inserv = MessageReader.Open(_inCommFile);
                    outserv = MessageWriter.Open(_outCommFile);
                    _inCommServer = inserv;
                    _outCommServer = outserv;
                    _connected = true;
                    
                }
                catch (Exception e) {
                    if (_inCommServer != null) _inCommServer.Dispose();
                    if (_outCommServer != null) _outCommServer.Dispose();
                    _pluginProcess.Dispose();
                }
                
            }
        }

        private string BuildParamsString() {
            var parameters = new CommandLineParametres {
                kWidth = kWidth,
                kHeight = kHeight,
                InitialUrl = _initialURL,
                SharedFileName = _sharedFileName,
                ResourceBundle = FilteredBundleName(),
                inCommFile = _outCommFile, //swapped
                outCommFile = _inCommFile,
                _enableWebRTC = _enableWebRTC,
                _enableGPU = _enableGPU
            };
            return parameters.Encode();
        }

        #endregion



        #region SendEvents

        string FilteredBundleName() {
            return string.IsNullOrEmpty(StreamingResourceName)
                ? string.Empty
                : Path.Combine(StreamingAssetPath, StreamingResourceName);
        }
        public void SendNavigateEvent(string url, bool back, bool forward)
        {
            if (Initialized)
            {
                
                GenericEvent ge = new GenericEvent {
                    Type = GenericEventType.Navigate,
                    GenericType = BrowserEventType.Generic,
                    StringContent= RedirectLocalhost(url),
                    AdditionalStringContent = FilteredBundleName()
                };

                if (back)
                    ge.Type = GenericEventType.GoBack;
                else if (forward)
                    ge.Type = GenericEventType.GoForward;

                EventPacket ep = new EventPacket {
                    Event = ge,
                    Type = BrowserEventType.Generic
                };
                _outCommServer.TrySend(ep,1000);
            }
        }

        public void SendShutdownEvent()
        {
            if (Initialized)
            {
                GenericEvent ge = new GenericEvent {
                    Type = GenericEventType.Shutdown,
                    GenericType = BrowserEventType.Generic
                };

                EventPacket ep = new EventPacket {
                    Event = ge,
                    Type = BrowserEventType.Generic
                };

                _outCommServer.TrySend(ep,100);
            }
        }

        

       public void SendDialogResponse(bool ok, string dinput)
        {
            if (Initialized)
            {
                DialogEvent de = new DialogEvent {
                    GenericType = BrowserEventType.Dialog,
                    success = ok,
                    input = dinput
                };

                EventPacket ep = new EventPacket
                {
                    Event = de,
                    Type = BrowserEventType.Dialog
                };

                _outCommServer.TrySend(ep,100);
            }
        }

        public void SendQueryResponse(string response)
        {
            if (Initialized)
            {
                GenericEvent ge = new GenericEvent {
                    Type = GenericEventType.JSQueryResponse,
                    GenericType = BrowserEventType.Generic,
                    StringContent = response
                };

                EventPacket ep = new EventPacket {
                    Event = ge,
                    Type = BrowserEventType.Generic
                };

                _outCommServer.TrySend(ep,100);
            }
        }

        public void SendCharEvent(int character, KeyboardEventType type)
        {
            if (Initialized)
            {
                KeyboardEvent keyboardEvent = new KeyboardEvent {
                    Type = type,
                    Key = character
                };
                EventPacket ep = new EventPacket {
                    Event = keyboardEvent,
                    Type = BrowserEventType.Keyboard
                };

                _outCommServer.TrySend(ep, 100);
            }
        }

        public void SendMouseEvent(MouseMessage msg)
        {
            if (Initialized)
            {
                EventPacket ep = new EventPacket
                {
                    Event = msg,
                    Type = BrowserEventType.Mouse
                };

                if (!_outCommServer.TrySend(ep, 100)) {
                    Debug.LogWarningFormat("mouse message lost {0}", ep.Type);
                }
            }

        }

        public void SendExecuteJSEvent(string js)
        {
            if (Initialized)
            {
                GenericEvent ge = new GenericEvent {
                    Type = GenericEventType.ExecuteJS,
                    GenericType = BrowserEventType.Generic,
                    StringContent = js
                };

                EventPacket ep = new EventPacket {
                    Event = ge,
                    Type = BrowserEventType.Generic
                };

                _outCommServer.TrySend(ep, 100);
            }
        }
        public void SendDynamicResponse(string url,string result)
        {
            if (Initialized)
            {
                var ge = new GenericEvent {
                    Type = GenericEventType.DynamicDataResponse,
                    GenericType = BrowserEventType.Generic,
                    StringContent = url,
                    AdditionalStringContent = result
                };

                var ep = new EventPacket {
                    Event = ge,
                    Type = BrowserEventType.Generic
                };

                _outCommServer.TrySend(ep, 100);
            }
        }
        

        public void SendPing()
       {
            if (Initialized){
                GenericEvent ge = new GenericEvent {
                    Type = GenericEventType.Navigate, //could be any
                    GenericType = BrowserEventType.Ping
                };
                EventPacket ep = new EventPacket {
                    Event = ge,
                    Type = BrowserEventType.Ping
                };
                    _outCommServer.TrySend(ep, 100);
                }
        }


        #endregion


        #region Helpers

        /// <summary>
        /// Used to run JS on initialization, for example, to set CSS
        /// </summary>
        /// <param name="js">JS code</param>
       public void RunJSOnce(string js )
        {
            _needToRunOnce = true;
            _runOnceJS = js;
        }

        #endregion

        



     public void UpdateTexture()
        {

            if (Initialized)
            {


                UpdateInitialized();



                //execute run-once functions
                if (_needToRunOnce)
                {
                    SendExecuteJSEvent(_runOnceJS);
                    _needToRunOnce = false;
                }
            }
            else
            {
                if(_connected)
                { 
                        try
                        {
                            //init memory file
                            _mainTexArray = new SharedTextureBuffer(_sharedFileName);

                            Initialized = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.Log("Exception on init:" + ex.Message + ".Waiting for plugin server");
                        }

                }
               

            }
        }

        //Receiver
        public void CheckMessage()
        {

            if (Initialized)
            {
                try
                {
                    // Ensure that no other threads try to use the stream at the same time.
                    EventPacket ep = _inCommServer.TryRecive(0);


                    if (ep != null) {
                        //main handlers
                        switch (ep.Type) {
                            case BrowserEventType.Dialog:
                                DialogEvent dev = ep.Event as DialogEvent;
                                if (dev != null)
                                {
                                    if (OnJavaScriptDialog != null)
                                        OnJavaScriptDialog(dev.Message, dev.DefaultPrompt, dev.Type);
                                }

                                break;
                            case BrowserEventType.Generic:
                                var ge = ep.Event as GenericEvent;
                                if (ge != null) {
                                    switch (ge.Type) {
                                        case GenericEventType.JSQuery:
                                            if (OnJavaScriptQuery != null)
                                                OnJavaScriptQuery(ge.StringContent);
                                            break;
                                        case GenericEventType.PageLoaded:
                                            if (OnPageLoaded != null)
                                                OnPageLoaded(ge.StringContent);
                                            break;
                                        case GenericEventType.DynamicDataRequest:
                                            string result = null;
                                            if(dynamicRequestHandler!=null)
                                                result = dynamicRequestHandler.Request(ge.StringContent,
                                                            ge.AdditionalStringContent);
                                            SendDynamicResponse(ge.StringContent,result);
                                            break;
                                    }
                                }
                                break;
                        }
                    }

                }
                catch (Exception e)
                {
                    Debug.Log("Error reading from socket,waiting for plugin server to start...");
                }
            }
        }
        #region Keys

        private static KeyCode[] _keymap = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        public void ProcessKeyEvents() {
            foreach (var c in Input.inputString){
                SendCharEvent((int) c, KeyboardEventType.CharKey);
            }
            foreach (KeyCode k in _keymap)
                CheckKey(k);
        }
        private void CheckKey(KeyCode code) {
            if (Input.GetKeyDown(code))
                SendCharEvent((int) code, KeyboardEventType.Down);
            else if (Input.GetKeyUp(code))
                SendCharEvent((int) code, KeyboardEventType.Up);
        }

        #endregion

        public void Shutdown() {
            StopRequestFlag = true;
            SendShutdownEvent();
            Initialized = false;
            if (_pollthread!=null)
                _pollthread.Join();
        }

        //////////Added//////////
        public void UpdateInitialized()
        {
            if (Initialized)
            {
                if (_mainTexArray.AcquireReadLock(0)) {
                    _mainTexArray.MarkProcessed();
                    if (_mainTexArray.Length > 0) {
                        BrowserTexture.LoadRawTextureData(_mainTexArray.UnsafeDataPointer(), _mainTexArray.Length);
                        BrowserTexture.Apply();
                    }
                    _mainTexArray.ReleaseReadLock();
                }
                else {
                    int i = 0;
                }
            }
        }
    }
   
}