using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Player;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Core.Records.Player;
using PubQuizMaster.Core.Scoring;
using Drawing = DocumentFormat.OpenXml.Drawing;
using P14 = DocumentFormat.OpenXml.Office2010.PowerPoint;

namespace PubQuizMaster.Services.Event
{
    public static class ExportService
    {
        #region Private Fields

        private const string PresentationContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        private const string SlideMarkerPrefix = "#";
        private const string SlideshowContentType = "application/vnd.openxmlformats-officedocument.presentationml.slideshow";

        // Slide texts are German, numbers must match regardless of the server culture
        private static readonly CultureInfo slideCulture = CultureInfo.GetCultureInfo("de-DE");

        #endregion Private Fields

        #region Public Methods

        /// <summary>
        /// Fills the results of the given round into an existing presentation (pptx, ppsx or potx).
        /// The document is modified in place, the stream must be seekable and writable.
        /// </summary>
        public static PresentationFormat FillPresentation(Quiz quizNight, Guid roundId,
            PresentationMode mode, Stream document)
        {
            if (!document.CanSeek || !document.CanWrite)
            {
                throw new ArgumentException("The document stream must be seekable and writable.", nameof(document));
            }

            // Include gives no ordering guarantee, cumulative scores and isFirstRound rely on it
            var rounds = quizNight.Rounds.OrderBy(r => r.CreatedAt).ToArray();

            var round = rounds.FirstOrDefault(r => r.Id == roundId)
                ?? throw new InvalidOperationException("Round not found.");

            if (!round.IsFinalized)
            {
                throw new InvalidOperationException($"Round '{round.Name}' is not finalized yet.");
            }

            var isFinalRound = mode == PresentationMode.Final;

            document.Position = 0;

            using var doc = PresentationDocument.Open(document, isEditable: true);

            var format = PrepareDocumentType(doc);

            var presoPart = doc.PresentationPart
                ?? throw new InvalidOperationException("Presentation has no presentation part.");

            var slides = GetRoundSlides(presoPart, round.Name);

            var allTeams = quizNight.ParticipatingTeams.Select(pt => pt.Team).ToArray();
            var roundTeamIds = round.GetTeamIds();

            // 1. Calculate scores and rankings for current round
            var roundScores = allTeams
                .Where(t => roundTeamIds.Contains(t.Id))
                .Select(t => (
                    Team: t,
                    Score: round.Answers.Where(a => a.TeamId == t.Id).Sum(a => a.Value.GetScore())))
                .ToArray();

            var roundRanks = CalculateRanks(roundScores);

            // 2. Calculate cumulative overall scores up to this round
            var completedRounds = rounds
                .TakeWhile(r => r.Id != roundId)
                .Append(round)
                .ToArray();

            var totalScores = allTeams
                .Select(t => (
                    Team: t,
                    Score: completedRounds
                        .SelectMany(r => r.Answers)
                        .Where(a => a.TeamId == t.Id)
                        .Sum(a => a.Value.GetScore())))
                .ToArray();

            var totalRanks = CalculateRanks(totalScores);

            var isFirstRound = rounds[0].Id == round.Id;

            // 3. Question slides
            for (var qi = 0; qi < round.QuestionCount; qi++)
            {
                var slidePart = FindSlide(slides, $"Answer{qi + 1}");

                if (slidePart != null)
                {
                    var questionAnswers = round.Answers.Where(a => a.QuestionIndex == qi).ToArray();
                    var correctCount = questionAnswers.Count(a => a.Value.GetScore() > 0);
                    var totalCount = questionAnswers.Length;

                    var pointsText = FormatQuestionCorrectText(correctCount, totalCount);

                    SetShapeVisibility(slidePart, "Points");
                    SetShapeText(slidePart, "Points", pointsText);
                }
            }

            // 4. Round standings slides
            if (roundRanks.Length > 0 && !(isFirstRound && isFinalRound))
            {
                var placesEntries = roundRanks.Where(e => e.Rank > 1).ToArray();
                var roundPlacesPart = FindSlide(slides, "RoundPlaces");

                if (placesEntries.Length > 0 && roundPlacesPart != null)
                {
                    var avg = roundScores.Length > 0 ? roundScores.Average(x => x.Score) : 0m;
                    FillPlacesSlide(roundPlacesPart, placesEntries, avg);
                    SetSlideVisibility(roundPlacesPart, true);
                }

                var roundFirstPart = FindSlide(slides, "RoundFirst");
                if (roundFirstPart != null)
                {
                    var firstTeams = roundRanks.Where(e => e.Rank == 1).Select(e => e.Team).ToArray();
                    var firstScore = roundRanks.First(e => e.Rank == 1).Score;

                    FillWinnersSlide(roundFirstPart, firstTeams, firstScore);
                    SetSlideVisibility(roundFirstPart, true);
                }
            }

            // 5. Total standings and podium slides
            if (totalRanks.Length > 0)
            {
                if (!isFirstRound || isFinalRound)
                {
                    var totalPlacesPart = FindSlide(slides, "AllPlacings");
                    var placingEntries = totalRanks.Where(e => !isFinalRound || e.Rank >= 4).ToArray();

                    if (placingEntries.Length > 0 && totalPlacesPart != null)
                    {
                        var avg = totalScores.Length > 0 ? totalScores.Average(x => x.Score) : 0m;
                        FillPlacesSlide(totalPlacesPart, placingEntries, avg);
                        SetSlideVisibility(totalPlacesPart, true);
                    }
                }

                if (isFinalRound)
                {
                    FillPodiumSlide(slides, "AllThird", totalRanks, 3);
                    FillPodiumSlide(slides, "AllSecond", totalRanks, 2);
                    FillPodiumSlide(slides, "AllFirst", totalRanks, 1);

                    var goodByePart = FindSlide(slides, "GoodBye");
                    if (goodByePart != null)
                    {
                        SetSlideVisibility(goodByePart, true);
                    }
                }
            }

            presoPart.Presentation!.Save();

            return format;
        }

