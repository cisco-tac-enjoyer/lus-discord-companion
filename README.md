# LUS Discord Companion

Ein kleines Windows-Programm, das deine **World of Warcraft Level-up-Screenshots automatisch in einen Discord-Kanal postet**.

So funktioniert es: Das Addon **[LevelUpScreenshot](https://www.curseforge.com/wow/addons/levelupscreenshot)** macht bei jedem Level-up einen Screenshot in einem eigenen Dateiformat (standardmäßig PNG). Der Companion beobachtet deinen Screenshots-Ordner und schickt jede neue Datei in diesem Format an Discord. Deine normalen Screenshots (JPG) werden nie gepostet.

## Einrichtung

1. **Addon installieren:** [LevelUpScreenshot auf CurseForge](https://www.curseforge.com/wow/addons/levelupscreenshot).
2. **Webhook in Discord anlegen:** Kanal-Einstellungen → Integrationen → Webhooks → Neuer Webhook → Webhook-URL kopieren.
3. **`lus-discord-companion.exe` starten** und die Felder ausfüllen:
   - *Discord webhook URL*: die kopierte URL einfügen.
   - *Screenshots folder*: **Auto-detect** klicken oder mit **Browse…** den Ordner `World of Warcraft\_retail_\Screenshots` (bzw. Classic) wählen.
   - *Bot name*: der Name, der in Discord angezeigt wird (ohne „discord“ oder „clyde“).
   - *Level-up format*: muss zu `/lus format` im Spiel passen (Standard: PNG).

   Mit **Send test message** prüfen, ob es klappt. Danach **Save** und **▶ Start watching**.
4. **Im Spiel testen:** `/lus test` eingeben. Nach wenigen Sekunden sollte der Screenshot in Discord erscheinen.

Mit **Start with Windows** startet das Programm nach dem Anmelden automatisch im Tray (unten rechts neben der Uhr) und legt gleich los.

> Windows warnt evtl. vor der unbekannten exe (SmartScreen): **Weitere Informationen → Trotzdem ausführen**.

## Probleme?

- **Es wird nichts gepostet:** Screenshots-Ordner prüfen, und ob das Format zu `/lus format` im Spiel passt.
- **Jeder Screenshot wird gepostet:** Normales Format und Level-up-Format sind gleich. Eins davon ändern (`/lus format png` oder `/console screenshotFormat jpg`).
- **„Webhook is invalid or was deleted“:** In Discord einen neuen Webhook anlegen und die neue URL eintragen.

## Gut zu wissen

- Einstellungen, Liste der geposteten Bilder und Protokoll liegen in `%APPDATA%\lus-discord-companion\`. Der genaue Pfad steht im Programm unter *Config file*.
- Einstellungen älterer Versionen (*WoW LevelUp to Discord*, *LevelUpScreenshot Companion*) werden automatisch übernommen. Die alte Version vorher beenden.
- Deine Screenshots werden nie gelöscht oder verschoben.
- Keine Installation nötig: Die exe ist eine einzelne Datei und läuft von überall.

## Für Entwickler

`./build.ps1` baut `dist\lus-discord-companion.exe` mit dem C#-Compiler, der in Windows enthalten ist (.NET Framework 4.8, nichts zu installieren). Ist das .NET SDK installiert, laufen zusätzlich die Tests.

```
build.ps1                    Baut die exe nach dist\
src/LusDiscordCompanion/
  Program.cs                 Startpunkt; verhindert, dass die App doppelt läuft
  app.manifest               Windows-Einstellungen der exe (scharfe Darstellung)
  UI/                        Fenster und Tray-Icon
  Discord/                   Senden an den Discord-Webhook
  Screenshots/               Ordner beobachten, Bilder vorbereiten, Ordner finden,
                             merken was schon gepostet wurde
  Settings/                  Einstellungen laden/speichern, "Start with Windows"
  Logging/                   Protokoll
  Assets/                    Icon
tests/                       Automatische Tests (nur mit .NET SDK)
dist/                        Fertige exe zum Herunterladen
```

## Lizenz

MIT, siehe [LICENSE](LICENSE).
