using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;

namespace Aseprite.Chunks
{
    public class SliceKey
    {
        //this slice is valid from this frame to the end of the animation
        public uint FrameNumber { get; private set; }
        
        public int SliceXOrigin { get; private set; }
        public int SliceYOrigin { get; private set; }
        public uint SliceWidth { get; private set; }
        public uint SliceHeight { get; private set; }
        
        //If Flags have bit 1
        public int CenterXOrigin { get; private set; }
        public int CenterYOrigin { get; private set; }
        public uint CenterWidth { get; private set; }
        public uint CenterHeight { get; private set; }
        
        //If Flags have bit 2
        public int PivotXOrigin { get; private set; }
        public int PivotYOrigin { get; private set; }

        public RectInt GetSliceKeyRect()
        {
            Vector2Int sliceOrigin = new Vector2Int(SliceXOrigin, SliceYOrigin);
            Vector2Int sliceSize = new Vector2Int((int)SliceWidth, (int)SliceHeight);
            RectInt rect = new RectInt(sliceOrigin, sliceSize);
            return rect;
        }

        public SliceKey(BinaryReader reader, uint flags)
        {
            FrameNumber = reader.ReadUInt32();

            SliceXOrigin = reader.ReadInt32();
            SliceYOrigin = reader.ReadInt32();
            SliceWidth = reader.ReadUInt32();
            SliceHeight = reader.ReadUInt32();

            if ((flags & 1) != 0)
            {
                CenterXOrigin = reader.ReadInt32();
                CenterYOrigin = reader.ReadInt32();
                CenterWidth = reader.ReadUInt32();
                CenterHeight = reader.ReadUInt32();
            }

            if ((flags & 2) != 0)
            {
                PivotXOrigin = reader.ReadInt32();
                PivotYOrigin = reader.ReadInt32();
            }
        }
    }
    
    public class SliceChunk : Chunk
    {
        public SliceKey[] SliceKeys;
        
        public uint SliceKeysNumber { get; private set; }
        //1 = It's a 9-patches slice
        //2 = Has pivot information
        public uint Flags { get; private set; }
        public uint Reserved { get; private set; }
        public string Name { get; private set; }
        
        public bool HasCenterInfo
        {
            get { return (Flags & 1) != 0; }
        }
        
        public bool HasPivotInfo
        {
            get { return (Flags & 2) != 0; }
        }

        public SliceChunk(uint length, BinaryReader reader) : base(length, ChunkType.Palette)
        {
            SliceKeysNumber = reader.ReadUInt32();
            Flags = reader.ReadUInt32();
            Reserved = reader.ReadUInt32();
            
            ushort nameLength = reader.ReadUInt16();
            Name = Encoding.Default.GetString(reader.ReadBytes(nameLength));

            SliceKeys = new SliceKey[SliceKeysNumber];

            for (int i = 0; i < SliceKeysNumber; i++)
            {
                SliceKeys[i] = new SliceKey(reader, Flags);
            }
        }
    }
}
