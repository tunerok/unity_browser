using Xilium.CefGlue;

namespace SharedPluginServer
{
    class WorkerCefLoadHandler : CefLoadHandler
    {

      

        private CefWorker _mainWorker;

        public WorkerCefLoadHandler(CefWorker mainWorker)
        {
            _mainWorker = mainWorker;
        }

        protected override void OnLoadStart(CefBrowser browser, CefFrame frame, CefTransitionType transitionType)
        {
           
        }

        protected override void OnLoadEnd(CefBrowser browser, CefFrame frame, int httpStatusCode)
        {
            
            if (frame.IsMain)
            {
            _mainWorker.InvokePageLoaded(frame.Url,httpStatusCode);
             
            }
        }
    }
}