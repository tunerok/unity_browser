using System;
using System.Runtime.InteropServices;
using SharedMemory;
public class SharedMemoryArray : SharedArray<byte>
{
    public SharedMemoryArray(string name) : base(name) {

    }
    public SharedMemoryArray(string name, int length) : base(name, length) {

    }
    public unsafe IntPtr UnsafeDataPointer() {
        return new IntPtr(BufferStartPtr);
    }
    public unsafe byte * UnsafeDataPointerPtr() {
        return BufferStartPtr;
    }

    public void MarkDataReaded() {
        this.ReadWaitEvent.Reset(); //this content already readed
    }
    public void MarkDataWrited() {
        this.WriteWaitEvent.Reset(); //this content already writed and not processed
    }
}