        #endregion Public Methods

        #region Private Methods

        private static RankedTeam[] CalculateRanks((Team Team, decimal Score)[] scores)
        {
            // Pre-sorted by name, so teams with equal scores appear alphabetically
            return [.. CompetitionRanking.Rank(scores.OrderBy(x => x.Team.Name), x => x.Score)
                .Select(r => new RankedTeam(r.Item.Team, r.Item.Score, r.Rank))];
        }

        private static void FillPlacesSlide(SlidePart sp, RankedTeam[] entries, decimal average)
        {
            if (entries.Length == 0) return;

            var positions = string.Join("\n", entries.Select(e => $"{e.Rank}."));
            SetShapeText(sp, "Positions", positions);

            var teams = string.Join("\n", entries.Select(e => e.Team.Name));
            SetShapeText(sp, "Teams", teams);

            var points = string.Join("\n", entries.Select(e => FormatScore(e.Score)));
            SetShapeText(sp, "Points", points);

            var avgText = FormatAverage(average);
            SetShapeText(sp, "Average", avgText);
        }

        private static void FillPodiumSlide(SlidePart[] slides, string slideName, RankedTeam[] ranks, int rank)
        {
            var slidePart = FindSlide(slides, slideName);
            if (slidePart == null || !ranks.Any(e => e.Rank == rank)) return;

            var teams = ranks.Where(e => e.Rank == rank).Select(e => e.Team).ToArray();
            var score = ranks.First(e => e.Rank == rank).Score;

            FillWinnersSlide(slidePart, teams, score);
            SetSlideVisibility(slidePart, true);
        }

