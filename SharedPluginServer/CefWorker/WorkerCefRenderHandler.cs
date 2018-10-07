using System;
using System.Runtime.InteropServices;
using Xilium.CefGlue;

namespace SharedPluginServer
{
    class WorkerCefRenderHandler : CefRenderHandler
    {
        
        private readonly int _windowHeight;
        private readonly int _windowWidth;

        private int _copysize = 0;

        public int CurrentWidth=0;
        public int CurrentHeight = 0;

        public SharedTextureWriter _memServer = null;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="windowWidth"></param>
        /// <param name="windowHeight"></param>
        /// 
       protected override void OnImeCompositionRangeChanged(CefBrowser browser, CefRange selectedRange, CefRectangle[] characterBounds)
        {

        }

        public WorkerCefRenderHandler(int windowWidth, int windowHeight)
        {
            
            _windowWidth = windowWidth;
            _windowHeight = windowHeight;
        }

        protected override CefAccessibilityHandler GetAccessibilityHandler() {
            return null;
        }

        protected override bool GetRootScreenRect(CefBrowser browser, ref CefRectangle rect)
        {
            return GetViewRect(browser, ref rect);
        }

        protected override bool GetScreenPoint(CefBrowser browser, int viewX, int viewY, ref int screenX, ref int screenY)
        {
            screenX = viewX;
            screenY = viewY;
            return true;
        }

        protected override bool GetViewRect(CefBrowser browser, ref CefRectangle rect)
        {
            //see https://www.magpcss.org/ceforum/viewtopic.php?f=6&t=12835
            rect.X = 0;//_windowX;
            rect.Y = 0;//_windowY;
            rect.Width = _windowWidth;
            rect.Height = _windowHeight;
            return true;
        }

        protected override bool GetScreenInfo(CefBrowser browser, CefScreenInfo screenInfo)
        {
            return false;
        }

        protected override void OnPopupSize(CefBrowser browser, CefRectangle rect)
        {
        }

        /*Called when an element should be painted. |type| indicates whether the element is the view or the popup widget. |buffer| contains the pixel data for the whole image. 
         * |dirtyRects| contains the set of rectangles that need to be repainted. 
         * On Windows |buffer| will be width*height*4 bytes in size and represents a BGRA image with an upper-left origin.
         *  The CefBrowserSettings.animation_frame_rate value controls the rate at which this method is called.*/
        protected override void OnPaint(CefBrowser browser, CefPaintElementType type, CefRectangle[] dirtyRects, IntPtr buffer, int width, int height)
        {
            _copysize = width * height * 4;
            CurrentWidth = width;
            CurrentHeight = height;
            if (_memServer!=null)
                _memServer.PushTexture(buffer, _copysize);
            
        }

        //TODO: use this?
    protected override void OnCursorChange(CefBrowser browser, IntPtr cursorHandle, CefCursorType type, CefCursorInfo customCursorInfo)
        {
            
        }
        
        protected override void OnScrollOffsetChanged(CefBrowser browser, double x, double y)
        {
        }

        
    }
}