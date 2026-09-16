# Übergabe: PubQuizMaster, Funktions- und UI-Änderungen

## 1. Anweisungen für den Chat

**Ablauf der Codeübergabe**
- Der Code wird **in mehreren Teilen** übergeben (Codebase-Export in mehreren Chunks, Teil 1 enthält Verzeichnisbaum und Dateiindex).
- Nach jedem Teil nur den Empfang bestätigen (z. B. "Teil 1 von 3 erhalten."). **Keine Analyse, keine Vorschläge, bevor alle Teile vollständig übergeben sind** und der Nutzer das bestätigt hat.
- Achtung: Der Export kann Dateien über Chunkgrenzen hinweg fortsetzen, ohne erneuten `## FILE:`-Header. Dateiinhalte daher über den Dateiindex und den Kontext zuordnen.

**Antwortstil**
- Sprache: Deutsch (Sprache der Frage). Kein Sprachwechsel ohne Aufforderung.
- Tonfall: professionell, direkt, locker. Keine Floskeln, kein Loben der Frage, keine Wiederholung der Frage.
- Keine langen Einleitungen. Stattdessen immer eine **kurze Zusammenfassung der Aufgabe und des nächsten relevanten Schritts**.
- Formatierung kompakt. Tabellen wo sinnvoll. Bulletpoints nur für echte Aufzählungen.
- Keine Gedankenstriche ("-" oder "—") im Fließtext, stattdessen Komma oder Umformulierung.
- Fachbegriffe aus Software, ÖPNV und Rail nicht erklären. Technik auf Expertenniveau.
- **Technische Schulden, Fehler und Vereinfachungen immer explizit benennen.**

**Code-Konventionen**
- Kommentare und user-facing Texte im Code auf Englisch. Ausnahme: Folientexte in der PowerPoint bleiben Deutsch (`slideCulture` de-DE).
- C#: `ToArray()` statt `ToList()` wo möglich. **Keine verschachtelten Klassen**, Typen und Records in eigene Dateien (bestehende Ordner `Core/Records/...`, `Core/Enums`, `Web/Records`).
- Bestehende Struktur beibehalten: Code-behind in `.razor.cs`, Regions (`#region Private Fields`, `Public Properties`, `Public Methods`, `Protected Methods`, `Private Methods`), alphabetische Sortierung innerhalb der Regions, Primary Constructors bei Services.
- Kein abweichendes Verhalten ohne explizite Anfrage.
- Bei Signaturänderungen **immer alle Aufrufer nennen und mit anpassen** (Razor-Markup, Code-behind, Services). In der Vorrunde führte genau das zu Compilerfehlern.
- Änderungen als vollständige Dateien liefern oder als klar abgegrenzte Alt/Neu-Blöcke mit Dateipfad.
- EF-Migrationen immer benennen und den Befehl angeben (`dotnet ef migrations add <Name> -p PubQuizMaster.Data -s PubQuizMaster.Web`).

**CSS**
- Der Nutzer ist schwach in CSS. **Jede CSS-Regel genau erklären** (was sie bewirkt, warum sie nötig ist).
- **Immer angeben, in welcher Datei und an welcher Stelle** der Code ergänzt wird (z. B. "in `wwwroot/css/site.css` ans Ende, nach dem Block `#blazor-error-ui .dismiss`").
- Bootstrap-Klassen bevorzugen und ebenfalls kurz erklären.

## 2. Kontext

PubQuizMaster ist eine Blazor-Server-App zur Durchführung von Pub-Quiz-Abenden: Teams registrieren, Runden mit Scorer-Stationen (Token-Links) starten, Antworten erfassen, Ergebnisse als PowerPoint ausgeben, All-Time-Rangliste führen.

