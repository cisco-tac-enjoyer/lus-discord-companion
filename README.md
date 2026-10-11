# LUS Discord Companion

A small Windows app that posts your World of Warcraft level-up screenshots to a Discord channel.

It works together with the **LevelUpScreenshot** addon: the addon takes a screenshot in a separate file format (PNG by default) every time you level up, and the Companion watches your Screenshots folder and posts every new file in that format to a Discord webhook. Your normal screenshots (JPG) are never posted.

## Setup

1. **Install the addon** from CurseForge: *LevelUpScreenshot*.
2. **Create a webhook** in Discord: channel settings → Integrations → Webhooks → New Webhook → Copy Webhook URL.
3. **Start `lus-discord-companion.exe`** and fill in the fields:
   - *Discord webhook URL*: paste the URL you copied.
   - *Screenshots folder*: click **Auto-detect**, or **Browse…** to `World of Warcraft\_retail_\Screenshots` (or your Classic folder).
   - *Bot name*: the name shown in Discord (no "discord" or "clyde").
   - *Level-up format*: must match `/lus format` in game (PNG by default).

   Click **Send test message** to check the webhook, then **Save** and **▶ Start watching**.

   Windows SmartScreen may warn about the unsigned exe: click **More info → Run anyway**.
4. **Test it**: type `/lus test` in game. The screenshot should appear in Discord within a few seconds.

Tick **Start with Windows** to have the Companion start in the tray and watch automatically after login.

## Troubleshooting

- **Nothing is posted**: check the Screenshots folder, and that the format dropdown matches `/lus format` in game.
- **Every screenshot is posted**: your normal and level-up formats are the same. Change one of them (`/lus format png` or `/console screenshotFormat jpg`).
- **"Webhook is invalid or was deleted"**: create a new webhook and paste the new URL.

If you used an older version (*WoW LevelUp to Discord* or *LevelUpScreenshot Companion*), exit it first. Your settings are taken over automatically.

Settings, the posted-files list and the log are stored in `%APPDATA%\lus-discord-companion\`. The Companion never deletes or moves your screenshots.

## Building

Run `./build.ps1` on Windows. It compiles `dist/lus-discord-companion.exe` with the C# compiler that ships with Windows (.NET Framework 4.8, nothing to install). If the .NET SDK is installed, it also runs the tests.

The exe is a single file and needs no installation: copy it anywhere and start it.

## License

MIT, see [LICENSE](LICENSE).