        private static void FillWinnersSlide(SlidePart sp, Team[] teams, decimal score)
        {
            for (var index = 1; index <= 5; index++)
            {
                var name = index <= teams.Length ? teams[index - 1].Name : string.Empty;
                SetShapeText(sp, $"Team{index}", name);
            }

            SetShapeText(sp, "Points", FormatScore(score));
        }

        /// <summary>
        /// Finds a slide by its internal name (cSld/@name, only settable via VBA)
        /// or by a shape named "#&lt;name&gt;" (settable in the selection pane).
        /// </summary>
        private static SlidePart? FindSlide(SlidePart[] slides, string name)
        {
            var markerName = SlideMarkerPrefix + name;

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
            if (correct == total) return "Alle Teams richtig";
            if (correct == 1) return "1 Team richtig";
            if (correct == 0) return "Kein Team richtig";
            return $"{correct} Teams richtig";
        }

        private static string FormatScore(decimal score)
        {
            var rounded = Math.Round(score, 1);
            if (rounded == 1m) return "1 Punkt";

            var formatted = rounded.ToString("0.#", slideCulture);
            return $"{formatted} Punkte";
        }

        /// <summary>
        /// Returns the slides belonging to the round. Decks with sections must contain a section
        /// named like the round, decks without sections are treated as a single round.
        /// </summary>
        private static SlidePart[] GetRoundSlides(PresentationPart presoPart, string roundName)
        {
            var presentation = presoPart.Presentation
                ?? throw new InvalidOperationException("Presentation part is empty.");

            var slideIds = presentation.SlideIdList?.Elements<SlideId>().ToArray() ?? [];
            var sections = presentation.Descendants<P14.Section>().ToArray();

            if (sections.Length > 0)
            {
                var section = sections.FirstOrDefault(s => string.Equals(
                        s.Name?.Value?.Trim(), roundName.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"No section named '{roundName}' found in the presentation.");

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

        /// <summary>
        /// Keeps presentations and slideshows as they are, templates are converted to presentations.
        /// </summary>
        private static PresentationFormat PrepareDocumentType(PresentationDocument doc)
        {
            switch (doc.DocumentType)
            {
                case PresentationDocumentType.Presentation:
                    return new PresentationFormat(".pptx", PresentationContentType);

                case PresentationDocumentType.Slideshow:
                    return new PresentationFormat(".ppsx", SlideshowContentType);

                case PresentationDocumentType.Template:
                    doc.ChangeDocumentType(PresentationDocumentType.Presentation);
                    return new PresentationFormat(".pptx", PresentationContentType);

                default:
                    throw new NotSupportedException($"Document type '{doc.DocumentType}' is not supported.");
            }
        }

        private static void SetShapeText(SlidePart sp, string shapeName, string text)
        {
            var shape = sp.Slide?.Descendants<Shape>()
                .FirstOrDefault(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value == shapeName);

            if (shape?.TextBody == null) return;

            var txBody = shape.TextBody;
            var firstPara = txBody.Elements<Drawing.Paragraph>().FirstOrDefault();
            var firstRun = firstPara?.Elements<Drawing.Run>().FirstOrDefault();

            txBody.RemoveAllChildren<Drawing.Paragraph>();

            foreach (var line in text.Split('\n'))
            {
                var para = new Drawing.Paragraph();
                if (firstPara?.ParagraphProperties?.CloneNode(true) is Drawing.ParagraphProperties pPr)
                {
                    para.Append(pPr);
                }

                var run = new Drawing.Run();
                if (firstRun?.RunProperties?.CloneNode(true) is Drawing.RunProperties rPr)
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

        private static void SetShapeVisibility(SlidePart sp, string prefix)
        {
            var shapes = sp.Slide?.Descendants<Shape>()
                .Where(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.StartsWith(prefix) == true)
                .ToArray() ?? [];

            foreach (var shape in shapes)
            {
                if (shape.NonVisualShapeProperties?.NonVisualDrawingProperties != null)
                {
                    shape.NonVisualShapeProperties.NonVisualDrawingProperties.Hidden = null;
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