| Bereich | Technik |
|---|---|
| Framework | .NET 9, Blazor Server mit `_Host.cshtml` (`ServerPrerendered`) |
| Daten | EF Core, PostgreSQL, `AnswerBase` als jsonb (polymorph: `AnswerBool`, `AnswerPoint`) |
| Export | DocumentFormat.OpenXml |
| UI | Bootstrap 5.3.3 (CDN), Open Iconic (CDN, nur NavMenu), sonst UTF-8-Symbole |
| Projekte | `Core` (Modelle, Records, Enums, `CompetitionRanking`), `Data` (DbContext, Migrationen), `Services`, `Web` |

**Domänenmodell (Kurzfassung)**
- `Quiz` hat `ParticipatingTeams` (`Participant` mit `SheetOrder`), `Rounds`, `Results`.
- `Round` hat `Assignments` (`Scorer` mit `TeamIds` in Blattreihenfolge), `Answers`, `IsFinalized`, `QuestionCount`, `Name` (eindeutig pro Quiz).
- `Round.GetTeamIds()` leitet die Rundenteams **nur aus den Scorer-Zuordnungen** ab.
- `Result` ist die materialisierte Gesamtpunktzahl pro Team und Quiz (`LiveResultBuilder` beim Abschluss, Legacy-Import).
- Dashboard (`Index`) pollt alle 2 s einen `QuizFingerprint`, `ScorerService` verteilt Events (`OnAnswersChanged`, `OnRoundChanged`, `OnStatusChanged`).
- Ranking: `CompetitionRanking.Rank` (1, 1, 3), Gleichstand nach Name vorsortiert.

**Bereits umgesetzt in der Vorrunde (im Code enthalten)**
- PowerPoint-Export arbeitet auf einer hochgeladenen Datei (pptx, ppsx, potx): `ExportService.FillPresentation(...)` liefert `PresentationFormat` (Endung, Content-Type).
- Folien werden über `cSld/@name` oder ein Marker-Shape `#<Name>` gefunden, bei mehreren Runden über einen PowerPoint-Abschnitt mit dem Rundennamen.
- `PresentationDownloadService.DownloadAsync(..., IBrowserFile sourceFile, ...)`, Upload über `<label class="btn ..."><InputFile class="d-none" .../></label>` mit wechselndem `@key` (`inputVersion`/`exportVersion`), damit dieselbe Datei erneut gewählt werden kann.
- Offener Prüfpunkt: Im Browser trat `downloadFileFromStream was undefined` auf. Empfehlung war `asp-append-version="true"` am `site.js`-Tag in `_Host.cshtml` und Prüfung per F12. Beim Start kurz nachfragen, ob das behoben ist.

## 3. Aufgaben

| ID | Aufgabe | Art | Schema-Änderung |
|---|---|---|---|
| A1 | Kürzerer Text im Shape `Points` der Antwortfolien | Export | nein |
| A2 | Finale Runde per Checkbox statt zwei Export-Buttons | Feature | `Round.IsFinal` |
| A3 | Teams "außer Konkurrenz" (AK) pro Quiz | Feature | `Participant.IsNonCompetitive` |
| A4 | Download-Dateiname = Upload-Name + `_adjusted` | Export | nein |
| A5 | Metro-UI-Icons statt UTF-8-Symbolen | UI | nein |
| A6 | Nachträglich in der Matrix erfasste Teams fehlen im Rundenergebnis | **Bug** | nein |
| A7 | Matrix nur zweiwertig (Erfolg / leer) | UI/Logik | nein |
| A8 | Matrix immer editierbar, Änderungen sofort speichern | UI/Logik | nein |
| A9 | Edit-Buttons nur mit Stift-Icon, Text im Tooltip | UI | nein |
| A10 | "Register Team" bleibt offen bis zum aktiven Schließen | UI | nein |
| A11 | Team löschen (ohne Punkte) bzw. deaktivieren (mit Punkten) | Feature | `Participant.IsActive` |
| A12 | Menü seitlich statt über dem Board | **Bug** Layout | nein |
| A13 | Runde ohne Erfassungen löschen | Feature | nein |
| A14 | Keine Trennung mehr zwischen Quiz-Nights und Active Night | UI/Navigation | nein |

