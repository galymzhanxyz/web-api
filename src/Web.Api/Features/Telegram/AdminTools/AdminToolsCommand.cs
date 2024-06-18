using Telegram.Bot;
using Telegram.Bot.Types;

namespace TechInterviewer.Features.Telegram.AdminTools
{
    public class AdminToolsCommand : MediatR.IRequest<string>
    {
        public AdminToolsCommand(
            ITelegramBotClient client,
            Update updateRequest)
        {
            BotClient = client;
            UpdateRequest = updateRequest;
        }

        public ITelegramBotClient BotClient { get; }

        public Update UpdateRequest { get; }
    }
}
