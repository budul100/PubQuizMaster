namespace PubQuizMaster.Core.Models.Contents
{
    /// <summary>Point-based answer for future use (e.g. partial credit).</summary>
    public class AnswerPoint 
        : AnswerBase
    {
        public decimal Points { get; set; }
        public override decimal GetScore() => Points;
    }
}