### A1: Text im Shape "Points"

Aktuell `FormatQuestionCorrectText` in `ExportService`: "Alle Teams richtig", "1 Team richtig", "Kein Team richtig", "{n} Teams richtig".

Vom Nutzer festgelegt:

| Fall | Neu |
|---|---|
| alle | `Alle richtig` |
| keiner | `Keiner richtig` |
| n | `{n}× richtig` |

**Wichtig in Verbindung mit A7:** `totalCount` basiert heute auf der Anzahl erfasster Antwortzeilen. Mit "leer = kein Erfolg" muss die Basis die **Anzahl der Rundenteams** sein (siehe A6 für die Definition der Rundenteams), sonst wird "Alle richtig" falsch ausgegeben.

### A2: Finale Runde als Eigenschaft der Runde

- `Round.IsFinal` (bool, Default false) plus Migration.
- Setzbar per Checkbox im `StartRoundModal` und in der Rundenliste (`RoundListPanel`, je Zeile eine Checkbox oder ein Toggle).
- Höchstens **eine** finale Runde pro Quiz: Setzen bei einer Runde setzt alle anderen zurück (Service-Logik in `QuizService`, nicht nur UI).
- In `RoundListPanel` gibt es nur noch **einen** Export-Button pro Runde. Der Modus ergibt sich aus `round.IsFinal ? PresentationMode.Final : PresentationMode.Round`. `PresentationMode` kann als internes Enum bestehen bleiben, `RoundExportRequest` verliert den Parameter `Mode`.
- `PresentationDownloadService.DownloadFinalAsync` (Quiz-Liste): nimmt die Runde mit `IsFinal`, sonst Fallback auf die letzte finalisierte Runde nach `CreatedAt`.
- Service-Methode z. B. `SetFinalRoundAsync(Guid roundId, bool isFinal)`, danach `ScorerService.NotifyRoundChanged()`, damit das Dashboard aktualisiert.

### A3: Teams außer Konkurrenz (AK)

**Datenmodell**: `Participant.IsNonCompetitive` (bool). Quiz-abhängig. Setzbar per Checkbox in der Teamliste des Dashboards (`TeamListPanel`), sofort gespeichert.

**Ranking-Regel (verbindlich)**
1. Die Plätze der regulären Teams werden **nur unter regulären Teams** vergeben (Competition Ranking 1, 1, 3). AK-Teams verschieben keine regulären Plätze.
2. Ein AK-Team erhält den Platz, den ein reguläres Team mit gleicher Punktzahl hätte: `Rang = 1 + Anzahl regulärer Teams mit mehr Punkten`.
3. Sortierung: Rang aufsteigend, innerhalb eines Rangs **reguläre Teams vor AK-Teams**, danach Name.

| Team | Punkte | AK | Rang | Anzeigereihenfolge |
|---|---|---|---|---|
| Host | 30 | ja | 1 | 3 |
| A | 28 | | 1 | 1 |
| B | 28 | | 1 | 2 |
| C | 25 | | 3 | 4 |

| Team | Punkte | AK | Rang | Anzeigereihenfolge |
|---|---|---|---|---|
| A | 30 | | 1 | 1 |
| Host | 29 | ja | 2 | 3 |
| B | 28 | | 2 | 2 |
| C | 25 | | 3 | 4 |

