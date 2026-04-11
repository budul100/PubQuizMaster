using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;
using A = DocumentFormat.OpenXml.Drawing;

namespace PubQuizMaster.Core.Services
{
    public class ExportService
    {
        #region Private Fields

        private const int ScrollMsPerTeam = 800;

        #endregion Private Fields

        #region Public Methods

        public static async Task<string> ExportAsync(QuizNightService quizSvc, string templatePath, bool isFinalRound)
        {
            var outputPath = BuildOutputPath(templatePath);
            File.Copy(templatePath, outputPath, overwrite: true);

            using (var doc = PresentationDocument.Open(outputPath, isEditable: true))
            {
                var presoPart = doc.PresentationPart
                    ?? throw new InvalidOperationException("Template has no presentation part.");

                var slideOrder = GetOrderedSlideParts(presoPart);

                var round = quizSvc.QuizNight.Rounds.FirstOrDefault(r => r.Id == quizSvc.ActiveRoundId)
                    ?? throw new InvalidOperationException("Active round not found.");

                var isFirstRound = quizSvc.QuizNight.Rounds.FirstOrDefault()?.Id == round.Id;

                var masterTeams = quizSvc.QuizNight.MasterTeamList;
                var roundRanking = RankingService.RankRound(round, masterTeams);
                var totalLeaderboard = quizSvc.GetLeaderboard();
                var totalRanking = RankingService.RankTotal(totalLeaderboard);

                for (int qi = 0; qi < round.QuestionCount && qi < 20; qi++)
                {
                    var slideName = $"Answer{qi + 1}";
                    var sp = FindSlideByName(slideOrder, slideName);
                    if (sp == null) continue;

                    var pointsText = BuildAnswerPointsText(round, qi);
                    SetShapeText(sp, "Points", pointsText);
                }

                if (!isFirstRound || !isFinalRound)
                {
                    var roundPlacesSp = FindSlideByName(slideOrder, "RoundPlaces");
                    if (roundPlacesSp != null)
                    {
                        var entriesForPlaces = roundRanking.Where(e => e.Rank > 1).ToList();

                        if (entriesForPlaces.Any())
                        {
                            var avg = RankingService.FormatAverage(RankingService.ComputeRoundAverage(round, masterTeams));

                            FillPlacesSlide(roundPlacesSp, entriesForPlaces, avg);
                            // ScaleAnimationDuration(roundPlacesSp, entriesForPlaces.Count, ["Teams", "Points", "Positions"]);
                            SetSlideVisible(roundPlacesSp, true);
                        }
                    }

                    var roundFirstSp = FindSlideByName(slideOrder, "RoundFirst");
                    if (roundFirstSp != null && roundRanking.Any())
                    {
                        var winners = RankingService.GetTeamsAtRank(roundRanking, 1);
                        var winnerScore = roundRanking.First(e => e.Rank == 1).Score;
                        FillFirstSlide(roundFirstSp, winners, winnerScore);
                        SetSlideVisible(roundFirstSp, true);
                    }
                }

                if (!isFirstRound)
                {
                    var allPlacingsSp = FindSlideByName(slideOrder, "AllPlacings");
                    if (allPlacingsSp != null && totalRanking.Any())
                    {
                        var entriesForAll = isFinalRound
                            ? totalRanking.Where(e => e.Rank >= 4).ToList()
                            : totalRanking.ToList();

                        var avg = RankingService.FormatAverage(RankingService.ComputeTotalAverage(totalLeaderboard));

                        FillPlacesSlide(allPlacingsSp, entriesForAll, avg);
                        // ScaleAnimationDuration(allPlacingsSp, entriesForAll.Count, ["Teams", "Points", "Positions"]);
                        SetSlideVisible(allPlacingsSp, true);
                    }
                }

                if (isFinalRound)
                {
                    var allThirdSp = FindSlideByName(slideOrder, "AllThird");
                    if (allThirdSp != null && totalRanking.Any(e => e.Rank == 3))
                    {
                        var third = RankingService.GetTeamsAtRank(totalRanking, 3);
                        var score = totalRanking.First(e => e.Rank == 3).Score;
                        FillFirstSlide(allThirdSp, third, score);
                        SetSlideVisible(allThirdSp, true);
                    }

                    var allSecondSp = FindSlideByName(slideOrder, "AllSecond");
                    if (allSecondSp != null && totalRanking.Any(e => e.Rank == 2))
                    {
                        var second = RankingService.GetTeamsAtRank(totalRanking, 2);
                        var score = totalRanking.First(e => e.Rank == 2).Score;
                        FillFirstSlide(allSecondSp, second, score);
                        SetSlideVisible(allSecondSp, true);
                    }

                    var allFirstSp = FindSlideByName(slideOrder, "AllFirst");
                    if (allFirstSp != null && totalRanking.Any(e => e.Rank == 1))
                    {
                        var first = RankingService.GetTeamsAtRank(totalRanking, 1);
                        var score = totalRanking.First(e => e.Rank == 1).Score;
                        FillFirstSlide(allFirstSp, first, score);
                        SetSlideVisible(allFirstSp, true);
                    }

                    var goodByeSp = FindSlideByName(slideOrder, "GoodBye");
                    if (goodByeSp != null)
                    {
                        SetSlideVisible(goodByeSp, true);
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

        private static string BuildAnswerPointsText(Round round, int questionIndex)
        {
            var answers = round.Answers
                .Where(a => a.QuestionIndex == questionIndex)
                .ToList();

            if (!answers.Any()) return string.Empty;

            int total = answers.Count;
            int correct = answers.Count(a => a.Value.GetScore() > 0);

            if (correct == total) return "Alle Teams richtig";
            if (correct == 1) return "1 Team richtig";
            if (correct == 0) return "Kein Team richtig";

            return $"{correct} Teams richtig";
        }

        private static string BuildOutputPath(string templatePath)
        {
            var dir = Path.GetDirectoryName(templatePath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(templatePath);
            return Path.Combine(dir, $"{name}_completed.pptx");
        }

        private static void FillFirstSlide(SlidePart sp, List<Team> winners, decimal score)
        {
            // Up to 5 slots; extras are left empty
            // Log a warning if more than 5 (edge case: 5-way tie on rank 1)
            if (winners.Count > 5)
                System.Diagnostics.Debug.WriteLine(
                    $"[PptxExport] Warning: {winners.Count} teams at rank 1 — truncated to 5.");

            for (int i = 1; i <= 5; i++)
            {
                var name = i <= winners.Count ? winners[i - 1].Name : string.Empty;
                SetShapeText(sp, $"Team{i}", name);
            }

            SetShapeText(sp, "Points", RankingService.FormatScore(score));
        }

        private static void FillPlacesSlide(SlidePart sp, List<RankedEntry> entries, string avg)
        {
            if (!entries.Any()) return;

            var positions = string.Join("\n", entries.Select(e => $"{e.Rank}."));
            var teams = string.Join("\n", entries.Select(e => e.Team.Name));
            var points = string.Join("\n", entries.Select(e => RankingService.FormatScore(e.Score)));

            SetShapeText(sp, "Positions", positions);
            SetShapeText(sp, "Teams", teams);
            SetShapeText(sp, "Points", points);
            SetShapeText(sp, "Average", avg);
        }

        private static Shape? FindShapeByName(SlidePart sp, string shapeName) => sp.Slide?
            .Descendants<Shape>()
            .FirstOrDefault(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value == shapeName);

        private static SlidePart? FindSlideByName(List<SlidePart> slides, string name) => slides
            .FirstOrDefault(sp => sp.Slide.CommonSlideData?.Name?.Value == name);

        private static List<SlidePart> GetOrderedSlideParts(PresentationPart presoPart) => presoPart.Presentation
            .SlideIdList!.Elements<SlideId>()
            .Select(sid => presoPart.GetPartById(sid.RelationshipId!) as SlidePart)
            .Where(sp => sp != null)
            .Select(sp => sp!).ToList();

        private static void ScaleAnimationDuration(SlidePart sp, int teamCount, IEnumerable<string> scrollShapeNames)
        {
            if (teamCount <= 0) return;

            var targetDuration = teamCount * ScrollMsPerTeam;
            var timing = sp.Slide.Timing;
            if (timing == null) return;

            // Resolve shape names to IDs
            var nameSet = scrollShapeNames.ToHashSet();
            var scrollShapeIds = sp.Slide.CommonSlideData?.ShapeTree?
                .Descendants<NonVisualDrawingProperties>()
                .Where(p => nameSet.Contains(p.Name?.Value ?? ""))
                .Select(p => p.Id!.Value.ToString())
                .ToHashSet() ?? new HashSet<string>();

            if (scrollShapeIds.Count == 0) return;

            foreach (var anim in timing.Descendants<Animate>())
            {
                var cbhvr = anim.CommonBehavior;
                var spid = cbhvr?.TargetElement?.ShapeTarget?.ShapeId?.Value;
                if (spid == null || !scrollShapeIds.Contains(spid)) continue;

                var attrName = cbhvr?.AttributeNameList?
                    .Elements<AttributeName>()
                    .FirstOrDefault()?.Text;
                if (attrName != "ppt_x" && attrName != "ppt_y") continue;

                var ctn = cbhvr?.CommonTimeNode;
                if (ctn != null)
                    ctn.Duration = new StringValue(targetDuration.ToString());
            }

            sp.Slide.Save();
        }

        private static void SetShapeText(SlidePart sp, string shapeName, string text)
        {
            var shape = FindShapeByName(sp, shapeName);
            if (shape == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PptxExport] Shape not found: '{shapeName}' on slide '{sp.Slide.CommonSlideData?.Name}'");
                return;
            }

            var txBody = shape.TextBody;
            if (txBody == null) return;

            // Capture formatting from first paragraph / run before clearing
            var firstPara = txBody.Elements<A.Paragraph>().FirstOrDefault();
            var pPr = firstPara?.ParagraphProperties?.CloneNode(true) as A.ParagraphProperties;
            var firstRun = firstPara?.Elements<A.Run>().FirstOrDefault();
            var rPr = firstRun?.RunProperties?.CloneNode(true) as A.RunProperties;

            // Remove all existing paragraphs
            foreach (var p in txBody.Elements<A.Paragraph>().ToList())
                p.Remove();

            // Rebuild: one paragraph per line
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                var para = new A.Paragraph();

                if (pPr != null)
                    para.Append(pPr.CloneNode(true));

                var run = new A.Run();
                if (rPr != null)
                    run.Append(rPr.CloneNode(true));
                run.Append(new A.Text(line));
                para.Append(run);

                txBody.Append(para);
            }

            // Ensure at least one paragraph (OOXML requirement)
            if (!txBody.Elements<A.Paragraph>().Any())
                txBody.Append(new A.Paragraph());
        }

        private static void SetSlideVisible(SlidePart sp, bool visible)
        {
            sp.Slide.Show = visible
                ? new BooleanValue(true)
                : new BooleanValue(false);

            sp.Slide.Save();
        }

        #endregion Private Methods
    }
}