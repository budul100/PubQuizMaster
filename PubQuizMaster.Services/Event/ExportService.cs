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
        private const string QuestionShapeName = "Question";
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

            var participantLookup = quizNight.ParticipatingTeams.ToDictionary(p => p.TeamId);
            var allTeams = quizNight.ParticipatingTeams.Select(pt => pt.Team).ToArray();
            var roundTeamIds = round.GetTeamIds();

            // 1. Calculate scores and rankings for current round
            var roundScores = allTeams
                .Where(t => roundTeamIds.Contains(t.Id))
                .Select(t => (
                    Team: t,
                    Score: round.Answers.Where(a => a.TeamId == t.Id).Sum(a => a.Value.GetScore()),
                    IsAK: participantLookup.TryGetValue(t.Id, out var p) && p.IsNonCompetitive))
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
                        .Sum(a => a.Value.GetScore()),
                    IsAK: participantLookup.TryGetValue(t.Id, out var p) && p.IsNonCompetitive))
                .ToArray();

            var totalRanks = CalculateRanks(totalScores);

            var isFirstRound = rounds[0].Id == round.Id;

            // 3. Question slides: "x correct" counts against the round teams, not the recorded rows.
            //    Explicit slide names win, otherwise the questions map to the Question slides in deck order.
            var questionSlides = GetQuestionSlides(slides);

            for (var qi = 0; qi < round.QuestionCount; qi++)
            {
                var slidePart = FindSlide(slides, $"Answer{qi + 1}")
                    ?? (qi < questionSlides.Length ? questionSlides[qi] : null);

                if (slidePart != null)
                {
                    var questionAnswers = round.Answers.Where(a => a.QuestionIndex == qi).ToArray();
                    var correctCount = questionAnswers.Count(a => a.Value.GetScore() > 0);
                    var totalCount = roundTeamIds.Length;

                    var pointsText = FormatQuestionCorrectText(correctCount, totalCount);

                    SetShapeVisibility(slidePart, "Points");
                    SetShapeText(slidePart, "Points", pointsText);
                }
            }

            // 4. Round standings slides
            var roundPlacesPart = FindSlide(slides, "RoundPlaces");
            var roundFirstPart = FindSlide(slides, "RoundFirst");

            if (roundRanks.Length > 0)
            {
                var placesEntries = roundRanks.Where(e => e.Rank > 1).ToArray();

                if (placesEntries.Length > 0 && roundPlacesPart != null)
                {
                    var avg = roundScores.Length > 0 ? roundScores.Average(x => x.Score) : 0m;
                    FillPlacesSlide(roundPlacesPart, placesEntries, avg);
                    SetSlideVisibility(roundPlacesPart, true);
                }
                else if (roundPlacesPart != null)
                {
                    SetSlideVisibility(roundPlacesPart, false);
                }

                if (roundFirstPart != null)
                {
                    var firstEntries = roundRanks.Where(e => e.Rank == 1).ToArray();
                    // Regular winners first, non-competitive winners at the end
                    var firstTeams = firstEntries
                        .OrderBy(e => participantLookup.TryGetValue(e.Team.Id, out var p) && p.IsNonCompetitive ? 1 : 0)
                        .Select(e => e.Team)
                        .ToArray();
                    var firstScore = firstEntries.Length > 0 ? firstEntries[0].Score : 0m;

                    FillWinnersSlide(roundFirstPart, firstTeams, firstScore);
                    SetSlideVisibility(roundFirstPart, true);
                }
            }
            else
            {
                if (roundPlacesPart != null) SetSlideVisibility(roundPlacesPart, false);
                if (roundFirstPart != null) SetSlideVisibility(roundFirstPart, false);
            }

            // 5. Total standings and podium slides
            var totalPlacesPart = FindSlide(slides, "AllPlacings");
            var goodByePart = FindSlide(slides, "GoodBye");

            if (totalRanks.Length > 0)
            {
                if (!isFirstRound || isFinalRound)
                {
                    var placingEntries = totalRanks.Where(e => !isFinalRound || e.Rank >= 4).ToArray();

                    if (placingEntries.Length > 0 && totalPlacesPart != null)
                    {
                        var avg = totalScores.Length > 0 ? totalScores.Average(x => x.Score) : 0m;
                        FillPlacesSlide(totalPlacesPart, placingEntries, avg);
                        SetSlideVisibility(totalPlacesPart, true);
                    }
                    else if (totalPlacesPart != null)
                    {
                        SetSlideVisibility(totalPlacesPart, false);
                    }
                }
                else if (totalPlacesPart != null)
                {
                    SetSlideVisibility(totalPlacesPart, false);
                }

                if (isFinalRound)
                {
                    FillPodiumSlide(slides, "AllThird", totalRanks, participantLookup, 3);
                    FillPodiumSlide(slides, "AllSecond", totalRanks, participantLookup, 2);
                    FillPodiumSlide(slides, "AllFirst", totalRanks, participantLookup, 1);

                    if (goodByePart != null)
                    {
                        SetSlideVisibility(goodByePart, true);
                    }
                }
                else
                {
                    HideSlide(slides, "AllThird");
                    HideSlide(slides, "AllSecond");
                    HideSlide(slides, "AllFirst");
                    if (goodByePart != null) SetSlideVisibility(goodByePart, false);
                }
            }
            else
            {
                if (totalPlacesPart != null) SetSlideVisibility(totalPlacesPart, false);
                HideSlide(slides, "AllThird");
                HideSlide(slides, "AllSecond");
                HideSlide(slides, "AllFirst");
                if (goodByePart != null) SetSlideVisibility(goodByePart, false);
            }

            presoPart.Presentation!.Save();

            return format;
        }

        #endregion Public Methods

        #region Private Methods

        private static RankedTeam[] CalculateRanks((Team Team, decimal Score, bool IsAK)[] scores)
        {
            var ordered = scores.OrderBy(x => x.Team.Name).ToArray();
            return [.. CompetitionRanking.Rank(ordered, x => x.Score, x => x.IsAK)
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

        private static void FillPodiumSlide(SlidePart[] slides, string slideName, RankedTeam[] ranks,
            Dictionary<Guid, Participant> participantLookup, int rank)
        {
            var slidePart = FindSlide(slides, slideName);
            if (slidePart == null) return;

            var rankEntries = ranks.Where(e => e.Rank == rank).ToArray();
            if (rankEntries.Length == 0)
            {
                SetSlideVisibility(slidePart, false);
                return;
            }

            var teams = rankEntries
                .OrderBy(e => participantLookup.TryGetValue(e.Team.Id, out var p) && p.IsNonCompetitive ? 1 : 0)
                .Select(e => e.Team)
                .ToArray();
            var score = rankEntries[0].Score;

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

        /// <summary>
        /// Question slides in deck order, recognized by their Question shape.
        /// Standings and podium slides never carry one, so the two sets cannot overlap.
        /// </summary>
        private static SlidePart[] GetQuestionSlides(SlidePart[] slides)
        {
            return [.. slides.Where(sp => HasShape(sp, QuestionShapeName))];
        }

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

        private static bool HasShape(SlidePart sp, string shapeName)
        {
            return sp.Slide?.Descendants<Shape>()
                .Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value == shapeName) == true;
        }

        private static void HideSlide(SlidePart[] slides, string name)
        {
            var slidePart = FindSlide(slides, name);
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