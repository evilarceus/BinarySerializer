﻿using System.IO;

namespace BinarySerializer
{
    public class EncodedLinearFile : PhysicalFile
	{
        public EncodedLinearFile(Context context, string filePath, IStreamEncoder encoder, Endian endianness = Endian.Little, long? fileLength = null) : base(context, filePath, endianness, fileLength: fileLength)
        {
            Encoder = encoder;
            length = fileLength;
        }

        public IStreamEncoder Encoder { get; }

		private long? length;
        public override long Length
        {
            get
            {
                if (length == null)
                {
                    // Open the file
                    using Stream s = FileManager.GetFileReadStream(SourcePath);

                    // Decode the file
                    using var decoded = Encoder.DecodeStream(s);

                    // Set the length
                    length = decoded.Length;
                }

				return length.Value;
            }
        }

        public override bool IsMemoryMapped => false;

        public override Reader CreateReader() 
        {
			// Open the file
			using Stream s = FileManager.GetFileReadStream(SourcePath);

			// Decode the file
			var decoded = Encoder.DecodeStream(s);

			// Set the length
			length = decoded.Length;

			// Return a reader
			return new Reader(decoded, isLittleEndian: Endianness == Endian.Little);
		}

		public override Writer CreateWriter() 
        {
			Stream memStream = new MemoryStream();
			memStream.SetLength(Length);
			Writer writer = new Writer(memStream, isLittleEndian: Endianness == Endian.Little);
			return writer;
		}

		public override void EndWrite(Writer writer) 
        {
			if (writer != null) 
            {
				CreateBackupFile();

                using Stream s = FileManager.GetFileWriteStream(DestinationPath, RecreateOnWrite);
                writer.BaseStream.Position = 0;
                using var encoded = Encoder.EncodeStream(writer.BaseStream);
                encoded.CopyTo(s);
            }
			base.EndWrite(writer);
		}
    }
}
