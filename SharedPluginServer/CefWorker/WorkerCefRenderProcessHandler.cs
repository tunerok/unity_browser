using Xilium.CefGlue;
using Xilium.CefGlue.Wrapper;

namespace SharedPluginServer
{
    class WorkerCefRenderProcessHandler : CefRenderProcessHandler
    {
        internal CefMessageRouterRendererSide MessageRouter { get; private set; }

        public WorkerCefRenderProcessHandler()
        {
            MessageRouter = new CefMessageRouterRendererSide(new CefMessageRouterConfig());
        }

        protected override void OnContextCreated(CefBrowser browser, CefFrame frame, CefV8Context context)
        {
            MessageRouter.OnContextCreated(browser, frame, context);
        }

        protected override void OnContextReleased(CefBrowser browser, CefFrame frame, CefV8Context context)
        {
            MessageRouter.OnContextReleased(browser, frame, context);
        }

        protected override bool OnProcessMessageReceived(CefBrowser browser, CefProcessId sourceProcess,
            CefProcessMessage message)
        {
            var handled = MessageRouter.OnProcessMessageReceived(browser, sourceProcess, message);
            if (handled) return true;

            return false;
        }

        protected override void OnWebKitInitialized()
        {
            
        }

       protected override bool OnBeforeNavigation(CefBrowser browser, CefFrame frame, CefRequest request,
            CefNavigationType navigation_type, bool isRedirect)
        {
            return false;
        }
    }


}