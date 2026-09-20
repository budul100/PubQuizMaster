using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Core.Records.Standings;
using Drawing = DocumentFormat.OpenXml.Drawing;
using P14 = DocumentFormat.OpenXml.Office2010.PowerPoint;

namespace PubQuizMaster.Services.Event
{
    public static class ExportService
    {
        #region Private Fields

        private const string ContentTypePresentation = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        private const string ContentTypeSlideshow = "application/vnd.openxmlformats-officedocument.presentationml.slideshow";

        private const string ShapeNameAverage = "Average";
        private const string ShapeNamePoints = "Points";
        private const string ShapeNamePositions = "Positions";
        private const string ShapeNameQuestion = "Question";
        private const string ShapeNameTeams = "Teams";
        private const string ShapePrefixTeam = "Team";

        private const string SlideNameAllFirst = "AllFirst";
        private const string SlideNameAllPlacings = "AllPlacings";
        private const string SlideNameAllSecond = "AllSecond";
        private const string SlideNameAllThird = "AllThird";
        private const string SlideNameGoodBye = "GoodBye";
        private const string SlideNameRoundFirst = "RoundFirst";
        private const string SlideNameRoundPlaces = "RoundPlaces";

        private const string SlidePrefixAnswer = "Answer";
        private const string SlidePrefixMarker = "#";

        // Slide texts are German, numbers must match regardless of the server culture
        private static readonly CultureInfo slideCulture = CultureInfo.GetCultureInfo("de-DE");

        #endregion Private Fields

        #region Public Methods

