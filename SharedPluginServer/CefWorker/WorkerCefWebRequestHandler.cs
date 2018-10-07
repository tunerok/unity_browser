using Xilium.CefGlue;

namespace SharedPluginServer
{
    class WorkerCefWebRequestHandler : CefRequestHandler
    {
        private readonly CefWorker _mainWorker;

        public WorkerCefWebRequestHandler(CefWorker mainCefWorker)
        {
            _mainWorker = mainCefWorker;
        }

        protected override bool OnBeforeBrowse(CefBrowser browser, CefFrame frame, CefRequest request, bool isRedirect)
        {
            _mainWorker.BrowserMessageRouter.OnBeforeBrowse(browser, frame);
            return base.OnBeforeBrowse(browser, frame, request, isRedirect);
        }

        protected override void OnRenderProcessTerminated(CefBrowser browser, CefTerminationStatus status)
        {
            _mainWorker.BrowserMessageRouter.OnRenderProcessTerminated(browser);
        }
    }
}