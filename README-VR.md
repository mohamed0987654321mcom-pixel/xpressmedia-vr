# XpressMedia — Meta Quest (VR)

A native Quest client for XpressMedia: the Feed gets a real spatial redesign
(a curved video wall you flick through, a floating like/comment/follow rail,
a wrist-raise menu), and it talks to the **same** Node/Express backend the
iOS/Mac app uses — nothing on the server changes for this to work.

**Read this first, honestly:** Quest runs Horizon OS, which is Android under
the hood. There is no Swift/SwiftUI path onto it, so none of
`XpressMediaApp/`'s code carries over — this is a full second client, built
in Unity/C#. What *does* carry over is the API contract: every script in
here mirrors a specific Swift file 1:1 (noted in each file's header comment)
so both clients stay in sync if the backend ever changes.

I also don't have Unity, an Android SDK, or a Quest headset available to me
here, so — same as the Xcode project — this has been written and checked
carefully but not literally built and run. See "What's built vs what's
stubbed" below for exactly where that honesty matters most: the Feed got a
full pass; Profile/Discover/Inbox/Comments are functional world-space panels,
not yet a deep spatial redesign.

## 0. What you already have open

You mentioned you've got the Meta Quest developer dashboard open — that's
the right starting point regardless of anything below. Concretely, from
there:

1. Make sure your developer org is verified (2FA + basic org info).
2. Create a new **App** entry, platform **Quest**. Grab the **App ID** it
   gives you — Unity's Meta Platform settings ask for this later.
3. On the headset: **Meta Horizon mobile app → your headset → Developer
   Mode → on.** This is what lets Quest accept builds from Unity/adb instead
   of only the Horizon Store.

## 1. Install the tools

- **Unity Hub** + a recent Unity **LTS** Editor with **Android Build
  Support** (+ the Android SDK/NDK/OpenJDK components Unity Hub offers to
  install alongside it). Meta's supported Unity version shifts with SDK
  releases — check developers.meta.com's Unity requirements page for the
  exact version to install before you pick one; don't just grab whatever's
  newest.
- **Meta Quest Developer Hub (MQDH)** — optional but makes pairing/build
  deployment much easier than raw `adb`.
- A USB-C cable to plug the headset into your Mac.

## 2. Create the Unity project and bring these files in

Unity project files (scenes, prefabs) are binary/YAML and easy to corrupt by
hand — unlike the Xcode `.pbxproj`, there's no safe way for me to hand-author
those and guarantee they'll open. So this delivery is **scripts + assets to
drop into a project Unity itself creates**, not a double-click-to-open
project:

1. **Unity Hub → New Project → 3D (URP or Built-in, either works)**. Name it
   `XpressMediaVR`.
2. Copy this delivery's `Assets/Scripts/` and `Assets/Plugins/Android/` into
   your new project's `Assets/` folder.
3. Merge this delivery's `Packages/manifest.json` entries into your new
   project's `Packages/manifest.json` (don't overwrite it wholesale — keep
   whatever Unity's template already put there and add these dependencies).

## 3. Install the Meta XR SDK

The current standard route: **Window → Package Manager → My Assets**, find
**Meta XR All-in-One SDK** (this used to be called "Oculus Integration" —
same publisher, same purpose, name's changed a couple of times) and Import.
That pulls in the Core, Interaction, and Platform SDKs together.

After importing:

1. **Edit → Project Settings → XR Plug-in Management** → install it if
   prompted → check the **Oculus/Meta XR** provider under the Android tab.
2. **File → Build Settings → Android → Switch Platform.**
3. Find the Meta XR SDK's **Platform settings** (menu location varies by SDK
   version — look under a top-level **Meta** or **Oculus** menu once
   imported) and paste in the **App ID** from step 0.
4. The SDK's setup wizard (**Meta → Project Setup Tool**, or similar) will
   flag any Android manifest/Player Settings it wants fixed — run through
   it; it's generally safe to accept its fixes.

## 4. Scene setup

Build one scene, `FeedScene`, by hand in the Editor:

1. **GameObject → XR → the Meta XR rig prefab** (name varies by SDK version
   — look for something like "OVRCameraRig" or "Meta XR Rig" in the SDK's
   prefabs folder) — this gives you head + hand tracking for free.
2. Empty GameObject `VideoWall` → add `MeshFilter`, `MeshRenderer`,
   `CurvedMeshGenerator`, `VideoPlayer`, `VideoWallController`. Assign a new
   Material (Unlit/Texture is fine) to the MeshRenderer, and a new
   RenderTexture asset (1920×1080) to `VideoWallController`'s `Render
   Texture` field and to the material's texture.
3. Empty GameObject `ActionRail` (a small world-space Canvas positioned
   beside the wall) with `FeedActionRail` — build 3 buttons + 3 TMP labels
   under it (like/comment/follow) and wire them into the script's Inspector
   fields.
4. Empty GameObject `FeedManager` with `FeedController` (assign `VideoWall`
   and `ActionRail`) and `FeedInput` (assign `FeedManager` itself as `feed`).
5. Comments/Profile/Discover/Inbox: each is a world-space `Canvas` (Render
   Mode: World Space, scaled down like `0.001` per unit) positioned a couple
   meters from the origin, with the matching `*PanelController` script on
   its root and simple TMP/RawImage/Button hierarchies wired into each
   field per the comments at the top of each script.
6. `WristMenu`: parent an empty GameObject to the rig's left hand/controller
   anchor, add `WristMenu`, assign `headTransform` = the rig's center-eye
   anchor, `wristTransform` = itself, `panelRoot` = a small Canvas with
   Home/Profile/Discover/Inbox buttons wired to `NavigateTo(...)`.
7. A `SessionStore` and `AuthManager` object each (either script works fine
   living on the same empty GameObject) — these are the sign-in gate; show a
   simple "Sign in with MPARADISE" button calling
   `SessionStore.Instance.SignInWithMparadise()` until `IsSignedIn` is true,
   then load `FeedScene`'s content (or just gate the Feed's `Start()` — up
   to you).

None of this needs to be pixel-perfect on the first pass — get one video
playing on the curved wall with like/comment working before polishing the
rest.

## 5. Registering with MPARADISE

Reuse your existing MPARADISE app registration from the iOS project — same
`client_id`, same redirect URI (`xpressmedia://oauth-callback`) — as long as
that redirect URI is already registered, this Quest client can share it. Fill
in `Assets/Scripts/App/Config.cs`:

- `MparadiseClientID` — same value as the iOS app's `Config.swift`.
- `BackendBaseURL` — your backend machine's **LAN IP**, reachable from the
  headset's Wi-Fi (same rule as testing the iOS app on a physical device —
  `localhost` on the headset means the headset itself).

