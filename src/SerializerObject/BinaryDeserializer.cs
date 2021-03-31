﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BinarySerializer
{
    /// <summary>
    /// A binary serializer used for deserializing
    /// </summary>
    public class BinaryDeserializer : SerializerObject, IDisposable 
    {
        public BinaryDeserializer(Context context) : base(context)
        {
            Readers = new Dictionary<BinaryFile, Reader>();
        }

        public override Pointer CurrentPointer 
        {
            get 
            {
                if (CurrentFile == null)
                    return null;

                uint curPos = (uint)Reader.BaseStream.Position;
                return new Pointer((uint)(curPos + CurrentFile.BaseAddress), CurrentFile);
            }
        }

        public override uint CurrentLength => (uint)Reader.BaseStream.Length;
        private string LogPrefix => IsLogEnabled ? ($"(READ) {CurrentPointer}:{new string(' ', (Depth + 1) * 2)}") : null;

        protected Dictionary<BinaryFile, Reader> Readers { get; }
        protected Reader Reader { get; set; }
        protected BinaryFile CurrentFile { get; set; }

        protected void SwitchToFile(BinaryFile newFile) 
        {
            if (newFile == null) 
                return;

            if (!Readers.ContainsKey(newFile)) 
            {
                Readers.Add(newFile, newFile.CreateReader());
                newFile.InitFileReadMap(Readers[newFile].BaseStream.Length);
            }

            Reader = Readers[newFile];
            CurrentFile = newFile;
        }

        // Helper method which returns an object so we can cast it
        protected object ReadAsObject<T>(string name = null) {
            // Get the type
            var type = typeof(T);

            TypeCode typeCode = Type.GetTypeCode(type);

            switch (typeCode) {
                case TypeCode.Boolean:
                    var b = Reader.ReadByte();

                    if (b != 0 && b != 1) {
                        Logger.LogWarning($"Binary boolean '{name}' ({b}) was not correctly formatted");

                        if (IsLogEnabled)
                            Context.Log.Log(LogPrefix + "(" + typeof(T) + "): Binary boolean was not correctly formatted (" + b + ")");
                    }

                    return b == 1;

                case TypeCode.SByte:
                    return Reader.ReadSByte();

                case TypeCode.Byte:
                    return Reader.ReadByte();

                case TypeCode.Int16:
                    return Reader.ReadInt16();

                case TypeCode.UInt16:
                    return Reader.ReadUInt16();

                case TypeCode.Int32:
                    return Reader.ReadInt32();

                case TypeCode.UInt32:
                    return Reader.ReadUInt32();

                case TypeCode.Int64:
                    return Reader.ReadInt64();

                case TypeCode.UInt64:
                    return Reader.ReadUInt64();

                case TypeCode.Single:
                    return Reader.ReadSingle();

                case TypeCode.Double:
                    return Reader.ReadDouble();
                case TypeCode.String:
                    return Reader.ReadNullDelimitedString(Context.DefaultEncoding);

                case TypeCode.Decimal:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.Empty:
                case TypeCode.DBNull:
                case TypeCode.Object:
                    if (type == typeof(UInt24)) {
                        return Reader.ReadUInt24();
                    } else if(type == typeof(byte?)) {
                        byte nullableByte = Reader.ReadByte();
                        if(nullableByte == 0xFF) return (byte?)null;
                        return nullableByte;
                    } else {
                        throw new NotSupportedException($"The specified generic type ('{name}') can not be read from the reader");
                    }
                default:
                    throw new NotSupportedException($"The specified generic type ('{name}') can not be read from the reader");
            }
        }

        public override string SerializeString(string obj, long? length = null, Encoding encoding = null, string name = null) 
        {
            string logString = LogPrefix;

            var t = length.HasValue ? Reader.ReadString(length.Value, encoding ?? Context.DefaultEncoding) : Reader.ReadNullDelimitedString(encoding ?? Context.DefaultEncoding);

            if (IsLogEnabled)
                Context.Log.Log($"{logString}(string) {(name ?? "<no name>")}: {t}");

            return t;
        }

        public override string[] SerializeStringArray(string[] obj, long count, int length, Encoding encoding = null, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(String[{count}]) {name ?? "<no name>"}");
            }
            string[] buffer;
            if (obj != null) {
                buffer = obj;
                if (buffer.Length != count) {
                    Array.Resize(ref buffer, (int)count);
                }
            } else {
                buffer = new string[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializeString(default, length, encoding, name: name == null ? null : name + "[" + i + "]");

            return buffer;
        }

        /// <summary>
        /// Begins calculating byte checksum for all decrypted bytes read from the stream
        /// </summary>
        /// <param name="checksumCalculator">The checksum calculator to use</param>
        public override void BeginCalculateChecksum(IChecksumCalculator checksumCalculator) => Reader.BeginCalculateChecksum(checksumCalculator);

        /// <summary>
        /// Ends calculating the checksum and return the value
        /// </summary>
        /// <typeparam name="T">The type of checksum value</typeparam>
        /// <returns>The checksum value</returns>
        public override T EndCalculateChecksum<T>() => Reader.EndCalculateChecksum<T>();


        public override void BeginXOR(IXORCalculator xorCalculator) => Reader.BeginXOR(xorCalculator);
        public override void EndXOR() => Reader.EndXOR();
        public override IXORCalculator GetXOR() => Reader.GetXORCalculator();

		public override void Goto(Pointer offset) {
            if (offset == null) 
                return;

            if (offset.File != CurrentFile)
                SwitchToFile(offset.File);

            Reader.BaseStream.Position = offset.FileOffset;
        }

        public override T Serialize<T>(T obj, string name = null) 
        {
            string logString = LogPrefix;

            var start = Reader.BaseStream.Position;

            T t = (T)ReadAsObject<T>(name);

            CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            if (IsLogEnabled)
                Context.Log.Log($"{logString}({typeof(T)}) {(name ?? "<no name>")}: {(t?.ToString() ?? "null")}");

            return t;
        }

        public override T SerializeChecksum<T>(T calculatedChecksum, string name = null) 
        {
            string logString = LogPrefix;

            var start = Reader.BaseStream.Position;

            T checksum = (T)ReadAsObject<T>(name);

            CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            if (!checksum.Equals(calculatedChecksum))
                Logger.LogWarning($"Checksum {name} did not match!");

            if (IsLogEnabled)
                Context.Log.Log($"{logString}({typeof(T)}) {(name ?? "<no name>")}: {checksum} - Checksum to match: {calculatedChecksum} - Matched? {checksum.Equals(calculatedChecksum)}");

            return checksum;
        }

        public override T SerializeObject<T>(T obj, Action<T> onPreSerialize = null, string name = null) {
            Pointer current = CurrentPointer;
            T instance = Context.Cache.FromOffset<T>(current);
            if (instance == null || CurrentFile.IgnoreCacheOnRead) {
                bool newInstance = false;
                if (instance == null) {
                    newInstance = true;
                    instance = new T();
                }
                instance.Init(current);

                // Do not cache already created objects
                if (newInstance)
                    Context.Cache.Add<T>(instance);
                
                string logString = IsLogEnabled ? LogPrefix : null;
                bool isLogTemporarilyDisabled = false;
                if (!DisableLogForObject && instance.IsShortLog) {
                    DisableLogForObject = true;
                    isLogTemporarilyDisabled = true;
                }

                if (IsLogEnabled) Context.Log.Log($"{logString}(Object: {typeof(T)}) {(name ?? "<no name>")}");

                Depth++;
                onPreSerialize?.Invoke(instance);
                instance.Serialize(this);
                Depth--;

                if (isLogTemporarilyDisabled) {
                    DisableLogForObject = false;
                    if (IsLogEnabled)
                        Context.Log.Log($"{logString}({typeof(T)}) {(name ?? "<no name>")}: {(instance.ShortLog ?? "null")}");
                }
            } 
            else 
            {
                Goto(current + instance.Size);
            }
            return instance;
        }

        public override Pointer SerializePointer(Pointer obj, Pointer anchor = null, bool allowInvalid = false, string name = null) 
        {
            string logString = LogPrefix;
            Pointer current = CurrentPointer;
            uint value = Reader.ReadUInt32();
            Pointer ptr = CurrentFile.GetPreDefinedPointer(current.AbsoluteOffset);

            if (ptr != null)
                ptr = ptr.SetAnchor(anchor);

            if(ptr == null) 
                ptr = CurrentFile.GetPointer(value, anchor: anchor);

            if (ptr == null && value != 0 && !allowInvalid && !CurrentFile.AllowInvalidPointer(value, anchor: anchor)) 
            {
                if (IsLogEnabled)
                    Context.Log.Log(logString + "(Pointer) " + (name ?? "<no name>") + ": InvalidPointerException - " + string.Format("{0:X8}", value));

                throw new PointerException("Not a valid pointer at " + (current) + ": " + string.Format("{0:X8}", value), "SerializePointer");
            }

            if (IsLogEnabled)
                Context.Log.Log(logString + "(Pointer) " + (name ?? "<no name>") + ": " + ptr?.ToString());

            return ptr;
        }

        public override Pointer<T> SerializePointer<T>(Pointer<T> obj, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, string name = null) 
        {
            if (IsLogEnabled) 
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(Pointer<T>: {typeof(T)}) {(name ?? "<no name>")}");
            }

            Depth++;
            Pointer<T> p = new Pointer<T>(this, anchor: anchor, resolve: resolve, onPreSerialize: onPreSerialize, allowInvalid: allowInvalid);
            Depth--;
            return p;
        }

        public override T[] SerializeArray<T>(T[] obj, long count, string name = null) 
        {
            // Use byte reading method if requested
            if (typeof(T) == typeof(byte)) {
                CurrentFile.UpdateReadMap(Reader.BaseStream.Position, count);
                if (IsLogEnabled) {
                    string normalLog = $"{LogPrefix}({typeof(T)}[{count}]) {(name ?? "<no name>")}: ";
                    byte[] bytes = Reader.ReadBytes((int)count);
                    Context.Log.Log(normalLog
                        + bytes.ToHexString(align: 16, newLinePrefix: new string(' ', normalLog.Length), maxLines: 10));
                    return (T[])(object)bytes;
                } else {
                    return (T[])(object)Reader.ReadBytes((int)count);
                }
            }
            if (IsLogEnabled) {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}({typeof(T)}[{count}]) {(name ?? "<no name>")}");
            }
            T[] buffer;
            if (obj != null) {
                buffer = obj;
                if (buffer.Length != count) {
                    Array.Resize(ref buffer, (int)count);
                }
            } else {
                buffer = new T[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = Serialize<T>(buffer[i], name: name == null ? null : name + "[" + i + "]");

            return buffer;
        }

        public override T[] SerializeObjectArray<T>(T[] obj, long count, Action<T> onPreSerialize = null, string name = null) {
            if (IsLogEnabled) {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(Object[]: {typeof(T)}[{count}]) {(name ?? "<no name>")}");
            }
            T[] buffer;
            if (obj != null) {
                buffer = obj;
                if (buffer.Length != count) {
                    Array.Resize(ref buffer, (int)count);
                }
            } else {
                buffer = new T[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializeObject<T>(buffer[i], onPreSerialize: onPreSerialize, name: name == null ? null : name + "[" + i + "]");

            return buffer;
        }

        public override Pointer[] SerializePointerArray(Pointer[] obj, long count, Pointer anchor = null, bool allowInvalid = false, string name = null) {
            if (IsLogEnabled) {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(Pointer[{count}]) {(name ?? "<no name>")}");
            }
            Pointer[] buffer;
            if (obj != null) {
                buffer = obj;
                if (buffer.Length != count) {
                    Array.Resize(ref buffer, (int)count);
                }
            } else {
                buffer = new Pointer[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializePointer(buffer[i], anchor: anchor, allowInvalid: allowInvalid, name: name == null ? null : $"{name}[{i}]");

            return buffer;
        }

        public override Pointer<T>[] SerializePointerArray<T>(Pointer<T>[] obj, long count, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, string name = null) {
            if (IsLogEnabled) {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(Pointer<{typeof(T)}>[{count}]) {(name ?? "<no name>")}");
            }
            Pointer<T>[] buffer;
            if (obj != null) {
                buffer = obj;
                if (buffer.Length != count) {
                    Array.Resize(ref buffer, (int)count);
                }
            } else {
                buffer = new Pointer<T>[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializePointer<T>(buffer[i], anchor: anchor, resolve: resolve, onPreSerialize: onPreSerialize, allowInvalid: allowInvalid, name: name == null ? null : $"{name}[{i}]");

            return buffer;
        }

        public override T[] SerializeArraySize<T, U>(T[] obj, string name = null) {
            //U Size = (U)Convert.ChangeType((obj?.Length) ?? 0, typeof(U));
            U Size = default; // For performance reasons, don't supply this argument
            Size = Serialize<U>(Size, name: $"{name}.Length");
            // Convert size to int, slow
            int intSize = (int)Convert.ChangeType(Size, typeof(int));
            if (obj == null) {
                obj = new T[intSize];
            } else if (obj.Length != intSize) {
                Array.Resize(ref obj, intSize);
            }
            return obj;
        }

        public override void SerializeBitValues<T>(Action<SerializeBits> serializeFunc) {
            string logPrefix = LogPrefix;
            // Convert to int so we can work with it
            var valueInt = Convert.ToInt32(Serialize<T>(default, name: "Value"));

            // Extract bits
            int pos = 0;
            serializeFunc((v, length, name) => {
                var bitValue = BitHelpers.ExtractBits(valueInt, length, pos);

                if (IsLogEnabled)
                    Context.Log.Log($"{logPrefix}  ({typeof(T)}) {name ?? "<no name>"}: {bitValue}");

                pos += length;
                return bitValue;
            });
        }

        public void Dispose() 
        {
            foreach (KeyValuePair<BinaryFile, Reader> r in Readers)
                r.Key.EndRead(r.Value);

                Readers.Clear();
            Reader = null;
        }

        public void DisposeFile(BinaryFile file) 
        {
            if (!Readers.ContainsKey(file)) 
                return;
            
            Reader r = Readers[file];
            file.EndRead(r);
            Readers.Remove(file);
        }

        public override void DoEncoded(IStreamEncoder encoder, Action action, Endian? endianness = null, bool allowLocalPointers = false) {
            // Stream key
            string key = $"{CurrentPointer}_decoded";
            // Decode the data into a stream
            using (var memStream = encoder.DecodeStream(Reader.BaseStream)) {

                // Add the stream
                StreamFile sf = new StreamFile(key, memStream, Context)
                {
                    Endianness = endianness ?? CurrentFile.Endianness,
                    AllowLocalPointers = allowLocalPointers
                };
                Context.AddFile(sf);

                DoAt(sf.StartPointer, () => {
                    action();
                    if (CurrentPointer != sf.StartPointer + sf.Length) {
                        Logger.LogWarning($"Encoded block {key} was not fully deserialized: Serialized size: {CurrentPointer - sf.StartPointer} != Total size: {sf.Length}");
                    }
                });

                Context.RemoveFile(sf);

            }
        }
        public override void DoEndian(Endian endianness, Action action) {
            Reader r = Reader;
            bool isLittleEndian = r.IsLittleEndian;
            if (isLittleEndian != (endianness == Endian.Little)) {
                r.IsLittleEndian = (endianness == Endian.Little);
                action();
                r.IsLittleEndian = isLittleEndian;
            } else {
                action();
            }
        }

        public override void Log(string logString) 
        {
            if (IsLogEnabled)
                Context.Log.Log(LogPrefix + logString);
        }

        public override Task FillCacheForReadAsync(int length) => FileManager.FillCacheForReadAsync(length, Reader);
    }
}