using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using ResourceCollection;
using Xilium.CefGlue;

namespace XiliumXWT
{
    internal interface IInternalHttpRequestHandler {
        string Request(string url, string query);
    }
    internal class MySchemeHandlerFactory : CefSchemeHandlerFactory
    {
        private readonly IInternalHttpRequestHandler _callback;
        private readonly HttpResources _sources;

        public MySchemeHandlerFactory(HttpResources sources,IInternalHttpRequestHandler callback)
        {
            _sources = sources;
            _callback = callback;
        }

        void DecodeUrlEncodedQuery(string query,Dictionary<string, string> target) {
            var s = Uri.UnescapeDataString(query);

            /*var query = UriExtensions.ParseQueryString(query);
            for (var i = 0; i < query.Count; i++)
            {
                var key = query.Keys[i];
                var value = query[key];
                recivedCollection.Add(key, value);
            }*/
        }
        private ResourceInfo HandleDynamicRequest(string path, CefRequest request)
        {
            if (_callback == null)
                return null;
            try {
                var query = string.Empty;
               var collection = request.PostData;
               if (collection != null)
                {
                    var recivedCollection = new Dictionary<string, string>();
                    foreach (var elem in collection.GetElements())
                        if (elem.ElementType == CefPostDataElementType.Bytes)
                        {
                            var bytes = elem.GetBytes();
                            query = Encoding.UTF8.GetString(bytes);
                        }
                }
                var respinse=_callback.Request(path,query);
                var buf = Encoding.UTF8.GetBytes(respinse);
                return new ResourceInfo(buf,"application/json");
            }
            catch (Exception e)
            {
            }
            return null;
        }

        protected override CefResourceHandler Create(CefBrowser browser, CefFrame frame, string schemeName,
            CefRequest request)
        {
            var uri = new Uri(request.Url);
            var pathpart = uri.AbsolutePath;
            var staticresoyurce = TryResolve(pathpart);
            if (staticresoyurce != null)
                return staticresoyurce;
            var resoure=HandleDynamicRequest(pathpart, request);
            if(resoure!=null)
                return new MySchemeHandler(resoure);
            return null;
        }

        private CefResourceHandler TryResolve(string pathpart)
        {
            var retval = _sources.Search(pathpart);
            return retval?.Resource?.Length > 0 ? new MySchemeHandler(retval) : null;
        }
    }

    internal class MySchemeHandler : CefResourceHandler
    {
        private readonly ResourceInfo _source;
        private bool _completed;
        private int _transmittedLen;

        public MySchemeHandler(ResourceInfo source){
            _source = source;
        }

        protected override bool ProcessRequest(CefRequest request, CefCallback callback){
            callback.Continue();
            _completed = false;
            _transmittedLen = 0;
            return true;
        }

        private static readonly NameValueCollection Staticfileheader=new NameValueCollection(StringComparer.InvariantCultureIgnoreCase)
        {
            {"Cache-Control", "max-age=3600"},
            {"Access-Control-Allow-Origin", "*"}
        };

        private static readonly NameValueCollection Dynamicfileheader=new NameValueCollection(StringComparer.InvariantCultureIgnoreCase)
        {
            {"Cache-Control", "no-cache, no-store, must-revalidate"},
            {"Access-Control-Allow-Origin", "*"}
        };
        protected override void GetResponseHeaders(CefResponse response, out long responseLength, out string redirectUrl)
        {
            response.SetHeaderMap(_source.IsStatic ? Staticfileheader : Dynamicfileheader);
            response.Status = 200;
            response.MimeType = _source.Mime; // "text/html";
            response.StatusText = "OK";
            responseLength = _source.Resource.Length; // unknown content-length
            redirectUrl = null; // no-redirect
        }

        protected override bool ReadResponse(Stream response, int bytesToRead, out int bytesRead, CefCallback callback)
        {
            if (_completed)
            {
                bytesRead = 0;
                return false;
            }
            // very simple response with one block
            var transmitSize = Math.Min(_source.Resource.Length - _transmittedLen, bytesToRead);

            response.Write(_source.Resource, _transmittedLen, transmitSize);
            bytesRead = transmitSize;
            _transmittedLen += transmitSize;
            if (_transmittedLen == _source.Resource.Length)
                _completed = true;
            if (!_completed)
                callback.Continue();
            return true;
        }

        protected override bool CanGetCookie(CefCookie cookie) => false;
        protected override bool CanSetCookie(CefCookie cookie) => false;

        protected override void Cancel(){}
    }
}