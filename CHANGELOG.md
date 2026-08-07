# Changelog

## 1.1.0

- **The player window now says EchoCore, not Roblox.** That caption is produced by the
  packed client at runtime - there is no string in the file to patch - so the launcher
  renames the window from outside once it appears, and holds the name for as long as the
  game runs (the caption is set again when the 3D window opens a few seconds in). The
  launcher stays resident, hidden, for the session to do this and exits when the game closes.

## 1.0.9

- **A place can now run on the 2021 client, and the launcher picks which one to
  open.** The website puts the place's client version in the launch link; the
  launcher reads it and starts the matching player. A link without it - which is
  every link made before this - means the 2016 client, so nothing about an
  existing install changes.
- The 2021 client is kept in its own folder (`client2021`) with its own manifest,
  and is only downloaded the first time somebody joins a place that needs it.
  Nobody pays for a second client they never open, and the 2016 client's update,
  which replaces its whole folder, cannot take it with it.
- If the server has not published the 2021 client yet, the launcher says exactly
  that instead of reporting a manifest it could not read - the player has nothing
  to fix on their end and should not be sent looking.

## 1.0.8

- The "you are out of date" message is shown for three seconds before the
  download page opens. Opening it straight away put a browser window over the
  launcher before its one line of explanation could be read, so the site turned
  up for no visible reason.

## 1.0.7

- **Removed self-updating.** It used to download the newest release, rename
  itself out of the way, put the new file in its place and start it again.
  Windows Defender quarantined 1.0.6 on sight as `Trojan:Win32/SuspExecRep.A!cl`.
  Rebuilding the same source under a different version number produced a file it
  did not touch, so the verdict rested on the file being unsigned and unknown
  rather than on the code - but downloading and running a program is what puts a
  launcher in front of that judgement at all. It now only compares its version
  with the newest release, and asks for the new one to be downloaded from the
  site.

## 1.0.6

- Stopped writing `launch.log`. Nothing about a launch is kept on disk any more.

## 1.0.5

- Added a self-update. Removed again in 1.0.7 - see above.

## 1.0.4

- Desktop shortcut for Studio. It was being installed and then left with no way
  to start it.

## 1.0.3

- **Studio is installed alongside the client**, not behind a `--studio` flag that
  nobody passed.
- Fixed the Studio executable name: the archive ships `EchoCoreStudioBt.exe`, not
  `RobloxStudioBeta.exe`. The "already installed" check could never pass, so
  Studio was re-downloaded in full - 97 MB - on every single launch.
- The window now names what it is downloading.

## 1.0.2

- Player executable renamed to `EchoCorePlayerBt.exe`.

## 1.0.1

- Updates are checked on every launch, not only on the first one.
- The client is unpacked into a staging folder and moved into place only once it
  is complete, so an interrupted update no longer leaves a half-installed client.

## 1.0.0

- First release. Reads `manifest-v2.json`, downloads and verifies the client
  packages by SHA-256, registers the `echo-player` protocol and starts the game
  from a link on the site.
