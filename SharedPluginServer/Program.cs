#define USE_ARGS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using MessageLibrary;
using Xilium.CefGlue;

namespace SharedPluginServer
{

    //Main application


    static class Program
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static CommandLineParametres parametres = new CommandLineParametres {
            kWidth = 1280,
            kHeight = 720,
            InitialUrl = "http://test.webrtc.org",
            SharedFileName = "MainSharedMem",
            inCommFile = "InSharedMem",
            outCommFile = "OutSharedMem",
            _enableWebRTC = false,
            _enableGPU = false
        };
        static readonly string temporaryDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        

        [STAThread]
        static int Main(string[] args) {
            var version=Environment.Version;
            Log.InfoFormat("start at current directory {0}",Directory.GetCurrentDirectory() );
            Log.Info("parsing command args");
            
            if (!ParseCommandLine(args)) {
                Log.Error("command line parse error");
                return 0;
            }
            Log.InfoFormat("Starting plugin, settings:{0}",parametres);

            Log.InfoFormat("Statring cef runtime");

            if (!CefRintimePrepare(args, temporaryDirectoryPath)) {
                Log.Error("cef runtime initialisation failed");
                return 0; //immediate exit
            }


            try {
                Log.Info("starting cef worker");
                var worker = new CefWorker(parametres.SharedFileName);
                    worker.Init(parametres);
                    Log.Info("Binding shared memory");
                    SharedTextureWriter server = new SharedTextureWriter(parametres.SharedFileName,parametres.TextureSize);
                    MessageReader inSrv = MessageReader.Create(parametres.inCommFile, 10000);
                    MessageWriter outSrv = MessageWriter.Create(parametres.outCommFile, 10000);
                    Log.Info("complete to bind shared memory, ready and wait");
                    var app = new App(worker, server, inSrv, outSrv, false);
                    Log.Info("Enter main loop");
                    try {
                        while (app.IsRunning) {
                            Application.DoEvents();
                            app.CheckMessage(); //check incoming messages
                        }
                    }
                    catch (Exception e) {
                        Log.ErrorFormat("abnormal exit main loop{0}", e.Message);
                    }

                    Log.Info("Exit main loop END DISPOSING ALL");
                    worker.Dispose();
                    server.Dispose();
                    inSrv.Dispose();
                    outSrv.Dispose();
            }
            catch (Exception e) {
                Log.ErrorFormat("Unclean exit error {0}", e.Message);
            }
            GC.Collect();
            GC.WaitForFullGCComplete(-1);
            Log.Info("CefRuntime.Shutdown");
            CefRuntime.Shutdown();
            Log.Info("Final exit");
            return 0;
        }

        
        static bool ParseCommandLine(string[] args) {
            try {
                var newparametres = CommandLineParametres.Decode(args[0]);
                if (newparametres != null) {
                    parametres = newparametres;
                }
                    
            }
            catch (Exception ex) {
                Log.ErrorFormat("{0} error", ex.Message);
            }
            

            return true;
        }
        
        static bool CefRintimePrepare(string[] args,string temporaryDirectoryPath) {
            
            try {
                
                string path = Directory.GetCurrentDirectory();
                var runtimepath = path;
                
                var clientpath = Path.Combine(runtimepath, "cefclient.exe");
                Log.InfoFormat("using client path {0}", clientpath);
                var resourcepath = runtimepath;
                var localepath = Path.Combine(resourcepath, "locales");
                Log.Info("===============START================");
                CefRuntime.Load(runtimepath); //using native render helper
                Log.Info("appending disable cache keys");
                CefMainArgs cefMainArgs = new CefMainArgs(args) {};
                if (parametres._enableWebRTC) {
                    Log.Info("starting with webrtc");
                }
                var cefApp = new WorkerCefApp(parametres._enableWebRTC, parametres._enableGPU);
                int exit_code = CefRuntime.ExecuteProcess(cefMainArgs, cefApp, IntPtr.Zero);

                if (exit_code >= 0) {
                    Log.ErrorFormat("CefRuntime return " + exit_code);
                    return false;
                }
                var cefSettings = new CefSettings
                {
                    SingleProcess = false,
                    MultiThreadedMessageLoop = true,
                    WindowlessRenderingEnabled = true,
                    //
                    BrowserSubprocessPath = clientpath,
                    FrameworkDirPath = runtimepath,
                    ResourcesDirPath = resourcepath,
                    LocalesDirPath = localepath,
                    LogFile = Path.Combine(Path.GetTempPath(), "cefruntime-"+Guid.NewGuid() + ".log"),
                    Locale = "en-US",
                    LogSeverity = CefLogSeverity.Error,
                    //RemoteDebuggingPort = 8088,
                    NoSandbox = true,
                    //CachePath = temporaryDirectoryPath

                };
                CefRuntime.Initialize(cefMainArgs, cefSettings, cefApp, IntPtr.Zero);
                /////////////
            }
            catch (Exception ex) {
                Log.Info("EXCEPTION ON CEF INITIALIZATION:" + ex.Message + "\n" + ex.StackTrace);
                return false;
            }
            return true;
        }
    }
}