## 6. Build & Run

With the headset connected via USB and Developer Mode on: **File → Build
Settings → make sure `FeedScene` is in the build → Build And Run** (or use
MQDH to push a built `.apk`). Accept the USB debugging prompt that pops up
inside the headset the first time.

## What's built vs what's stubbed

**Built and real:**

- Full API client (`APIClient.cs`) covering every endpoint the iOS app uses
  — feed, likes, comments, follow, profile, search, notifications, DMs,
  video upload.
- MPARADISE PKCE sign-in (`AuthManager.cs`), adapted for Quest's system
  browser + a deep-link callback instead of `ASWebAuthenticationSession`.
- The Feed: a real procedurally-curved video wall, streamed playback via
  Unity's VideoPlayer, thumbstick-flick navigation, and a spatial
  like/comment/follow rail — this is the screen that got the actual "real
  spatial redesign" treatment.
- Profile, Discover (search), Inbox, and Comments: functional world-space
  panels wired to the real API — but ported as flat panels floating in
  space, not yet redesigned the way the Feed was. Extending them the same
  way (curved layouts, spatial interaction) is the natural next pass, and
  every script's header comment says exactly what it still needs.

**Explicitly not built:**

- **Video upload from the headset itself** — Quest has no Photos-library
  equivalent to pick an existing clip from, so `UploadVideo()` takes a local
  file path you'd have to get onto the headset some other way (`adb push`,
  or a simple in-app file browser you'd add). Realistically, posting stays
  an iOS/Mac thing for now; Quest is a *viewing* experience first.
- **In-headset camera recording** — same story as the iOS app's own
  "What's stubbed" section, just doubly true here.
- **Hand-tracking gestures beyond thumbstick flick** — `FeedInput.cs` has a
  clear extension point (`Next()`/`Previous()`) for wiring up a real
  grab-swipe via the Meta XR Interaction SDK later.
- **Earnings/Wallet** — excluded here too, same as the iOS app.
- Everything the main README already calls stubbed on the backend side
  (duet/stitch compositing, transcoding, etc.) is equally stubbed here,
  since it's the same backend.
