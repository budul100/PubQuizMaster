using System.Text.Json.Serialization;

namespace PubQuizMaster.Core.Models.Content
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(AnswerBool), "bool")]
    [JsonDerivedType(typeof(AnswerPoint), "point")]
    public abstract class AnswerBase
    {
        #region Public Methods

        public abstract decimal GetScore();

        #endregion Public Methods
    }
}