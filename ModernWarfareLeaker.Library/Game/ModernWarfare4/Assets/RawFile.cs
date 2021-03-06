﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace ModernWarfareLeaker.Library
{
    public partial class ModernWarfare4
    {
        public class RawFile : IAssetPool
        {
            #region AssetStructures
            /// <summary>
            /// RawFile Asset Structure
            /// </summary>
            private struct RawFileAsset
            {
                public long NamePointer;
                public int compressedLength;
                public int len;
                public long RawDataPtr;
            }
            #endregion
            public override string Name => "rawfile";
            public override int Index => (int) AssetPool.rawfile;
            public override long EndAddress { get { return StartAddress + (AssetCount * AssetSize); } set => throw new NotImplementedException(); }
            public override List<GameAsset> Load(LeakerInstance instance)
            {
                var results = new List<GameAsset>();

                var poolInfo = instance.Reader.ReadStruct<AssetPoolInfo>(instance.Game.BaseAddress + instance.Game.AssetPoolsAddresses[instance.Game.ProcessIndex] + (Index * 24));
                
                StartAddress = poolInfo.PoolPtr;
                AssetSize = poolInfo.AssetSize;
                AssetCount = poolInfo.PoolSize;

                for (int i = 0; i < AssetCount; i++)
                {
                    var header = instance.Reader.ReadStruct<RawFileAsset>(StartAddress + (i * AssetSize));

                    if (IsNullAsset(header.NamePointer))
                        continue;

                    results.Add(new GameAsset()
                    {
                        Name = instance.Reader.ReadNullTerminatedString(header.NamePointer),
                        HeaderAddress = StartAddress + (i * AssetSize),
                        AssetPool = this,
                        Type = Name,
                        Information = string.Format("Size: 0x{0:X}", header.len)
                    });
                }
                
                return results;
            }

            public override LeakerStatus Export(GameAsset asset, LeakerInstance instance)
            {
                var header = instance.Reader.ReadStruct<RawFileAsset>(asset.HeaderAddress);
                
                if (asset.Name != instance.Reader.ReadNullTerminatedString(header.NamePointer))
                    return LeakerStatus.MemoryChanged;
                
                string path = Path.Combine("exported_files", instance.Game.Name, asset.Name);
                
                // Create path
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                
                MemoryStream DecodedCodeStream = Decode(instance.Reader.ReadBytes(header.RawDataPtr + 2, header.compressedLength - 2));

                try
                {
                    using (var outputStream = new FileStream(path, FileMode.Create))
                    {
                        DecodedCodeStream.CopyTo(outputStream);
                    }
                }
                catch
                {
                    return LeakerStatus.Exception;
                }

                return LeakerStatus.Success;
            }

            public static MemoryStream Decode(byte[] data)
            {
                MemoryStream output = new MemoryStream();
                MemoryStream input = new MemoryStream(data);

                using (DeflateStream deflateStream = new DeflateStream(input, CompressionMode.Decompress))
                {
                    deflateStream.CopyTo(output);
                }

                output.Flush();
                output.Position = 0;

                return output;
            }
        }
    }
}