namespace EchoBootstrapper
{
    internal static class Config
    {
        public const string ProductName = "Echocore";

        public const string DisplayName = "EchoCore";

        public const string SiteUrl = "https://echocore.xyz";

        public const string ManifestUrl = "https://echocore.xyz/client/manifest-v2.json";

        public const string StudioUrl = "https://echocore.xyz/client/studio.zip";

        // A place can be published for the 2021 client instead of the 2016 one. That
        // client is a separate build with its own packages, so it gets its own manifest
        // and its own folder rather than overwriting the one everybody already has.
        public const string Manifest2021Url = "https://echocore.xyz/client/manifest-2021.json";

        public const string PlayerExecutable = "EchoCorePlayerBt.exe";

        public const string StudioExecutable = "EchoCoreStudioBt.exe";

        public const string ProtocolScheme = "echo-player";

        // The website's "Edit in Studio" button opens echo-studio://<edit url>. Nothing
        // claimed that scheme before, so the click silently did nothing after an install.
        public const string StudioProtocolScheme = "echo-studio";

        public const string ReleasesApiUrl = "https://api.github.com/repos/j5nya/ECBootstraper/releases/latest";

        public const string DownloadPageUrl = "https://echocore.xyz/download";

        public const int ParallelDownloads = 3;
    }
}
