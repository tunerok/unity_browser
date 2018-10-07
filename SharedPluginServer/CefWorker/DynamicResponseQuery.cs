using System;
using System.Collections.Generic;
using System.Threading;

namespace SharedPluginServer {
    class DynamicResponseBinding {
        public string Url;
        public string Result;
        private readonly ManualResetEvent _waitEvent=new ManualResetEvent(false);

        public bool Wait(TimeSpan period) {
            return _waitEvent.WaitOne(period);
        }

        public void PushResponse(string lresult) {
            Result = lresult;
            _waitEvent.Set();
        }
    }
    class DynamicResponseQuery {
        private readonly List<DynamicResponseBinding> _internalQuery = new List<DynamicResponseBinding>();

        public DynamicResponseBinding AddRequest(string url, string querystring) {
            var binding = new DynamicResponseBinding {Url = url};
            lock(_internalQuery)
                _internalQuery.Add(binding);
            return binding;
        }

        public void PushQueryResonse(string url, string genericEventAdditionalStringContent) {
            lock (_internalQuery) {
                var primary = _internalQuery.Find(e => e.Url == url);
                if (primary != null) {
                    _internalQuery.Remove(primary);
                    primary.PushResponse(genericEventAdditionalStringContent);
                }
            }
        }

        public void RemoveExpired(DynamicResponseBinding item) {
            lock (_internalQuery) {
                _internalQuery.Remove(item);
            }
        }
    }
}