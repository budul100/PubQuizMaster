# PubQuizMaster – Übergabe-Zusammenfassung

## Projektname & Technologie-Stack

**PubQuizMaster** – eine lokale Pub-Quiz-Verwaltungsanwendung für einen Host und mehrere Scorer-Clients.

- **Host-App:** Avalonia UI (.NET), MVVM mit CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`)
- **Scorer-Client:** Browser-basiert (HTML/JS), verbindet sich per SignalR
- **Backend/Kommunikation:** ASP.NET Core, SignalR Hub (`QuizHub`), Kestrel als eingebetteter Webserver (`KestrelHost`)
- **Core-Models:** separates Projekt `PubQuizMaster.Core`
- **Sprache:** C# (.NET 8)

---

## Dateistruktur (relevant)

```
PubQuizMaster/
├── Core/
│   └── Models/
│       ├── Contents/
│       │   ├── Answer.cs              # TeamId, QuestionIndex, Value (AnswerBase)
│       │   ├── AnswerBase.cs          # abstrakt, GetScore()
│       │   ├── AnswerBool.cs          # bool Correct → 1 / 0 Punkte
│       │   └── AnswerPoint.cs         # decimal Points (Zukunft)
│       ├── Event/
│       │   └── Round.cs               # Answers, Assignments, GetAnswer(), GetTeamScore()
│       └── Participants/
│           └── LeaderboardEntry.cs
│   └── Services/
│       └── QuizNightService.cs        # AddTeam, CreateRound, AssignScorer,
│                                      # SetAnswer, GetLeaderboard, GetRoundProgress
│
├── Desktop/
│   ├── Web/
│   │   ├── KestrelHost.cs             # eingebetteter Webserver, HubContext
│   │   └── QuizHub.cs                 # SignalR Hub, NotifyRoundStarted/Finalized
│   └── Views/
│       └── MainWindow.axaml           # 3-Spalten-Layout mit GridSplitter
│
└── ViewModels/
    ├── MainWindowViewModel.cs          # Haupt-VM, Phasen: Setup / ActiveRound / Results
    ├── ScorerAssignmentViewModel.cs    # Scorer + AssignableTeamViewModel
    ├── RoundMatrixViewModel.cs         # Matrix pro Runde, Edit/Save/Cancel
    ├── TeamViewModel.cs
    ├── ScorerStatusViewModel.cs
    └── LeaderboardEntryViewModel.cs
