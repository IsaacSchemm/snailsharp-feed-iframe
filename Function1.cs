using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net;
using CodeHollow.FeedReader;
using System.Linq;
using System.Net.Http;
using System;
using System.Text;

namespace snailsharp_embedded_feed
{
    public static class Function1
    {
        [FunctionName("feed")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            Uri getUri()
            {
                foreach (string str in req.Query["url"])
                    if (Uri.TryCreate(str, UriKind.Absolute, out Uri uri))
                        return uri;

                return new Uri("https://snailsharp.dreamwidth.org/data/rss");
            }

            int getCount()
            {
                foreach (string str in req.Query["count"])
                    if (int.TryParse(str, out int i))
                        return i;

                return 5;
            }

            using var client = new HttpClient();
            string str = await client.GetStringAsync(getUri());

            var feed = FeedReader.ReadFromString(str);

            using var sw = new StringWriter();

            string enc(string x) => WebUtility.HtmlEncode(x);

            IEnumerable<string> build()
            {
                yield return $$"""
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>{{enc(feed.Title)}}</title>
                        <style type="text/css">
                            body {
                                background-color: Window;
                                color: WindowText;
                                font-family: sans-serif;
                            }
                            p {
                                margin: 0;
                            }
                            img {
                                width: 100px;
                                height: 100px;
                                object-fit: contain;
                                border-radius: .5rem;
                                margin: 0 1em 0 0;
                            }
                            a {
                                color: inherit;
                            }
                            a:not(:hover):not(:active) {
                                text-decoration: none;
                            }
                            .entry {
                                display: block;
                                margin: 1em 0;
                                padding: .5rem;
                                border-radius: .5rem;
                                background-color: rgba(128, 128, 128, .2);
                            }
                            .tag {
                                display: inline-block;
                                background-color: #666;
                                color: white;
                                border-radius: .25rem;
                                margin: .25rem .25rem 0 0;
                                padding: 0 .25rem;
                                font-size: 75%;
                            }
                            .datetime {
                                text-align: right;
                                margin: 0 0 .5rem;
                                font-size: 75%;
                            }
                            @media (prefers-color-scheme: dark) {
                                body {
                                    background-color: #242424;
                                    color: white;
                                }
                            }
                            @media (forced-colors: active) {
                                .entry {
                                    border: 1px solid black;
                                }
                            }
                        </style>
                    </head>
                    <body style="font-family: sans-serif">
                        <a class="entry" href="{{enc(feed.Link)}}" target="_top">
                    """;
                if (feed.ImageUrl != null)
                {
                    yield return $"""
                            <img src="{enc(feed.ImageUrl)}" alt="" align="left" />
                            """;
                }
                yield return $"""
                            <h1>{enc(feed.Title)}</h1>
                            <div style="clear: both" role="none"></div>
                        </a>
                    """;
                bool recentlyUpdated = feed.Items.Any(x => x.PublishingDate is DateTime dt && dt > DateTime.UtcNow.AddMonths(-3));
                foreach (var item in feed.Items.Take(getCount()))
                {
                    yield return $"""<a class="entry" href="{enc(item.Link)}" target="_top">""";
                    if (recentlyUpdated)
                        yield return $"""<div class="datetime">{enc(item.PublishingDate?.ToString("MMMM d, yyyy") ?? item.PublishingDateString)}</div>""";
                    if (item.Title is string title)
                        yield return $"<div>{enc(item.Title)}</div>";
                    else
                        yield return $"<div>{item.Description}</div>";
                    foreach (var category in item.Categories)
                        yield return $"""
                            <span class="tag" aria-label="Tag">{enc(category)}</span>
                            """;
                    yield return $"""</a>""";
                }
                if (feed.Items.Count > getCount())
                    yield return $"""
                        <a class="entry" href="{enc(feed.Link)}" target="_top">
                            <center>Read more...</center>
                        </a>
                        """;
                yield return $"""
                        <p align="center" style="font-size: 75%">
                            <a href="https://github.com/IsaacSchemm/snailsharp-feed-iframe/blob/main/Function1.cs" target="_top">src</a>
                        </p>
                    </body>
                    </html>
                    """;
            }

            return new FileContentResult(
                Encoding.UTF8.GetBytes(string.Join("", build())),
                "text/html; charset=utf-8");
        }
    }
}
