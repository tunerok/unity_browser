using System;


namespace MessageLibrary
{
    public enum BrowserEventType
    {
        Ping=-1,
        Generic=0,
        Mouse=1,
        Keyboard=2,
        Dialog = 3,
        StopPacket=4
    }

    [Serializable]
    public abstract class AbstractEvent
    {
        public BrowserEventType GenericType;
    }

    [Serializable]
    public class EventPacket
    {
        public BrowserEventType Type;

        public AbstractEvent Event;

      /*  public static bool operator != (EventPacket ep1, EventPacket ep2)
        {
            return !(ep1.Type == ep2.Type && ep1.Event != ep2.Event);
        }

        public static bool operator ==(EventPacket ep1, EventPacket ep2)
        {
            return (ep1.Type == ep2.Type && ep1.Event != ep2.Event);
        }*/
    }

    public enum GenericEventType
    {
        Shutdown,
        Navigate,
        GoBack,
        GoForward,
        ExecuteJS,
        JSQuery,
        JSQueryResponse,
        PageLoaded,
        DynamicDataRequest,
        DynamicDataResponse,
    }

   

    [Serializable]
    public class GenericEvent : AbstractEvent
    {
        public GenericEventType Type;
        public string StringContent;
        public string AdditionalStringContent;
    }
}
