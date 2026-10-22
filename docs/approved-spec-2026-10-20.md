# Approved SteamGate spec (2026-10-20)

**As the orchestrator or a reviewer checking SteamGate work, I need Trevor's approved spec text unedited, with dated amendments appended below the original, so I can tell what he approved from what was added later.**

Approval source: Trevor said go on 2026-10-20 (session local_9d1a5e4a-29cd-4cb2-ae67-ba26dbd1d900) to the plan he pasted plus the four fixes the assistant proposed, then asked for "a max cooldown so you don't accidentally set it for like a year" and for Blender, OBS, and Wallpaper Engine on the not-counted list. Two values were added in the build brief afterward and were not shown to him before his go: "Steamworks Shared" in the pre-filled list, and the number 7 for the cooldown maximum (he asked for a maximum without a number). Trevor approved both later on 2026-10-20, passed on from the main session (local_9d1a5e4a-29cd-4cb2-ae67-ba26dbd1d900), in his words: "Both of those sound good to me."

## Approved spec

Copied word for word from the brief that started this build. Trevor said go on 2026-10-20 and later approved the two values added after his go (see Approval source above), so the full spec below is approved.

Issues, in order:

1. Service skeleton, install/uninstall, auto-restart recovery
2. Steam game detection (read libraryfolders.vdf + appmanifest files, match running programs to steamapps\common folders, exclude the not-counted list)
3. Time accounting (night ends 4 AM, weeknight/weekend nights, weekly cap resets Monday 4 AM, saved to disk every minute)
4. Enforcement (warnings at 10/5/1 min with sound, polite close then force, close any game started after the limit)
5. The lock (protected settings folder, cooldown rules below)
6. Status screen (see where you stand, request changes, edit the not-counted list)

SPEC

- Windows service in C# / .NET. No window, tray icon, or Alt-Tab entry. Windows restarts it if killed. Settings and tally in a protected system folder (ProgramData, admin-only writes).
- A small invisible helper runs in the user's own login and shows the warnings and does the polite close, because services can't draw on the screen or talk to game windows. The service does the counting and the force-close. Each warning plays a sound too, since Windows mutes notifications during full-screen games.
- Counts wall-clock minutes while at least one Steam game is open; two games at once don't double-count.
- Defaults: weeknight 90 min; weekend night 3 h; weekend nights = Friday + Saturday; weekly cap 10 h; cooldown 48 h.
- Cooldown rules: any change that loosens a rule (raise a limit, add a weekend night, shorten the cooldown, add a program to the not-counted list, disable, uninstall) waits the full cooldown. Any change that tightens applies right away. The cooldown has a hard maximum of 7 days, so it can't be set to a year by mistake.
- Not-counted list (whitelist), pre-filled with Blender, OBS Studio, Wallpaper Engine, and Steamworks Shared. Editable from the status screen; adding waits the cooldown, removing applies now.
- Known gaps, documented, not solved: Trevor is the admin, so a deliberate admin teardown can remove it; changing the system clock could fool the counter. Never hide from antivirus or Windows or block their removal.
- Trevor's account is an admin, so Task Manager CAN kill the service. Never claim it can't; Windows restarts it and the tally survives.
- Current Steam library: Arc Raiders, Cyberpunk 2077, Dying Light The Beast, Overwatch, Red Dead Redemption 2, Viewfinder (C:\Program Files (x86)\Steam).
- The .NET SDK was not found on this PC. CI can build on GitHub's Windows runners; ask Trevor before installing the SDK locally.
- The status screen is visual: Trevor approves its look on a preview before it is built.

## Amendment 2026-10-21 (weekend nights)

Trevor, in chat with the build session (session cd2c77e4) on 2026-10-21: "weekend nights are fri sat and sunday btw".
The weekend nights are Friday, Saturday, and Sunday. This replaces the weekend-nights default in the SPEC list above and wins over it.
Nothing else in the spec changes.
