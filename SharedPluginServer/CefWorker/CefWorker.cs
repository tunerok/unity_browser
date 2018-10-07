using System;
using System.Runtime.InteropServices;
using System.Threading;
using MessageLibrary;
using ResourceCollection;
using Xilium.CefGlue;
using Xilium.CefGlue.Wrapper;
using XiliumXWT;


namespace SharedPluginServer
{
    

    //Main CEF worker
    public class CefWorker : IDisposable,IInternalHttpRequestHandler
    {
        public CefWorker(string domainId) {
            _domainId = domainId;
        }

        private IQueryHandler _clientqueryHandler;
        private DynamicResponseQuery querystorage=new DynamicResponseQuery();
        private string _lastBundlePath = string.Empty;
        private readonly string _domainId;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(CefWorker));
        private readonly HttpResources _staticResourceStorage = new HttpResources();

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);
        private WorkerCefClient _client;

        private static bool _initialized = false;

        public CefMessageRouterBrowserSide BrowserMessageRouter { get; private set; }

        private WorkerCefMessageRouterHandler _queryHandler;

#region Status

        public delegate void PageLoaded(string url, int status);

        public event PageLoaded OnPageLoaded;

        public void InvokePageLoaded(string url, int status)
        {
            OnPageLoaded?.Invoke(url,status);
        }


#endregion

#region Dialogs

        public void InvokeCefDialog(string message, string prompt, DialogEventType type)
        {
            _clientqueryHandler?._mainWorker_OnJSDialog(message,prompt,type);
        }

        public void ContinueDialog(bool res, string input)
        {
            _client.ContinueDialog(res, input);
        }
#endregion



#region IDisposable
        ~CefWorker()
        {
            Dispose(false);
        }

        

        public void Dispose()
        {

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                log.Info("disposing CefWorker");
                Shutdown();
            }

        }
#endregion

        
        /// <summary>
        /// Initialization
        /// </summary>
        /// <param name="width">Browser rect width</param>
        /// <param name="height">Browser rect height</param>
        /// <param name="starturl"></param>
        public void Init(CommandLineParametres parametres)
        {
               RegisterMessageRouter();
                var cefWindowInfo = CefWindowInfo.Create();
                cefWindowInfo.SetAsWindowless(IntPtr.Zero, false);
                var cefBrowserSettings = new CefBrowserSettings {
                    JavaScript = CefState.Enabled,
                    TabToLinks = CefState.Enabled,
                    WebSecurity = CefState.Disabled,
                    WebGL = CefState.Enabled,
                    WindowlessFrameRate = 30
                };

            

            _client = new WorkerCefClient(parametres.kWidth, parametres.kHeight,this);
            
            var url = string.IsNullOrEmpty(parametres.InitialUrl) ? "about:blank" : parametres.InitialUrl;
            PrepareBundle(parametres.ResourceBundle);
            CefBrowserHost.CreateBrowser(cefWindowInfo, _client, cefBrowserSettings, url);
            _initialized = true;
        }

        public void SetMemServer(SharedTextureWriter memServer)
        {
            _client.SetMemServer(memServer);
        }

#region Queries

        private void RegisterMessageRouter()
        {
            if (!CefRuntime.CurrentlyOn(CefThreadId.UI))
            {
                PostTask(CefThreadId.UI, this.RegisterMessageRouter);
                return;
            }

            
            BrowserMessageRouter = new CefMessageRouterBrowserSide(new CefMessageRouterConfig());
            _queryHandler=new WorkerCefMessageRouterHandler();
            _queryHandler.OnBrowserQuery += Handler_OnBrowserQuery;
            BrowserMessageRouter.AddHandler(_queryHandler);
            var myFactory = new MySchemeHandlerFactory(_staticResourceStorage, this);
            CefRuntime.RegisterSchemeHandlerFactory("http", _domainId, myFactory);
        }

        

        private void Handler_OnBrowserQuery(string query) => _clientqueryHandler?._mainWorker_OnBrowserJSQuery(query);
        public void AnswerQuery(string resp) => _queryHandler.Callback(resp);

        #endregion

#region Task helper

        public static void PostTask(CefThreadId threadId, Action action)
        {
            CefRuntime.PostTask(threadId, new ActionTask(action));
        }

        internal sealed class ActionTask : CefTask
        {
            private readonly Action _action;
            public ActionTask(Action action){
                _action = action;
            }
            protected override void Execute() {
                _action?.Invoke();
            }
        }

        

#endregion

        public void Shutdown() {
            
            if (_client != null) {
                _client.Shutdown();
                _client = null;
            }
        }

        void PrepareBundle(string bundlepath) {
            if (bundlepath != _lastBundlePath) {
                _staticResourceStorage.Clear();
                _lastBundlePath = bundlepath;
                if(!string.IsNullOrEmpty(bundlepath))
                    _staticResourceStorage.UploadPackage(bundlepath);
            }
        }
#region Navigation and controls
        public void Navigate(string url,string bundlepath)
        {
            if (url.Contains(_domainId)) {
                int i = 0; //try to load local resource
            }
            PrepareBundle(bundlepath);

            _client.Navigate(url);
        }

        public void GoBack()
        {
            _client.GoBack();
        }

        public void GoForward()
        {
            _client.GoForward();
        }

        public void ExecuteJavaScript(string jscode)
        {
            _client.ExecuteJavaScript(jscode);
        }
#endregion

#region Mouse and keyboard
        public void MouseEvent(int x, int y,bool updown,MouseButton button)
        {
            _client.MouseEvent(x,y,updown,button);
        }

        public void MouseMoveEvent(int x, int y,MouseButton button)
        {
            _client.MouseMoveEvent(x, y,button);
        }

        public void KeyboardEvent(int character,KeyboardEventType type)
        {
            _client.KeyboardEvent(character,type);
        }

        public void FocusEvent(int focus)
        {
            _client.FocusEvent(focus);
        }

        public void MouseLeaveEvent()
        {
            _client.MouseLeaveEvent();
        }

        public void MouseWheelEvent(int x, int y, int delta)
        {
            _client.MouseWheelEvent(x,y,delta);
        }
#endregion
        public string Request(string url, string query) {
            var pendingQuery = querystorage.AddRequest(url, query);
            _clientqueryHandler?.SendDynamicPageQuery(url, query);
            if (!pendingQuery.Wait(TimeSpan.FromSeconds(1))) {
                querystorage.RemoveExpired(pendingQuery);
                return null;
            }

            
            return pendingQuery.Result;
        }
        public void HandleDynamicDataResonse(string genericEventStringContent, string genericEventAdditionalStringContent) {
            querystorage.PushQueryResonse(genericEventStringContent, genericEventAdditionalStringContent);
        }

        internal void SetQueryHandler(IQueryHandler clientqueryHandler) => _clientqueryHandler = clientqueryHandler;
    }
}
