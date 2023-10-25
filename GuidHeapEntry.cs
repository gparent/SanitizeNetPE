using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace SanitizeNetPE;

internal class GuidHeapEntry
{
    readonly MetadataReader metadata;
    readonly GuidHandle handle;

    public int Index => metadata.GetHeapOffset(handle);

    public static int Length => 16;

    public Guid Guid => metadata.GetGuid(handle);

    public GuidHeapEntry(MetadataReader metadata, GuidHandle handle)
    {
        this.metadata = metadata;
        this.handle = handle;
    }
}
