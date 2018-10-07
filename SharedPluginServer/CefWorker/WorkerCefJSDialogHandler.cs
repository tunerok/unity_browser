using MessageLibrary;
using Xilium.CefGlue;

namespace SharedPluginServer
{
    class WorkerCefJSDialogHandler : CefJSDialogHandler
    {
        private CefJSDialogCallback _currentCallback=null;

        private CefWorker _mainWorker;


        public WorkerCefJSDialogHandler(CefWorker mainCefWorker)
        {
            _mainWorker = mainCefWorker;
        }

        protected override bool OnJSDialog(CefBrowser browser, string originUrl, CefJSDialogType dialogType,
            string message_text, string default_prompt_text, CefJSDialogCallback callback, out bool suppress_message)
        {
            _currentCallback = callback;
            switch (dialogType)
            {
                    case CefJSDialogType.Alert:
                    _mainWorker.InvokeCefDialog(message_text, default_prompt_text, DialogEventType.Alert);
                    break;
                    case CefJSDialogType.Confirm:
                    _mainWorker.InvokeCefDialog(message_text, default_prompt_text, DialogEventType.Confirm);
                    break;
                case CefJSDialogType.Prompt:
                    _mainWorker.InvokeCefDialog(message_text, default_prompt_text, DialogEventType.Prompt);
                    break;

            }
           
            suppress_message = false;
            return true;
        }

        public void Continue(bool success, string input)
        {
            _currentCallback?.Continue(success,input);
            _currentCallback = null;
        }

        protected override bool OnBeforeUnloadDialog(CefBrowser browser, string messageText, bool isReload, CefJSDialogCallback callback)
        {
            return true;
        }

        protected override void OnDialogClosed(CefBrowser browser)
        {
        }

        protected override void OnResetDialogState(CefBrowser browser)
        {
        }
    }
}
