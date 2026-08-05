using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EchoBootstrapper
{
    [DataContract]
    internal class Release
    {
        [DataMember(Name = "tag_name")] public string Tag { get; set; }

        [DataMember(Name = "assets")] public List<ReleaseAsset> Assets { get; set; }
    }

    [DataContract]
    internal class ReleaseAsset
    {
        [DataMember(Name = "name")] public string Name { get; set; }

        [DataMember(Name = "browser_download_url")] public string Url { get; set; }
    }
}
