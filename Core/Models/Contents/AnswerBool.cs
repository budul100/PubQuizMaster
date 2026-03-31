namespace PubQuizMaster.Core.Models.Contents
{
    /// <summary>Boolean answer: correct = 1 point, incorrect = 0 points.</summary>
    public class AnswerBool 
        : AnswerBase
    {
        public bool Correct { get; set; }
        public override decimal GetScore() => Correct ? 1m : 0m;
    }
}