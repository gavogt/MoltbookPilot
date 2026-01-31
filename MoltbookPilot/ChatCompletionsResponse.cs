
namespace MoltbookPilot
{
    public sealed class ChatCompletionsResponse
    {
        public Choice[]? choices { get; set; }

        public sealed class Choice
        {
            public Message? message { get; set; }
        }

        public sealed class Message
        {
            public string? content { get; set; }
        }
    }
}
