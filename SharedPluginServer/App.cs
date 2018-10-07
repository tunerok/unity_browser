using System;
using MessageLibrary;
namespace SharedPluginServer {
    interface IQueryHandler {
        void _mainWorker_OnJSDialog(string message, string prompt, DialogEventType type);
        void _mainWorker_OnBrowserJSQuery(string query);
        void SendDynamicPageQuery(string url, string query);
    }
    public class App : IQueryHandler
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private bool _enableWebRtc = false;

        private readonly SharedTextureWriter _memServer;

        //SharedMem comms
        private readonly MessageReader _inCommServer;
        private readonly MessageWriter _outCommServer;

        private readonly CefWorker _mainWorker;

        private DateTime _lastMessageTime = DateTime.UtcNow;
        

        public bool IsRunning;

        /// <summary>
        /// App constructor
        /// </summary>
        /// <param name="worker">Main CEF worker</param>
        /// <param name="memServer">Shared memory file</param>
        /// <param name="commServer">TCP server</param>
        
        public App(CefWorker worker, SharedTextureWriter memServer, MessageReader inServer, MessageWriter outServer, bool enableWebRtc)
        {
            //    _renderProcessHandler = new WorkerCefRenderProcessHandler();
            _enableWebRtc = enableWebRtc;

            _memServer = memServer;
            _mainWorker = worker;
            //init SharedMem comms
            _inCommServer = inServer;
            _outCommServer = outServer;

            _mainWorker.SetMemServer(_memServer);

            _mainWorker.SetQueryHandler(this);
            //attach page events
            _mainWorker.OnPageLoaded += _mainWorker_OnPageLoaded;

           

            IsRunning = true;

            
        }

        public void CheckMessage()
        {
            var ep = _inCommServer.TryRecive(100);
            if (ep != null)
                HandleMessage(ep);
            CheckExitTimer();
        }

     
        private void _mainWorker_OnPageLoaded(string url, int status)
        {
            // log.Info("Navigated to:"+url);

            GenericEvent msg = new GenericEvent()
            {
                StringContent = url,
                GenericType = BrowserEventType.Generic,
                Type = GenericEventType.PageLoaded
            };

            EventPacket ep = new EventPacket
            {
                Event = msg,
                Type = BrowserEventType.Generic
            };

            if (!_outCommServer.TrySend(ep, 100)) {
                log.Info("message send failed");
            }
        }

        //shut down by timer, in case of client crash/hang
        private void CheckExitTimer() {
            var delta = DateTime.UtcNow - this._lastMessageTime;
            if (delta <= TimeSpan.FromSeconds(10))
                return;
            try
            {
                log.Info("Exiting by timer,timeout:"+delta.TotalSeconds);
                log.Info("==============SHUTTING DOWN==========");
             
                _mainWorker.Shutdown();

                _memServer.Dispose();

               
                _inCommServer.Dispose();
                _outCommServer.Dispose();

                IsRunning = false;
             

            }
            catch (Exception ex)
            {

                log.Info("Exception on shutdown:" + ex.StackTrace);
            }
        }

        public void _mainWorker_OnBrowserJSQuery(string query)
        {
            GenericEvent msg = new GenericEvent()
            {
                StringContent = query,
                GenericType = BrowserEventType.Generic,
                Type = GenericEventType.JSQuery
            };

            EventPacket ep = new EventPacket
            {
                Event = msg,
                Type = BrowserEventType.Generic
            };
            _outCommServer.TrySend(ep,100);
        }

        public void SendDynamicPageQuery(string url, string parametres) {
            var msg = new GenericEvent()
            {
                GenericType = BrowserEventType.Generic,
                Type=GenericEventType.DynamicDataRequest,
                StringContent = url,
                AdditionalStringContent = parametres
            };

            var ep = new EventPacket
            {
                Event = msg,
                Type = BrowserEventType.Generic
            };
            _outCommServer.TrySend(ep,100);
        }
        public void _mainWorker_OnJSDialog(string message, string prompt, DialogEventType type)
        {
            DialogEvent msg = new DialogEvent()
            {
                DefaultPrompt = prompt,
                Message = message,
                Type = type,
                GenericType = BrowserEventType.Dialog
            };

            EventPacket ep = new EventPacket
            {
                Event = msg,
                Type = BrowserEventType.Dialog
            };
            _outCommServer.TrySend(ep,100);
        }

