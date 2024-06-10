using System.Security.Cryptography;
using System.Text;
using HtmlAgilityPack;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

var botClient = new TelegramBotClient(Environment.GetEnvironmentVariable("TOKEN") ?? args[0]);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("User-Agent",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: new ReceiverOptions()
    {
        AllowedUpdates = new[]
        {
            UpdateType.Message, UpdateType.CallbackQuery
        }
    });
Console.WriteLine($"[{DateTime.Now}] {botClient.GetMeAsync().Result.Username} started!");
await Task.Delay(Timeout.Infinite);

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    await (update.Type switch
    {
        UpdateType.EditedMessage => Task.CompletedTask,
        UpdateType.Message => GetUserInput(),
        UpdateType.CallbackQuery=> update.CallbackQuery.Data.StartsWith("download-") ? SendTorrentFile() : GetFilmFromUser(),
        _ => throw new Exception()
    });
    
    async Task GetUserInput()
    {
        if (update.Message.Text == "/start")
        {
            await botClient.SendTextMessageAsync(update.Message.Chat.Id,
                "Напиши мне название фильма и я его поищу!");
            return;
        }

        string filmName = update.Message.Text;
        var foundFilms = GetFilms(filmName);
        if (foundFilms.Count == 0)
        {
            await botClient.SendTextMessageAsync(update.Message.Chat.Id, "Ничего не найдено :(");
            return;
        }

        var markupButtons = new List<InlineKeyboardButton[]>();
        for (int i = 0; i < foundFilms.Count; i++)
        {
            markupButtons.Add(new[]
                { InlineKeyboardButton.WithCallbackData(foundFilms[i].Item1, foundFilms[i].Item2) });
        }

        await botClient.SendTextMessageAsync(update.Message.Chat.Id, "Нашёл для тебя несколько фильмов...",
            replyMarkup: new InlineKeyboardMarkup(markupButtons.Take(10)));

        Console.WriteLine(
            $"[{DateTime.Now}] Search \"{update.Message.Text}\" from {update.Message.From.Username} ({update.Message.From.Id})");
    }

    async Task GetFilmFromUser()
    {
        FilmMetadata filmMetadata;
        try
        {
            filmMetadata = GetFilmMetadata(update.CallbackQuery.Data);
        }
        catch (Exception)
        {
            await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                "Поиск недоступен. Попробуйте позже");
            return;
        }

        var downloadButton =
            InlineKeyboardButton.WithCallbackData("Скачать!", $"download-{update.CallbackQuery.Data}");
        await botClient.SendPhotoAsync(update.CallbackQuery.Message.Chat.Id,
            InputFile.FromUri(filmMetadata.ImageUri),
            replyMarkup: new InlineKeyboardMarkup(downloadButton),
            caption: filmMetadata.GetBeautifulCaption(filmMetadata));
    }
    
    async Task SendTorrentFile()
    {
        FilmMetadata filmMetadata;
        try
        {
            filmMetadata = GetFilmMetadata(update.CallbackQuery.Data.Remove(0, "download-".Length));
        }
        catch (Exception)
        {
            await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                "Поиск недоступен. Попробуйте позже");
            return;
        }
        
        var fileStream = await GetFileStreamAsync(update.CallbackQuery.Data.Remove(0, "download-".Length));
        await botClient.SendDocumentAsync(update.CallbackQuery.Message.Chat.Id,
            InputFile.FromStream(fileStream, filmMetadata.OriginalName + ".torrent"));
    }
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var errorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(errorMessage);
    return Task.CompletedTask;
}

List<(string, string)> GetFilms(string search)
{
    var address = "https://kinozal.tv/browse.php?s=" + search;
    var pageContent = httpClient.GetStringAsync(address).Result;

    var html = new HtmlDocument();
    html.LoadHtml(pageContent);

    var foundFilms = new List<(string, string)>();

    var filmsHtmlTable = html.DocumentNode.SelectNodes("//*[@id=\"main\"]/div[2]/div[2]/table");

    if (filmsHtmlTable == null)
        return foundFilms;

    foreach (var film in filmsHtmlTable.Where(node => node is HtmlNode))
    {
        for (int i = 2; i < film.ChildNodes.Count; i += 2)
        {
            foundFilms.Add((
                film.ChildNodes[i].ChildNodes[1].ChildNodes[0].InnerText, // Title
                "https://kinozal.tv" +
                film.ChildNodes[i].ChildNodes[1].ChildNodes[0].Attributes["href"].Value)); // Page uri
        }
    }

    return foundFilms;
}

FilmMetadata GetFilmMetadata(string uri)
{
    string pageContent = string.Empty;
    pageContent = httpClient.GetStringAsync(uri).Result;

    if (string.IsNullOrEmpty(pageContent))
    {
        return null;
    }

    var html = new HtmlDocument();
    html.LoadHtml(pageContent);

    return new FilmMetadata(html.DocumentNode);
}

async Task<Stream> GetFileStreamAsync(string filmUri)
{
    var uri = new Uri(filmUri.Replace("kinozal.tv/details.php?", "dl.kinozal.tv/download.php?"));
    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
    httpRequestMessage.Headers.Add("Cookie", "pass=75PCylKTim; uid=20597582;");

    return await (await httpClient.SendAsync(httpRequestMessage)).Content.ReadAsStreamAsync();
}
