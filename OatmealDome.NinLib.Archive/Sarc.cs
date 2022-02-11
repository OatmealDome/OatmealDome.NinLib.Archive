using System.Collections;
using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.Archive;

public sealed class Sarc : IEnumerable<KeyValuePair<string, byte[]>>
{
    private struct SfatNode
    {
        public uint Hash;
        public bool HasName;
        public int NameOfs;
        public uint DataOfs;
        public uint DataLength;
    }

    private Dictionary<string, byte[]> _files;

    public byte[] this[string key]
    {
        get => _files[key];
        set => throw new NotImplementedException();
    }

    public Sarc(byte[] rawSarc)
    {
        using (MemoryStream memoryStream = new MemoryStream(rawSarc))
        {
            Read(memoryStream);
        }
    }

    public Sarc(Stream stream)
    {
        Read(stream);
    }

    private void Read(Stream stream)
    {
        // Create a dictionary to hold the files
        _files = new Dictionary<string, byte[]>();

        using (BinaryDataReader reader = new BinaryDataReader(stream, true))
        {
            // Set endianness to big by default
            reader.ByteOrder = ByteOrder.BigEndian;

            // Verify the magic numbers
            if (reader.ReadString(4) != "SARC")
            {
                throw new ArchiveException("Not a SARC file");
            }

            // Skip the header length
            reader.Seek(2);

            // Check the byte order mark to see if this file is little endian
            if (reader.ReadUInt16() == 0xFFFE)
            {
                // Set the endianness to little
                reader.ByteOrder = ByteOrder.LittleEndian;
            }

            // Check the file length
            if (reader.ReadUInt32() != reader.Length)
            {
                throw new ArchiveException("SARC is possibly corrupt, invalid length");
            }

            // Read the beginning of data offset
            uint dataBeginOfs = reader.ReadUInt32();

            // Verify the version
            if (reader.ReadUInt16() != 0x0100)
            {
                throw new ArchiveException("Unsupported SARC version");
            }

            // Seek past the reserved area
            reader.Seek(2);

            // Verify the SFAT magic numbers
            if (reader.ReadString(4) != "SFAT")
            {
                throw new ArchiveException("Could not find SFAT section");
            }

            // Skip the header length
            reader.Seek(2);

            // Read the node count and hash key
            ushort nodeCount = reader.ReadUInt16();
            uint hashKey = reader.ReadUInt32();

            // Read every node
            List<SfatNode> nodes = new List<SfatNode>();
            for (ushort i = 0; i < nodeCount; i++)
            {
                // Read the node details
                uint hash = reader.ReadUInt32();
                uint fileAttrs = reader.ReadUInt32();
                uint nodeDataBeginOfs = reader.ReadUInt32();
                uint nodeDataEndOfs = reader.ReadUInt32();

                // Create a new SfatNode
                nodes.Add(new SfatNode()
                {
                    Hash = hash,
                    HasName = (fileAttrs & 0x01000000) == 0x01000000, // check for name flag
                    NameOfs = (int)(fileAttrs & 0x0000FFFF) * 4, // mask upper bits and multiply by 4
                    DataOfs = nodeDataBeginOfs,
                    DataLength = nodeDataEndOfs - nodeDataBeginOfs
                });
            }

            // Verify the SFNT magic numbers
            if (reader.ReadString(4) != "SFNT")
            {
                throw new ArchiveException("Could not find SFNT section");
            }

            // SKip header length and reserved area
            reader.Seek(4);

            // Get the file name beginning offset
            long nameBeginOfs = reader.Position;

            // Read each file using its SfatNode
            foreach (SfatNode node in nodes)
            {
                // Read the filename
                string filename;

                // Check if there is a name offset
                if (node.HasName)
                {
                    // Read the name at this position
                    using (reader.TemporarySeek(nameBeginOfs + node.NameOfs, SeekOrigin.Begin))
                    {
                        filename = reader.ReadString(StringDataFormat.ZeroTerminated);
                    }
                }
                else
                {
                    // Use the hash as the name
                    filename = node.Hash.ToString("X8") + ".bin";
                }

                // Read the file data
                byte[] fileData;
                using (reader.TemporarySeek(dataBeginOfs + node.DataOfs, SeekOrigin.Begin))
                {
                    fileData = reader.ReadBytes((int)node.DataLength);
                }

                // Add the file to the dictionary
                _files.Add(filename, fileData);
            }
        }
    }

    public IEnumerator<KeyValuePair<string, byte[]>> GetEnumerator()
    {
        return _files.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}