**Umsetzung**
- Regel **zentral** in `Core/Scoring` umsetzen (z. B. Überladung von `CompetitionRanking.Rank` mit `Func<T, bool> isNonCompetitive` oder neuer statischer Rechner), nicht in jeder Ansicht einzeln. Siehe Z1.
- Betroffen: Dashboard-Standings (`Index.ComputeTeamStandings`), Matrix (`Matrix.AssignRanks`), Export (`ExportService.CalculateRanks`), `LiveResultBuilder` (`Result.Rank`).
- **Nicht betroffen: `LeaderboardService`.** In der All-Time-Rangliste gibt es kein AK, die Punkte eines AK-Teams zählen dort wie bei jedem anderen Team, das Ranking bleibt das normale Competition Ranking.
- Export: Auf `RoundFirst`/`AllFirst` erscheint ein AK-Team mit Rang 1 zusätzlich, **am Ende** der Teamliste (`Team1..5`). Platzierungsfolien entsprechend der Sortierung.
- UI: AK-Teams sichtbar kennzeichnen (Badge "AK" oder Icon, Tooltip "Out of competition").

**Festlegungen**
- All-Time-Rangliste: **kein AK**. `Result` bekommt **kein** AK-Feld, `TotalScore` wird für AK-Teams normal materialisiert.
- `Result.Rank` ist der Platz innerhalb des Quizabends und wird mit der AK-Regel berechnet (Annahme, da `Rank` nur quizbezogen ist und von `LeaderboardService` nicht verwendet wird; bei Abweichung Nutzer fragen).
- Optionaler Zusatz auf Folien (z. B. "Host (a. K.)") nur auf Wunsch.

### A4: Dateiname

- Neuer Name: `{Path.GetFileNameWithoutExtension(sourceFile.Name)}_adjusted{format.Extension}`.
- Kein doppelter Suffix: Endet der Basisname bereits auf `_adjusted`, nicht erneut anhängen (sonst `X_adjusted_adjusted.pptx` bei Ketten-Uploads).
- `SanitizeFileNamePart` weiterverwenden, `CreateFileName` mit Datum/Rundenname entfällt.
- Hinweis: Der Browser hängt bei gleichem Namen selbst "(1)" an, das ist nicht beeinflussbar.

### A5: Metro-UI-Icons

- Nur das Icon-Stylesheet einbinden, **nicht** das komplette Metro-UI-CSS (kollidiert mit Bootstrap). In `Pages/_Host.cshtml` im `<head>` nach dem Bootstrap-Link:
  `<link href="https://cdn.metroui.org.ua/5.1.1/icons.css" rel="stylesheet" />`
- Verwendung: `<span class="mif-pencil" aria-hidden="true"></span>`. Größen über `mif-lg`, `mif-2x` usw. Farben über Bootstrap (`text-success`), da die Metro-`fg-*`-Klassen nicht mit eingebunden sind.
- Vollständige Iconliste: https://panda.metroui.org.ua/mif/core-pack. **Iconnamen vor Verwendung dort prüfen, nicht raten.** Wenn kein exakt passendes Icon existiert, das inhaltlich nächstliegende wählen und dem Nutzer nennen.
- Open Iconic (`oi oi-*` im NavMenu) ebenfalls ersetzen und danach den Open-Iconic-Link aus `_Host.cshtml` entfernen.

Bekannte Fundstellen (per Suche nach Nicht-ASCII-Symbolen ermittelt, vor Umsetzung per Suche vervollständigen, `Events.razor` enthält z. B. zusätzlich `↺`):

| Datei | Symbole |
|---|---|
| `Shared/NavMenu.razor` | 🎯, `oi-*` |
| `Pages/Index.razor` | 🎯 🏁 ← |
| `Pages/Event/Events.razor` | ↺ u. a. |
| `Pages/Event/Matrix.razor` | ✎ ✓ ✗ ✕ ← |
| `Pages/Event/Scorer.razor` | ✎ ✓ ✗ ← → ▶ 🎯 📋 |
| `Components/Event/ActiveScoringBanner.razor` | ✎ 🏁 |
| `Components/Event/RoundListPanel.razor` | ▶ 🎯 |
| `Components/Event/StartRoundModal.razor` | ↻ ▶ ✕ 🔄 |
| `Components/Player/TeamListPanel.razor` | 👥 ✕ |
| `Components/Player/TeamSelector.razor` | ⚠ |
| `Pages/Player/Teams.razor` | 👥 ⚠ |
| `Pages/Player/Standings.razor` | 🏆 🥇 🥈 🥉 |
| `Pages/Import/LegacyImport.razor` | ⚠ ✅ ✏ ➖ 👥 📊 📥 |

