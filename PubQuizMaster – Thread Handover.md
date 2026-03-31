# PubQuizMaster – Thread Handover

## Projekt & Stack

**PubQuizMaster** ist eine Desktop-Host-Applikation für Pub-Quiz-Abende. Der Host steuert den Ablauf am Laptop, Scorer-Clients verbinden sich per Browser (Smartphone/Laptop) über WLAN.

**Technologie-Stack:**

- **Desktop:** Avalonia UI 11, .NET 8, C#, MVVM via CommunityToolkit.Mvvm
- **Backend (in-process):** ASP.NET Core / Kestrel, SignalR (Hub: `QuizHub`)
- **Scorer-Client:** Single-Page HTML/JS (vanilla, kein Framework), SignalR JS Client
- **Persistenz:** JSON-Dateien via `PersistenceService`
- **Patterns:** Partial classes mit `[ObservableProperty]`, `[RelayCommand]`, Primary Constructors

---

## Dateistruktur

```
PubQuizMaster/
├── PubQuizMaster.Core/
│   ├── Models/
│   │   ├── Event/
│   │   │   ├── QuizNight.cs
│   │   │   ├── Round.cs
│   │   │   └── Answer.cs
│   │   └── Contents/
│   ├── Services/
│   │   ├── QuizNightService.cs       ← Hauptlogik, Events: AnswerRecorded
│   │   ├── PersistenceService.cs     ← JSON Load/Save, ListSavedNights()
│   │   └── ScorerSessionService.cs
│
├── PubQuizMaster.Desktop/
│   ├── App.axaml / App.axaml.cs     ← Startup-Flow, DI-Ersatz
│   ├── Models/
│   │   └── HostPhase.cs             ← Enum: Review, Scoring
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs
│   │   ├── MainWindowViewModel.cs   ← Zentrale Orchestrierung
│   │   ├── LeftPanelViewModel.cs    ← Rundenlist + TotalBoard
│   │   ├── CenterViewModel.cs       ← Switcht zwischen Setup/Matrix
│   │   ├── SetupViewModel.cs        ← Neue Runde konfigurieren
│   │   ├── RoundMatrixViewModel.cs  ← Antwort-Grid einer Runde
│   │   ├── RoundEntryViewModel.cs   ← Eintrag in der Rundenliste
│   │   ├── TeamAnswerRowViewModel.cs
│   │   ├── AnswerCellViewModel.cs
│   │   ├── ActiveRoundViewModel.cs  ← Fortschrittsanzeige während Scoring
│   │   ├── LeaderboardEntryViewModel.cs
│   │   ├── StartupViewModel.cs      ← Neu/Laden-Entscheidung
│   │   └── SavedNightEntry.cs       ← Record für ListBox
│   ├── Views/
│   │   ├── MainWindow.axaml
│   │   ├── StartupWindow.axaml
│   │   └── ...
│   └── Web/
│       ├── KestrelHost.cs
│       ├── QuizHub.cs               ← SignalR Hub
│       └── wwwroot/
│           ├── index.html           ← Scorer-Client (Single HTML)
│           └── signalr.min.js
```

---

## Was bereits funktioniert

- **Startup-Flow:** `StartupWindow` erscheint vor `MainWindow`, erlaubt neue Quiz-Night anlegen oder gespeicherte laden. `App.axaml.cs` wartet auf Close, initialisiert dann Services und öffnet `MainWindow`.
- **Rundenmanagement:** Runde erstellen (Name, Fragenanzahl, Teams), Scorer-Zuweisung, Runde starten.
- **Scorer-Client:** Verbindet per SignalR, zeigt Sort-Screen, Scoring-Screen mit ✓/✗-Buttons, Overview, Keyboard-Support (Firefox-Bug gefixt).
- **Antwort-Matrix:** Desktop zeigt Grid mit Teams × Fragen, Edit-Modus zum Nachkorrigieren.
- **Leaderboard:** `TotalBoard` im `LeftPanelViewModel` wird nach Finalisieren und nach manuellem Edit (`OnSaved`-Callback) aktualisiert.
- **Finalize Round:** Schreibt Ergebnis, broadcastet per SignalR, refresht Panel, zeigt Matrix.
- **Persistenz:** Speichern und Laden per JSON.
- **MVVM-Patterns:** `Classes.selected="{Binding IsSelected}"` statt DataTrigger, `TeamScoreEntry` als Record statt ValueTuple für Compiled Bindings.

---

## Bekannte offene Punkte

Folgende Punkte waren zum Zeitpunkt dieses Handovers noch offen (Reihenfolge entspricht dem ursprünglichen Plan):

- **C** – Leaderboard / Gesamtranking: Darstellung verfeinern, ggf. Animationen oder Highlighting bei Rangänderungen.
- **D** – Persistenz vervollständigen: Auto-Save nach jeder Antwort oder nach Finalisierung prüfen, Fehlerbehandlung bei korrupten JSON-Dateien.
- **E** – Export: Ergebnisse als CSV oder PDF exportieren (`ExportAction` ist als Callback im `RoundMatrixViewModel` bereits vorbereitet, aber nicht implementiert).
- **F** – UX-Polish: Ladeindikator, Fehlerdialoge, Responsiveness des Layouts bei kleinen Fenstergrößen.
- **G** – Scorer-Client Reconnect-Handling: Testen ob State korrekt wiederhergestellt wird nach Verbindungsabbruch während aktivem Scoring.

---

## Architekturentscheidungen

| Entscheidung | Begründung |
|---|---|
| Kein DI-Container | Kleine App, manuelle Verdrahtung in `App.axaml.cs` reicht |
| `Action`-Callbacks statt Events zwischen VMs | Einfacher, kein Speicherleck-Risiko durch falsch abgemeldete Events |
| `OnSaved`/`ExportAction` als `Action?` im VM | Gleiche Konvention wie Avalonia-typische Callbacks |
| Compiled Bindings (`x:DataType`) überall | Performance + Compile-Time-Fehler statt Runtime-Fehler |
| `record` statt ValueTuple für Binding-Typen | Compiled Bindings lösen ValueTuple-Felder nicht auf |
| `Classes.selected="{Binding ...}"` | Avalonia-Ersatz für WPF `DataTrigger` |
| `partial void OnXxxChanged` ohne `private` | CommunityToolkit generiert ohne explizites Modifier, muss übereinstimmen |
| HTML Single-File Scorer-Client | Kein Build-Step, einfach per Kestrel ausgeliefert, funktioniert auf jedem Smartphone-Browser |
| Optimistic UI im Scorer-Client | Antwort lokal setzen, Server bestätigt via `AnswerUpdated`-Broadcast |

---

## Für den nächsten Thread

Bitte füge diesen Prompt am Anfang des neuen Threads ein:

> Ich arbeite an **PubQuizMaster**, einer Avalonia-/.NET-8-Desktop-App für Pub-Quiz-Abende mit integriertem Kestrel/SignalR-Backend und einem HTML-only Scorer-Client. Das Handover-Dokument mit Projektstruktur, offenem Stand und Architekturentscheidungen ist angehängt. Ich stelle dir gleich die relevanten Code-Dateien bereit. Wir arbeiten auf Deutsch, Code-Kommentare und User-Outputs auf Englisch. Bitte mach direkt weiter ohne lange Einleitung.