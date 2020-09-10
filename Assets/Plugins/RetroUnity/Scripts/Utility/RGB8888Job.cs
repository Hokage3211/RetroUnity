using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

public unsafe struct ARGB8888Job : IJob
{
    [ReadOnly] [NativeDisableUnsafePtrRestriction] public uint* SourceData;
    [ReadOnly] public int Width;
    [ReadOnly] public int Height;
    [ReadOnly] public uint PitchPixels;
    [WriteOnly] public NativeArray<uint> TextureData;

    public void Execute()
    {
        uint* line = SourceData;
        for (int y = 0; y < Height; ++y)
        {
            for (int x = 0; x < Width; ++x)
            {
                TextureData[y * Width + x] = line[x];
            }
            line += PitchPixels / 4;
        }
    }
}