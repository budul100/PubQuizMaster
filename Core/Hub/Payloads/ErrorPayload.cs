using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PubQuizMaster.Core.Hub.Payloads
{

    public class ErrorPayload
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}