using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
namespace Genevo {
sealed class EdgeScroll : IDisposable {
    [StructLayout(LayoutKind.Sequential)]struct KeyInput {public ushort VirtualKey,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]struct MouseInput {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Explicit)]struct InputUnion {[FieldOffset(0)]public KeyInput Key;[FieldOffset(0)]public MouseInput Mouse;}
    [StructLayout(LayoutKind.Sequential)]struct Input {public uint Type;public InputUnion Value;}
    [DllImport("user32.dll")]static extern uint SendInput(uint n,Input[] input,int size);
    [DllImport("user32.dll")]static extern uint MapVirtualKey(uint key,uint kind);
    [DllImport("user32.dll")]static extern bool GetCursorPos(out Point point);
    readonly HashSet<int> held=new HashSet<int>();
    public static int[] Directions(Rectangle bounds,Point cursor,int zone){var result=new List<int>();if(!bounds.Contains(cursor))return result.ToArray();if(cursor.X<bounds.Left+zone)result.Add(0x25);else if(cursor.X>=bounds.Right-zone)result.Add(0x27);if(cursor.Y<bounds.Top+zone)result.Add(0x26);else if(cursor.Y>=bounds.Bottom-zone)result.Add(0x28);return result.ToArray();}
    static bool Key(int key,bool up){var input=new Input {Type=1,Value=new InputUnion {Key=new KeyInput {Scan=(ushort)MapVirtualKey((uint)key,0),Flags=(uint)(0x8|0x1|(up?0x2:0))}}};return SendInput(1,new[]{input},Marshal.SizeOf(typeof(Input)))==1;}
    public void Update(Rectangle bounds,bool active){Point cursor;var desired=new HashSet<int>();if(active&&GetCursorPos(out cursor)&&(Native.GetAsyncKeyState(1)&0x8000)==0&&(Native.GetAsyncKeyState(2)&0x8000)==0)foreach(int key in Directions(bounds,cursor,10))desired.Add(key);
        foreach(int key in new List<int>(held))if(!desired.Contains(key)){Key(key,true);held.Remove(key);}foreach(int key in desired)if(!held.Contains(key)&&(Native.GetAsyncKeyState(key)&0x8000)==0&&Key(key,false))held.Add(key);
    }
    public void Dispose(){foreach(int key in held)Key(key,true);held.Clear();}
}
}
