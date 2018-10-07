using Xilium.CefGlue;
using Xilium.CefGlue.Wrapper;

namespace SharedPluginServer
{
    class WorkerCefMessageRouterHandler : CefMessageRouterBrowserSide.Handler
    {
        
        //simple JS binding
        public delegate void BrowserQuery(string query);

        public event BrowserQuery OnBrowserQuery;

        private CefMessageRouterBrowserSide.Callback _lastCallback=null;

        public override bool OnQuery(CefBrowser browser, CefFrame frame, long queryId, string request, bool persistent, CefMessageRouterBrowserSide.Callback callback)
        {
           
            OnBrowserQuery?.Invoke(request);
            _lastCallback = callback;
            return true;
        }

        public void Callback(string result)
        {
            _lastCallback?.Success(result);
            _lastCallback = null;
        }

        public override void OnQueryCanceled(CefBrowser browser, CefFrame frame, long queryId)
        {
        }
    }
}