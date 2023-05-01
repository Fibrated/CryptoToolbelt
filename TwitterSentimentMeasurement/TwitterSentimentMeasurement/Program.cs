using System.Net.Http.Headers;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.AI.TextAnalytics;
using Newtonsoft.Json.Linq;
using System.Text;

public static class SentimentAnalysisFunction
{
    private static readonly HttpClient httpClient = new HttpClient();

    [FunctionName("SentimentAnalysisFunction")]
    public static async Task Run([TimerTrigger("0 0 22 * * *")] TimerInfo myTimer, ILogger log)
    {
        log.LogInformation($"SentimentAnalysisFunction executed at: {DateTime.Now}");

        // Twitter API setup
        string bearerToken = GetEnvironmentVariable("BearerToken");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        // Get tweets from home timeline
        var response = await httpClient.GetAsync("https://api.twitter.com/2/users/:id/tweets");
        var content = await response.Content.ReadAsStringAsync();
        var tweets = JArray.Parse(content);

        // Initialize sentiment analysis client
        var credentials = new AzureKeyCredential(GetEnvironmentVariable("TextAnalyticsKey"));
        var client = new TextAnalyticsClient(new Uri(GetEnvironmentVariable("TextAnalyticsEndpoint")), credentials);

        double totalSentiment = 0;
        int tweetCount = 0;

        foreach (var tweet in tweets)
        {
            DocumentSentiment sentiment = await client.AnalyzeSentimentAsync(tweet["text"].ToString());
            totalSentiment += (double)sentiment.Sentiment;
            tweetCount++;
        }

        double averageSentiment = totalSentiment / tweetCount;

        // Tweet sentiment
        var sentimentTweet = new StringContent(
            $"{{\"status\":\"The overall market sentiment is: {averageSentiment}\"}}",
            Encoding.UTF8,
            "application/json");

        await httpClient.PostAsync("https://api.twitter.com/1.1/statuses/update.json", sentimentTweet);
    }

    private static string GetEnvironmentVariable(string name)
    {
        return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
    }
}
