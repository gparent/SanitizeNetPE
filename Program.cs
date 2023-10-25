using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using CommandLine;
using ICSharpCode.Decompiler.Metadata;

using SanitizeNetPE;

internal class Program
{
    private static readonly int DOSChecksumOffset = 18;

    internal class ProgramOptions
    {
        [Value(index: 0, Required = true, HelpText = "Image file path")]
        public string Path { get; set; }

        [Option('v', Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }

        [Option('w', Required = false, HelpText = "Write new image file")]
        public bool Write { get; set; }
    }

    private static void Main(string[] args)
    {
        Parser.Default.ParseArguments<ProgramOptions>(args).WithParsed(MainWithOptions);
    }

    private static void MainWithOptions(ProgramOptions options)
    {
        bool dirty = false;
        var imageBytes = File.ReadAllBytes(options.Path);

        using (var ms = new MemoryStream(imageBytes))
        {
            using var peReader = new PEReader(ms);

            UInt16 dosChecksum = BitConverter.ToUInt16(imageBytes, DOSChecksumOffset);
            if (dosChecksum != 0)
            {
                Array.Clear(imageBytes, DOSChecksumOffset, 2);
                dirty = true;
            }

            int timestampOffset = peReader.PEHeaders.CoffHeaderStartOffset + 4;
            UInt32 timestamp = BitConverter.ToUInt32(imageBytes, timestampOffset);
            if (timestamp != 0)
            {
                Array.Clear(imageBytes, timestampOffset, 4);
                dirty = true;
            }

            int optOffset = peReader.PEHeaders.CoffHeaderStartOffset + 20;
            int optChecksumOffset = optOffset + 64;
            UInt32 optChecksum = BitConverter.ToUInt32(imageBytes, optChecksumOffset);
            if (optChecksum != 0)
            {
                Array.Clear(imageBytes, optChecksumOffset, 4);
                dirty = true;
            }

            // If this is a .NET Assembly...
            if (peReader.HasMetadata)
            {
                using var module = new PEFile(options.Path, peReader);

                int heapEntries = module.Metadata.GetHeapSize(HeapIndex.Guid) / GuidHeapEntry.Length;
                if (heapEntries > 1)
                    throw new Exception("More than one GUID in the GUID heap");
                
                // The first GUID entry is probably the module's compiler generated GUID
                var entry = new GuidHeapEntry(module.Metadata, MetadataTokens.GuidHandle(1));

                if (entry.Guid != Guid.Empty) // Ignore Assemblies we've already processed
                {
                    var guidIdx = SearchBytes(imageBytes, entry.Guid.ToByteArray());

                    if (guidIdx != -1)
                    {
                        Array.Clear(imageBytes, guidIdx, GuidHeapEntry.Length);
                        dirty = true;
                    }
                }
            }
        }

        if (dirty && options.Write)
        {
            File.WriteAllBytes(options.Path, imageBytes);
        }
    }

    // https://stackoverflow.com/a/26880541
    private static int SearchBytes(byte[] haystack, byte[] needle)
    {
        var len = needle.Length;
        var limit = haystack.Length - len;
        for (var i = 0; i <= limit; i++)
        {
            var k = 0;
            for (; k < len; k++)
            {
                if (needle[k] != haystack[i + k]) break;
            }
            if (k == len) return i;
        }
        return -1;
    }
}