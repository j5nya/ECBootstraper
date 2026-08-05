using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace EchoBootstrapper
{
    internal class InstallOptions
    {
        public bool DesktopShortcut;
        public bool RegisterProtocol = true;
    }

    internal struct Status
    {
        public string Text;
        public int Percent;
        public Status(string text, int percent) { Text = text; Percent = percent; }
    }

    internal class Installer
    {
        private readonly HttpClient _http;

        public Installer()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            _http.DefaultRequestHeaders.Add("User-Agent", Config.ProductName + "Bootstrapper/1.0");
        }

        public static string RootDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Config.ProductName);

        public static string ClientDir => Path.Combine(RootDir, "client");

        public static string StudioDir => Path.Combine(RootDir, "studio");

        public static string DownloadsDir => Path.Combine(RootDir, "Downloads");

        private static string VersionFile(string folder) => Path.Combine(folder, ".version");

        private static string InstalledVersion(string folder)
        {
            try
            {
                var file = VersionFile(folder);
                return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
            }
            catch { return null; }
        }

        public static string PlayerPath() => Path.Combine(ClientDir, Config.PlayerExecutable);

        public static string StudioPath() => Path.Combine(StudioDir, Config.StudioExecutable);

        public static bool IsClientCurrent(string version) =>
            InstalledVersion(ClientDir) == version && File.Exists(PlayerPath());

        private static string SelfPath() => Process.GetCurrentProcess().MainModule.FileName;

        private static string AttemptedUpdateFile => Path.Combine(DownloadsDir, "selfupdate.txt");

        private static Version CurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
        }

        /// <summary>
        /// Reads a release tag like "v1.0.4" as a version. Missing parts count as zero,
        /// so 1.0.4 and 1.0.4.0 compare equal instead of the shorter one losing.
        /// </summary>
        internal static Version ParseVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var match = Regex.Match(text.Trim().TrimStart('v', 'V'), @"^\d+(\.\d+){0,3}");
            if (!match.Success) return null;

            var parts = match.Value.Split('.');
            var numbers = new int[4];
            for (var i = 0; i < 4; i++)
                numbers[i] = i < parts.Length && int.TryParse(parts[i], out var value) ? value : 0;

            return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        }

        /// <summary>
        /// Replaces this exe with a newer release and starts it again with the same
        /// arguments. Returns true when that happened, and the caller should quit.
        ///
        /// Anything that goes wrong here is not worth stopping over: the point of the
        /// program is to launch the game, so every failure falls through to doing that
        /// with the version already on disk.
        /// </summary>
        public async Task<bool> TryUpdateSelfAsync(string[] args, IProgress<Status> progress, CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(DownloadsDir);
                var exe = SelfPath();
                TryDelete(exe + ".old");

                var current = CurrentVersion();
                var release = await FetchReleaseAsync(ct).ConfigureAwait(false);
                var latest = ParseVersion(release?.Tag);
                if (latest == null || latest <= current) { ForgetAttempt(current); return false; }

                // A release whose exe reports an older version than its own tag would
                // otherwise be downloaded again on every single launch, forever.
                if (latest.ToString() == LastAttempt()) return false;

                var asset = release.Assets?.FirstOrDefault(a =>
                    string.Equals(a.Name, Config.UpdateAssetName, StringComparison.OrdinalIgnoreCase));
                if (asset == null || string.IsNullOrEmpty(asset.Url)) return false;

                progress?.Report(new Status("Updating the launcher...", 3));

                var fresh = Path.Combine(DownloadsDir, "EchoBootstrapper.new.exe");
                await DownloadAsync(asset.Url, fresh, null, ct).ConfigureAwait(false);

                if (!LooksLikeProgram(fresh)) { TryDelete(fresh); return false; }

                RememberAttempt(latest);

                var parked = exe + ".old";
                File.Move(exe, parked);
                try
                {
                    File.Move(fresh, exe);
                }
                catch (Exception)
                {
                    File.Move(parked, exe);
                    throw;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = string.Join(" ", args.Select(Quote)),
                    UseShellExecute = false,
                });
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<Release> FetchReleaseAsync(CancellationToken ct)
        {
            // The launch must not hang on a slow or blocked api; the client download
            // that follows is the part worth waiting for.
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(10));

                using (var request = new HttpRequestMessage(HttpMethod.Get, Config.ReleasesApiUrl))
                {
                    request.Headers.Add("Accept", "application/vnd.github+json");
                    using (var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) return null;
                        return Deserialize<Release>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    }
                }
            }
        }

        private static bool LooksLikeProgram(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < 64 * 1024) return false;

                using (var file = File.OpenRead(path))
                    return file.ReadByte() == 'M' && file.ReadByte() == 'Z';
            }
            catch { return false; }
        }

        private static string LastAttempt()
        {
            try
            {
                return File.Exists(AttemptedUpdateFile) ? File.ReadAllText(AttemptedUpdateFile).Trim() : null;
            }
            catch { return null; }
        }

        private static void RememberAttempt(Version version)
        {
            try { File.WriteAllText(AttemptedUpdateFile, version.ToString()); } catch { }
        }

        private static void ForgetAttempt(Version current)
        {
            try
            {
                var attempted = ParseVersion(LastAttempt());
                if (attempted != null && current >= attempted) TryDelete(AttemptedUpdateFile);
            }
            catch { }
        }

        public async Task<Manifest> FetchManifestAsync(CancellationToken ct)
        {
            var json = await _http.GetStringAsync(Config.ManifestUrl).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            var manifest = Deserialize<Manifest>(json);

            if (manifest == null) throw new Exception("The server sent a manifest this build cannot read.");
            if (!manifest.Available) throw new Exception("Downloads are turned off on the server right now.");
            if (manifest.Format != 2) throw new Exception("Manifest format " + manifest.Format + " is newer than this bootstrapper. Update it.");
            if (manifest.Packages == null || manifest.Packages.Count == 0) throw new Exception("The manifest lists no packages.");

            return manifest;
        }

        public async Task InstallAsync(Manifest manifest, InstallOptions options,
            IProgress<Status> progress, CancellationToken ct)
        {
            Directory.CreateDirectory(DownloadsDir);

            var installed = false;

            if (!IsClientCurrent(manifest.Version))
            {
                await InstallClientAsync(manifest, progress, ct).ConfigureAwait(false);
                installed = true;
            }

            installed |= await InstallStudioAsync(progress, ct).ConfigureAwait(false);

            progress?.Report(new Status("Finishing up...", 98));

            if (options.RegisterProtocol) RegisterProtocol();

            if (options.DesktopShortcut && installed)
            {
                CreateDesktopShortcut(PlayerPath(), Config.ProductName);
                CreateDesktopShortcut(StudioPath(), Config.DisplayName + " Studio");
            }

            RemoveLegacyLayout();

            progress?.Report(new Status("Ready.", 100));
        }

        private async Task InstallClientAsync(Manifest manifest, IProgress<Status> progress, CancellationToken ct)
        {
            long totalBytes = manifest.Packages.Sum(p => p.Size);
            long doneBytes = 0;
            var gate = new object();

            var queue = new Queue<Package>(manifest.Packages.OrderByDescending(p => p.Size));
            var workers = new List<Task>();

            for (var i = 0; i < Math.Min(Config.ParallelDownloads, manifest.Packages.Count); i++)
            {
                workers.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        Package package;
                        lock (gate)
                        {
                            if (queue.Count == 0) return;
                            package = queue.Dequeue();
                        }

                        ct.ThrowIfCancellationRequested();
                        var zipPath = Path.Combine(DownloadsDir, package.Sha256 + ".zip");

                        if (!(File.Exists(zipPath) && Sha256OfFile(zipPath) == package.Sha256))
                        {
                            await DownloadAsync(package.Url, zipPath, read =>
                            {
                                lock (gate)
                                {
                                    doneBytes += read;
                                    var pct = totalBytes > 0 ? (int)(doneBytes * 90 / totalBytes) : 0;
                                    progress?.Report(new Status("Downloading " + package.Name + "...", Math.Min(pct, 90)));
                                }
                            }, ct).ConfigureAwait(false);

                            if (Sha256OfFile(zipPath) != package.Sha256)
                            {
                                TryDelete(zipPath);
                                throw new Exception("Package '" + package.Name + "' arrived damaged. Try again.");
                            }
                        }
                        else
                        {
                            lock (gate)
                            {
                                doneBytes += package.Size;
                                progress?.Report(new Status("Verified " + package.Name, Math.Min((int)(doneBytes * 90 / Math.Max(totalBytes, 1)), 90)));
                            }
                        }

                    }
                }, ct));
            }

            await Task.WhenAll(workers).ConfigureAwait(false);

            var staging = ClientDir + ".new";
            ReplaceFolder(staging);

            foreach (var package in manifest.Packages)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new Status("Unpacking " + package.Name + "...", -1));
                ExtractInto(Path.Combine(DownloadsDir, package.Sha256 + ".zip"), staging);
            }

            File.WriteAllText(VersionFile(staging), manifest.Version);

            DeleteFolder(ClientDir);
            Directory.Move(staging, ClientDir);
        }

        private async Task<bool> InstallStudioAsync(IProgress<Status> progress, CancellationToken ct)
        {
            var version = await RemoteVersionAsync(Config.StudioUrl, ct).ConfigureAwait(false);
            if (InstalledVersion(StudioDir) == version && File.Exists(StudioPath())) return false;

            progress?.Report(new Status("Downloading Studio...", 92));
            var zip = Path.Combine(DownloadsDir, "studio.zip");
            await DownloadAsync(Config.StudioUrl, zip, null, ct).ConfigureAwait(false);

            progress?.Report(new Status("Unpacking Studio...", 96));
            ReplaceFolder(StudioDir);
            ExtractInto(zip, StudioDir);
            File.WriteAllText(VersionFile(StudioDir), version);

            TryDelete(zip);
            return true;
        }

        private static void DeleteFolder(string folder)
        {
            if (!Directory.Exists(folder)) return;
            try { Directory.Delete(folder, true); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new Exception("Close " + Config.DisplayName + " first - its files are still open.");
            }
        }

        private static void ReplaceFolder(string folder)
        {
            DeleteFolder(folder);
            Directory.CreateDirectory(folder);
        }

        private static void RemoveLegacyLayout()
        {
            try
            {
                var stale = Path.Combine(RootDir, "Versions");
                if (Directory.Exists(stale)) Directory.Delete(stale, true);
            }
            catch {  }
        }

        private async Task<string> RemoteVersionAsync(string url, CancellationToken ct)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Head, url))
            using (var response = await _http.SendAsync(request, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var tag = response.Headers.ETag != null ? response.Headers.ETag.Tag : null;
                if (!string.IsNullOrEmpty(tag)) return tag.Trim('"', 'W', '/');

                var modified = response.Content.Headers.LastModified;
                var length = response.Content.Headers.ContentLength;
                return (modified.HasValue ? modified.Value.UtcTicks.ToString() : "?")
                       + "-" + (length.HasValue ? length.Value.ToString() : "?");
            }
        }

        private async Task DownloadAsync(string url, string path, Action<long> onRead, CancellationToken ct)
        {
            var temp = path + ".part";
            TryDelete(temp);

            using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await file.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                        onRead?.Invoke(read);
                    }
                }
            }

            TryDelete(path);
            File.Move(temp, path);
        }

        private static string Sha256OfFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static void ExtractInto(string zipPath, string targetDir)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    var destination = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

                    if (!destination.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Package contains a path outside the install folder: " + entry.FullName);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    entry.ExtractToFile(destination, true);
                }
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        public static void RegisterProtocol()
        {
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Config.ProtocolScheme))
            {
                key.SetValue("", "URL:" + Config.ProductName + " Protocol");
                key.SetValue("URL Protocol", "");
                using (var icon = key.CreateSubKey("DefaultIcon")) icon.SetValue("", exe + ",1");
                using (var cmd = key.CreateSubKey(@"shell\open\command")) cmd.SetValue("", "\"" + exe + "\" \"%1\"");
            }
        }

        public static void CreateDesktopShortcut(string targetExe, string name)
        {
            try
            {
                if (!File.Exists(targetExe)) return;

                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var linkPath = Path.Combine(desktop, name + ".lnk");

                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                var shell = Activator.CreateInstance(shellType);
                var shortcut = Invoke(shell, "CreateShortcut", linkPath);
                if (shortcut == null) return;

                SetProperty(shortcut, "TargetPath", targetExe);
                SetProperty(shortcut, "WorkingDirectory", Path.GetDirectoryName(targetExe));
                SetProperty(shortcut, "IconLocation", targetExe + ",0");
                SetProperty(shortcut, "Description", name);
                Invoke(shortcut, "Save");
            }
            catch {  }
        }

        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, target, args);

        private static void SetProperty(object target, string property, object value) =>
            target.GetType().InvokeMember(property, System.Reflection.BindingFlags.SetProperty, null, target, new[] { value });

        public bool LaunchFromProtocol(string argument, IProgress<Status> progress)
        {
            if (string.IsNullOrEmpty(argument)) return false;

            var prefix = Config.ProtocolScheme + ":";
            if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

            var url = ResolveJoinUrl(argument);
            var parsed = new Uri(url);
            var ticket = ReadQueryValue(parsed.Query, "ticket");
            if (string.IsNullOrEmpty(ticket))
                throw new Exception("The join link carries no ticket, so the game could not log in.");

            var player = PlayerPath();
            if (!File.Exists(player)) throw new Exception("The player is missing from the install. Run the installer again.");

            var arguments =
                "--authenticationUrl " + Quote(parsed.Scheme + "://" + parsed.Authority + "/Login/Negotiate.ashx")
                + " --authenticationTicket " + Quote(ticket)
                + " --joinScriptUrl " + Quote(url);

            progress?.Report(new Status("Starting the game...", 100));
            Process.Start(new ProcessStartInfo
            {
                FileName = player,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(player),
                UseShellExecute = false,
            });
            return true;
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static string ReadQueryValue(string query, string key)
        {
            foreach (var pair in (query ?? string.Empty).TrimStart('?').Split('&'))
            {
                var equals = pair.IndexOf('=');
                if (equals <= 0) continue;
                if (!string.Equals(pair.Substring(0, equals), key, StringComparison.OrdinalIgnoreCase)) continue;

                return Uri.UnescapeDataString(pair.Substring(equals + 1).Replace("+", " "));
            }
            return null;
        }

        internal static string ResolveJoinUrl(string argument)
        {
            var prefix = Config.ProtocolScheme + ":";
            var url = argument.Substring(prefix.Length).TrimStart('/');

            if (Regex.IsMatch(url, "^https?%3A", RegexOptions.IgnoreCase))
                url = Uri.UnescapeDataString(url);

            url = Regex.Replace(url, "^(https?):?/{1,3}", "$1://", RegexOptions.IgnoreCase);

            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                throw new Exception("Could not read the join link from the website.");

            return url;
        }

        private static T Deserialize<T>(string json) where T : class
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)))
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                return serializer.ReadObject(stream) as T;
            }
        }
    }
}
