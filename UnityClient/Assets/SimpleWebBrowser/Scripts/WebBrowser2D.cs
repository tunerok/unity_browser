using System;
using UnityEngine;
using System.Collections;
using System.Text;
//using System.Diagnostics;
using MessageLibrary;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleWebBrowser
{
    public class WebBrowser2D : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler
    {

        #region General

        [Header("General settings")]
        public int Width = 1024;
        public int Height = 768;
        public bool AutoFitParent = true;
        public string MemoryFile = "MainSharedMem";

        public bool RandomMemoryFile = true;

        public string InitialURL = "http://www.google.com";
        public string StreamingResourceName = string.Empty;

        public bool EnableWebRTC = false;

        [Header("Testing")]
        public bool EnableGPU = false;

        [Multiline]
        public string JSInitializationCode = "";

        #endregion


        [Header("2D setup")]
        [SerializeField]
        public RawImage Browser2D = null;


        [Header("UI settings")]
        [SerializeField]
        public BrowserUI mainUIPanel;

        public bool KeepUIVisible = false;
		public bool UIEnabled = true;
        [Header("Dialog settings")]
        [SerializeField]
        public GameObject DialogPanel;
        [SerializeField]
        public Text DialogText;
        [SerializeField]
        public Button OkButton;
        [SerializeField]
        public Button YesButton;
        [SerializeField]
        public Button NoButton;
        [SerializeField]
        public InputField DialogPrompt;

        //dialog states - threading
        private bool _showDialog = false;
        private string _dialogMessage = "";
        private string _dialogPrompt = "";
        private DialogEventType _dialogEventType;
        //query - threading
        private bool _startQuery = false;
        private string _jsQueryString = "";

        //status - threading
        private bool _setUrl = false;
        private string _setUrlString = "";


        #region JS Query events

        public delegate void JSQuery(string query);

        public event JSQuery OnJSQuery;

        #endregion

        private BrowserEngine _mainEngine;

        private bool _focused = false;

        private int posX = 0;
        private int posY = 0;

        private Camera _mainCamera;

        #region Initialization

        //why Unity does not store the links in package?
        T Search<T>(string name) {
            var child = transform.Find(name);
            if (child) {
                var component = child.gameObject.GetComponent<T>();
                return component;
            }
            return default(T);
        }

        void InitPrefabLinks() {
            if (mainUIPanel == null) {
                mainUIPanel = Search<BrowserUI>("MainUI");
            }
            if (Browser2D == null)
                Browser2D = gameObject.GetComponent<RawImage>();
            

            if (DialogPanel == null) {
                var messagebox = transform.Find("MessageBox");
                if(messagebox)
                    DialogPanel = messagebox.gameObject;
            }

            if (DialogText == null)
                    DialogText = Search<Text>("MessageText");
            if (OkButton == null)
                    OkButton = Search<Button>("OK");
            if (YesButton == null)
                    YesButton = Search<Button>("Yes");
            if (NoButton == null)
                    NoButton = Search<Button>("No");
            if (DialogPrompt == null)
                    DialogPrompt = Search<InputField>("Prompt");
            Debug.Log("Init prefab completed");
        }


        


        void Start() {
            
            Debug.Log("Browser2d start");
            if (AutoFitParent) {
                var pixsource = transform as RectTransform;
                var rect = pixsource.rect;
                Width = (int)rect.width;
                Height=(int)rect.height;
                Debug.LogFormat("Browser2d resize to {0}x{1}", Width, Height);
            }

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

            if (UIEnabled){
                InitPrefabLinks();
                if(mainUIPanel!=null)
                mainUIPanel.InitPrefabLinks();
            }

            var parentcanvas = GetComponentInParent<Canvas>();
            if (parentcanvas != null) {
                _mainCamera = parentcanvas.worldCamera; //get camera assigned to parent canvas
            }
            if(_mainCamera==null)  //try to get default but this completely wrong
            _mainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();

            if (mainUIPanel != null) {
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

            if(DialogPanel!=null)
            DialogPanel.SetActive(false);
            IEnumerator initCoroutine = _mainEngine.InitPlugin(Width, Height, MemoryFile, InitialURL, EnableWebRTC, EnableGPU);
            StartCoroutine(initCoroutine);


        }

        private void OnTextureObjectUpdated(Texture2D newtexture) {
            Debug.Log("start update");
            Browser2D.texture = newtexture;
            Browser2D.uvRect = new Rect(0f, 0f, 1f, -1f);
            Debug.Log("texture object updated");
        }

        private void _mainEngine_OnPageLoaded(string url)
        {
            _setUrl = true;
            _setUrlString = url;
        }

        #endregion

        #region Queries and dialogs

        //make it thread-safe
        private void _mainEngine_OnJavaScriptQuery(string message)
        {
            _jsQueryString = message;
            _startQuery = true;
        }

        public void RespondToJSQuery(string response)
        {
            _mainEngine.SendQueryResponse(response);
        }

        private void _mainEngine_OnJavaScriptDialog(string message, string prompt, DialogEventType type)
        {
            _showDialog = true;
            _dialogEventType = type;
            _dialogMessage = message;
            _dialogPrompt = prompt;

        }

        private void ShowDialog()
        {

            switch (_dialogEventType)
            {
                case DialogEventType.Alert:
                {
                    DialogPanel.SetActive(true);
                    OkButton.gameObject.SetActive(true);
                    YesButton.gameObject.SetActive(false);
                    NoButton.gameObject.SetActive(false);
                    DialogPrompt.text = "";
                    DialogPrompt.gameObject.SetActive(false);
                    DialogText.text = _dialogMessage;
                    break;
                }
                case DialogEventType.Confirm:
                {
                    DialogPanel.SetActive(true);
                    OkButton.gameObject.SetActive(false);
                    YesButton.gameObject.SetActive(true);
                    NoButton.gameObject.SetActive(true);
                    DialogPrompt.text = "";
                    DialogPrompt.gameObject.SetActive(false);
                    DialogText.text = _dialogMessage;
                    break;
                }
                case DialogEventType.Prompt:
                {
                    DialogPanel.SetActive(true);
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

        public void DialogResult(bool result)
        {
            DialogPanel.SetActive(false);
            _mainEngine.SendDialogResponse(result, DialogPrompt.text);

        }

       

        #endregion

        #region UI

        public void OnNavigate()
        {
            if(mainUIPanel!=null)
            _mainEngine.SendNavigateEvent(mainUIPanel.UrlField.text, false, false);

        }

        public void Navigate(string url) {
            if (mainUIPanel != null) {
                mainUIPanel.UrlField.text = url;
            }

            if (_mainEngine != null) {
                
                _mainEngine.SendNavigateEvent(url, false, false);
            }

            InitialURL = url;
        }

 public void RunJavaScript(string js)
        {
            _mainEngine.SendExecuteJSEvent(js);
        }
        public void GoBackForward(bool forward)
        {
            if (forward)
                _mainEngine.SendNavigateEvent("", false, true);
            else
                _mainEngine.SendNavigateEvent("", true, false);
        }

        #endregion




        #region Events 

        public void OnPointerEnter(PointerEventData data)
        {
            _focused = true;
			if(UIEnabled && mainUIPanel != null)
            	mainUIPanel.Show();
            StartCoroutine("TrackPointer");
        }

        public void OnPointerExit(PointerEventData data)
        {
            _focused = false;
			if(UIEnabled && mainUIPanel!=null)
            	mainUIPanel.Hide();
            StopCoroutine("TrackPointer");
        }

        //tracker
        IEnumerator TrackPointer()
        {
            var _raycaster = GetComponentInParent<GraphicRaycaster>();
            var _input = FindObjectOfType<StandaloneInputModule>();

            if (_raycaster != null && _input != null && _mainEngine.Initialized)
            {
                while (Application.isPlaying)
                {
                    Vector2 localPos = GetScreenCoords(_raycaster, _input);

                    int px = (int) localPos.x;
                    int py = (int) localPos.y;

                    ProcessScrollInput(px, py);

                    if (posX != px || posY != py)
                    {
                        MouseMessage msg = new MouseMessage
                        {
                            Type = MouseEventType.Move,
                            X = px,
                            Y = py,
                            GenericType = MessageLibrary.BrowserEventType.Mouse,
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

                    yield return 0;
                }
            }
            //  else
            //      UnityEngine.Debug.LogWarning("Could not find GraphicRaycaster and/or StandaloneInputModule");
        }

        public void OnPointerDown(PointerEventData data)
        {

            if (_mainEngine.Initialized)
            {
                var _raycaster = GetComponentInParent<GraphicRaycaster>();
                var _input = FindObjectOfType<StandaloneInputModule>();
                Vector2 pixelUV = GetScreenCoords(_raycaster, _input);

                switch (data.button)
                {
                    case PointerEventData.InputButton.Left:
                    {
                        SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Left,
                            MouseEventType.ButtonDown);
                        break;
                    }
                    case PointerEventData.InputButton.Right:
                    {
                        SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Right,
                            MouseEventType.ButtonDown);
                        break;
                    }
                    case PointerEventData.InputButton.Middle:
                    {
                        SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Middle,
                            MouseEventType.ButtonDown);
                        break;
                    }
                }


            }

        }




        public void OnPointerUp(PointerEventData data)
        {

            if (_mainEngine.Initialized)
            {
                var _raycaster = GetComponentInParent<GraphicRaycaster>();
                var _input = FindObjectOfType<StandaloneInputModule>();

                Vector2 pixelUV = GetScreenCoords(_raycaster, _input);

                switch (data.button)
                {
                    case PointerEventData.InputButton.Left:
                    {
                        SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Left, MouseEventType.ButtonUp);
                        break;
                    }
                    case PointerEventData.InputButton.Right:
                    {
                        SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Right,
                            MouseEventType.ButtonUp);
                        break;
                    }
                    case PointerEventData.InputButton.Middle:
                    {
                        SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Middle,
                            MouseEventType.ButtonUp);
                        break;
                    }
                }


            }

        }



        #endregion

        #region Helpers

        private Vector2 GetScreenCoords(GraphicRaycaster ray, StandaloneInputModule input)
        {
            var mousepos = Input.mousePosition;
            var relativeToDisplayPosition = Display.RelativeMouseAt(mousepos);
            if (relativeToDisplayPosition != Vector3.zero) {
                if (ray.eventCamera.targetDisplay == (int) relativeToDisplayPosition.z)
                    mousepos = relativeToDisplayPosition;
                else 
                    return new Vector2(-1,-1); //this click not on our display                    
            }

            Vector2 localPos; // Mouse position  
            RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, mousepos,
                ray.eventCamera, out localPos);

            // local pos is the mouse position.
            RectTransform trns = transform as RectTransform;
            localPos.y = trns.rect.height - localPos.y;
            //Debug.Log("x:"+localPos.x+",y:"+localPos.y);

            //now recalculate to texture
            localPos.x = (localPos.x*Width)/trns.rect.width;
            localPos.y = (localPos.y*Height)/trns.rect.height;

            return localPos;

        }

        private void SendMouseButtonEvent(int x, int y, MouseButton btn, MouseEventType type)
        {
            MouseMessage msg = new MouseMessage
            {
                Type = type,
                X = x,
                Y = y,
                GenericType = MessageLibrary.BrowserEventType.Mouse,
                // Delta = e.Delta,
                Button = btn
            };
            _mainEngine.SendMouseEvent(msg);
        }

        private void ProcessScrollInput(int px, int py)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            scroll = scroll*_mainEngine.BrowserTexture.height;

            int scInt = (int) scroll;

            if (scInt != 0)
            {
                MouseMessage msg = new MouseMessage
                {
                    Type = MouseEventType.Wheel,
                    X = px,
                    Y = py,
                    GenericType = MessageLibrary.BrowserEventType.Mouse,
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
        // Update is called once per frame
        void Update() {
            
            if (_mainEngine == null)
                return;
            
            _mainEngine.UpdateTexture();

            #region 2D mouse

            if (Browser2D != null)
            {
                //GetScreenCoords(true);
            }


            #endregion

            //Dialog
            if (_showDialog)
            {
                ShowDialog();
            }

            //Query
            if (_startQuery)
            {
                _startQuery = false;
                if (OnJSQuery != null)
                    OnJSQuery(_jsQueryString);
            }

            //Status
            if (_setUrl)
            {
                _setUrl = false;
                if(UIEnabled && mainUIPanel!=null)
                mainUIPanel.UrlField.text = _setUrlString;

            }

if (UIEnabled)
            {
                if (_focused && (mainUIPanel==null || !mainUIPanel.UrlField.isFocused)) //keys
                {
                    _mainEngine.ProcessKeyEvents();
                }
            }

			_mainEngine.CheckMessage();
        }

        

        void OnDisable()
        {
            Debug.Log("browser 2d disable");
            if (_mainEngine != null) {
                _mainEngine.Shutdown();
                _mainEngine = null;
            }
        }
    }
}