Mapping-Vorschlag (Namen gegen die Iconliste prüfen):

| Bedeutung | Kandidat |
|---|---|
| Bearbeiten | `mif-pencil` |
| Löschen | `mif-bin` |
| Schließen/Abbrechen | `mif-cross` |
| Richtig/Bestätigen | `mif-checkmark` |
| Zurück/Weiter | `mif-arrow-left` / `mif-arrow-right` |
| Starten | `mif-play` |
| Wiedereröffnen/Neu laden | `mif-loop2` bzw. `mif-undo` |
| Teams | `mif-users` |
| Rangliste/Podium | `mif-trophy` (Medaillen über Farbe) |
| Abschließen | `mif-flag` |
| Warnung | `mif-warning` |
| Import/Export | `mif-upload` / `mif-download` |
| Kalender, Liste, Diagramm | `mif-calendar`, `mif-list`, `mif-chart-bars` |
| Außer Konkurrenz | Vorschlag suchen (z. B. `mif-home` für Gastgeber) |

### A6: Bug, nachträglich erfasste Teams fehlen im Rundenergebnis

**Ursache**: `Round.GetTeamIds()` liefert nur Teams aus den Scorer-Zuordnungen. Die Matrix zeigt aber alle Teilnehmer des Quiz. Wird ein Team nach Finalisierung registriert oder war es keiner Station zugeordnet, landen seine Matrix-Antworten zwar in der DB, `ExportService` filtert es aber über `roundTeamIds.Contains(...)` heraus. Dasselbe gilt für die Spalte "Round" in `Index.ComputeTeamStandings`.

**Lösung**
- Rundenteams = Teams aus Zuordnungen **∪** Teams mit mindestens einer Antwort in der Runde. `GetTeamIds()` entsprechend erweitern (Doc-Kommentar: erfordert geladene `Assignments` und `Answers`), `.ToArray()` beibehalten.
- Alle Verwender prüfen: `ExportService`, `Index.ComputeTeamStandings`, `ActiveScoringBanner` (Fortschritt nur für zugeordnete Teams ist korrekt, nicht ändern).
- Matrix: zeigt weiterhin alle **aktiven** Teilnehmer (A11), damit Nachträge möglich sind.

### A7: Matrix zweiwertig

- Anzeige: Erfolg = Icon (grün), sonst leer. Kein ✗ und kein "—" mehr.
- Klick toggelt Erfolg an/aus.
- **Speichern als `AnswerBool { Correct = false }`, Zeile nicht löschen.** Grund: `ActiveScoringBanner` berechnet den Fortschritt über die Anzahl der Antwortzeilen, und die Scorer-Stationen schreiben ebenfalls `false`-Zeilen.
- Typen umstellen: `Dictionary<(Guid TeamId, int QuestionIndex), bool>` statt `bool?` in `QuizService.UpdateAnswersAsync`/`UpdateAnswersCoreAsync`, `MatrixRow.Answers` auf `bool[]`. Den `null`-Zweig (Löschen) entfernen.
- Hinweis: `AnswerPoint` (Teilpunkte, "future use") wird von der Matrix nicht abgebildet. Als technische Schuld vermerken.

### A8: Matrix sofort editierbar

