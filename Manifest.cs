using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EchoBootstrapper
{
    [DataContract]
    internal class Manifest
    {
        [DataMember(Name = "available")] public bool Available { get; set; }

        [DataMember(Name = "format")] public int Format { get; set; }

        [DataMember(Name = "version")] public string Version { get; set; }

        [DataMember(Name = "size")] public long Size { get; set; }

        [DataMember(Name = "url")] public string Url { get; set; }

        [DataMember(Name = "packages")] public List<Package> Packages { get; set; }
    }

    [DataContract]
    internal class Package
    {
        [DataMember(Name = "name")] public string Name { get; set; }

        [DataMember(Name = "sha256")] public string Sha256 { get; set; }

        [DataMember(Name = "size")] public long Size { get; set; }

        [DataMember(Name = "files")] public int Files { get; set; }

        [DataMember(Name = "dirs")] public List<string> Dirs { get; set; }

        [DataMember(Name = "url")] public string Url { get; set; }
    }
}
