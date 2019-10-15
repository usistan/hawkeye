/*****************************************************************************
 * Custom Translate skill
 * Calls the translate API to translate a piece of text.
 * 
 * IMPORTANT: Enter the key for the Text API in the credentials section.
 * By default the skill translates to english as the destination language.
 * Feel free to change the destination language in the call to the
 * TranslateText method.
 * 
 * Sample input:
 
        {
	        "values": 
	        [
    	        {
        	        "recordId": "foo1",
          	        "data": { "text":  "En los dias del c�lera no hab�a ninguna hoja de papel."}
                },
                {
        	        "recordId": "bar2",
          	        "data": { "text":  "Oui, avec plaisir! Dis-moi, parles-tu aussi anglais? "}
                }
            ]
        }

 * Sample output:

        {
            "values": [
                {
                    "recordId": "foo1",
                    "data": {
                        "translatedText": "In the days of cholera there was no sheet of paper."
                    },
                    "errors": null,
                    "warnings": null
                },
                {
                    "recordId": "bar2",
                    "data": {
                        "translatedText": "Yes, with pleasure! Tell me, do you speak English too? "
                    },
                    "errors": null,
                    "warnings": null
                }
            ]
        }

 *****************************************************************************/

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;

namespace SampleSkills
{
    public static class TranslateSkill
    {
        #region Translator Text API Credentials
        static readonly string path = Environment.GetEnvironmentVariable("CognitiveTranslateUrl", EnvironmentVariableTarget.Process);

        // NOTE: Replace this example key with a valid subscription key.
        static readonly string translatorApiKey = Environment.GetEnvironmentVariable("CognitiveTranslateKey", EnvironmentVariableTarget.Process);

        // If using the all in one key, modify with the region of the service (for example "westus"). See https://docs.microsoft.com/en-us/azure/cognitive-services/translator/reference/v3-0-reference  
        static readonly string translatorRegion = Environment.GetEnvironmentVariable("CognitiveTranslateRegion", EnvironmentVariableTarget.Process);
        #endregion

        #region Class used to deserialize the request
        public class InputRecord
        {
            public class InputRecordData
            {
                public string Text;
            }

            public string RecordId { get; set; }
            public InputRecordData Data { get; set; }
        }

        private class WebApiRequest
        {
            public List<InputRecord> Values { get; set; }
        }
        #endregion

        #region Classes used to serialize the response
        public class OutputRecord
        {
            public class OutputRecordData
            {
                public string TranslatedText { get; set; }
            }

            public class OutputRecordMessage
            {
                public string Message { get; set; }
            }

            public string RecordId { get; set; }
            public OutputRecordData Data { get; set; }
            public List<OutputRecordMessage> Errors { get; set; }
            public List<OutputRecordMessage> Warnings { get; set; }
        }

        private class WebApiResponse
        {
            public WebApiResponse()
            {
                this.values = new List<OutputRecord>();
            }

            public List<OutputRecord> values { get; set; }
        }
        #endregion

        [FunctionName("TranslateSkill")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Custom skill: C# HTTP trigger function processed a request.");

            // Read input, deserialize it and validate it.
            var data = GetStructuredInput(req.Body);
            if (data == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema.");
            }

            // Calculate the response for each value.
            var response = new WebApiResponse();
            foreach (var record in data.Values)
            {
                if (record == null || record.RecordId == null) continue;

                OutputRecord responseRecord = new OutputRecord();
                responseRecord.RecordId = record.RecordId;

                try
                {
                    responseRecord.Data = TranslateText(record.Data, "en").Result;
                }
                catch (Exception e)
                {
                    // Something bad happened, log the issue.
                    var error = new OutputRecord.OutputRecordMessage
                    {
                        Message = e.Message
                    };

                    responseRecord.Errors = new List<OutputRecord.OutputRecordMessage>
                    {
                        error
                    };
                }
                finally
                {
                    response.values.Add(responseRecord);
                }
            }

            return new OkObjectResult(response);
        }

        private static WebApiRequest GetStructuredInput(Stream requestBody)
        {
            string request = new StreamReader(requestBody).ReadToEnd();

            var data = JsonConvert.DeserializeObject<WebApiRequest>(request);
            if (data == null)
            {
                return null;
            }
            return data;
        }

        /// <summary>
        /// Use Cognitive Service to translate text from one language to another.
        /// </summary>
        /// <param name="inputRecord">The input record that contains the original text to translate.</param>
        /// <param name="toLanguage">The language you want to translate to.</param>
        /// <returns>Asynchronous task that returns the translated text. </returns>
        async static Task<OutputRecord.OutputRecordData> TranslateText(InputRecord.InputRecordData inputRecord, string toLanguage)
        {
            string originalText = inputRecord.Text;

            var outputRecord = new OutputRecord.OutputRecordData();

            object[] body = new object[] { new { Text = originalText } };
            var requestBody = JsonConvert.SerializeObject(body);

            var uri = $"{path}&to={toLanguage}";

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", translatorApiKey);
                request.Headers.Add("Ocp-Apim-Subscription-Region", translatorRegion);

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                dynamic data = JsonConvert.DeserializeObject(responseBody);

                outputRecord.TranslatedText = data?.First?.translations?.First?.text?.Value as string;
                return outputRecord;
            }
        }
    }
}