        /// <summary>
        /// Fills the results of the given round into an existing presentation (pptx, ppsx or potx).
        /// The document is modified in place, the stream must be seekable and writable.
        /// Slides and shapes the template lacks are skipped and reported in the result, the export still succeeds.
        /// </summary>
        public static PresentationResult FillPresentation(Quiz quizNight, Guid roundId,
            PresentationType mode, Stream document)
        {
            if (!document.CanSeek || !document.CanWrite)
            {
                throw new ArgumentException(
                    message: "The document stream must be seekable and writable.",
                    paramName: nameof(document));
            }

            var rounds = quizNight.Rounds.OrderBy(r => r.CreatedAt).ToArray();

            var round = rounds.FirstOrDefault(r => r.Id == roundId)
                ?? throw new InvalidOperationException("Round not found.");

            if (!round.IsFinalized)
            {
                throw new InvalidOperationException($"Round '{round.Name}' is not finalized yet.");
            }

            var isFinalRound = mode == PresentationType.Final;

            document.Position = 0;

            using var doc = PresentationDocument.Open(document, isEditable: true);

            var format = PrepareDocumentType(doc);

            var presoPart = doc.PresentationPart
                ?? throw new InvalidOperationException("Presentation has no presentation part.");

            var slides = GetRoundSlides(
                presoPart: presoPart,
                name: round.Name);

            var issues = new IssueService();

            var participantLookup = quizNight.ParticipatingTeams.ToDictionary(p => p.TeamId);
            var allTeams = quizNight.ParticipatingTeams.Select(pt => pt.Team).ToArray();
            var roundTeamIds = round.GetTeamIds();

            // 1. Calculate scores and rankings for current round
            var roundScores = allTeams
                .Where(t => roundTeamIds.Contains(t.Id))
                .Select(t => (
                    Team: t,
                    Score: round.Answers.Where(a => a.TeamId == t.Id).Sum(a => a.Value.GetScore()),
                    IsNonCompetitive: participantLookup.TryGetValue(t.Id, out var p) && p.IsNonCompetitive)).ToArray();

            var roundRanks = CalculateRanks(roundScores);

            // 2. Calculate cumulative overall scores up to this round
            var completedRounds = rounds
                .TakeWhile(r => r.Id != roundId)
                .Append(round).ToArray();

            var totalScores = allTeams
                .Select(t => (
                    Team: t,
                    Score: completedRounds
                        .SelectMany(r => r.Answers)
                        .Where(a => a.TeamId == t.Id)
                        .Sum(a => a.Value.GetScore()),
                    IsNonCompetitive: participantLookup.TryGetValue(t.Id, out var p) && p.IsNonCompetitive)).ToArray();

            var totalRanks = CalculateRanks(totalScores);

            var isFirstRound = rounds[0].Id == round.Id;

            // 3. Question slides: "x correct" counts against the round teams, not the recorded rows.
            //    Explicit slide names win, otherwise the questions map to the Question slides in deck order.
            var questionSlides = GetQuestionSlides(slides);

            for (var qi = 0; qi < round.Length; qi++)
            {
                var slideName = $"{SlidePrefixAnswer}{qi + 1}";
                var slidePart = FindSlide(slides, slideName)
                    ?? (qi < questionSlides.Length ? questionSlides[qi] : null);

                if (slidePart == null)
                {
                    issues.AddMissingSlide(slideName);
                    continue;
                }

                var questionAnswers = round.Answers.Where(a => a.QuestionIndex == qi).ToArray();
                var correctCount = questionAnswers.Count(a => a.Value.GetScore() > 0);
                var totalCount = roundTeamIds.Length;

                var pointsText = FormatQuestionCorrectText(
                    correct: correctCount,
                    total: totalCount);

                SetShapeVisibility(
                    sp: slidePart,
                    prefix: ShapeNamePoints,
                    visible: true);

                SetShapeText(
                    sp: slidePart,
                    slideName: slideName,
                    shapeName: ShapeNamePoints,
                    text: pointsText,
                    issues: issues);
            }

            // 4. Round standings slides
            var roundPlacesPart = FindRequiredSlide(
                slides: slides,
                name: SlideNameRoundPlaces,
                issues: issues);

            var roundFirstPart = FindRequiredSlide(
                slides: slides,
                name: SlideNameRoundFirst,
                issues: issues);

            if (roundRanks.Length > 0)
            {
                if (roundPlacesPart != null)
                {
                    var placesEntries = roundRanks.Where(e => e.Rank > 1).ToArray();

                    if (placesEntries.Length > 0)
                    {
                        var avg = roundScores.Length > 0
                            ? roundScores.Average(x => x.Score)
                            : 0m;

                        FillPlacesSlide(
                            sp: roundPlacesPart,
                            slideName: SlideNameRoundPlaces,
                            entries: placesEntries,
                            average: avg,
                            issues: issues);

                        SetSlideVisibility(
                            sp: roundPlacesPart,
                            visible: true);
                    }
                    else
                    {
                        SetSlideVisibility(
                            sp: roundPlacesPart,
                            visible: false);
                    }
                }

                if (roundFirstPart != null)
                {
                    // CalculateRanks orders regular winners before non-competitive ones
                    FillWinnersSlide(sp: roundFirstPart,
                        slideName: SlideNameRoundFirst,
                        entries: [.. roundRanks.Where(e => e.Rank == 1)],
                        issues: issues);

                    SetSlideVisibility(
                        sp: roundFirstPart,
                        visible: true);
                }
            }
            else
            {
                if (roundPlacesPart != null) SetSlideVisibility(
                    sp: roundPlacesPart,
                    visible: false);

                if (roundFirstPart != null) SetSlideVisibility(
                    sp: roundFirstPart,
                    visible: false);
            }

            // 5. Total standings and podium slides. Slides are only required where this export uses them.
            var showTotalPlacings = !isFirstRound || isFinalRound;

            var totalPlacesPart = showTotalPlacings
                ? FindRequiredSlide(
                    slides: slides,
                    name: SlideNameAllPlacings,
                    issues: issues)
                : FindSlide(
                    slides: slides,
                    name: SlideNameAllPlacings);

            var goodByePart = isFinalRound
                ? FindRequiredSlide(
                    slides: slides,
                    name: SlideNameGoodBye,
                    issues: issues)
                : FindSlide(
                    slides: slides,
                    name: SlideNameGoodBye);

            if (totalRanks.Length > 0)
            {
                if (totalPlacesPart != null)
                {
                    var placingEntries = showTotalPlacings
                        ? totalRanks.Where(e => !isFinalRound || e.Rank >= 4).ToArray()
                        : [];

                    if (placingEntries.Length > 0)
                    {
                        var avg = totalScores.Length > 0
                            ? totalScores.Average(x => x.Score)
                            : 0m;

                        FillPlacesSlide(
                            sp: totalPlacesPart,
                            slideName: SlideNameAllPlacings,
                            entries: placingEntries,
                            average: avg,
                            issues: issues);

                        SetSlideVisibility(
                            sp: totalPlacesPart,
                            visible: true);
                    }
                    else
                    {
                        SetSlideVisibility(
                            sp: totalPlacesPart,
                            visible: false);
                    }
                }

                if (isFinalRound)
                {
                    FillPodiumSlide(
                        slides: slides,
                        slideName: SlideNameAllThird,
                        ranks: totalRanks,
                        rank: 3,
                        issues: issues);

                    FillPodiumSlide(
                        slides: slides,
                        slideName: SlideNameAllSecond,
                        ranks: totalRanks,
                        rank: 2,
                        issues: issues);

                    FillPodiumSlide(
                        slides: slides,
                        slideName: SlideNameAllFirst,
                        ranks: totalRanks,
                        rank: 1,
                        issues: issues);

                    if (goodByePart != null) SetSlideVisibility(
                        sp: goodByePart,
                        visible: true);
                }
                else
                {
                    HideSlide(
                        slides: slides,
                        name: SlideNameAllThird);

                    HideSlide(
                        slides: slides,
                        name: SlideNameAllSecond);

                    HideSlide(
                        slides: slides,
                        name: SlideNameAllFirst);

                    if (goodByePart != null) SetSlideVisibility(
                        sp: goodByePart,
                        visible: false);
                }
            }
            else
            {
                if (totalPlacesPart != null) SetSlideVisibility(
                    sp: totalPlacesPart,
                    visible: false);

                HideSlide(
                    slides: slides,
                    name: SlideNameAllThird);

                HideSlide(
                    slides: slides,
                    name: SlideNameAllSecond);

                HideSlide(
                    slides: slides,
                    name: SlideNameAllFirst);

                if (goodByePart != null) SetSlideVisibility(
                    sp: goodByePart,
                    visible: false);
            }

            presoPart.Presentation!.Save();

            return issues.ToResult(format);
        }

