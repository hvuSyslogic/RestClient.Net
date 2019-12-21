﻿namespace RestClientDotNet.Abstractions
{
    public interface IZip
    {
        byte[] Unzip(byte[] compressedData);
        byte[] Zip(byte uncompressedData);
    }
}