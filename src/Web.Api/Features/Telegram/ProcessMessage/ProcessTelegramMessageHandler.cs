using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities.Telegram;
using Infrastructure.Currencies.Contracts;
using Infrastructure.Database;
using Infrastructure.Salaries;
using Infrastructure.Services.Global;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TechInterviewer.Features.Telegram.ProcessMessage.InlineQuery;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Web.Api.Features.Telegram.GetTelegramBotUsages;
using Web.Api.Features.Telegram.ProcessMessage.UserCommands;
using Web.Api.Features.Telegram.ReplyWithSalaries;

namespace Web.Api.Features.Telegram.ProcessMessage;

public class ProcessTelegramMessageHandler : IRequestHandler<ProcessTelegramMessageCommand, string>
{
    public const string ApplicationName = "techinterview.space/salaries";

    private const string CacheKey = "TelegramBotService_ReplyData";

    private const int CachingMinutes = 20;

    private readonly ILogger<ProcessTelegramMessageHandler> _logger;
    private readonly IMediator _mediator;
    private readonly ICurrencyService _currencyService;
    private readonly DatabaseContext _context;
    private readonly IMemoryCache _cache;
    private readonly IGlobal _global;

    public ProcessTelegramMessageHandler(
        ILogger<ProcessTelegramMessageHandler> logger,
        IMediator mediator,
        ICurrencyService currencyService,
        DatabaseContext context,
        IMemoryCache cache,
        IGlobal global)
    {
        _logger = logger;
        _mediator = mediator;
        _currencyService = currencyService;
        _context = context;
        _cache = cache;
        _global = global;
    }

    private async Task<User> GetBotUserName(
        ITelegramBotClient telegramBotClient)
    {
        return await _cache.GetOrCreateAsync(
            CacheKey + "_BotUserName",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30);
                return await telegramBotClient.GetMeAsync();
            });
    }

    public async Task<string> Handle(
        ProcessTelegramMessageCommand request,
        CancellationToken cancellationToken)
    {
        var allProfessions = await _cache.GetOrCreateAsync(
            CacheKey + "_AllProfessions",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(120);
                return await _context
                    .Professions
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
            });

        if (request.UpdateRequest.Type == UpdateType.InlineQuery &&
            request.UpdateRequest.InlineQuery != null)
        {
            await _mediator.Send(
                    new ProcessInlineQueryCommand(request.BotClient, request.UpdateRequest, allProfessions),
                    cancellationToken);

            return null;
        }

        if (request.UpdateRequest.Message == null)
        {
            return null;
        }

        var message = request.UpdateRequest.Message;
        var messageText = message.Text ?? string.Empty;

        var botUser = await GetBotUserName(request.BotClient);
        var mentionedInGroupChat =
            message.Entities?.Length > 0 &&
            message.Entities[0].Type == MessageEntityType.Mention &&
            botUser.Username != null &&
            messageText.StartsWith(botUser.Username);

        var privateMessage = message.Chat.Type == ChatType.Private;
        TelegramBotReplyData replyData = null;
        if (mentionedInGroupChat || privateMessage)
        {
            if (privateMessage && messageText.Equals("/start", StringComparison.InvariantCultureIgnoreCase))
            {
                var startReplyData = new TelegramBotStartCommandReplyData(new SalariesChartPageLink(_global, null));
                await request.BotClient.SendTextMessageAsync(
                    message.Chat.Id,
                    startReplyData.ReplyText,
                    parseMode: startReplyData.ParseMode,
                    replyMarkup: startReplyData.InlineKeyboardMarkup,
                    cancellationToken: cancellationToken);

                return startReplyData.ReplyText;
            }

            var parameters = TelegramBotUserCommandParameters.CreateFromMessage(
                messageText,
                allProfessions);

            replyData = await _cache.GetOrCreateAsync(
                CacheKey + "_" + parameters.GetKeyPostfix(),
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CachingMinutes);
                    return await _mediator.Send(
                                    new ReplyWithSalariesCommand(
                                            parameters,
                                            ApplicationName), cancellationToken);
                });

            var replyToMessageId = request.UpdateRequest.Message.ReplyToMessage?.MessageId ?? request.UpdateRequest.Message.MessageId;
            await request.BotClient.SendTextMessageAsync(
                request.UpdateRequest.Message!.Chat.Id,
                replyData.ReplyText,
                parseMode: replyData.ParseMode,
                replyMarkup: replyData.InlineKeyboardMarkup,
                replyToMessageId: replyToMessageId,
                cancellationToken: cancellationToken);

            if (message.From is not null)
            {
                var usageType = message.Chat.Type switch
                {
                    ChatType.Private => TelegramBotUsageType.DirectMessage,
                    ChatType.Sender => TelegramBotUsageType.DirectMessage,
                    ChatType.Group when mentionedInGroupChat => TelegramBotUsageType.GroupMention,
                    ChatType.Supergroup when mentionedInGroupChat => TelegramBotUsageType.SupergroupMention,
                    _ => TelegramBotUsageType.Undefined,
                };

                await _mediator.Send(
                        new GetOrCreateTelegramBotUsageCommand(
                            message.From.Username ?? $"{message.From.FirstName} {message.From.LastName}".Trim(),
                            message.Chat.Title ?? message.Chat.Username ?? message.Chat.Id.ToString(),
                            messageText,
                            usageType),
                        cancellationToken);
            }
        }

        return replyData?.ReplyText;
    }
}