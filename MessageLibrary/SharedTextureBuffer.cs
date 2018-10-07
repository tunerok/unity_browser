using System;
using System.Runtime.InteropServices;
using SharedMemory;

public class SharedTextureBuffer : SharedArray<byte>
{
    public SharedTextureBuffer(string name) : base(name) {
    }
    public SharedTextureBuffer(string name, int length): base(name, length) {
    }
    public unsafe IntPtr UnsafeDataPointer() {
        return new IntPtr(BufferStartPtr);
    }

    public void MarkProcessed() {
        this.ReadWaitEvent.Reset();
    }
}
public class SharedTextureWriter : IDisposable
{
    private SharedTextureBuffer _buffer;
    public SharedTextureWriter(string name, int length) {
        _buffer = new SharedTextureBuffer(name, length);
    }

    public void PushTexture(IntPtr from, int size) {
        if (_buffer != null) {
            Resize(size);
            //if (_buffer.AcquireWriteLock(10000)) { //don't wait on write lock, no VSync needed
            CopyMemory(_buffer.UnsafeDataPointer(), from, (uint) size);
            _buffer.ReleaseWriteLock();
        }

    }

    public void Dispose() {
        if (_buffer != null) {
            _buffer.Close();
            _buffer = null;
        }
    }
    private void Resize(int newSize) {
        if (_buffer.Length != newSize) {
            var name = _buffer.Name;
            _buffer.Close();
            _buffer = new SharedTextureBuffer(name, newSize);
        }
    }
    //use this windows function to avoid double copy
    [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
    public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);
}