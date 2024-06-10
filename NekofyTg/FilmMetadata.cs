using System.Text;
using HtmlAgilityPack;

public class FilmMetadata
{
    public string ImageUri { get; }
    public string LocaleName { get; }
    public string OriginalName { get; }
    public string Year { get; }
    public string About { get; }
    public string Tech { get; }
    public string Rating { get; }

    public FilmMetadata(HtmlNode document)
    {
        ImageUri = new Func<string>(() =>
        {
            var path = document
                       .SelectSingleNode("//*[@class=\"men w200\"]/li[1]/a/img")
                       .Attributes["src"].Value;

            if (path.StartsWith('/'))
            {
                path = path.Insert(0, "https://kinozal.tv");
            }

            return path;
        }).Invoke();
        LocaleName = document.SelectSingleNode("//*[@class=\"bx1 justify\"]/h2/b[1]")?.NextSibling.InnerText ?? string.Empty;
        OriginalName = document.SelectSingleNode("//*[@class=\"bx1 justify\"]/h2/b[2]")?.NextSibling.InnerText ?? string.Empty;
        Year = document.SelectSingleNode("//*[@class=\"bx1 justify\"]/h2/b[3]")?.NextSibling.InnerText ?? string.Empty;
        About = document.SelectSingleNode("//*[@class=\"bx1 justify\"]/p[1]")?.InnerText + document
            .SelectSingleNode("//*[@class=\"bx1 justify\"]/p[1]")?.LastChild.InnerText
            .Replace("&quot;", "\"") + "\n";
        Tech = document.SelectSingleNode("//*[@id=\"tabs\"]").InnerText + "\n";
        Rating = new Func<string>(() =>
        {
            var node = document.SelectNodes("//a")
                               .Where(a => a.Attributes["href"].Value.Contains("imdb.com")) as List<HtmlNode>;

            return node == null || node.Count == 0 ? string.Empty : "Рейтинг: " + node.First().LastChild.InnerText;
        }).Invoke();
    }

    public string GetBeautifulCaption(FilmMetadata film)
    {
        var caption = new StringBuilder();
        if (!string.IsNullOrEmpty(film.LocaleName))
        {
            caption.Append(film.OriginalName);
        }
        else
        {
            caption.Append(film.LocaleName);
            if (!string.IsNullOrEmpty(film.OriginalName))
            {
                caption.Append($"({film.OriginalName})");
            }
        }

        if (!string.IsNullOrEmpty(film.Year))
        {
            caption.Append($" / {film.Year}\n");
        }

        caption.Append(Tech + "\n");

        int index = caption.Length;

        if (!string.IsNullOrEmpty(film.Rating))
        {
            caption.AppendLine($"{film.Rating}/10");
        }

        if (!string.IsNullOrEmpty(film.About) && caption.Length + film.About.Length <= 1024)
        {
            caption.Insert(index, film.About + "\n");
        }

        return caption.ToString();
    }
}
