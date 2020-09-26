using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

public struct RGB8888Job : IJobParallelFor
{
    public NativeArray<Int32> pixelarray;
    public NativeArray<Color32> color32array;
    public void Execute(int index)
    {
        int packed = pixelarray[index];

        color32array[index] = new Color32((Byte)((packed >> 16) & 0x00FF), (Byte)((packed >> 8) & 0x00FF), (Byte)(packed & 0x00FF), (Byte)((packed >> 24) & 0x00FF));
    }
}