- `isEditing`, `SaveMatrixAsync`, `CancelEditing`, "Quick Edit"/"Save"/"Cancel" entfallen.
- Klick: optimistisch lokal umschalten, Summen und Ränge neu berechnen, dann sofort `UpdateAnswersAsync` für die eine Zelle.
- Klicks serialisieren (z. B. `SemaphoreSlim` in der Komponente), damit schnelle Folgeklicks nicht in falscher Reihenfolge ankommen.
- Fehler: Zelle zurücksetzen, Toast anzeigen.
- Nach erfolgreichem Schreiben `ScorerService.NotifyAnswersChanged()` (Existenz und Namen prüfen), damit Dashboard und Stationen aktualisieren.
- Matrix selbst auf `OnAnswersChanged` abonnieren und neu laden, wenn Scorer parallel erfassen (`IDisposable`, Abmeldung, `InvokeAsync`). Letzter Schreiber gewinnt, das ist akzeptiert.
- Bei abgeschlossenem Quiz baut jede Änderung die `Result`-Zeilen neu (bestehende Transaktion in `UpdateAnswersCoreAsync`). Das ist pro Klick teurer, aber korrekt.
- Das komponenteneigene `<style>` in `Matrix.razor` wirkt global. In `site.css` verschieben (Erklärung siehe CSS-Anweisungen).

### A9: Edit-Buttons

- Alle Bearbeiten-Buttons enthalten nur `<span class="mif-pencil" aria-hidden="true"></span>`.
- Beschreibung in `title` **und** `aria-label` (Screenreader, da kein sichtbarer Text).
- Betroffen u. a.: Matrix-Link im `ActiveScoringBanner` ("✎ Open Matrix"), Scorer-Seite, Events (Quiz bearbeiten), Teams (Umbenennen), LegacyImport. Vollständig per Suche nach "Edit", "✎", "✏" ermitteln.

### A10: Register-Team bleibt offen

- In `TeamListPanel.razor.cs` bei `HandleExistingTeamSelected` und `HandleNewTeamCreated` das `showAddTeamForm = false` entfernen.
- Nach dem Registrieren Eingabefeld leeren und **Fokus zurück ins Feld** (`TeamSelector` braucht dafür eine öffentliche `FocusAsync()` über `ElementReference`).
- Schließen nur über den Button (Icon `mif-cross`, Tooltip "Close").
- Fehler (Team bereits registriert) lassen das Formular offen.

### A11: Team löschen oder deaktivieren

**Datenmodell**: `Participant.IsActive` (bool, Default true) plus Migration. Deaktivierung gilt **pro Quiz** (vom Nutzer festgelegt), kein globales Feld am `Team`.

**Löschen** (Button nur sichtbar, wenn das Team in diesem Quiz keine Antworten hat, die Prüfung erfolgt zusätzlich serverseitig):
- `QuizService.RemoveTeamAsync(quizId, teamId)` in einer Transaktion:
  1. Prüfen: keine `Answer` des Teams in Runden dieses Quiz.
  2. `Participant` löschen.
  3. Team-ID aus `Scorer.TeamIds` aller Runden des Quiz entfernen (neue Listeninstanz zuweisen, wie in `AddTeamAsync`).
  4. `Team` komplett löschen, wenn es **keine** `Result`-Zeilen (auch Legacy), keine `Answer`-Zeilen und keine weiteren `Participant`-Einträge hat.
- Danach `NotifyRoundChanged()` (Stationen laden ihre Zuordnung neu). Bestätigung über `ConfirmModal`.

**Deaktivieren** (wenn Antworten existieren), per Toggle wieder aktivierbar:
- Nicht mehr in neuen Runden: `StartRoundModal` und `ValidateRoundRequest` berücksichtigen nur aktive Teilnehmer ("alle **aktiven** Teams genau einmal zugeordnet").
- In einer laufenden Runde: aus der Scorer-Zuordnung entfernen, wenn es in dieser Runde noch keine Antworten hat, sonst drin lassen.
- Nicht in der Matrix neuer Runden, nicht in neuen Rundenergebnissen (ergibt sich über A6).
- Gesamtwertung des Abends und All-Time: bleibt enthalten, in der Teamliste ausgegraut mit Hinweis.
- `AddTeamAsync` bei einem vorhandenen, inaktiven Teilnehmer: reaktivieren statt Fehler "already registered".
- Fingerprint (`QuizFingerprint`) um aktive Teilnehmer bzw. AK-Status ergänzen, sonst erkennt das Dashboard die Änderung erst beim periodischen Voll-Reload.

