﻿namespace BinarySerializer
{
    public class LinearFile : PhysicalFile 
    {
        public LinearFile(Context context, string filePath, Endian endianness = Endian.Little, long? fileLength = null) : base(context, filePath, endianness, fileLength: fileLength) { }

        public override bool IsMemoryMapped => false;
    }
}