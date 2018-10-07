using System.Collections;
using System.Collections.Generic;
using SimpleWebBrowser;
using UnityEngine;

public class BaseDynamicHandler : MonoBehaviour,IDynamicRequestHandler {
    public virtual string Request(string url, string query) {
        return null;
    }
}
