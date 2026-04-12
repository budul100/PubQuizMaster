using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Participants;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace PubQuizMaster.Core.Services
{
    public class ExportService
    {
        #region Public Methods

        public static async Task<string> ExportAsync(DataService dataService, Guid roundId,
            bool isFinalRound, string templatePath)
        {
            var outputPath = templatePath.GetOutputPath();

            File.Copy(
                sourceFileName: templatePath,
                destFileName: outputPath,
                overwrite: true);

            using (var doc = PresentationDocument.Open(outputPath, isEditable: true))
            {
                var presoPart = doc.PresentationPart
                    ?? throw new InvalidOperationException("Template has no presentation part.");

                var slides = presoPart.Presentation!
                    .SlideIdList!.Elements<SlideId>()
                    .Select(sid => presoPart.GetPartById(sid.RelationshipId!) as SlidePart)
                    .Where(sp => sp != null)
                    .Select(sp => sp!).ToArray();

                var round = dataService.QuizNight.Rounds.FirstOrDefault(r => r.Id == roundId)
                    ?? throw new InvalidOperationException("Active round not found.");

                var allTeams = dataService.QuizNight.MasterTeamList;
                var roundLeaders = dataService.GetLeaderboardsUpTo(round.Id);

                var roundScores = allTeams
                    .Where(t => round.ActiveTeamIds.Contains(t.Id))
                    .Select(t => (t, Score: round.GetTeamScore(t.Id) ?? 0m)).ToArray();
                var roundRanks = roundScores.GetRanks();

                var totalScores = roundLeaders
                    .Select(e => (e.Team, Score: e.TotalScore)).ToArray();
                var totalRanks = totalScores.GetRanks();

                var isFirstRound = dataService.QuizNight.Rounds.FirstOrDefault()?.Id == round.Id;

                for (int qi = 0; qi < round.QuestionCount && qi < 20; qi++)
                {
                    var slideName = $"Answer{qi + 1}";
                    var sp = slides.FindSlide(slideName);

                    if (sp is not null)
                    {
                        var pointsText = round.GetPointsText(qi);

                        SetShapeVisibility(
                            sp: sp,
                            prefix: "Points");

                        SetShapeText(
                            sp: sp,
                            shapeName: "Points",
                            text: pointsText);
                    }
                }

                if (roundRanks.Any()
                    && !(isFirstRound && isFinalRound))
                {
                    var entries = roundRanks.Where(e => e.Rank > 1).ToList();

                    var roundPlacesSp = slides.FindSlide("RoundPlaces");

                    if (entries.Count > 0
                        && roundPlacesSp is not null)
                    {
                        var average = roundScores.Length > 0
                            ? roundScores.Average(e => e.Score)
                            : 0m;

                        FillPlacesSlide(
                            sp: roundPlacesSp,
                            entries: entries,
                            average: average);

                        SetSlideVisible(
                            sp: roundPlacesSp,
                            visible: true);
                    }

                    var roundFirstSp = slides.FindSlide("RoundFirst");

                    if (roundFirstSp is not null)
                    {
                        var teams = roundRanks.GetAtRank(1).ToArray();
                        var score = roundRanks.First(e => e.Rank == 1).Score;

                        FillWinnersSlide(
                            sp: roundFirstSp,
                            teams: teams,
                            score: score);

                        SetSlideVisible(roundFirstSp, true);
                    }
                }

                if (totalRanks.Any())
                {
                    if (!isFirstRound || isFinalRound)
                    {
                        var entries = totalRanks
                            .Where(e => !isFinalRound || e.Rank >= 4).ToList();

                        var allPlacingsSp = slides.FindSlide("AllPlacings");

                        if (entries.Count > 0
                            && allPlacingsSp is not null)
                        {
                            var average = totalScores.Length > 0
                                ? totalScores.Average(e => e.Score)
                                : 0m;

                            FillPlacesSlide(
                                sp: allPlacingsSp,
                                entries: entries,
                                average: average);

                            SetSlideVisible(
                                sp: allPlacingsSp,
                                visible: true);
                        }
                    }

                    if (isFinalRound)
                    {
                        var allThirdSp = slides.FindSlide("AllThird");

                        if (totalRanks.Any(e => e.Rank == 3)
                            && allThirdSp is not null)
                        {
                            var teams = totalRanks.GetAtRank(3).ToArray();
                            var score = totalRanks.First(e => e.Rank == 3).Score;

                            FillWinnersSlide(
                                sp: allThirdSp,
                                teams: teams,
                                score: score);

                            SetSlideVisible(
                                sp: allThirdSp,
                                visible: true);
                        }

                        var allSecondSp = slides.FindSlide("AllSecond");

                        if (totalRanks.Any(e => e.Rank == 2)
                            && allSecondSp is not null)
                        {
                            var teams = totalRanks.GetAtRank(2).ToArray();
                            var score = totalRanks.First(e => e.Rank == 2).Score;

                            FillWinnersSlide(
                                sp: allSecondSp,
                                teams: teams,
                                score: score);

                            SetSlideVisible(
                                sp: allSecondSp,
                                visible: true);
                        }

                        var allFirstSp = slides.FindSlide("AllFirst");

                        if (totalRanks.Any(e => e.Rank == 1)
                            && allFirstSp is not null)
                        {
                            var teams = totalRanks.GetAtRank(1).ToArray();
                            var score = totalRanks.First(e => e.Rank == 1).Score;

                            FillWinnersSlide(
                                sp: allFirstSp,
                                teams: teams,
                                score: score);

                            SetSlideVisible(
                                sp: allFirstSp,
                                visible: true);
                        }

                        var goodByeSp = slides.FindSlide("GoodBye");

                        if (goodByeSp is not null)
                        {
                            SetSlideVisible(goodByeSp, true);
                        }
                    }
                }

                presoPart.Presentation.Save();
            }

            // Async wrapper: the actual work above is synchronous (OpenXml SDK),
            // but the public API is async to match Avalonia's async command pattern.
            await Task.CompletedTask;

            return outputPath;
        }

        #endregion Public Methods

        #region Private Methods

        private static void FillPlacesSlide(SlidePart sp, IEnumerable<RankedEntry> entries, decimal average)
        {
            if (!entries.Any()) return;

            var positions = string.Join("\n", entries.Select(e => $"{e.Rank}."));

            SetShapeText(
                sp: sp,
                shapeName: "Positions",
                text: positions);

            var teams = string.Join("\n", entries.Select(e => e.Team.Name));

            SetShapeText(
                sp: sp,
                shapeName: "Teams",
                text: teams);

            var points = string.Join("\n", entries.Select(e => e.Score.FormatScore()));

            SetShapeText(
                sp: sp,
                shapeName: "Points",
                text: points);

            var averageText = average.FormatAverage();

            SetShapeText(
                sp: sp,
                shapeName: "Average",
                text: averageText);
        }

        private static void FillWinnersSlide(SlidePart sp, Team[] teams, decimal score)
        {
            if (teams.Length > 5)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PptxExport] Warning: {teams.Length} teams at rank 1 — truncated to 5.");
            }

            for (var index = 1; index <= 5; index++)
            {
                var name = index <= teams.Length
                    ? teams.ElementAt(index - 1).Name
                    : string.Empty;

                SetShapeText(
                    sp: sp,
                    shapeName: $"Team{index}",
                    text: name);
            }

            SetShapeText(
                sp: sp,
                shapeName: "Points",
                text: score.FormatScore());
        }

        private static void SetShapeText(SlidePart sp, string shapeName, string text)
        {
            var shape = sp.Slide?.Descendants<Shape>()
                .FirstOrDefault(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value == shapeName);

            if (shape == default)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PptxExport] Shape not found: '{shapeName}' on slide '{sp.Slide?.CommonSlideData?.Name}'");

                return;
            }

            var txBody = shape.TextBody;

            if (txBody == default)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PptxExport] Shape has no text body: '{shapeName}' on slide '{sp.Slide?.CommonSlideData?.Name}'");

                return;
            }

            var firstPara = txBody.Elements<Drawing.Paragraph>().FirstOrDefault();
            var firstRun = firstPara?.Elements<Drawing.Run>().FirstOrDefault();

            foreach (var p in txBody.Elements<Drawing.Paragraph>().ToList())
            {
                p.Remove();
            }

            var lines = text.Split('\n');

            foreach (var line in lines)
            {
                var para = new Drawing.Paragraph();

                if (firstPara?.ParagraphProperties?.CloneNode(true) is Drawing.ParagraphProperties pPr)
                {
                    para.Append(pPr.CloneNode(true));
                }

                var run = new Drawing.Run();

                if (firstRun?.RunProperties?.CloneNode(true) is Drawing.RunProperties rPr)
                {
                    run.Append(rPr.CloneNode(true));
                }

                run.Append(new Drawing.Text(line));
                para.Append(run);

                txBody.Append(para);
            }

            // Ensure at least one paragraph (OOXML requirement)
            if (!txBody.Elements<Drawing.Paragraph>().Any())
            {
                txBody.Append(new Drawing.Paragraph());
            }
        }

        private static void SetShapeVisibility(SlidePart sp, string prefix)
        {
            var matchingShapes = sp.Slide?.Descendants<Shape>()
                .Where(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.StartsWith(prefix) == true
                    && s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Hidden is not null).ToArray();

            if (matchingShapes?.Length > 0)
            {
                foreach (var shape in matchingShapes)
                {
                    shape.NonVisualShapeProperties!.NonVisualDrawingProperties!.Hidden = default;
                }
            }

            var layoutShapes = sp.SlideLayoutPart?.SlideLayout?.Descendants<Shape>()
                .Where(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.StartsWith(prefix) == true
                    && s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Hidden is not null).ToArray();

            if (layoutShapes?.Length > 0)
            {
                foreach (var shape in layoutShapes)
                {
                    shape.NonVisualShapeProperties!.NonVisualDrawingProperties!.Hidden = default;
                }
            }
        }

        private static void SetSlideVisible(SlidePart sp, bool visible)
        {
            if (sp?.Slide is not null)
            {
                sp.Slide.Show = visible
                    ? new BooleanValue(true)
                    : new BooleanValue(false);

                sp.Slide.Save();
            }
        }

        #endregion Private Methods
    }
}