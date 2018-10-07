using System;
using MessageLibrary;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

public class MessageWriter : IDisposable{
    private object _locable = new object();
    private SharedMemoryArray _buf;
    private MessageWriter() {
    }

    public static MessageWriter Open(string name) {
        var buf = new SharedMemoryArray(name);
        return new MessageWriter()
        {
            _buf = buf
        };
    }
    public static MessageWriter Create(string name,int size) {
        var buf = new SharedMemoryArray(name, size);
        buf.MarkDataReaded(); //mark this don't still don't have any data
        return new MessageWriter()
        {
            _buf = buf
        };
        
    }

    unsafe bool InternalWritePipe(EventPacket packet) {
        try 
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (UnmanagedMemoryStream stream =
                new UnmanagedMemoryStream(_buf.UnsafeDataPointerPtr(), 0, _buf.Length, FileAccess.Write)) {
                bf.Serialize(stream, packet);
            }
            return true;
        }
        catch (Exception ex)
        {

        }
        return false;
    }
    public bool TrySend(EventPacket packet, int millesecondToWait) {
        bool result = false;
        lock (_locable) {
            if (_buf.AcquireWriteLock(millesecondToWait)) {
                result = InternalWritePipe(packet);
                if (result)
                    _buf.MarkDataWrited();
               _buf.ReleaseWriteLock();
            }
        }

        return result;
    }

    public void Dispose() {
        _buf?.Dispose();
    }
}
public class MessageReader : IDisposable {
    private SharedMemoryArray _buf;
    private MessageReader() {
    }
    public static MessageReader Open(string name) {
        var buf = new SharedMemoryArray(name);
        return new MessageReader()
        {
            _buf = buf
        };
    }
    public static  MessageReader Create(string name, int size) {
        var buf = new SharedMemoryArray(name, size);
        return new MessageReader()
        {
            _buf = buf
        };
    }

    private unsafe EventPacket InternalReadPipe() {
        try {
            BinaryFormatter bf = new BinaryFormatter();
            using (UnmanagedMemoryStream stream = new UnmanagedMemoryStream(_buf.UnsafeDataPointerPtr(), _buf.Count)) {
                EventPacket ep = bf.Deserialize(stream) as EventPacket;
                if (ep != null && ep.Type != BrowserEventType.StopPacket)
                    return ep;
            }
        }
        catch (Exception ex) {
        }
        return null;
    }
    public unsafe EventPacket TryRecive(int millesecondToWait) {
        EventPacket result = null;
        try {
            if (_buf.AcquireReadLock(millesecondToWait)) {
                result = InternalReadPipe();
                _buf.MarkDataReaded();  //mark as readed
                _buf.ReleaseReadLock();
            }
        }
        catch(Exception e) {

        }
        
        return result;
    }

    public void Dispose() {
        _buf?.Dispose();
    }
}

