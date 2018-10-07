using Xilium.CefGlue;

namespace SharedPluginServer
{
    class WorkerCefLifespanHandler : CefLifeSpanHandler
    {
        public CefBrowser MainBrowser;
        public CefBrowserHost MainBrowserHost;

        private readonly CefWorker _mainWorker;

        public WorkerCefLifespanHandler(CefWorker mainCefWorker)
        {
            _mainWorker = mainCefWorker;
        }

        protected override void OnAfterCreated(CefBrowser browser)
        {
            MainBrowser = browser;
            MainBrowserHost = browser.GetHost();
        }

        protected override bool DoClose(CefBrowser browser)
        {
            return false;
        }

        protected override void OnBeforeClose(CefBrowser browser)
        {
            _mainWorker.BrowserMessageRouter.OnBeforeClose(browser);
        }

        protected override bool OnBeforePopup(CefBrowser browser, CefFrame frame, string targetUrl,
            string targetFrameName, CefWindowOpenDisposition targetDisposition, bool userGesture,
            CefPopupFeatures popupFeatures, CefWindowInfo windowInfo, ref CefClient client, CefBrowserSettings settings,
            ref bool noJavascriptAccess)
        {
            // Block a new popup window.
            // Instead just redirect the popup target to the current browser.
            browser.GetMainFrame().LoadUrl(targetUrl);
            return true;
        }
    }
}