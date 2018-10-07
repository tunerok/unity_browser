using System;
using MessageLibrary;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleWebBrowser {
    public class WebBrowser : MonoBehaviour {
        #region General

        [Header("General settings")]
        public int Width = 1024;
        public int Height = 768;
        public string MemoryFile = "MainSharedMem";
        public bool SwapVericalAxis = false;
        public bool SwapHorisontalAxis = false;
        public bool RandomMemoryFile = true;
        public string InitialURL = "http://www.google.com";
        public string StreamingResourceName = string.Empty;

        public bool EnableWebRTC;

        [Header("Testing")] public bool EnableGPU;

        [Multiline] public string JSInitializationCode = "";

        #endregion

        private DialogEventType _dialogEventType;
        private string _dialogMessage = "";
        private string _dialogPrompt = "";


        private bool _focused;
        private string _jsQueryString = "";


        private BrowserEngine _mainEngine;


        private Material _mainMaterial;

        //status - threading
        private bool _setUrl;
        private string _setUrlString = "";

        //dialog states - threading
        private bool _showDialog;

        //query - threading
        private bool _startQuery;

        [SerializeField] public Canvas DialogCanvas;

        [Header("Dialog settings")] [SerializeField]
        public bool DialogEnabled;

        [SerializeField] public InputField DialogPrompt;

        [SerializeField] public Text DialogText;

        public bool KeepUIVisible;
        public Camera MainCamera;


        [Header("UI settings")] [SerializeField]
        public BrowserUI mainUIPanel;

        [SerializeField] public Button NoButton;

        [SerializeField] public Button OkButton;


        private int posX;
        private int posY;

        public bool UIEnabled = true;

        [SerializeField] public Button YesButton;

        T Search<T>(string name) {
            var child = transform.Find(name);
            if (child) {
                var component = child.gameObject.GetComponent<T>();
                return component;
            }
            return default(T);
        }
        //why Unity does not store the links in package?
        private void InitPrefabLinks() {
            if (mainUIPanel == null) {
                mainUIPanel = Search<BrowserUI>("MainUI");
            }
            
            if (DialogEnabled) {
                if (DialogCanvas == null)
                    DialogCanvas = gameObject.transform.Find("MessageBox").gameObject.GetComponent<Canvas>();
                if (DialogText == null)
                    DialogText = DialogCanvas.transform.Find("MessageText").gameObject.GetComponent<Text>();
                if (OkButton == null)
                    OkButton = DialogCanvas.transform.Find("OK").gameObject.GetComponent<Button>();
                if (YesButton == null)
                    YesButton = DialogCanvas.transform.Find("Yes").gameObject.GetComponent<Button>();
                if (NoButton == null)
                    NoButton = DialogCanvas.transform.Find("No").gameObject.GetComponent<Button>();
                if (DialogPrompt == null)
                    DialogPrompt = DialogCanvas.transform.Find("Prompt").gameObject.GetComponent<InputField>();
            }
        }

        private void Start() {
            _mainEngine = new BrowserEngine {
                dynamicRequestHandler = gameObject.GetComponent<IDynamicRequestHandler>()
            };

            if (RandomMemoryFile) {
                var memid = Guid.NewGuid();
                MemoryFile = memid.ToString();
            }


            //run initialization
            if (JSInitializationCode.Trim() != "")
                _mainEngine.RunJSOnce(JSInitializationCode);

            if (UIEnabled) {
                InitPrefabLinks();
                if(mainUIPanel!=null)
                mainUIPanel.InitPrefabLinks();
            }

            if (MainCamera == null) {
                MainCamera = Camera.main;
                if (MainCamera == null)
                    Debug.LogError("Error: can't find main camera");
            }

            


            if (UIEnabled && mainUIPanel!=null) {
                mainUIPanel.MainCanvas.worldCamera = MainCamera;
                mainUIPanel.KeepUIVisible = KeepUIVisible;
                if (!KeepUIVisible)
                    mainUIPanel.Hide();
            }

            //attach dialogs and querys
            _mainEngine.OnJavaScriptDialog += _mainEngine_OnJavaScriptDialog;
            _mainEngine.OnJavaScriptQuery += _mainEngine_OnJavaScriptQuery;
            _mainEngine.OnPageLoaded += _mainEngine_OnPageLoaded;
            _mainEngine.OnTextureObjectUpdated += OnTextureObjectUpdated;
            _mainEngine.StreamingResourceName = StreamingResourceName;


            if (DialogEnabled && DialogCanvas!=null) {
                DialogCanvas.worldCamera = MainCamera;
                DialogCanvas.gameObject.SetActive(false);
            }
			var initCoroutine = _mainEngine.InitPlugin(Width, Height, MemoryFile, InitialURL, EnableWebRTC, EnableGPU);
            StartCoroutine(initCoroutine);
        }
		private void OnTextureObjectUpdated(Texture2D newtexture) {
			_mainMaterial = GetComponent<MeshRenderer>().material;
            _mainMaterial.SetTexture("_MainTex", newtexture);
            _mainMaterial.SetTextureScale("_MainTex", new Vector2(SwapHorisontalAxis ? -1 : 1,SwapVericalAxis ? -1 : 1));
            Debug.Log("texture object updated");
        }

        private void _mainEngine_OnPageLoaded(string url) {
            _setUrl = true;
            _setUrlString = url;
        }

        //make it thread-safe
        private void _mainEngine_OnJavaScriptQuery(string message) {
            _jsQueryString = message;
            _startQuery = true;
        }

        public void RespondToJSQuery(string response) {
            _mainEngine.SendQueryResponse(response);
        }

        private void _mainEngine_OnJavaScriptDialog(string message, string prompt, DialogEventType type) {
            _showDialog = true;
            _dialogEventType = type;
            _dialogMessage = message;
            _dialogPrompt = prompt;
        }

        private void ShowDialog() {
            if (DialogEnabled) {
                switch (_dialogEventType) {
                    case DialogEventType.Alert: {
                        DialogCanvas.gameObject.SetActive(true);
                        OkButton.gameObject.SetActive(true);
                        YesButton.gameObject.SetActive(false);
                        NoButton.gameObject.SetActive(false);
                        DialogPrompt.text = "";
                        DialogPrompt.gameObject.SetActive(false);
                        DialogText.text = _dialogMessage;
                        break;
                    }
                    case DialogEventType.Confirm: {
                        DialogCanvas.gameObject.SetActive(true);
                        OkButton.gameObject.SetActive(false);
                        YesButton.gameObject.SetActive(true);
                        NoButton.gameObject.SetActive(true);
                        DialogPrompt.text = "";
                        DialogPrompt.gameObject.SetActive(false);
                        DialogText.text = _dialogMessage;
                        break;
                    }
                    case DialogEventType.Prompt: {
                        DialogCanvas.gameObject.SetActive(true);
                        OkButton.gameObject.SetActive(false);
                        YesButton.gameObject.SetActive(true);
                        NoButton.gameObject.SetActive(true);
                        DialogPrompt.text = _dialogPrompt;
                        DialogPrompt.gameObject.SetActive(true);
                        DialogText.text = _dialogMessage;
                        break;
                    }
                }

                _showDialog = false;
            }
        }

        #region Dialogs

        public void DialogResult(bool result) {
            if (DialogEnabled) {
                DialogCanvas.gameObject.SetActive(false);
                _mainEngine.SendDialogResponse(result, DialogPrompt.text);
            }
        }

        #endregion


        // Update is called once per frame
        private void Update() {
            if (_mainEngine == null)
                return;
            _mainEngine.UpdateTexture();


            //Dialog
            if (_showDialog) ShowDialog();

            //Query
            if (_startQuery) {
                _startQuery = false;
                if (OnJSQuery != null)
                    OnJSQuery(_jsQueryString);
            }

            //Status
            if (_setUrl) {
                _setUrl = false;
                if (UIEnabled && mainUIPanel!=null)
                    mainUIPanel.UrlField.text = _setUrlString;
            }

            if (UIEnabled)
                if (_focused && (mainUIPanel==null || !mainUIPanel.UrlField.isFocused)) //keys
                {
                    _mainEngine.ProcessKeyEvents();
                }

            _mainEngine.CheckMessage();
        }

        private void OnDisable() {
            if(_mainEngine!=null)
                _mainEngine.Shutdown();
        }


        public event BrowserEngine.PageLoaded OnPageLoaded;

        


        #region JS Query events

        public delegate void JSQuery(string query);

        public event JSQuery OnJSQuery;

        #endregion

        #region UI

        public void OnNavigate() {
            // MainUrlInput.isFocused
            _mainEngine.SendNavigateEvent(mainUIPanel.UrlField.text, false, false);
        }

        public void RunJavaScript(string js) {
            _mainEngine.SendExecuteJSEvent(js);
        }

        public void GoBackForward(bool forward) {
            if (forward)
                _mainEngine.SendNavigateEvent("", false, true);
            else
                _mainEngine.SendNavigateEvent("", true, false);
        }

        #endregion


        #region Events (3D)

        private void OnMouseEnter() {
            _focused = true;
			if(UIEnabled && mainUIPanel != null)
                mainUIPanel.Show();
        }

        private void OnMouseExit() {
            _focused = false;
			if(UIEnabled && mainUIPanel!=null)
                mainUIPanel.Hide();
        }

        private void OnMouseDown() {
            if (_mainEngine.Initialized) {
                var pixelUV = GetScreenCoords();

                if (pixelUV.x > 0)
                    SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Left, MouseEventType.ButtonDown);
            }
        }


        private void OnMouseUp() {
            if (_mainEngine.Initialized) {
                var pixelUV = GetScreenCoords();

                if (pixelUV.x > 0)
                    SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Left, MouseEventType.ButtonUp);
            }
        }

        private void OnMouseOver() {
            if (_mainEngine.Initialized) {
                var pixelUV = GetScreenCoords();

                if (pixelUV.x > 0) {
                    var px = (int) pixelUV.x;
                    var py = (int) pixelUV.y;

                    ProcessScrollInput(px, py);

                    if (posX != px || posY != py) {
                        var msg = new MouseMessage {
                            Type = MouseEventType.Move,
                            X = px,
                            Y = py,
                            GenericType = BrowserEventType.Mouse,
                            // Delta = e.Delta,
                            Button = MouseButton.None
                        };

                        if (Input.GetMouseButton(0))
                            msg.Button = MouseButton.Left;
                        if (Input.GetMouseButton(1))
                            msg.Button = MouseButton.Right;
                        if (Input.GetMouseButton(1))
                            msg.Button = MouseButton.Middle;

                        posX = px;
                        posY = py;
                        _mainEngine.SendMouseEvent(msg);
                    }

                    //check other buttons...
                    if (Input.GetMouseButtonDown(1))
                        SendMouseButtonEvent(px, py, MouseButton.Right, MouseEventType.ButtonDown);
                    if (Input.GetMouseButtonUp(1))
                        SendMouseButtonEvent(px, py, MouseButton.Right, MouseEventType.ButtonUp);
                    if (Input.GetMouseButtonDown(2))
                        SendMouseButtonEvent(px, py, MouseButton.Middle, MouseEventType.ButtonDown);
                    if (Input.GetMouseButtonUp(2))
                        SendMouseButtonEvent(px, py, MouseButton.Middle, MouseEventType.ButtonUp);
                }
            }

            // Debug.Log(pixelUV);
        }

        #endregion

        #region Helpers

        private Vector2 GetScreenCoords() {
            RaycastHit hit;
            if (!Physics.Raycast(MainCamera.ScreenPointToRay(Input.mousePosition), out hit))
                return new Vector2(-1f, -1f);
            var tex = _mainMaterial.mainTexture;

            var pixelUV = hit.textureCoord;
            pixelUV.x = tex.width * (SwapHorisontalAxis ? (1 - pixelUV.x) : pixelUV.x);
            pixelUV.y = tex.height* (SwapVericalAxis ? (1-pixelUV.y) : pixelUV.y);
            return pixelUV;
        }

        private void SendMouseButtonEvent(int x, int y, MouseButton btn, MouseEventType type) {
            var msg = new MouseMessage {
                Type = type,
                X = x,
                Y = y,
                GenericType = BrowserEventType.Mouse,
                // Delta = e.Delta,
                Button = btn
            };
            _mainEngine.SendMouseEvent(msg);
        }

        private void ProcessScrollInput(int px, int py) {
            var scroll = Input.GetAxis("Mouse ScrollWheel");

            scroll = scroll * _mainEngine.BrowserTexture.height;

            var scInt = (int) scroll;

            if (scInt != 0) {
                var msg = new MouseMessage {
                    Type = MouseEventType.Wheel,
                    X = px,
                    Y = py,
                    GenericType = BrowserEventType.Mouse,
                    Delta = scInt,
                    Button = MouseButton.None
                };

                if (Input.GetMouseButton(0))
                    msg.Button = MouseButton.Left;
                if (Input.GetMouseButton(1))
                    msg.Button = MouseButton.Right;
                if (Input.GetMouseButton(1))
                    msg.Button = MouseButton.Middle;

                _mainEngine.SendMouseEvent(msg);
            }
        }

        #endregion

        
    }
}