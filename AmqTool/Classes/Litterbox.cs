using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AmqTool
{
    /// <summary>
    /// Uploads to litterbox.catbox.moe (temporary hosting, what AMQ uploads use now). Litterbox deletes files
    /// after 72h, so a link saved in a job can expire.
    /// </summary>
    public static class Litterbox
    {
        // Set AMQTOOL_LITTERBOX_URL to a local fake api.php to test uploads without uploading anything
        static string ApiUrl => Environment.GetEnvironmentVariable("AMQTOOL_LITTERBOX_URL") ?? "https://litterbox.catbox.moe/resources/internals/api.php";

        /// <returns>ok + the link, or !ok + a short, readable reason why it failed. Never throws.</returns>
        public static async Task<(bool ok, string text)> UploadAsync(string filePath)
        {
            byte[] media;
            try
            {
                media = await Task.Run(() => File.ReadAllBytes(filePath));
            }
            catch (Exception ex)
            {
                return (false, $"Could not read the file: {ex.Message}");
            }

            string ext = Path.GetExtension(filePath).TrimStart('.');
            string t = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();

            try
            {
                using (var formContent = new MultipartFormDataContent())
                using (var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                {
                    formContent.Add(new StringContent("fileupload"), "reqtype");
                    formContent.Add(new StringContent("72h"), "time");

                    var fileContent = new ByteArrayContent(media);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    formContent.Add(fileContent, "fileToUpload", $"{t}.{ext}");

                    using (var res = await c.PostAsync(ApiUrl, formContent))
                    {
                        string body = ((await res.Content.ReadAsStringAsync()) ?? "").Trim();
                        if (!res.IsSuccessStatusCode)
                        {
                            string said = Readable(body);
                            return (false, $"Litterbox answered with an error: HTTP {(int)res.StatusCode} {res.ReasonPhrase}"
                                + (said != "" ? $"\n\nIt said: {said}" : "")
                                + "\n\nLitterbox is probably down or overloaded right now, try again later.");
                        }
                        // a working upload answers with just the link. Anything else (an error text, a maintenance page...) is a failure
                        if (!Uri.TryCreate(body, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http") || body.Any(char.IsWhiteSpace))
                        {
                            string said = Readable(body);
                            return (false, "Litterbox did not send back a link" + (said != "" ? $", it said: {said}" : " (empty answer).")
                                + "\n\nLitterbox is probably down or having problems right now, try again later.");
                        }
                        return (true, body);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return (false, "Litterbox did not answer within 10 minutes (timeout). It's probably down or very slow right now.");
            }
            catch (Exception ex)
            {
                return (false, $"Could not reach litterbox: {Messages(ex)}\n\nCheck your internet connection. If that's fine, litterbox is probably down right now.");
            }
        }

        /// <summary>The real reason ("Unable to connect to the remote server") is usually in an inner exception, not in the outer "An error occurred while sending the request."</summary>
        static string Messages(Exception ex)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var e = ex; e != null; e = e.InnerException)
                if (!string.IsNullOrWhiteSpace(e.Message) && !parts.Contains(e.Message.Trim())) parts.Add(e.Message.Trim());
            return string.Join(" -> ", parts);
        }

        /// <summary>Error pages (e.g. Cloudflare's "Bad gateway") are big HTML documents. Keep just the text, and not too much of it.</summary>
        static string Readable(string body, int max = 400)
        {
            if (string.IsNullOrWhiteSpace(body)) return "";
            string s = Regex.Replace(body, @"<(script|style|head)\b.*?</\1\s*>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<[^>]*>", " ");
            s = WebUtility.HtmlDecode(s);
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s.Length > max ? s.Substring(0, max) + "..." : s;
        }

        /// <summary>
        /// True only when the host clearly says the file is gone (404/410). No internet, timeouts etc. count as
        /// "still there", so a link never gets thrown away just because the check itself failed.
        /// </summary>
        public static async Task<bool> IsExpiredAsync(string link)
        {
            try
            {
                using (var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                using (var res = await c.SendAsync(new HttpRequestMessage(HttpMethod.Head, link)))
                    return res.StatusCode == HttpStatusCode.NotFound || res.StatusCode == HttpStatusCode.Gone;
            }
            catch
            {
                return false;
            }
        }
    }
}
