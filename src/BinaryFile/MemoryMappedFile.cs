﻿using System.IO;

namespace BinarySerializer
{
    public class MemoryMappedFile : BinaryFile 
    {
        public MemoryMappedFile(Context context, string filePath, uint baseAddress, Endian endianness = Endian.Little, long fileLength = 0) : base(context, filePath, endianness, baseAddress)
        {
            length = fileLength;
        }

		public override Reader CreateReader() {
			Stream s = FileManager.GetFileReadStream(AbsolutePath);
			length = s.Length;
			Reader reader = new Reader(s, isLittleEndian: Endianness == Endian.Little);
			return reader;
		}

		public override Writer CreateWriter() {
			CreateBackupFile();
			Stream s = FileManager.GetFileWriteStream(AbsolutePath, RecreateOnWrite);
			length = s.Length;
			Writer writer = new Writer(s, isLittleEndian: Endianness == Endian.Little);
			return writer;
		}

		private long length;
        public override long Length
        {
			get
            {
                if (length == 0)
                {
                    using Stream s = FileManager.GetFileReadStream(AbsolutePath);
                    length = s.Length;
                }
                return length;
            }
		}

        public override BinaryFile GetPointerFile(long serializedValue, Pointer anchor = null) => GetMemoryMappedPointerFile(serializedValue, anchor);
    }
}
