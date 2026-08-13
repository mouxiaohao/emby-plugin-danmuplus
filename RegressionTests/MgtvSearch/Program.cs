using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scrapers.Mgtv;
using Emby.Plugin.Danmu.Scrapers.Mgtv.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.MgtvSearchRegression
{
    internal static class Program
    {
        private static int Main()
        {
            HasExactlyOnePublicProductionConstructor();
            NormalizesCanonicalSuggestionFixtures();
            AcceptsNumericTypeAndAlternateSuggestionShapeThroughSearchAsync();
            UsesOnlyAnonymousSuggestRequestAndPositiveCache();
            CachesSuccessfulEmptyButNeverFailures();
            RetriesOnlyTransientTransportFailures();
            RejectsBusinessAndSchemaFailuresAndHonorsCancellation();
            PreservesUnknownTypesAndExistingConsumers();
            GuardsTheNarrowDiscoveryContract();
            Console.WriteLine("MGTV search regression checks passed.");
            return 0;
        }

        private static void HasExactlyOnePublicProductionConstructor()
        {
            Assert(typeof(Mgtv).GetConstructors(BindingFlags.Instance | BindingFlags.Public).Length == 1,
                "Mgtv must expose exactly one public production constructor for SimpleInjector activation");
        }

        private static void NormalizesCanonicalSuggestionFixtures()
        {
            var response = Deserialize(Success("[{\"cid\":\"00101\",\"title\":\"<em>First</em> &amp; Best\",\"type\":17,\"typeName\":\"tv\",\"year\":\"2020\"},{\"id\":102,\"showTitle\":\"Second\",\"type\":\"legacy-series\",\"typeName\":\"tv\",\"year\":\"1888\"},{\"url\":\"//www.mgtv.com/b/103/990.html\",\"hit\":\"<b>Third</b>\"},{\"url\":\"//www.mgtv.com/h/104.html\",\"hit\":\"<i>Fourth</i>\"},{\"video\":[{\"url\":\"https://pcweb.api.mgtv.com/b/105/77\"}],\"title\":\"Nested\"},{\"videoList\":[{\"url\":\"//pcweb.api.mgtv.com/b/106/88\"}],\"title\":\"Video list\"},{\"cid\":\"0\",\"title\":\"zero\"},{\"cid\":\"abc\",\"title\":\"bad\"},{\"cid\":\"123456789012345678901\",\"title\":\"long\"},{\"url\":\"https://mgtv.example.com/b/107/7\",\"title\":\"off-domain\"},{\"cid\":\"101\",\"title\":\"later duplicate\",\"year\":2021},{\"cid\":\"107\",\"title\":\"person\",\"type\":9,\"typeName\":\"person\"},{\"cid\":\"108\",\"title\":\"<em></em>\"}]"));
            var items = MgtvSuggestionNormalizer.Normalize(response);
            Assert(items.Select(item => item.Id).SequenceEqual(new[] { "101", "102", "103", "104", "105", "106" }),
                "cid/id/protocol-relative b/h/nested video/videoList URL fallbacks must retain first endpoint order and reject unsafe suggestions");
            Assert(items[0].Title == "First & Best" && items[0].Year == 2020 && items[1].Year == null,
                "titles must be plain and only reasonable four-digit years retained");
            Assert(items[0].VideoCount == 0 && items.All(item => item.Title.IndexOf('<') < 0),
                "suggestions must not invent episode counts or expose highlight markup");
            Assert(items[0].Year == 2020 && items[0].TypeName == "tv",
                "later duplicate metadata must not replace first candidate metadata");
        }

        private static void AcceptsNumericTypeAndAlternateSuggestionShapeThroughSearchAsync()
        {
            const string suggestions = "[{\"cid\":\"201\",\"title\":\"Numeric type\",\"type\":42,\"typeName\":\"tv\"},{\"videoList\":[{\"url\":\"//www.mgtv.com/h/202\"}],\"title\":\"Video list\",\"type\":7}]";
            var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, Success(suggestions)));
            var items = CreateApi(handler).SearchAsync("numeric-type", CancellationToken.None).GetAwaiter().GetResult();
            Assert(items.Select(item => item.Id).SequenceEqual(new[] { "201", "202" }) &&
                   items[0].TypeName == "tv" && handler.Requests.Count == 1,
                "numeric type and videoList payloads must be accepted by SearchAsync, not classified as malformed JSON");
        }

        private static void UsesOnlyAnonymousSuggestRequestAndPositiveCache()
        {
            var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, Success("[{\"cid\":\"9\",\"title\":\"Alpha\"}]")));
            var api = CreateApi(handler);
            var first = api.SearchAsync(" Alpha   Beta ", CancellationToken.None).GetAwaiter().GetResult();
            first[0].Title = "mutated";
            first.Clear();
            var second = api.SearchAsync("alpha beta", CancellationToken.None).GetAwaiter().GetResult();
            Assert(second.Count == 1 && second[0].Title == "Alpha" && handler.Requests.Count == 1,
                "normalized positive search must use its five-minute cache without exposing its mutable list or items");
            var request = handler.Requests.Single();
            Assert(request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/pc/suggest/v1" &&
                   request.RequestUri.Query == "?q=alpha+beta&src=mgtv",
                "discovery must use exactly encoded q and src=mgtv on the PC suggestion endpoint");
            Assert(!request.Headers.Contains("Cookie") && !request.Headers.Contains("Authorization") &&
                   !request.Headers.Contains("Origin") && !request.RequestUri.Query.Contains("did") &&
                   !request.RequestUri.Query.Contains("uuid") && !request.RequestUri.Query.Contains("token"),
                "discovery must not transmit cookies, authorization, origin, device, or token values");
            Assert(handler.Requests.All(item => item.RequestUri.AbsolutePath == "/pc/suggest/v1"),
                "cold discovery must make one suggestion request and zero detail/showlist requests");
        }

        private static void CachesSuccessfulEmptyButNeverFailures()
        {
            var emptyHandler = new RecordingHandler(_ => Json(HttpStatusCode.OK, Success("[]")));
            var emptyApi = CreateApi(emptyHandler);
            Assert(emptyApi.SearchAsync("empty", CancellationToken.None).GetAwaiter().GetResult().Count == 0 &&
                   emptyApi.SearchAsync("empty", CancellationToken.None).GetAwaiter().GetResult().Count == 0 &&
                   emptyHandler.Requests.Count == 1,
                "legal empty suggest arrays must be successful and use the short negative cache");

            var failedHandler = new RecordingHandler(_ => Json(HttpStatusCode.Forbidden, "{}"));
            var failedApi = CreateApi(failedHandler);
            AssertFails(() => failedApi.SearchAsync("forbidden", CancellationToken.None).GetAwaiter().GetResult(), "http-403");
            AssertFails(() => failedApi.SearchAsync("forbidden", CancellationToken.None).GetAwaiter().GetResult(), "http-403");
            Assert(failedHandler.Requests.Count == 2, "forbidden responses must not be retried or cached");
        }

        private static void RetriesOnlyTransientTransportFailures()
        {
            var retryHandler = new RecordingHandler(new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(new Func<HttpRequestMessage, HttpResponseMessage>[]
            {
                _ => WithRetryAfter(Json((HttpStatusCode)429, "{}"), TimeSpan.Zero),
                _ => Json(HttpStatusCode.OK, Success("[{\"cid\":\"10\",\"title\":\"Retry\"}]")),
            }));
            var retryApi = CreateApi(retryHandler);
            Assert(retryApi.SearchAsync("retry", CancellationToken.None).GetAwaiter().GetResult().Single().Id == "10" &&
                   retryHandler.Requests.Count == 2,
                "one transient 429 must get exactly one cancellation-aware retry");
            Assert(retryHandler.RequestTimes[1] - retryHandler.RequestTimes[0] >= TimeSpan.FromMilliseconds(450),
                "the permitted retry must pass through the same 500 ms provider limiter");

            var dateRetryHandler = new RecordingHandler(new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(new Func<HttpRequestMessage, HttpResponseMessage>[]
            {
                _ => WithRetryAfterDate(Json((HttpStatusCode)429, "{}"), DateTimeOffset.UtcNow.AddMinutes(1)),
                _ => Json(HttpStatusCode.OK, Success("[]")),
            }));
            var elapsed = Stopwatch.StartNew();
            Assert(CreateApi(dateRetryHandler).SearchAsync("retry-date", CancellationToken.None).GetAwaiter().GetResult().Count == 0 &&
                   dateRetryHandler.Requests.Count == 2,
                "Retry-After HTTP-date must permit the one transient retry");
            Assert(elapsed.Elapsed < TimeSpan.FromSeconds(3),
                "Retry-After HTTP-date must be clamped to the two-second discovery retry bound");

            foreach (var status in new[] { 502, 503, 504 })
            {
                var handler = new RecordingHandler(new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(new Func<HttpRequestMessage, HttpResponseMessage>[]
                {
                    _ => Json((HttpStatusCode)status, "{}"),
                    _ => Json(HttpStatusCode.OK, Success("[]")),
                }));
                Assert(CreateApi(handler).SearchAsync("retry-" + status, CancellationToken.None).GetAwaiter().GetResult().Count == 0 &&
                       handler.Requests.Count == 2,
                    "each permitted transient status must make at most one retry");
            }
        }

        private static void RejectsBusinessAndSchemaFailuresAndHonorsCancellation()
        {
            AssertApiFails(Json(HttpStatusCode.OK, "{\"code\":500,\"data\":{\"suggest\":[]}}"), "business-500");
            AssertApiFails(Json(HttpStatusCode.OK, "{\"code\":200,\"data\":{}}"), "incompatible-schema");
            AssertApiFails(Json(HttpStatusCode.OK, "not-json"), "malformed-json");

            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var api = CreateApi(new RecordingHandler(_ => Json(HttpStatusCode.OK, Success("[]"))));
            try
            {
                api.SearchAsync("cancel", cancellation.Token).GetAwaiter().GetResult();
                throw new InvalidOperationException("pre-cancelled discovery must stop before an upstream request");
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }

        private static void PreservesUnknownTypesAndExistingConsumers()
        {
            const string typed = "[{\"cid\":\"1\",\"title\":\"TV\",\"type\":1,\"typeName\":\"tv\"},{\"cid\":\"2\",\"title\":\"Movie\",\"type\":2,\"typeName\":\"movie\"},{\"cid\":\"3\",\"title\":\"Unknown\",\"type\":0}]";
            var movieProvider = CreateProvider(new RecordingHandler(_ => Json(HttpStatusCode.OK, Success(typed))));
            var seriesProvider = CreateProvider(new RecordingHandler(_ => Json(HttpStatusCode.OK, Success(typed))));
            Assert(movieProvider.Search(new Movie { Name = "typed" }).GetAwaiter().GetResult().Select(item => item.Id)
                       .SequenceEqual(new[] { "2", "3" }),
                "Movie search must exclude only explicit television suggestions");
            Assert(seriesProvider.Search(new Season { Name = "typed" }).GetAwaiter().GetResult().Select(item => item.Id)
                       .SequenceEqual(new[] { "1", "3" }),
                "TV search must exclude only explicit movie suggestions");

            const string unknown = "[{\"cid\":\"9\",\"title\":\"Happy Movie\"}]";
            var provider = CreateProvider(new RecordingHandler(_ => Json(HttpStatusCode.OK, Success(unknown))));
            var movie = new Movie { Name = "Happy Movie" };
            var fromSearch = provider.Search(movie).GetAwaiter().GetResult().Single().Id;
            var fromId = provider.SearchMediaId(movie).GetAwaiter().GetResult();
            var fromApi = provider.SearchForApi("Happy Movie").GetAwaiter().GetResult().Single().Id;
            Assert(fromSearch == "9" && fromId == "9" && fromApi == "9",
                "Search, SearchMediaId, and SearchForApi must consume identical normalized canonical IDs");
        }

        private static void GuardsTheNarrowDiscoveryContract()
        {
            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var source = File.ReadAllText(Path.Combine(root, "Scraper", "Mgtv", "MgtvApi.cs"));
            Assert(Count(source, "/pc/suggest/v1") == 1 && !source.Contains("/msite/search/v2"),
                "the product must contain one PC suggestion literal and no retired legacy search endpoint");
            Assert(source.Contains("UseCookies = false") && source.Contains("src=mgtv") &&
                   !source.Contains("DefaultRequestHeaders.Authorization"),
                "the suggest client must remain anonymous and source-bounded");
            Assert(source.Contains("MaximumSuggestRetryDelay = TimeSpan.FromSeconds(2)") &&
                   source.Contains("status == 429 || status == 502 || status == 503 || status == 504"),
                "retry policy must stay bounded to the documented transient statuses");
        }

        private static MgtvApi CreateApi(HttpMessageHandler handler)
        {
            var logs = DispatchProxy.Create<ILogManager, NoOpDispatchProxy>();
            var http = DispatchProxy.Create<IHttpClient, NoOpDispatchProxy>();
            return new MgtvApi(logs, http, handler);
        }

        private static Mgtv CreateProvider(HttpMessageHandler handler)
        {
            var logs = DispatchProxy.Create<ILogManager, NoOpDispatchProxy>();
            var http = DispatchProxy.Create<IHttpClient, NoOpDispatchProxy>();
            return new Mgtv(logs, http, handler);
        }

        private static void AssertApiFails(HttpResponseMessage response, string category)
        {
            AssertFails(() => CreateApi(new RecordingHandler(_ => response)).SearchAsync("failure-" + category, CancellationToken.None)
                .GetAwaiter().GetResult(), category);
        }

        private static MgtvSearchResult Deserialize(string body)
        {
            return JsonSerializer.Deserialize<MgtvSearchResult>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private static string Success(string suggest) => "{\"code\":200,\"data\":{\"suggest\":" + suggest + "}}";

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
        {
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        private static HttpResponseMessage WithRetryAfter(HttpResponseMessage response, TimeSpan delay)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delay);
            return response;
        }

        private static HttpResponseMessage WithRetryAfterDate(HttpResponseMessage response, DateTimeOffset date)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(date);
            return response;
        }

        private static void AssertFails(Action action, string category)
        {
            try
            {
                action();
                throw new InvalidOperationException("Expected failure category " + category);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains(category, StringComparison.Ordinal))
            {
                // Expected safe provider-local category.
            }
        }

        private static int Count(string value, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        private static string FindRepositoryRoot(string start)
        {
            var current = new DirectoryInfo(start);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Emby.Plugin.Danmu.csproj"))) return current.FullName;
                current = current.Parent;
            }
            throw new InvalidOperationException("Repository root not found.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _next;
            private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _queue;
            public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();
            public List<DateTimeOffset> RequestTimes { get; } = new List<DateTimeOffset>();

            public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> next) { _next = next; }
            public RecordingHandler(Queue<Func<HttpRequestMessage, HttpResponseMessage>> queue) { _queue = queue; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                RequestTimes.Add(DateTimeOffset.UtcNow);
                return Task.FromResult(_queue != null ? _queue.Dequeue()(request) : _next(request));
            }
        }

        private class NoOpDispatchProxy : DispatchProxy
        {
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.ReturnType == typeof(ILogger)) return DispatchProxy.Create<ILogger, NoOpDispatchProxy>();
                return targetMethod.ReturnType == typeof(void) ? null :
                    targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }
    }
}