        public void ResetTimer() => _lastMessageTime = DateTime.UtcNow;

        /// <summary>
        /// Main message handler
        /// </summary>
        /// <param name="msg">Message from client app</param>
        public void HandleMessage(EventPacket msg) {
            ResetTimer();
            //reset timer
            switch (msg.Type)
            {
                case BrowserEventType.Ping:
                {
                    break;
                }

                case BrowserEventType.Generic:
                {
                    GenericEvent genericEvent=msg.Event as GenericEvent;
                    if (genericEvent != null)
                    {
                        switch (genericEvent.Type)
                        {
                            case GenericEventType.Shutdown:
                            {
                                try
                                {
                                    log.Info("==============SHUTTING DOWN==========");
                                   

                                    IsRunning = false;
                                          

                                }
                                catch (Exception e)
                                {

                                    log.Info("Exception on shutdown:"+e.StackTrace);
                                }

                                break;
                            }
                            case GenericEventType.Navigate:
                                _mainWorker.Navigate(genericEvent.StringContent,genericEvent.AdditionalStringContent);
                                break;

                            case GenericEventType.GoBack:
                                _mainWorker.GoBack();
                                break;

                            case GenericEventType.GoForward:
                                _mainWorker.GoForward();
                                break;

                            case GenericEventType.ExecuteJS:
                                _mainWorker.ExecuteJavaScript(genericEvent.StringContent);
                                break;
                            case GenericEventType.JSQueryResponse:
                                _mainWorker.AnswerQuery(genericEvent.StringContent);
                                break;
                            case GenericEventType.DynamicDataResponse:
                                _mainWorker.HandleDynamicDataResonse(genericEvent.StringContent,
                                    genericEvent.AdditionalStringContent);
                                break;
                        }
                    }
                    break;
                }

                case BrowserEventType.Dialog:
                {
                    DialogEvent de=msg.Event as DialogEvent;
                    if (de != null)
                    {
                        _mainWorker.ContinueDialog(de.success,de.input);
                    }
                    break;
                    
                }

                case BrowserEventType.Keyboard:
                {
                    KeyboardEvent keyboardEvent=msg.Event as KeyboardEvent;

                      

                    if (keyboardEvent != null)
                    {
                        if (keyboardEvent.Type != KeyboardEventType.Focus)
                            _mainWorker.KeyboardEvent(keyboardEvent.Key, keyboardEvent.Type);
                        else
                            _mainWorker.FocusEvent(keyboardEvent.Key);

                    }
                    break;
                }
                case BrowserEventType.Mouse:
                {
                    MouseMessage mouseMessage=msg.Event as MouseMessage;
                    if (mouseMessage != null)
                    {
                          
                        switch (mouseMessage.Type)
                        {
                            case MouseEventType.ButtonDown:
                                _mainWorker.MouseEvent(mouseMessage.X, mouseMessage.Y, false,mouseMessage.Button);
                                break;
                            case MouseEventType.ButtonUp:
                                _mainWorker.MouseEvent(mouseMessage.X, mouseMessage.Y, true,mouseMessage.Button);
                                break;
                            case MouseEventType.Move:
                                _mainWorker.MouseMoveEvent(mouseMessage.X, mouseMessage.Y, mouseMessage.Button);
                                break;
                            case MouseEventType.Leave:
                                _mainWorker.MouseLeaveEvent();
                                break;
                            case MouseEventType.Wheel:
                                _mainWorker.MouseWheelEvent(mouseMessage.X,mouseMessage.Y,mouseMessage.Delta);
                                break;
                        }
                    }

                    break;
                }
            }

           
        }

     
    }
}