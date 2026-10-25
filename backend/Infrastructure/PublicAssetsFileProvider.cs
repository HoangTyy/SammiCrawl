using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace SammiCrawl.Backend.Infrastructure;

// Keep private media out of the public provider even if a path contains dot segments.
public sealed class PublicAssetsFileProvider(IFileProvider inner, string mediaRoot) : IFileProvider
{
    readonly string root = Path.GetFullPath(mediaRoot).TrimEnd(Path.DirectorySeparatorChar);
    bool Private(IFileInfo file)
    {
        if (file.PhysicalPath is null) return false;
        var path = Path.GetFullPath(file.PhysicalPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.Equals(root, comparison) || path.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }
    public IFileInfo GetFileInfo(string subpath)
    {
        var file = inner.GetFileInfo(subpath);
        return Private(file) ? new NotFoundFileInfo(file.Name) : file;
    }
    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
    public IChangeToken Watch(string filter) => inner.Watch(filter);
}
