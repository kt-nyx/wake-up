# Public 1.7 development source

The `1.7` branch is the shared source for ongoing local and cloud development.
It is separate from the published 1.6 release on `main`. Publishing this source
does not publish a Workshop package, GitHub release, release tag or installation.

The current product is 0.5.0-preview.2. Its runtime and build checks admit the
reviewed 1.7.4982 rev1280 game and Harmony rc.3 on Windows or Linux x64 with
the .NET 10 runtime. The latest Linux unstable build 26031547, depot 294103,
manifest 4415223985052641865 has byte-identical Assembly-CSharp, UnityStubs and
Waylith references to the Windows review. Exact identity and method checks remain
unchanged. Linux live qualification is pending; admission alone is insufficient.
See [the compatibility review](compatibility-1.7.4982.md).
The staged candidate has offline checks; those do not establish current-game
performance, graphics or gameplay compatibility.

Development follows the latest 1.7 build on Steam's `unstable` track while 1.7
remains there, then the main release track when it becomes the intended 1.7
stream. A new build requires refreshed static review and exact contracts; never
remove the admission checks to accept an unreviewed build.

Cloud environment setup and fresh-task verification are pending. The intended
workflow uses a project environment, the exact SDK in `global.json`, matching
private game references and chosen full or partial snapshots of the owner's
active mod collection. Game files, third-party mod files, saves, private paths,
raw diagnostics and private investigation history must not be added to Git.

Cloud agents may develop and test source on this branch. Final Windows candidate
verification and owner-controlled release publication occur locally. A cloud
test is not a Windows release qualification.

Public changes use branches derived from this public history. The initial source
snapshot was exported from the accepted private implementation without merging
or publishing its private commit ancestry. Preserve that history separation.
