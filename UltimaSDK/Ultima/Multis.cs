using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Ultima
{
    public sealed class Multis
    {
        const int MAX_MULTI_DATA_INDEX_COUNT = 0x2200;
        private static MultiComponentList[] m_Components = new MultiComponentList[MAX_MULTI_DATA_INDEX_COUNT];
        private static FileIndex m_FileIndex = new FileIndex("multi.idx", "multi.mul", "multicollection.uop", MAX_MULTI_DATA_INDEX_COUNT, 14, ".dat", -1, false);
        public static bool IsUOP { get; set; } = false;
        public static bool MultiCollectionLoaded { get; set; } = false;
        public static UOFileIndex[] Entries;
        public static UOFileUop m_File;
        public static int Count { get; private set; }

        public static Multis Instance { get; set; } = new Multis();

        public enum ImportType
        {
            TXT,
            UOA,
            UOAB,
            WSC,
            MULTICACHE,
            UOADESIGN
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public ref struct MultiBlock
        {
            public ushort ID;
            public short X;
            public short Y;
            public short Z;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public ref struct MultiBlockNew
        {
            public ushort ID;
            public short X;
            public short Y;
            public short Z;
            public ushort Flags;
            public uint Unknown;
        }

        public static bool PostHSFormat { get; set; }

        /// <summary>
        /// ReReads multi.mul
        /// </summary>
       // public static void Reload()
        //{
        //    m_FileIndex = new FileIndex("Multi.idx", "Multi.mul", 0x2000, 14);
        //    m_Components = new MultiComponentList[0x2000];
        //}

        /// <summary>
        /// Gets <see cref="MultiComponentList"/> of multi
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static MultiComponentList GetComponents(int index)
        {
            MultiComponentList mcl;

            index &= 0x1FFF;

            if (index >= 0 && index < m_Components.Length)
            {
                mcl = m_Components[index];

                if (mcl == null)
                    m_Components[index] = mcl = Load(index);
            }
            else
                mcl = MultiComponentList.Empty;

            return mcl;
        }

        public static MultiComponentList Load(int index)
        {
            try
            {

                string uopPath = Path.Combine(Files.RootDir, "MultiCollection.uop");

                if (!MultiCollectionLoaded && System.IO.File.Exists(uopPath))
                {
                    const int MAX_MULTI_DATA_INDEX_COUNT = 0x2200;
                    Count = MAX_MULTI_DATA_INDEX_COUNT;
                    //m_FileIndex = new FileIndex("multi.idx", "multi.mul", "multicollection.uop", MAX_MULTI_DATA_INDEX_COUNT, 14, ".dat", -1, false);

                    m_File = new UOFileUop(uopPath, "build/multicollection/{0:D6}.bin");
                    Entries = new UOFileIndex[Count];
                    IsUOP = true;

                    m_File.FillEntries(ref Entries);
                    MultiCollectionLoaded = true;
                }

                {
                    int length, extra;
                    bool patched;
                    Stream stream = m_FileIndex.Seek(index, out length, out extra, out patched);

                    if (stream == null)
                        return MultiComponentList.Empty;

                    if (PostHSFormat || Art.IsUOAHS())
                        return new MultiComponentList(new BinaryReader(stream), length / 16);
                    else
                        return new MultiComponentList(new BinaryReader(stream), length / 12);
                }
            }
            catch
            {
                return MultiComponentList.Empty;
            }

        }

        static public ref UOFileIndex GetValidRefEntry(int index)
        {
            if (index < 0 || Entries == null || index >= Entries.Length)
            {
                return ref UOFileIndex.Invalid;
            }

            ref UOFileIndex entry = ref Entries[index];

            if (entry.Offset < 0 || entry.Length <= 0 || entry.Offset == 0x0000_0000_FFFF_FFFF)
            {
                return ref UOFileIndex.Invalid;
            }

            return ref entry;
        }


        public static void Remove(int index)
        {
            m_Components[index] = MultiComponentList.Empty;
        }

        public static void Add(int index, MultiComponentList comp)
        {
            m_Components[index] = comp;
        }

        public static MultiComponentList ImportFromFile(int index, string FileName, Multis.ImportType type)
        {
            try
            {
                return m_Components[index] = new MultiComponentList(FileName, type);
            }
            catch
            {
                return m_Components[index] = MultiComponentList.Empty;
            }
        }

        public static MultiComponentList LoadFromFile(string FileName, Multis.ImportType type)
        {
            try
            {
                return new MultiComponentList(FileName, type);
            }
            catch
            {
                return MultiComponentList.Empty;
            }
        }

        public static List<MultiComponentList> LoadFromCache(string FileName)
        {
            List<MultiComponentList> multilist = new List<MultiComponentList>();
            using (StreamReader ip = new StreamReader(FileName))
            {
                string line;
                while ((line = ip.ReadLine()) != null)
                {
                    string[] split = Regex.Split(line, @"\s+");
                    if (split.Length == 7)
                    {
                        int count = Convert.ToInt32(split[2]);
                        multilist.Add(new MultiComponentList(ip, count));
                    }
                }
            }
            return multilist;
        }

        public static string ReadUOAString(BinaryReader bin)
        {
            byte flag = bin.ReadByte();

            if (flag == 0)
                return null;
            else
                return bin.ReadString();
        }

        public static List<Object[]> LoadFromDesigner(string FileName)
        {
            List<Object[]> multilist = new List<Object[]>();
            string root = Path.GetFileNameWithoutExtension(FileName);
            string idx = String.Format("{0}.idx", root);
            string bin = String.Format("{0}.bin", root);
            if ((!File.Exists(idx)) || (!File.Exists(bin)))
                return multilist;
            using (FileStream idxfs = new FileStream(idx, FileMode.Open, FileAccess.Read, FileShare.Read),
                              binfs = new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BinaryReader idxbin = new BinaryReader(idxfs),
                                    binbin = new BinaryReader(binfs))
                {
                    int count = idxbin.ReadInt32();
                    int version = idxbin.ReadInt32();

                    for (int i = 0; i < count; ++i)
                    {
                        Object[] data = new Object[2];
                        switch (version)
                        {
                            case 0:
                                data[0] = ReadUOAString(idxbin);
                                List<MultiComponentList.MultiTileEntry> arr = new List<MultiComponentList.MultiTileEntry>();
                                data[0] += "-" + ReadUOAString(idxbin);
                                data[0] += "-" + ReadUOAString(idxbin);
                                int width = idxbin.ReadInt32();
                                int height = idxbin.ReadInt32();
                                int uwidth = idxbin.ReadInt32();
                                int uheight = idxbin.ReadInt32();
                                long filepos = idxbin.ReadInt64();
                                int reccount = idxbin.ReadInt32();

                                binbin.BaseStream.Seek(filepos, SeekOrigin.Begin);
                                int index, x, y, z, level, hue;
                                for (int j = 0; j < reccount; ++j)
                                {
                                    index = x = y = z = level = hue = 0;
                                    int compVersion = binbin.ReadInt32();
                                    switch (compVersion)
                                    {
                                        case 0:
                                            index = binbin.ReadInt32();
                                            x = binbin.ReadInt32();
                                            y = binbin.ReadInt32();
                                            z = binbin.ReadInt32();
                                            level = binbin.ReadInt32();
                                            break;

                                        case 1:
                                            index = binbin.ReadInt32();
                                            x = binbin.ReadInt32();
                                            y = binbin.ReadInt32();
                                            z = binbin.ReadInt32();
                                            level = binbin.ReadInt32();
                                            hue = binbin.ReadInt32();
                                            break;
                                    }
                                    MultiComponentList.MultiTileEntry tempitem = new MultiComponentList.MultiTileEntry();
                                    tempitem.m_ItemID = (ushort)index;
                                    tempitem.m_Flags = 1;
                                    tempitem.m_OffsetX = (short)x;
                                    tempitem.m_OffsetY = (short)y;
                                    tempitem.m_OffsetZ = (short)z;
                                    tempitem.m_Unk1 = 0;
                                }
                                data[1] = new MultiComponentList(arr);
                                break;
                        }
                        multilist.Add(data);
                    }
                }
                return multilist;
            }
        }

        public static List<MultiComponentList.MultiTileEntry> RebuildTiles(MultiComponentList.MultiTileEntry[] tiles)
        {
            List<MultiComponentList.MultiTileEntry> newtiles = new List<MultiComponentList.MultiTileEntry>();
            newtiles.AddRange(tiles);

            if (newtiles[0].m_OffsetX == 0 && newtiles[0].m_OffsetY == 0 && newtiles[0].m_OffsetZ == 0) // found a centeritem
            {
                if (newtiles[0].m_ItemID != 0x1) // its a "good" one
                {
                    for (int j = newtiles.Count - 1; j >= 0; --j) // remove all invis items
                    {
                        if (newtiles[j].m_ItemID == 0x1)
                            newtiles.RemoveAt(j);
                    }
                    return newtiles;
                }
                else // a bad one
                {
                    for (int i = 1; i < newtiles.Count; ++i) // do we have a better one?
                    {
                        if (newtiles[i].m_OffsetX == 0 && newtiles[i].m_OffsetY == 0
                            && newtiles[i].m_ItemID != 0x1 && newtiles[i].m_OffsetZ == 0)
                        {
                            MultiComponentList.MultiTileEntry centeritem = newtiles[i];
                            newtiles.RemoveAt(i); // jep so save it
                            for (int j = newtiles.Count - 1; j >= 0; --j) // and remove all invis
                            {
                                if (newtiles[j].m_ItemID == 0x1)
                                    newtiles.RemoveAt(j);
                            }
                            newtiles.Insert(0, centeritem);
                            return newtiles;
                        }
                    }
                    for (int j = newtiles.Count - 1; j >= 1; --j) // nothing found so remove all invis exept the first
                    {
                        if (newtiles[j].m_ItemID == 0x1)
                            newtiles.RemoveAt(j);
                    }
                    return newtiles;
                }
            }
            for (int i = 0; i < newtiles.Count; ++i) // is there a good one
            {
                if (newtiles[i].m_OffsetX == 0 && newtiles[i].m_OffsetY == 0
                    && newtiles[i].m_ItemID != 0x1 && newtiles[i].m_OffsetZ == 0)
                {
                    MultiComponentList.MultiTileEntry centeritem = newtiles[i];
                    newtiles.RemoveAt(i); // store it
                    for (int j = newtiles.Count - 1; j >= 0; --j) // remove all invis
                    {
                        if (newtiles[j].m_ItemID == 0x1)
                            newtiles.RemoveAt(j);
                    }
                    newtiles.Insert(0, centeritem);
                    return newtiles;
                }
            }
            for (int j = newtiles.Count - 1; j >= 0; --j) // nothing found so remove all invis
            {
                if (newtiles[j].m_ItemID == 0x1)
                    newtiles.RemoveAt(j);
            }
            MultiComponentList.MultiTileEntry invisitem = new MultiComponentList.MultiTileEntry();
            invisitem.m_ItemID = 0x1; // and create a new invis
            invisitem.m_OffsetX = 0;
            invisitem.m_OffsetY = 0;
            invisitem.m_OffsetZ = 0;
            invisitem.m_Flags = 0;
            invisitem.m_Unk1 = 0;
            newtiles.Insert(0, invisitem);
            return newtiles;
        }

        public static void Save(string path)
        {
            bool isUOAHS = PostHSFormat || Art.IsUOAHS();
            string idx = Path.Combine(path, "multi.idx");
            string mul = Path.Combine(path, "multi.mul");
            using (FileStream fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write),
                              fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                using (BinaryWriter binidx = new BinaryWriter(fsidx),
                                    binmul = new BinaryWriter(fsmul))
                {
                    for (int index = 0; index < 0x2000; ++index)
                    {
                        MultiComponentList comp = GetComponents(index);

                        if (comp == MultiComponentList.Empty)
                        {
                            binidx.Write((int)-1); // lookup
                            binidx.Write((int)-1); // length
                            binidx.Write((int)-1); // extra
                        }
                        else
                        {
                            List<MultiComponentList.MultiTileEntry> tiles = RebuildTiles(comp.SortedTiles);
                            binidx.Write((int)fsmul.Position); //lookup
                            if (isUOAHS)
                                binidx.Write((int)(tiles.Count * 16)); //length
                            else
                                binidx.Write((int)(tiles.Count * 12)); //length
                            binidx.Write((int)-1); //extra
                            for (int i = 0; i < tiles.Count; ++i)
                            {
                                binmul.Write((ushort)tiles[i].m_ItemID);
                                binmul.Write((short)tiles[i].m_OffsetX);
                                binmul.Write((short)tiles[i].m_OffsetY);
                                binmul.Write((short)tiles[i].m_OffsetZ);
                                binmul.Write((int)tiles[i].m_Flags);
                                if (isUOAHS)
                                    binmul.Write((int)tiles[i].m_Unk1);
                            }
                        }
                    }
                }
            }
        }
    }

    public sealed class MultiComponentList
    {       
        private Point m_Min, m_Max, m_Center;
        private int m_Width, m_Height, m_maxHeight, m_Surface;
        private MTile[][][] m_Tiles;
        private MultiTileEntry[] m_SortedTiles;

        public static readonly MultiComponentList Empty = new MultiComponentList();

        public Point Min { get { return m_Min; } }
        public Point Max { get { return m_Max; } }
        public Point Center { get { return m_Center; } }
        public int Width { get { return m_Width; } }
        public int Height { get { return m_Height; } }
        public MTile[][][] Tiles { get { return m_Tiles; } }
        public int maxHeight { get { return m_maxHeight; } }
        public MultiTileEntry[] SortedTiles { get { return m_SortedTiles; } }
        public int Surface { get { return m_Surface; } }
        public int Count { get; set; }

        public struct MultiTileEntry
        {
            public ushort m_ItemID;
            public short m_OffsetX, m_OffsetY, m_OffsetZ;
            public int m_Flags;
            public int m_Unk1;
        }

        /// <summary>
        /// Returns Bitmap of Multi
        /// </summary>
        /// <returns></returns>
        public Bitmap GetImage()
        {
            return GetImage(300);
        }

        /// <summary>
        /// Returns Bitmap of Multi to maxheight
        /// </summary>
        /// <param name="maxheight"></param>
        /// <returns></returns>
        public Bitmap GetImage(int maxheight)
        {
            if (m_Width == 0 || m_Height == 0)
                return null;

            int xMin = 1000, yMin = 1000;
            int xMax = -1000, yMax = -1000;

            for (int x = 0; x < m_Width; ++x)
            {
                for (int y = 0; y < m_Height; ++y)
                {
                    MTile[] tiles = m_Tiles[x][y];

                    for (int i = 0; i < tiles.Length; ++i)
                    {
                        Bitmap bmp = Art.GetStatic(tiles[i].ID);

                        if (bmp == null)
                            continue;

                        int px = (x - y) * 22;
                        int py = (x + y) * 22;

                        px -= (bmp.Width / 2);
                        py -= tiles[i].Z << 2;
                        py -= bmp.Height;

                        if (px < xMin)
                            xMin = px;

                        if (py < yMin)
                            yMin = py;

                        px += bmp.Width;
                        py += bmp.Height;

                        if (px > xMax)
                            xMax = px;

                        if (py > yMax)
                            yMax = py;
                    }
                }
            }

            Bitmap canvas = new Bitmap(xMax - xMin, yMax - yMin);
            Graphics gfx = Graphics.FromImage(canvas);
            gfx.Clear(Color.White);
            for (int x = 0; x < m_Width; ++x)
            {
                for (int y = 0; y < m_Height; ++y)
                {
                    MTile[] tiles = m_Tiles[x][y];

                    for (int i = 0; i < tiles.Length; ++i)
                    {
                        Bitmap bmp = Art.GetStatic(tiles[i].ID);

                        if (bmp == null)
                            continue;
                        if ((tiles[i].Z) > maxheight)
                            continue;
                        int px = (x - y) * 22;
                        int py = (x + y) * 22;

                        px -= (bmp.Width / 2);
                        py -= tiles[i].Z << 2;
                        py -= bmp.Height;
                        px -= xMin;
                        py -= yMin;

                        gfx.DrawImageUnscaled(bmp, px, py, bmp.Width, bmp.Height);
                    }

                    int tx = (x - y) * 22;
                    int ty = (x + y) * 22;
                    tx -= xMin;
                    ty -= yMin;
                }
            }

            gfx.Dispose();

            return canvas;
        }

        public MultiComponentList(BinaryReader reader, int count)
        {
            Count = count;
            bool useNewMultiFormat = Multis.PostHSFormat || Art.IsUOAHS();
            m_Min = m_Max = Point.Empty;
            m_SortedTiles = new MultiTileEntry[count];
            for (int i = 0; i < count; ++i)
            {
                m_SortedTiles[i].m_ItemID = Art.GetLegalItemID(reader.ReadUInt16());
                m_SortedTiles[i].m_OffsetX = reader.ReadInt16();
                m_SortedTiles[i].m_OffsetY = reader.ReadInt16();
                m_SortedTiles[i].m_OffsetZ = reader.ReadInt16();
                m_SortedTiles[i].m_Flags = reader.ReadInt32();
                if (useNewMultiFormat)
                    m_SortedTiles[i].m_Unk1 = reader.ReadInt32();
                else
                    m_SortedTiles[i].m_Unk1 = 0;

                MultiTileEntry e = m_SortedTiles[i];

                if (e.m_OffsetX < m_Min.X)
                    m_Min.X = e.m_OffsetX;

                if (e.m_OffsetY < m_Min.Y)
                    m_Min.Y = e.m_OffsetY;

                if (e.m_OffsetX > m_Max.X)
                    m_Max.X = e.m_OffsetX;

                if (e.m_OffsetY > m_Max.Y)
                    m_Max.Y = e.m_OffsetY;

                if (e.m_OffsetZ > m_maxHeight)
                    m_maxHeight = e.m_OffsetZ;
            }
            ConvertList();
            reader.Close();
        }

        public MultiComponentList(string FileName, Multis.ImportType Type)
        {
            m_Min = m_Max = Point.Empty;
            int itemcount;
            switch (Type)
            {
                case Multis.ImportType.TXT:
                    itemcount = 0;
                    using (StreamReader ip = new StreamReader(FileName))
                    {
                        string line;
                        while ((line = ip.ReadLine()) != null)
                        {
                            itemcount++;
                        }
                    }
                    m_SortedTiles = new MultiTileEntry[itemcount];
                    itemcount = 0;
                    m_Min.X = 10000;
                    m_Min.Y = 10000;
                    using (StreamReader ip = new StreamReader(FileName))
                    {
                        string line;
                        while ((line = ip.ReadLine()) != null)
                        {
                            string[] split = line.Split(' ');

                            string tmp = split[0];
                            tmp = tmp.Replace("0x", "");

                            m_SortedTiles[itemcount].m_ItemID = ushort.Parse(tmp, System.Globalization.NumberStyles.HexNumber);
                            m_SortedTiles[itemcount].m_OffsetX = Convert.ToInt16(split[1]);
                            m_SortedTiles[itemcount].m_OffsetY = Convert.ToInt16(split[2]);
                            m_SortedTiles[itemcount].m_OffsetZ = Convert.ToInt16(split[3]);
                            m_SortedTiles[itemcount].m_Flags = Convert.ToInt32(split[4]);
                            m_SortedTiles[itemcount].m_Unk1 = 0;

                            MultiTileEntry e = m_SortedTiles[itemcount];

                            if (e.m_OffsetX < m_Min.X)
                                m_Min.X = e.m_OffsetX;

                            if (e.m_OffsetY < m_Min.Y)
                                m_Min.Y = e.m_OffsetY;

                            if (e.m_OffsetX > m_Max.X)
                                m_Max.X = e.m_OffsetX;

                            if (e.m_OffsetY > m_Max.Y)
                                m_Max.Y = e.m_OffsetY;

                            if (e.m_OffsetZ > m_maxHeight)
                                m_maxHeight = e.m_OffsetZ;

                            itemcount++;
                        }
                        Count = itemcount;
                        int centerx = m_Max.X - (int)(Math.Round((m_Max.X - m_Min.X) / 2.0));
                        int centery = m_Max.Y - (int)(Math.Round((m_Max.Y - m_Min.Y) / 2.0));

                        m_Min = m_Max = Point.Empty;
                        int i = 0;
                        for (; i < m_SortedTiles.Length; i++)
                        {
                            m_SortedTiles[i].m_OffsetX -= (short)centerx;
                            m_SortedTiles[i].m_OffsetY -= (short)centery;
                            if (m_SortedTiles[i].m_OffsetX < m_Min.X)
                                m_Min.X = m_SortedTiles[i].m_OffsetX;
                            if (m_SortedTiles[i].m_OffsetX > m_Max.X)
                                m_Max.X = m_SortedTiles[i].m_OffsetX;

                            if (m_SortedTiles[i].m_OffsetY < m_Min.Y)
                                m_Min.Y = m_SortedTiles[i].m_OffsetY;
                            if (m_SortedTiles[i].m_OffsetY > m_Max.Y)
                                m_Max.Y = m_SortedTiles[i].m_OffsetY;
                        }
                    }
                    break;

                case Multis.ImportType.UOA:
                    itemcount = 0;

                    using (StreamReader ip = new StreamReader(FileName))
                    {
                        string line;
                        while ((line = ip.ReadLine()) != null)
                        {
                            ++itemcount;
                            if (itemcount == 4)
                            {
                                string[] split = line.Split(' ');
                                itemcount = Convert.ToInt32(split[0]);
                                break;
                            }
                        }
                    }
                    m_SortedTiles = new MultiTileEntry[itemcount];
                    itemcount = 0;
                    m_Min.X = 10000;
                    m_Min.Y = 10000;
                    using (StreamReader ip = new StreamReader(FileName))
                    {
                        string line;
                        int i = -1;
                        while ((line = ip.ReadLine()) != null)
                        {
                            ++i;
                            if (i < 4)
                                continue;
                            string[] split = line.Split(' ');

                            m_SortedTiles[itemcount].m_ItemID = Convert.ToUInt16(split[0]);
                            m_SortedTiles[itemcount].m_OffsetX = Convert.ToInt16(split[1]);
                            m_SortedTiles[itemcount].m_OffsetY = Convert.ToInt16(split[2]);
                            m_SortedTiles[itemcount].m_OffsetZ = Convert.ToInt16(split[3]);
                            m_SortedTiles[itemcount].m_Flags = Convert.ToInt32(split[4]);
                            m_SortedTiles[itemcount].m_Unk1 = 0;

                            MultiTileEntry e = m_SortedTiles[itemcount];

                            if (e.m_OffsetX < m_Min.X)
                                m_Min.X = e.m_OffsetX;

                            if (e.m_OffsetY < m_Min.Y)
                                m_Min.Y = e.m_OffsetY;

                            if (e.m_OffsetX > m_Max.X)
                                m_Max.X = e.m_OffsetX;

                            if (e.m_OffsetY > m_Max.Y)
                                m_Max.Y = e.m_OffsetY;

                            if (e.m_OffsetZ > m_maxHeight)
                                m_maxHeight = e.m_OffsetZ;

                            ++itemcount;
                        }
                        Count = itemcount;
                        int centerx = m_Max.X - (int)(Math.Round((m_Max.X - m_Min.X) / 2.0));
                        int centery = m_Max.Y - (int)(Math.Round((m_Max.Y - m_Min.Y) / 2.0));

                        m_Min = m_Max = Point.Empty;
                        i = 0;
                        for (; i < m_SortedTiles.Length; ++i)
                        {
                            m_SortedTiles[i].m_OffsetX -= (short)centerx;
                            m_SortedTiles[i].m_OffsetY -= (short)centery;
                            if (m_SortedTiles[i].m_OffsetX < m_Min.X)
                                m_Min.X = m_SortedTiles[i].m_OffsetX;
                            if (m_SortedTiles[i].m_OffsetX > m_Max.X)
                                m_Max.X = m_SortedTiles[i].m_OffsetX;

                            if (m_SortedTiles[i].m_OffsetY < m_Min.Y)
                                m_Min.Y = m_SortedTiles[i].m_OffsetY;
                            if (m_SortedTiles[i].m_OffsetY > m_Max.Y)
                                m_Max.Y = m_SortedTiles[i].m_OffsetY;
                        }
                    }

                    break;

                case Multis.ImportType.UOAB:
                    using (FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (BinaryReader reader = new BinaryReader(fs))
                    {
                        if (reader.ReadInt16() != 1) //Version check
                            return;
                        string tmp;
                        tmp = Multis.ReadUOAString(reader); //Name
                        tmp = Multis.ReadUOAString(reader); //Category
                        tmp = Multis.ReadUOAString(reader); //Subsection
                        int width = reader.ReadInt32();
                        int height = reader.ReadInt32();
                        int uwidth = reader.ReadInt32();
                        int uheight = reader.ReadInt32();

                        int count = reader.ReadInt32();
                        Count = count;
                        m_SortedTiles = new MultiTileEntry[count];
                        itemcount = 0;
                        m_Min.X = 10000;
                        m_Min.Y = 10000;
                        for (; itemcount < count; ++itemcount)
                        {
                            m_SortedTiles[itemcount].m_ItemID = (ushort)reader.ReadInt16();
                            m_SortedTiles[itemcount].m_OffsetX = reader.ReadInt16();
                            m_SortedTiles[itemcount].m_OffsetY = reader.ReadInt16();
                            m_SortedTiles[itemcount].m_OffsetZ = reader.ReadInt16();
                            reader.ReadInt16(); // level
                            m_SortedTiles[itemcount].m_Flags = 1;
                            reader.ReadInt16(); // hue
                            m_SortedTiles[itemcount].m_Unk1 = 0;

                            MultiTileEntry e = m_SortedTiles[itemcount];

                            if (e.m_OffsetX < m_Min.X)
                                m_Min.X = e.m_OffsetX;

                            if (e.m_OffsetY < m_Min.Y)
                                m_Min.Y = e.m_OffsetY;

                            if (e.m_OffsetX > m_Max.X)
                                m_Max.X = e.m_OffsetX;

                            if (e.m_OffsetY > m_Max.Y)
                                m_Max.Y = e.m_OffsetY;

                            if (e.m_OffsetZ > m_maxHeight)
                                m_maxHeight = e.m_OffsetZ;
                        }
                        int centerx = m_Max.X - (int)(Math.Round((m_Max.X - m_Min.X) / 2.0));
                        int centery = m_Max.Y - (int)(Math.Round((m_Max.Y - m_Min.Y) / 2.0));

                        m_Min = m_Max = Point.Empty;
                        itemcount = 0;
                        for (; itemcount < m_SortedTiles.Length; ++itemcount)
                        {
                            m_SortedTiles[itemcount].m_OffsetX -= (short)centerx;
                            m_SortedTiles[itemcount].m_OffsetY -= (short)centery;
                            if (m_SortedTiles[itemcount].m_OffsetX < m_Min.X)
                                m_Min.X = m_SortedTiles[itemcount].m_OffsetX;
                            if (m_SortedTiles[itemcount].m_OffsetX > m_Max.X)
                                m_Max.X = m_SortedTiles[itemcount].m_OffsetX;

                            if (m_SortedTiles[itemcount].m_OffsetY < m_Min.Y)
                                m_Min.Y = m_SortedTiles[itemcount].m_OffsetY;
                            if (m_SortedTiles[itemcount].m_OffsetY > m_Max.Y)
                                m_Max.Y = m_SortedTiles[itemcount].m_OffsetY;
                        }
                    }
                    break;

                case Multis.ImportType.WSC:
                    itemcount = 0;
                    using (StreamReader ip = new StreamReader(FileName))
                    {
                        string line;
                        while ((line = ip.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (line.StartsWith("SECTION WORLDITEM"))
                                ++itemcount;
                        }
                    }
                    m_SortedTiles = new MultiTileEntry[itemcount];
                    itemcount = 0;
                    m_Min.X = 10000;
                    m_Min.Y = 10000;
                    using (StreamReader ip = new StreamReader(FileName))
                    {
                        string line;
                        MultiTileEntry tempitem = new MultiTileEntry();
                        tempitem.m_ItemID = 0xFFFF;
                        tempitem.m_Flags = 1;
                        tempitem.m_Unk1 = 0;
                        while ((line = ip.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (line.StartsWith("SECTION WORLDITEM"))
                            {
                                if (tempitem.m_ItemID != 0xFFFF)
                                {
                                    m_SortedTiles[itemcount] = tempitem;
                                    ++itemcount;
                                }
                                tempitem.m_ItemID = 0xFFFF;
                            }
                            else if (line.StartsWith("ID"))
                            {
                                line = line.Remove(0, 2);
                                line = line.Trim();
                                tempitem.m_ItemID = Convert.ToUInt16(line);
                            }
                            else if (line.StartsWith("X"))
                            {
                                line = line.Remove(0, 1);
                                line = line.Trim();
                                tempitem.m_OffsetX = Convert.ToInt16(line);
                                if (tempitem.m_OffsetX < m_Min.X)
                                    m_Min.X = tempitem.m_OffsetX;
                                if (tempitem.m_OffsetX > m_Max.X)
                                    m_Max.X = tempitem.m_OffsetX;
                            }
                            else if (line.StartsWith("Y"))
                            {
                                line = line.Remove(0, 1);
                                line = line.Trim();
                                tempitem.m_OffsetY = Convert.ToInt16(line);
                                if (tempitem.m_OffsetY < m_Min.Y)
                                    m_Min.Y = tempitem.m_OffsetY;
                                if (tempitem.m_OffsetY > m_Max.Y)
                                    m_Max.Y = tempitem.m_OffsetY;
                            }
                            else if (line.StartsWith("Z"))
                            {
                                line = line.Remove(0, 1);
                                line = line.Trim();
                                tempitem.m_OffsetZ = Convert.ToInt16(line);
                                if (tempitem.m_OffsetZ > m_maxHeight)
                                    m_maxHeight = tempitem.m_OffsetZ;
                            }
                        }
                        Count = itemcount;
                        if (tempitem.m_ItemID != 0xFFFF)
                            m_SortedTiles[itemcount] = tempitem;

                        int centerx = m_Max.X - (int)(Math.Round((m_Max.X - m_Min.X) / 2.0));
                        int centery = m_Max.Y - (int)(Math.Round((m_Max.Y - m_Min.Y) / 2.0));

                        m_Min = m_Max = Point.Empty;
                        int i = 0;
                        for (; i < m_SortedTiles.Length; i++)
                        {
                            m_SortedTiles[i].m_OffsetX -= (short)centerx;
                            m_SortedTiles[i].m_OffsetY -= (short)centery;
                            if (m_SortedTiles[i].m_OffsetX < m_Min.X)
                                m_Min.X = m_SortedTiles[i].m_OffsetX;
                            if (m_SortedTiles[i].m_OffsetX > m_Max.X)
                                m_Max.X = m_SortedTiles[i].m_OffsetX;

                            if (m_SortedTiles[i].m_OffsetY < m_Min.Y)
                                m_Min.Y = m_SortedTiles[i].m_OffsetY;
                            if (m_SortedTiles[i].m_OffsetY > m_Max.Y)
                                m_Max.Y = m_SortedTiles[i].m_OffsetY;
                        }
                    }
                    break;
            }
            ConvertList();
        }

        public MultiComponentList(List<MultiTileEntry> arr)
        {
            m_Min = m_Max = Point.Empty;
            int itemcount = arr.Count;
            Count = itemcount;
            m_SortedTiles = new MultiTileEntry[itemcount];
            m_Min.X = 10000;
            m_Min.Y = 10000;
            int i = 0;
            foreach (MultiTileEntry entry in arr)
            {
                if (entry.m_OffsetX < m_Min.X)
                    m_Min.X = entry.m_OffsetX;

                if (entry.m_OffsetY < m_Min.Y)
                    m_Min.Y = entry.m_OffsetY;

                if (entry.m_OffsetX > m_Max.X)
                    m_Max.X = entry.m_OffsetX;

                if (entry.m_OffsetY > m_Max.Y)
                    m_Max.Y = entry.m_OffsetY;

                if (entry.m_OffsetZ > m_maxHeight)
                    m_maxHeight = entry.m_OffsetZ;
                m_SortedTiles[i] = entry;

                ++i;
            }
            arr.Clear();
            int centerx = m_Max.X - (int)(Math.Round((m_Max.X - m_Min.X) / 2.0));
            int centery = m_Max.Y - (int)(Math.Round((m_Max.Y - m_Min.Y) / 2.0));

            m_Min = m_Max = Point.Empty;
            for (i = 0; i < m_SortedTiles.Length; ++i)
            {
                m_SortedTiles[i].m_OffsetX -= (short)centerx;
                m_SortedTiles[i].m_OffsetY -= (short)centery;
                if (m_SortedTiles[i].m_OffsetX < m_Min.X)
                    m_Min.X = m_SortedTiles[i].m_OffsetX;
                if (m_SortedTiles[i].m_OffsetX > m_Max.X)
                    m_Max.X = m_SortedTiles[i].m_OffsetX;

                if (m_SortedTiles[i].m_OffsetY < m_Min.Y)
                    m_Min.Y = m_SortedTiles[i].m_OffsetY;
                if (m_SortedTiles[i].m_OffsetY > m_Max.Y)
                    m_Max.Y = m_SortedTiles[i].m_OffsetY;
            }
            ConvertList();
        }

        public MultiComponentList(StreamReader stream, int count)
        {
            string line;
            int itemcount = 0;
            m_Min = m_Max = Point.Empty;
            m_SortedTiles = new MultiTileEntry[count];
            m_Min.X = 10000;
            m_Min.Y = 10000;

            while ((line = stream.ReadLine()) != null)
            {
                string[] split = Regex.Split(line, @"\s+");
                m_SortedTiles[itemcount].m_ItemID = Convert.ToUInt16(split[0]);
                m_SortedTiles[itemcount].m_Flags = Convert.ToInt32(split[1]);
                m_SortedTiles[itemcount].m_OffsetX = Convert.ToInt16(split[2]);
                m_SortedTiles[itemcount].m_OffsetY = Convert.ToInt16(split[3]);
                m_SortedTiles[itemcount].m_OffsetZ = Convert.ToInt16(split[4]);
                m_SortedTiles[itemcount].m_Unk1 = 0;

                MultiTileEntry e = m_SortedTiles[itemcount];

                if (e.m_OffsetX < m_Min.X)
                    m_Min.X = e.m_OffsetX;
                if (e.m_OffsetY < m_Min.Y)
                    m_Min.Y = e.m_OffsetY;
                if (e.m_OffsetX > m_Max.X)
                    m_Max.X = e.m_OffsetX;
                if (e.m_OffsetY > m_Max.Y)
                    m_Max.Y = e.m_OffsetY;
                if (e.m_OffsetZ > m_maxHeight)
                    m_maxHeight = e.m_OffsetZ;

                ++itemcount;
                if (itemcount == count)
                    break;
            }
            Count = itemcount;
            int centerx = m_Max.X - (int)(Math.Round((m_Max.X - m_Min.X) / 2.0));
            int centery = m_Max.Y - (int)(Math.Round((m_Max.Y - m_Min.Y) / 2.0));

            m_Min = m_Max = Point.Empty;
            int i = 0;
            for (; i < m_SortedTiles.Length; i++)
            {
                m_SortedTiles[i].m_OffsetX -= (short)centerx;
                m_SortedTiles[i].m_OffsetY -= (short)centery;
                if (m_SortedTiles[i].m_OffsetX < m_Min.X)
                    m_Min.X = m_SortedTiles[i].m_OffsetX;
                if (m_SortedTiles[i].m_OffsetX > m_Max.X)
                    m_Max.X = m_SortedTiles[i].m_OffsetX;

                if (m_SortedTiles[i].m_OffsetY < m_Min.Y)
                    m_Min.Y = m_SortedTiles[i].m_OffsetY;
                if (m_SortedTiles[i].m_OffsetY > m_Max.Y)
                    m_Max.Y = m_SortedTiles[i].m_OffsetY;
            }
            ConvertList();
        }

        private void ConvertList()
        {
            m_Center = new Point(-m_Min.X, -m_Min.Y);
            m_Width = (m_Max.X - m_Min.X) + 1;
            m_Height = (m_Max.Y - m_Min.Y) + 1;

            MTileList[][] tiles = new MTileList[m_Width][];
            m_Tiles = new MTile[m_Width][][];

            for (int x = 0; x < m_Width; ++x)
            {
                tiles[x] = new MTileList[m_Height];
                m_Tiles[x] = new MTile[m_Height][];

                for (int y = 0; y < m_Height; ++y)
                    tiles[x][y] = new MTileList();
            }

            for (int i = 0; i < m_SortedTiles.Length; ++i)
            {
                int xOffset = m_SortedTiles[i].m_OffsetX + m_Center.X;
                int yOffset = m_SortedTiles[i].m_OffsetY + m_Center.Y;

                tiles[xOffset][yOffset].Add((ushort)(m_SortedTiles[i].m_ItemID), (sbyte)m_SortedTiles[i].m_OffsetZ, (sbyte)m_SortedTiles[i].m_Flags, m_SortedTiles[i].m_Unk1);
            }

            m_Surface = 0;

            for (int x = 0; x < m_Width; ++x)
            {
                for (int y = 0; y < m_Height; ++y)
                {
                    m_Tiles[x][y] = tiles[x][y].ToArray();
                    for (int i = 0; i < m_Tiles[x][y].Length; ++i)
                        m_Tiles[x][y][i].Solver = i;
                    if (m_Tiles[x][y].Length > 1)
                        Array.Sort(m_Tiles[x][y]);
                    if (m_Tiles[x][y].Length > 0)
                        ++m_Surface;
                }
            }
        }


        public unsafe MultiComponentList(int graphic, int center_x, int center_y)
        {
            graphic &= (~0x4000);

            m_Center = new Point(center_x, center_y);

            short minX = 0;
            short minY = 0;
            short maxX = 0;
            short maxY = 0;
            Count = 0;

            if (Multis.MultiCollectionLoaded == false)
                return;

            ref UOFileIndex entry = ref Multis.GetValidRefEntry(graphic);

            Multis.m_File.SetData(entry.Address, entry.FileSize);
            bool movable = false;

            if (Multis.IsUOP)
            {
                if (entry.Length > 0 && entry.DecompressedLength > 0)
                {
                    Multis.m_File.Seek(entry.Offset);

                    byte[] buffer = null;
                    Span<byte> span = entry.DecompressedLength <= 1024 ? stackalloc byte[entry.DecompressedLength] : (buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(entry.DecompressedLength));
                    try
                    {
                        fixed (byte* dataPtr = span)
                        {
                            ZLib.Decompress
                            (
                                Multis.m_File.PositionAddress,
                                entry.Length,
                                0,
                                (IntPtr)dataPtr,
                                entry.DecompressedLength
                            );

                            StackDataReader reader = new StackDataReader(span.Slice(0, entry.DecompressedLength));
                            reader.Skip(4);

                            int count = reader.ReadInt32LE();
                            Count = count;
                            m_SortedTiles = new MultiTileEntry[count];

                            int sizeOf = sizeof(Multis.MultiBlockNew);

                            for (int i = 0; i < count; i++)
                            {
                                Multis.MultiBlockNew* block = (Multis.MultiBlockNew*)(reader.PositionAddress + i * sizeOf);
                                if (block->Unknown != 0)
                                {
                                    reader.Skip((int)(block->Unknown * 4));
                                }

                                if (block->X < minX)
                                {
                                    minX = block->X;
                                }

                                if (block->X > maxX)
                                {
                                    maxX = block->X;
                                }

                                if (block->Y < minY)
                                {
                                    minY = block->Y;
                                }

                                if (block->Y > maxY)
                                {
                                    maxY = block->Y;
                                }

                                if (block->Flags == 0 || block->Flags == 0x100)
                                {
                                    MultiComponentList.MultiTileEntry tempitem = new MultiComponentList.MultiTileEntry();
                                    tempitem.m_ItemID = block->ID;
                                    tempitem.m_Flags = block->Flags;
                                    tempitem.m_OffsetX = block->X;
                                    tempitem.m_OffsetY = block->Y;
                                    tempitem.m_OffsetZ = block->Z;
                                    tempitem.m_Unk1 = 0;
                                    m_SortedTiles[i] = tempitem;
                                    //Multi m = Multi.Create(block->ID);
                                    //m_OffsetX = block->X;
                                    //m.MultiOffsetY = block->Y;
                                    //m.MultiOffsetZ = block->Z;
                                    //m.Hue = Hue;
                                    //m.AlphaHue = 255;
                                    //m.IsCustom = false;
                                    //m.State = CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_DONT_REMOVE;
                                    //m.IsMovable = ItemData.IsMultiMovable;

                                    //m.SetInWorldTile((ushort)(X + block->X), (ushort)(Y + block->Y), (sbyte)(Z + block->Z));

                                    //house.Components.Add(m);

                                    //if (m.ItemData.IsMultiMovable)
                                    //{
                                    //    movable = true;
                                    //}
                                }
                                //else if (i == 0)
                                //{
                                 //   MultiGraphic = block->ID;
                                //}
                            }

                            reader.Release();
                            m_Max = new Point(maxX, maxY);
                            m_Min = new Point(minX, minY);
                            m_Width = (m_Max.X - m_Min.X) + 1;
                            m_Height = (m_Max.Y - m_Min.Y) + 1;

                            MTileList[][] tiles = new MTileList[m_Width][];
                            m_Tiles = new MTile[m_Width][][];

                            for (int x = 0; x < m_Width; ++x)
                            {
                                tiles[x] = new MTileList[m_Height];
                                m_Tiles[x] = new MTile[m_Height][];

                                for (int y = 0; y < m_Height; ++y)
                                    tiles[x][y] = new MTileList();
                            }

                            for (int i = 0; i < m_SortedTiles.Length; ++i)
                            {
                                int xOffset = m_SortedTiles[i].m_OffsetX - m_Min.X;
                                int yOffset = m_SortedTiles[i].m_OffsetY - m_Min.Y;

                                tiles[xOffset][yOffset].Add((ushort)(m_SortedTiles[i].m_ItemID), (sbyte)m_SortedTiles[i].m_OffsetZ, (sbyte)m_SortedTiles[i].m_Flags, m_SortedTiles[i].m_Unk1);
                            }
                        }

                    }
                    finally
                    {
                        if (buffer != null)
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }

                }
            }
        }
        /*
                        fixed (byte* dataPtr = span)
                        {
                            ZLib.Decompress
                            (
                                MultiLoader.Instance.File.PositionAddress,
                                entry.Length,
                                0,
                                (IntPtr)dataPtr,
                                entry.DecompressedLength
                            );

                            StackDataReader reader = new StackDataReader(span.Slice(0, entry.DecompressedLength));
                            reader.Skip(4);

                            int count = reader.ReadInt32LE();

                            int sizeOf = sizeof(MultiBlockNew);

                            for (int i = 0; i < count; i++)
                            {
                                MultiBlockNew* block = (MultiBlockNew*)(reader.PositionAddress + i * sizeOf);

                                if (block->Unknown != 0)
                                {
                                    reader.Skip((int)(block->Unknown * 4));
                                }

                                if (block->X < minX)
                                {
                                    minX = block->X;
                                }

                                if (block->X > maxX)
                                {
                                    maxX = block->X;
                                }

                                if (block->Y < minY)
                                {
                                    minY = block->Y;
                                }

                                if (block->Y > maxY)
                                {
                                    maxY = block->Y;
                                }

                                if (block->Flags == 0 || block->Flags == 0x100)
                                {
                                    Multi m = Multi.Create(block->ID);
                                    m.MultiOffsetX = block->X;
                                    m.MultiOffsetY = block->Y;
                                    m.MultiOffsetZ = block->Z;
                                    m.Hue = Hue;
                                    m.AlphaHue = 255;
                                    m.IsCustom = false;
                                    m.State = CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_DONT_REMOVE;
                                    m.IsMovable = ItemData.IsMultiMovable;

                                    m.SetInWorldTile((ushort)(X + block->X), (ushort)(Y + block->Y), (sbyte)(Z + block->Z));

                                    house.Components.Add(m);

                                    if (m.ItemData.IsMultiMovable)
                                    {
                                        movable = true;
                                    }
                                }
                                else if (i == 0)
                                {
                                    MultiGraphic = block->ID;
                                }
                            }

                            reader.Release();
                        }
                }
                else
                {
                    Log.Warn($"[MultiCollection.uop] invalid entry (0x{Graphic:X4})");
                }
            }
        }
        */

        public MultiComponentList(MTileList[][] newtiles, int count, int width, int height)
        {
            m_Min = m_Max = Point.Empty;
            m_SortedTiles = new MultiTileEntry[count];
            m_Center = new Point((int)(Math.Round((width / 2.0))) - 1, (int)(Math.Round((height / 2.0))) - 1);
            if (m_Center.X < 0)
                m_Center.X = width / 2;
            if (m_Center.Y < 0)
                m_Center.Y = height / 2;
            m_maxHeight = -128;

            int counter = 0;
            for (int x = 0; x < width; ++x)
            {
                for (int y = 0; y < height; ++y)
                {
                    MTile[] tiles = newtiles[x][y].ToArray();
                    for (int i = 0; i < tiles.Length; ++i)
                    {
                        m_SortedTiles[counter].m_ItemID = (ushort)(tiles[i].ID);
                        m_SortedTiles[counter].m_OffsetX = (short)(x - m_Center.X);
                        m_SortedTiles[counter].m_OffsetY = (short)(y - m_Center.Y);
                        m_SortedTiles[counter].m_OffsetZ = (short)(tiles[i].Z);
                        m_SortedTiles[counter].m_Flags = (int)tiles[i].Flag;
                        m_SortedTiles[counter].m_Unk1 = 0;

                        if (m_SortedTiles[counter].m_OffsetX < m_Min.X)
                            m_Min.X = m_SortedTiles[counter].m_OffsetX;
                        if (m_SortedTiles[counter].m_OffsetX > m_Max.X)
                            m_Max.X = m_SortedTiles[counter].m_OffsetX;
                        if (m_SortedTiles[counter].m_OffsetY < m_Min.Y)
                            m_Min.Y = m_SortedTiles[counter].m_OffsetY;
                        if (m_SortedTiles[counter].m_OffsetY > m_Max.Y)
                            m_Max.Y = m_SortedTiles[counter].m_OffsetY;
                        if (m_SortedTiles[counter].m_OffsetZ > m_maxHeight)
                            m_maxHeight = m_SortedTiles[counter].m_OffsetZ;
                        ++counter;
                    }
                }
            }
            Count = counter;
            ConvertList();
        }

        private MultiComponentList()
        {
            m_Tiles = new MTile[0][][];
            Count = 0;  
        }

        public void ExportToTextFile(string FileName)
        {
            using (StreamWriter Tex = new StreamWriter(new FileStream(FileName, FileMode.Create, FileAccess.ReadWrite), System.Text.Encoding.GetEncoding(1252)))
            {
                for (int i = 0; i < m_SortedTiles.Length; ++i)
                {
                    Tex.WriteLine(String.Format("0x{0:X} {1} {2} {3} {4}",
                                m_SortedTiles[i].m_ItemID,
                                m_SortedTiles[i].m_OffsetX,
                                m_SortedTiles[i].m_OffsetY,
                                m_SortedTiles[i].m_OffsetZ,
                                m_SortedTiles[i].m_Flags));
                }
            }
        }

        public void ExportToWscFile(string FileName)
        {
            using (StreamWriter Tex = new StreamWriter(new FileStream(FileName, FileMode.Create, FileAccess.ReadWrite), System.Text.Encoding.GetEncoding(1252)))
            {
                for (int i = 0; i < m_SortedTiles.Length; ++i)
                {
                    Tex.WriteLine(String.Format("SECTION WORLDITEM {0}", i));
                    Tex.WriteLine("{");
                    Tex.WriteLine(String.Format("\tID\t{0}", m_SortedTiles[i].m_ItemID));
                    Tex.WriteLine(String.Format("\tX\t{0}", m_SortedTiles[i].m_OffsetX));
                    Tex.WriteLine(String.Format("\tY\t{0}", m_SortedTiles[i].m_OffsetY));
                    Tex.WriteLine(String.Format("\tZ\t{0}", m_SortedTiles[i].m_OffsetZ));
                    Tex.WriteLine("\tColor\t0");
                    Tex.WriteLine("}");
                }
            }
        }

        public void ExportToUOAFile(string FileName)
        {
            using (StreamWriter Tex = new StreamWriter(new FileStream(FileName, FileMode.Create, FileAccess.ReadWrite), System.Text.Encoding.GetEncoding(1252)))
            {
                Tex.WriteLine("6 version");
                Tex.WriteLine("1 template id");
                Tex.WriteLine("-1 item version");
                Tex.WriteLine(String.Format("{0} num components", m_SortedTiles.Length));
                for (int i = 0; i < m_SortedTiles.Length; ++i)
                {
                    Tex.WriteLine(String.Format("{0} {1} {2} {3} {4}",
                                m_SortedTiles[i].m_ItemID,
                                m_SortedTiles[i].m_OffsetX,
                                m_SortedTiles[i].m_OffsetY,
                                m_SortedTiles[i].m_OffsetZ,
                                m_SortedTiles[i].m_Flags));
                }
            }
        }
    }
}
