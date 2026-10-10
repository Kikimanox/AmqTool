using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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

        /// <returns>ok + the link, or !ok + litterbox's error text. Network problems throw.</returns>
        public static async Task<(bool ok, string text)> UploadAsync(string filePath)
        {
            var media = await Task.Run(() => File.ReadAllBytes(filePath));
            string ext = Path.GetExtension(filePath).TrimStart('.');
            string t = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();

            var formContent = new MultipartFormDataContent();
            formContent.Add(new StringContent("fileupload"), "reqtype");
            formContent.Add(new StringContent("72h"), "time");

            var fileContent = new ByteArrayContent(media);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            formContent.Add(fileContent, "fileToUpload", $"{t}.{ext}");

            using (var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            using (var res = await c.PostAsync(ApiUrl, formContent))
                return (res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
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
