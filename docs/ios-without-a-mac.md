# Getting it on an iPhone without a Mac

Apple only lets an iPhone app be built on macOS, with Xcode, and Unity's iOS module only exports an
Xcode project there. So a Linux PC can't build this app itself. What it can do is have the build run
on GitHub's macOS runners and carry the result back to your PC, where it gets signed for your phone:

    push  ->  GitHub Actions (macOS): Unity exports, Xcode builds  ->  unsigned .ipa
    Linux PC: signs the .ipa with your Apple ID and installs it over USB, or the phone's Wi-Fi

You need: your GitHub fork of this repository, a free Unity account, and a free Apple ID. No paid
Apple Developer account, and no Mac.

The workflows are in `.github/workflows/`: `unity-activation.yml` makes the licence file once, and
`ios-build.yml` builds the app.

## 1. Turn on Actions

A fork starts with Actions off. On the repository's page: **Actions**, then *I understand my
workflows, go ahead and enable them*.

## 2. Get a Unity licence for the build

The macOS runner needs a licence, and a free Unity Personal one is enough.

1. In GitHub: **Settings > Secrets and variables > Actions > New repository secret**, add
   `UNITY_EMAIL` and `UNITY_PASSWORD` with a Unity account's own details (create one at
   <https://id.unity.com> if you haven't).
2. Run the **Unity activation** workflow (Actions > Unity activation > Run workflow). When it
   finishes, download its `unity-license-request` artifact, and unzip it: it holds a `.alf` file.
3. Open <https://license.unity3d.com/manual>, sign in with the same Unity account, upload the
   `.alf`, and choose *Unity Personal* (and that you don't use Unity professionally). It gives you a
   `.ulf` file.
4. Add a third secret, `UNITY_LICENSE`, whose value is the **whole text** of that `.ulf` file
   (open it in a text editor and paste all of it).

The licence belongs to that Unity account: keep the secret private.

## 3. Build the app

Actions > **iOS build** > *Run workflow* (or just push to `main`). It takes a while the first time —
it installs the Unity editor, so give it 30–60 minutes — and less afterwards, thanks to the cached
`Library` folder. When it's done, the run has an artifact called **CardboardXR-unsigned-ipa**
holding `CardboardXR-unsigned.ipa`.

That file is built but not signed: iOS won't run it until it's signed for your phone.

## 4. Sign and install from your Linux PC

Use a sideloading tool that can sign with a free Apple ID from Linux. [SideStore](https://sidestore.io)
is the one that needs no Mac and no Windows PC:

1. Install SideStore's server on your PC and its app on the iPhone (their site has the current steps;
   it uses your phone's pairing and can work over Wi-Fi).
2. In SideStore, sign in with the same free Apple ID, add the `CardboardXR-unsigned.ipa`, and install
   it. The signing replaces the missing signature with one made for your device.

[AltStore](https://altstore.io) with its Linux server does the same if you prefer it. A free Apple
ID's signature expires after 7 days: re-sign the same `.ipa` in the app to keep playing.

## 5. On the iPhone

- The first install asks you to trust the developer: **Settings > General > VPN & Device Management**,
  pick the Apple ID, then *Trust*.
- Developer Mode may be needed on iOS 16 and later: **Settings > Privacy & Security > Developer
  Mode**. That row only appears after a first install attempt.
- Launch CardboardXR once with the phone held normally. Set your viewer in the app's gear menu —
  *Tune viewer* for a Shinecon with no QR code, or *Set viewer* to scan a QR code — and you're in.

## If the Unity build fails

- **"No Unity version matches 6000.6.0f1"**: the runner couldn't find that editor. Change
  `unityVersion:` in `ios-build.yml` to the version Unity Hub offers you (any 6000.x works for this
  project), and re-run.
- **"License is not valid"**: re-do step 2; the `.ulf` must be pasted whole, and for the same
  account as `UNITY_EMAIL`.
- **Xcode can't find a scheme or project**: read the *Unity export* step's log; the export itself
  failed, and Unity's compiler errors are in it.

The same repository also builds for Android without any of this: with Unity's Linux editor and the
Android module on your PC, `zsh Tools/build_and_run.sh` builds and installs the APK. Only the iPhone
needs the Mac side above.