### A12: Menü seitlich

**Ursache**: `MainLayout.razor` und `NavMenu.razor` nutzen die Klassen des Blazor-Templates (`page`, `sidebar`, `top-row`, `nav-scrollable`), die zugehörigen Styles (`MainLayout.razor.css`, `NavMenu.razor.css`) fehlen aber. `site.css` enthält nur die Fehlerleiste. Ohne Styles stapeln sich Sidebar und Inhalt.

**Lösung**
- Layout-Regeln in `wwwroot/css/site.css` ergänzen (neuer, kommentierter Abschnitt am Dateiende), kein Scoped CSS. Grund: Das Scoped-CSS-Bundle `PubQuizMaster.Web.styles.css` ist in `_Host.cshtml` nicht eingebunden, `.razor.css`-Dateien würden also nicht greifen.
- Inhalt: `.page` als Flex-Container (ab Breakpoint `md` nebeneinander, darunter untereinander), `.sidebar` mit fester Breite, dunklem Hintergrund und `position: sticky; top: 0; height: 100vh`, `main` mit `flex: 1`, `.top-row` als Kopfzeile, `.nav-link`-Styles inkl. `.active`.
- **Jede Regel einzeln erklären**, inklusive Media Query.
- Das statische Badge "Live Mode" in der Kopfzeile entfernen oder nur bei aktivem Quiz zeigen.

### A13: Runde löschen

- Löschen-Button (`mif-bin`) in `RoundListPanel`, nur wenn `round.Answers.Count == 0`. Serverseitig erneut prüfen.
- `QuizService.DeleteRoundAsync(roundId)`: Prüfung auf Antworten in der DB, dann löschen (Assignments per Cascade, prüfen). Bei offener Runde danach `NotifyRoundChanged()`, damit Scorer-Stationen nicht auf eine gelöschte Runde zeigen.
- Bestätigung über `ConfirmModal`.
- Folgeproblem: `StartRoundModal` schlägt `Round {Rounds.Count + 1}` vor. Nach einer Löschung kann der Name mit einer bestehenden Runde kollidieren. Nächsten freien Namen ermitteln.

### A14: Eine Startseite statt Quiz-Nights und Active Night

- Route `/`: Ist ein Quiz aktiv, Dashboard anzeigen, sonst die Quizliste.
- Den Inhalt von `Events.razor` in eine Komponente verschieben (z. B. `Components/Event/QuizListPanel.razor` plus `.razor.cs`), `Index` entscheidet, was gerendert wird.
- `/events` entfernen oder auf `/` umleiten. NavMenu: "Events" entfällt, "Live Quiz" wird z. B. "Quiz".
- Nach "Quiz anlegen" oder "Wiedereröffnen" lädt `Index` den Zustand neu und zeigt das Dashboard. Nach "Complete Quiz Night" erscheint die Liste.
- **Quiz-Details bearbeiten (`QuizEditModal`) muss im Dashboard erreichbar sein** (Stift-Icon neben dem Titel), da die Liste bei aktivem Quiz nicht sichtbar ist.
- Buttons "← All Events" im Dashboard entfallen. Matrix "Back" führt auf `/`.
- Export der Endpräsentation abgeschlossener Quizzes bleibt in der Liste.

## 4. Zusätzliche Befunde und Vorschläge

