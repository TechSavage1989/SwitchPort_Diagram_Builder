# Application updates

## First installation

Download SwitchportDiagramBuilder.exe or the portable ZIP from the latest GitHub
release. Extract the ZIP before running the executable. Version 1.12.1 starts
the current application identity. Earlier builds are historical; download the
current release directly rather than using their updater. Future releases use
the built-in updater. Create or re-pin shortcuts to the current executable.
Existing files from earlier builds are not deleted.

## Normal use

Open Updates in the application to check, download, read release notes or review
installation. Automatic checking starts after startup and repeats while the app
is open. The setting can be disabled without disabling manual checks. Later
defers the update reminder for 24 hours.

An available update is downloaded only after the download action. Installation
requires a separate confirmation and restarts the application. Finish report
generation before installing. Output folders, the selected report logo and app
preferences remain in their existing locations.

## Verification and recovery

The feed is the stable GitHub Releases channel for
TechSavage1989/SwitchPort_Diagram_Builder. The updater accepts newer stable
versions and validates their release metadata, expected repository URLs,
package SHA-256, executable SHA-256, executable format and product/version
metadata. Unsafe or unexpected archive entries and incomplete downloads fail
validation before the installed application changes.

Release hashes provide integrity through the trusted GitHub repository. Current
builds are not Authenticode signed. The updater does not claim that a checksum
is a code-signing certificate.

Installation uses a local copy of the application as the helper. It waits for
the existing process to exit, retains one previous executable and replaces the
application atomically. The restarted application must acknowledge that its
visible interface is ready within 90 seconds. Failed startup restores the
previous executable and reopens it. Recovery files and diagnostic logs are
under the app's Local AppData folder, separated by portable installation.

If the application folder is not writable, choose a writable portable location
or update it through the normal Windows account/admin process. The updater does
not silently request elevation or replace an unrelated executable.

## Publishing a release

1. Commit development changes and regression checks to this repository.
2. Advance the application and generator version together to a new stable
   three-part version.
3. Push the tested change to main, or run the release workflow manually.
4. GitHub Actions tests the source, publishes the Windows x64 single-file app,
   creates and verifies the portable ZIP and manifest, then creates the release.
5. Review the workflow result and release assets. Existing versions are retained.

Release assets are the stable executable, versioned portable ZIP,
update-manifest.json and SHA256SUMS.txt. The ZIP contains exactly the executable,
READ_ME.txt, UPDATE_INFO.txt and an inner SHA256SUMS.txt. The manifest records
the package and executable sizes and SHA-256 values. The app checks the latest
published stable release; draft and prerelease versions are excluded.