        #endregion Public Methods

        #region Private Methods

        private static RankedTeam[] CalculateRanks((Team Team, decimal Score, bool IsNonCompetitive)[] scores)
        {
            // Tie order within a rank, same comparer as dashboard and matrix
            var ordered = scores.OrderBy(x => x.Team.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            return [.. ordered.Rank(x => x.Score, x => x.IsNonCompetitive)
                .Select(r => new RankedTeam(r.Item.Team, r.Item.Score, r.Rank))];
        }

        /// <summary>
        /// Run formatting of the paragraph's first run, or of its endParaRPr when the paragraph is empty.
        /// Both elements share the CT_TextCharacterProperties shape, so attributes and children carry over.
        /// Placeholders that were never typed into only ever carry endParaRPr.
        /// </summary>
        private static Drawing.RunProperties? CloneRunProperties(Drawing.Paragraph? paragraph)
        {
            if (paragraph?.Elements<Drawing.Run>().FirstOrDefault()?.RunProperties?.CloneNode(true)
                is Drawing.RunProperties fromRun)
            {
                return fromRun;
            }

            var endProperties = paragraph?.Elements<Drawing.EndParagraphRunProperties>().FirstOrDefault();
            if (endProperties == null) return null;

            var fromEnd = new Drawing.RunProperties();

            foreach (var attribute in endProperties.GetAttributes())
            {
                fromEnd.SetAttribute(attribute);
            }

            foreach (var child in endProperties.ChildElements)
            {
                fromEnd.AppendChild(child.CloneNode(true));
            }

            return fromEnd;
        }

        /// <summary>
        /// Number of consecutively numbered shapes (Team1, Team2, ...) on the slide.
        /// The template decides how many winners fit, not the code.
        /// </summary>
        private static int CountNumberedShapes(SlidePart sp, string prefix)
        {
            var count = 0;

            while (HasShape(sp, $"{prefix}{count + 1}"))
            {
                count++;
            }

            return count;
        }

        private static void FillPlacesSlide(SlidePart sp, string slideName, RankedTeam[] entries, decimal average,
            IssueService issues)
        {
            if (entries.Length == 0) return;

            var positions = string.Join("\n", entries.Select(e => $"{e.Rank}."));
            SetShapeText(
                sp: sp,
                slideName: slideName,
                shapeName: ShapeNamePositions,
                text: positions,
                issues: issues);

            var teams = string.Join("\n", entries.Select(e => e.Team.Name));
            SetShapeText(
                sp: sp,
                slideName: slideName,
                shapeName: ShapeNameTeams,
                text: teams,
                issues: issues);

            var points = string.Join("\n", entries.Select(e => FormatScore(e.Score)));
            SetShapeText(
                sp: sp,
                slideName: slideName,
                shapeName: ShapeNamePoints,
                text: points,
                issues: issues);

            var avgText = FormatAverage(average);
            SetShapeText(
                sp: sp,
                slideName: slideName,
                shapeName: ShapeNameAverage,
                text: avgText,
                issues: issues);
        }

        private static void FillPodiumSlide(SlidePart[] slides, string slideName, RankedTeam[] ranks, int rank,
            IssueService issues)
        {
            var slidePart = FindRequiredSlide(slides, slideName, issues);
            if (slidePart == null) return;

            var rankEntries = ranks.Where(e => e.Rank == rank).ToArray();
            if (rankEntries.Length == 0)
            {
                SetSlideVisibility(slidePart, false);
                return;
            }

            FillWinnersSlide(slidePart, slideName, rankEntries, issues);
            SetSlideVisibility(slidePart, true);
        }

        /// <summary>
        /// Fills the Team1..TeamN shapes and the Points shape of a winners or podium slide.
        /// Entries arrive in ranking order, regular teams first. The slide shows their score;
        /// a non-competitive team sharing the rank with a different score gets its own score behind the name.
        /// </summary>
        private static void FillWinnersSlide(SlidePart sp, string slideName, RankedTeam[] entries,
            IssueService issues)
        {
            var capacity = CountNumberedShapes(sp, ShapePrefixTeam);

            var slideScore = entries.Length > 0
                ? entries[0].Score
                : 0m;

            if (capacity == 0)
            {
                issues.AddMissingShape(
                    slideName: slideName,
                    shapeName: $"{ShapePrefixTeam}1");
            }
            else if (entries.Length > capacity)
            {
                var dropped = string.Join(", ", entries.Skip(capacity).Select(e => e.Team.Name));

                issues.AddWarning(
                    $"{slideName}: {entries.Length} teams share this place, the slide has room for {capacity}. " +
                    $"Not shown: {dropped}.");
            }

            for (var index = 1; index <= capacity; index++)
            {
                var text = index <= entries.Length
                    ? FormatWinnerName(entries[index - 1], slideScore)
                    : string.Empty;

                SetShapeText(
                    sp: sp,
                    slideName: slideName,
                    shapeName: $"{ShapePrefixTeam}{index}",
                    text: text,
                    issues: issues);
            }

            SetShapeText(
                sp: sp,
                slideName: slideName,
                shapeName: ShapeNamePoints,
                text: FormatScore(slideScore),
                issues: issues);
        }

        /// <summary>Like FindSlide, but records the slide as missing when the template lacks it.</summary>
        private static SlidePart? FindRequiredSlide(SlidePart[] slides, string name, IssueService issues)
        {
            var slidePart = FindSlide(
                slides: slides,
                name: name);

            if (slidePart == null)
            {
                issues.AddMissingSlide(name);
            }

            return slidePart;
        }

        private static SlidePart? FindSlide(SlidePart[] slides, string name)
        {
            var markerName = SlidePrefixMarker + name;

            return slides.FirstOrDefault(sp => sp.Slide?.CommonSlideData?.Name?.Value == name)
                ?? slides.FirstOrDefault(sp => sp.Slide?.Descendants<NonVisualDrawingProperties>()
                    .Any(p => p.Name?.Value == markerName) == true);
        }

        private static string FormatAverage(decimal average)
        {
            var rounded = Math.Round(average, 1);
            var formatted = rounded.ToString("0.#", slideCulture);
            return $"{formatted} Punkte / Team";
        }

        private static string FormatQuestionCorrectText(int correct, int total)
        {
            if (total == 0) return string.Empty;
            if (correct == total) return "Alle richtig";
            if (correct == 0) return "Keiner richtig";
            return $"{correct}× richtig";
        }

        private static string FormatScore(decimal score)
        {
            var rounded = Math.Round(score, 1);
            if (rounded == 1m) return "1 Punkt";

            var formatted = rounded.ToString("0.#", slideCulture);
            return $"{formatted} Punkte";
        }

        private static string FormatWinnerName(RankedTeam entry, decimal slideScore)
        {
            return entry.Score == slideScore
                ? entry.Team.Name
                : $"{entry.Team.Name} ({FormatScore(entry.Score)})";
        }

        /// <summary>
        /// Question slides in deck order, recognized by their Question shape.
        /// Standings and podium slides never carry one, so the two sets cannot overlap.
        /// </summary>
        private static SlidePart[] GetQuestionSlides(SlidePart[] slides)
        {
            return [.. slides.Where(sp => HasShape(
                sp: sp,
                name: ShapeNameQuestion))];
        }

        private static SlidePart[] GetRoundSlides(PresentationPart presoPart, string name)
        {
            var presentation = presoPart.Presentation
                ?? throw new InvalidOperationException("Presentation part is empty.");

            var slideIds = presentation.SlideIdList?.Elements<SlideId>().ToArray() ?? [];
            var sections = presentation.Descendants<P14.Section>().ToArray();

            if (sections.Length > 0)
            {
                var section = sections.FirstOrDefault(s => string.Equals(
                        s.Name?.Value?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"No section named '{name}' found in the presentation.");

                var sectionIds = section.Descendants<P14.SectionSlideIdListEntry>()
                    .Select(e => e.Id?.Value)
                    .ToHashSet();

                slideIds = [.. slideIds.Where(s => sectionIds.Contains(s.Id?.Value))];
            }

            return [.. slideIds
                .Where(s => s.RelationshipId?.Value != null)
                .Select(s => presoPart.GetPartById(s.RelationshipId!.Value!))
                .OfType<SlidePart>()];
        }

        private static bool HasShape(SlidePart sp, string name)
        {
            return sp.Slide?.Descendants<Shape>()
                .Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value == name) == true;
        }

        private static void HideSlide(SlidePart[] slides, string name)
        {
            var slidePart = FindSlide(
                slides: slides,
                name: name);

            if (slidePart != null)
            {
                SetSlideVisibility(slidePart, false);
            }
        }

        private static PresentationFormat PrepareDocumentType(PresentationDocument doc)
        {
            switch (doc.DocumentType)
            {
                case PresentationDocumentType.Presentation:
                    return new PresentationFormat(
                        Extension: ".pptx",
                        ContentType: ContentTypePresentation);

                case PresentationDocumentType.Slideshow:
                    return new PresentationFormat(
                        Extension: ".ppsx",
                        ContentType: ContentTypeSlideshow);

                case PresentationDocumentType.Template:
                    doc.ChangeDocumentType(PresentationDocumentType.Presentation);
                    return new PresentationFormat(
                        Extension: ".pptx",
                        ContentType: ContentTypePresentation);

                default:
                    throw new NotSupportedException($"Document type '{doc.DocumentType}' is not supported.");
            }
        }

        private static void SetShapeText(SlidePart sp, string slideName, string shapeName, string text,
            IssueService issues)
        {
            var shape = sp.Slide?.Descendants<Shape>()
                .FirstOrDefault(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value == shapeName);

            if (shape?.TextBody == null)
            {
                issues.AddMissingShape(
                    slideName: slideName,
                    shapeName: shapeName);

                return;
            }

            var txBody = shape.TextBody;
            var firstPara = txBody.Elements<Drawing.Paragraph>().FirstOrDefault();
            var runProperties = CloneRunProperties(firstPara);

            txBody.RemoveAllChildren<Drawing.Paragraph>();

            foreach (var line in text.Split('\n'))
            {
                var para = new Drawing.Paragraph();
                if (firstPara?.ParagraphProperties?.CloneNode(true) is Drawing.ParagraphProperties pPr)
                {
                    para.Append(pPr);
                }

                var run = new Drawing.Run();
                if (runProperties?.CloneNode(true) is Drawing.RunProperties rPr)
                {
                    run.Append(rPr);
                }

                run.Append(new Drawing.Text(line));
                para.Append(run);
                txBody.Append(para);
            }

            if (!txBody.Elements<Drawing.Paragraph>().Any())
            {
                txBody.Append(new Drawing.Paragraph());
            }
        }

        /// <summary>Shows or hides all shapes whose name starts with the prefix.</summary>
        private static void SetShapeVisibility(SlidePart sp, string prefix, bool visible)
        {
            var shapes = sp.Slide?.Descendants<Shape>()
                .Where(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.StartsWith(prefix) == true)
                .ToArray() ?? [];

            foreach (var shape in shapes)
            {
                if (shape.NonVisualShapeProperties?.NonVisualDrawingProperties != null)
                {
                    // Removing the attribute is the default state "visible", only hiding writes it
                    shape.NonVisualShapeProperties.NonVisualDrawingProperties.Hidden = visible
                        ? null
                        : true;
                }
            }
        }

        private static void SetSlideVisibility(SlidePart sp, bool visible)
        {
            if (sp.Slide != null)
            {
                sp.Slide.Show = new BooleanValue(visible);
                sp.Slide.Save();
            }
        }

        #endregion Private Methods
    }
}