| ID | Befund | Vorschlag | Priorität |
|---|---|---|---|
| Z1 | Ranking wird an fünf Stellen separat berechnet (`Index`, `Matrix`, `ExportService`, `LiveResultBuilder`, `LeaderboardService`) | Zentraler Rechner in `Core/Scoring` für Runden- und Gesamtwertung inkl. AK und aktiv/inaktiv. **Voraussetzung für A3** | hoch |
| Z2 | Export "x richtig" bezieht sich auf erfasste Zeilen | Basis = Rundenteams (A1/A6/A7) | hoch |
| Z3 | Präsentation muss bei jedem Export neu hochgeladen werden | **Vom Nutzer abgelehnt: keine serverseitige Speicherung.** Der Upload pro Export bleibt, nicht erneut vorschlagen | entfällt |
| Z4 | Finale Runde finalisieren und Quiz abschließen sind zwei Schritte | Beim Finalisieren einer `IsFinal`-Runde direkt "Quiz abschließen?" anbieten | mittel |
| Z5 | Rundenname muss exakt dem PowerPoint-Abschnitt entsprechen, Runden sind aber nicht umbenennbar | Rundenname per Stift-Icon editierbar machen (Eindeutigkeit prüfen) | mittel |
| Z6 | `StartRoundModal` setzt die Fragenanzahl fest auf 20 | Anzahl der letzten Runde übernehmen | niedrig |
| Z7 | Einmal eingeblendete Folien werden nie wieder ausgeblendet | In den `else`-Zweigen `SetSlideVisibility(..., false)` | mittel |
| Z8 | Fehlende Folien/Shapes werden beim Export still ignoriert | Liste fehlender Namen im Toast als Warnung | mittel |
| Z9 | `FillWinnersSlide` schneidet bei mehr als 5 Teams still ab | Warnung ausgeben | niedrig |
| Z10 | Scoped-CSS-Bundle nicht in `_Host.cshtml` eingebunden | Bewusst bei `site.css` bleiben oder Bundle-Link ergänzen, einheitlich entscheiden | niedrig |
| Z11 | Export-Fehler zeigt kompletten JS-Stacktrace im Toast | Eigener `catch (JSException)` mit kurzer Meldung | niedrig |
| Z12 | Speicher: Präsentation liegt beim Export mehrfach im RAM, Up- und Download über SignalR | Bei großen Dateien Minimal-API-Endpoint mit Temp-Datei | niedrig |

## 5. Empfohlene Reihenfolge

| Paket | Inhalt | Begründung |
|---|---|---|
| 1 | A12, A5, A9 | Layout und Icons zuerst, alle späteren UI-Änderungen nutzen sie |
| 2 | Z1, A6 | Zentrale Ranking- und Rundenteam-Logik als Basis |
| 3 | A7, A8 | Matrix auf neue Logik |
| 4 | Migration: `Round.IsFinal`, `Participant.IsNonCompetitive`, `Participant.IsActive` | Eine Migration für alle Schemaänderungen |
| 5 | A3, A11, A10 | Teamverwaltung |
| 6 | A2, A13, Z5, Z6 | Rundenverwaltung |
| 7 | A1, A4, Z7, Z8 | Export |
| 8 | A14, Z4 | Navigation zuletzt, da sie viele Dateien berührt |

Nach jedem Paket: kompilierbarer Stand, Liste der geänderten Dateien, bekannte Einschränkungen.

## 6. Bekannte technische Schulden (Stand jetzt)

- `AnswerPoint` existiert, wird aber von Matrix, Export-Text und Fortschritt nicht sinnvoll unterstützt.
- Dashboard pollt alle 2 s und lädt periodisch den kompletten Graphen.
- Folientexte sind hart kodiert deutsch, UI englisch.
- Abschnittszuordnung der Präsentation über den Rundennamen (Exception bei Tippfehler).
- ppsm/pptm werden beim Export abgelehnt.
- Zeilenumbrüche in den Quelldateien gemischt (CRLF/LF).

## 7. Nächster Schritt

Code-Export in Teilen entgegennehmen, prüfen, ob der Fehler `downloadFileFromStream was undefined` behoben ist, und mit Paket 1 beginnen. A1, A3 (All-Time), A11 (Deaktivierung pro Quiz) und Z3 (keine serverseitige Speicherung) sind festgelegt, es gibt keine offenen Grundsatzfragen.
