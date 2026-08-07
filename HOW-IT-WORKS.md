# How it works

The bootstrapper does three things: it keeps the client and Studio up to date, it
lets the website start a game, and it tells you when it is itself out of date.

## What happens on a run

1. **Version check.** It asks GitHub for the newest release and compares the tag
   with its own assembly version. The answer is only remembered for step 5; a
   failed or slow check (10 second limit) counts as "not outdated" and never
   holds up a launch.
2. **Manifest.** It reads `manifest-v2.json` from the site. The manifest carries a
   version string and a list of packages, each with a name, a size and a SHA-256.
3. **Client.** If the version in `client\.version` differs from the manifest, or
   the player executable is missing, the packages are downloaded three at a time.
   Each one is verified against its SHA-256 - a package already in `Downloads`
   with a matching hash is reused instead of fetched again. They are unpacked
   into `client.new`, the version file is written, and only then is the old
   folder deleted and the new one moved into place, so an interrupted update
   never leaves a half-installed client.
4. **Studio.** The same idea with one file. Its version is the `ETag` of
   `studio.zip`, read with a HEAD request; if that matches `studio\.version` and
   the executable is there, nothing is downloaded.
5. **Protocol, shortcuts, launch.** The `echo-player` URL protocol is registered
   for the current user, desktop shortcuts are written for the client and Studio,
   and - when started from a game link - the player is launched. If step 1 said
   the launcher is out of date, the window then says so, waits three seconds and
   opens the download page.

## Layout on disk

```
%LOCALAPPDATA%\Echocore\
    client\     the player, plus .version - the manifest version it came from
    client2021\ the 2021 player, same layout, fetched on demand (see below)
    studio\     Studio, plus .version - the ETag of studio.zip
    Downloads\  verified package archives, reused between updates
```

`client2021` is a sibling of `client`, not a folder inside it, because a client
update deletes and replaces its whole folder.

## Starting a game

The website hands the launcher a link:

```
echo-player:https://echocore.xyz/game/placelauncher.ashx?ticket=...
```

The address is read back out of that (percent-encoded, `http//` and `http:/`
forms all survive), the ticket is taken from the query string, and the player is
started with the three arguments it expects:

```
--authenticationUrl "https://echocore.xyz/Login/Negotiate.ashx"
--authenticationTicket "<ticket>"
--joinScriptUrl "<the whole link>"
```

### Which client

Each place is published for one client version, and the site puts it in the link
as `&era=2016` or `&era=2021`. `2021` starts the player in `client2021`,
downloading it from `manifest-2021.json` first if this is the first 2021 place
the user has joined. Anything else - including the parameter being absent, which
is every link made before this existed - starts the usual client.

The launcher only decides *which player to open*. `placelauncher.ashx` re-reads
the era from the place itself and ignores what the link claimed, so editing the
parameter by hand changes nothing except which client the person editing it ends
up running.

## What it deliberately does not do

**It does not update itself.** Downloading a program and running it in place of
the running one is the shape of a dropper, and Windows Defender read it exactly
that way: an unsigned build with no reputation was quarantined on sight as
`Trojan:Win32/SuspExecRep.A!cl`. Rebuilding the same source under a different
version number produced a file it did not touch, so the verdict was about the
file being unknown rather than about the code - but the behaviour is what puts a
launcher in front of that judgement in the first place. So it only looks, and
asks for the new one to be downloaded from the site.

**It does not write a log.** Nothing about a launch is kept on disk. Failures are
shown in the window instead.

## Where things come from

| what | where |
| --- | --- |
| manifest | `https://echocore.xyz/client/manifest-v2.json` |
| Studio | `https://echocore.xyz/client/studio.zip` |
| version check | `https://api.github.com/repos/j5nya/ECBootstraper/releases/latest` |
| download page | `https://echocore.xyz/download` |

All of them are constants in `Config.cs`. Nothing the launcher runs or opens comes
from a server response.