```

---

## Was bereits funktioniert

**Setup-Phase**
- Teams hinzufügen mit Unique-Constraint (Duplikate werden abgefangen, Fehlermeldung im UI)
- Teams alphabetisch sortieren (jederzeit)
- Teams entfernen
- Neue Teams erscheinen automatisch bei allen bestehenden Scorern (`CollectionChanged`-Subscription)
- Scorer hinzufügen / entfernen
- Teams werden automatisch Round-Robin auf alle Scorer verteilt beim Start und bei Änderungen
- Doppelzuweisung verhindert: ein Team kann nur einem Scorer zugeordnet sein (`_assignedTeamIds` als globales `HashSet<Guid>`)
- Checkboxen für bereits anderweitig zugeordnete Teams werden deaktiviert (`IsDisabled`)
- Start-Button ist disabled bis alle Teams korrekt zugeordnet sind (`CanStartRound`)

**Active Round**
- Runde wird gestartet, Teams und Scorer werden dem Hub mitgeteilt
- Scorer-Status-Übersicht (wie viele Antworten pro Scorer bereits erfasst)
- Fortschrittsanzeige (erfasste / erwartete Antworten)
- Finalize-Button beendet die Runde

**Ergebnisse & Matrix**
- Leaderboard (letzte Runde + Gesamt) immer in der rechten Sidebar sichtbar
- Alle Runden als Matrix (Teams × Fragen) im Tab „Rounds"
- Matrix zeigt ✓ / ✗ / – pro Antwort, Zeilensumme (Total), Spaltensumme (Correct)
- Runden nachträglich editierbar: Edit → Checkboxen → Save / Cancel
- `SetAnswer` im Service persistiert Korrekturen

**Layout**
- 3-Spalten-Layout mit `GridSplitter` (verschiebbar)
- Spalte 0: Teams (Setup) / Active Round + Results (ColumnSpan 0–3)
- Spalte 2: Round-Config + Scorer-Assignments + Start-Button (nur Setup)
- Spalte 4: Standings + Rounds-Matrix als TabControl (immer sichtbar)
- Dunkles Design durchgängig, inkl. TabControl/Expander lokal überschrieben

**Scorer-Client (Browser)**
- Verbindet sich per SignalR mit `ScorerConnected`-Event
- Sortier-Screen vor dem Scoring (wenn keine bestehenden Antworten)
- `RoundStarted`-Event triggert `RequestState` → `ScorerConnected` → Sortier-Screen
- Reconnect-Logik: bei bestehenden Antworten direkt ins Scoring

---

## Bekannte offene Punkte

- `ScorerStatusViewModel.Progress` und `IsComplete` müssen als Properties existieren (wurden im AXAML referenziert, aber nicht explizit gezeigt)
- `QuizNightService.ReorderTeams()` wurde im ViewModel referenziert – muss im Service implementiert sein
- `Round.GetTeamScore()` wird in `BuildLeaderboards` aufgerufen – muss im Round-Model existieren
- `QuizNightService.ActiveRoundId` wird in `FinalizeRound` referenziert – muss als Property existieren
- Edit-Modus der Matrix: `IsThreeState="True"` auf der Checkbox erlaubt null (nicht erfasst) – Verhalten bei Save für `null`-Antworten ist definiert (Antwort wird entfernt)
- Scorer-ID wird nicht mehr im UI editierbar angezeigt (Label only) – ID wird intern als `"scorer-a"`, `"scorer-b"` etc. generiert

---

## Architekturentscheidungen

| Entscheidung | Begründung |
|---|---|
| `AnswerBase` mit `GetScore()` statt `bool` direkt | Erweiterbar für Teilpunkte (`AnswerPoint`) ohne Breaking Changes |
| `_assignedTeamIds` als globales `HashSet` im MainVM | Single Source of Truth für Doppelzuweisungs-Prüfung |
| `CollectionChanged` in `ScorerAssignmentViewModel` | Teams aktualisieren sich automatisch bei allen Scorern |
| `IsEditing` auf `AnswerCellViewModel` gepusht | Kein komplexes Binding-Traversal im AXAML nötig |
| Kestrel eingebettet, kein separater Server-Prozess | Einfache Deployment-Unit, Host-App ist gleichzeitig Server |
| `HostPhase` Enum statt bool-Flags | Klar definierte Zustände, keine inkonsistenten Flag-Kombinationen |
| Matrix in der Sidebar (immer sichtbar) | Kein explizites „Ergebnisse abrufen" nötig, Live-Update während Scoring |
| `GridSplitter` statt fixer Breiten | Host kann Layout je nach Teamanzahl und Bildschirmgröße anpassen |

---

## Kontext für den nächsten Thread

Bitte verwende folgende Eröffnung:

> Wir entwickeln **PubQuizMaster**, eine Avalonia-Desktop-App (.NET 8) mit eingebettetem Kestrel/SignalR-Server für Browser-basierte Scorer-Clients. MVVM mit CommunityToolkit. Den bisherigen Stand und die Architektur entnimmst du der beigefügten Übergabe-Zusammenfassung. Ich stelle dir die aktuellen Codedateien bereit, sobald du bestätigt hast.

Sinnvolle nächste Entwicklungsschritte, die du direkt ansprechen kannst:

- *„Implementiere `ScorerStatusViewModel` vollständig mit `Progress` und `IsComplete`."*
- *„Ergänze `QuizNightService.ReorderTeams()` und `ActiveRoundId`."*
- *„Füge eine Quiz-Night-Abschlussansicht hinzu (alle Runden, Gesamtsieger, Export)."*
- *„Implementiere einen zweiten Antworttyp (`AnswerPoint`) mit UI-Unterstützung im Scorer-Client."*
- *„Füge Persistenz hinzu: Quiz-Night als JSON speichern und laden."*