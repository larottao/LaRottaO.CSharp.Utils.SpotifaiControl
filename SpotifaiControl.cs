﻿/*

After having terrible experiences with some NuGet packages designed to control Spotify using C#, 
I put together this code by integrating various parts found online. It manages authentication using the web browser 
and player control using pure POST and PUT requests.

The statement that it controls Spotify "with only HTTP requests, no ext. packages" is inexact, because it uses the 
great RestSharp library. Storing your credentials in JSON uses Newtonsoft.Json, is optional and can be omitted.

Please note that this will only work with Spotify Premium accounts. I hope it saves you time on your own implementations,

Remember to set the redirect URL on the spotify dashboard as http://localhost:8888/callback/

*/

using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Web;

namespace LaRottaO.CSharp.SpotifaiControl
{
    public static class Spotifai
    {
        private static readonly string TOKEN_ENDPOINT = "https://accounts.spotify.com/api/token";
        private static readonly string AUTHORIZE_ENDPOINT = "https://accounts.spotify.com/authorize";
        private static readonly string REDIRECT_URI = "http://localhost:8888/callback/";
        private static readonly string CREDENTIALS_FILE = "spotify_credentials.json";
        private static readonly string TOKEN_FILE = "spotify_token.txt";
        private static readonly string BASE_URL = "https://api.spotify.com/v1";

        static string clientId;
        static string clientSecret;
        static string accessToken;
        static RestClient client;

        public static void LoadCredentials()
        {
            if (!File.Exists(CREDENTIALS_FILE))
            {
                var credentials = new
                {
                    CLIENT_ID = "your_client_id",
                    CLIENT_SECRET = "your_client_secret"
                };

                //Write your spotify credentials on the newly created file and launch the application again
                File.WriteAllText(CREDENTIALS_FILE, Newtonsoft.Json.JsonConvert.SerializeObject(credentials, Newtonsoft.Json.Formatting.Indented));
                Console.WriteLine($"Created {CREDENTIALS_FILE}. Please fill in your Spotify credentials.");
                Environment.Exit(1);
            }

            try
            {
                string jsonContent = File.ReadAllText(CREDENTIALS_FILE);
                JObject credentials = JObject.Parse(jsonContent);

                clientId = (string)credentials["CLIENT_ID"];
                clientSecret = (string)credentials["CLIENT_SECRET"];

                Console.WriteLine($"CLIENT_ID: {clientId}");
                Console.WriteLine($"CLIENT_SECRET: {clientSecret}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading JSON file: {ex.Message}");
            }
        }

        public static bool CheckAccessToken()
        {
            if (File.Exists(TOKEN_FILE))
            {
                accessToken = File.ReadAllText(TOKEN_FILE);
                if (IsTokenValid())
                {
                    Console.WriteLine("Access token loaded from file.");
                    return true;
                }
                else
                {
                    Console.WriteLine("Stored access token is invalid or expired.");
                }
            }
            return false;
        }

        public static bool IsTokenValid()
        {
            var tokenClient = new RestClient(BASE_URL);
            var request = new RestRequest("me", Method.Get);
            request.AddHeader("Authorization", $"Bearer {accessToken}");

            var response = tokenClient.Execute(request);
            return response.IsSuccessful;
        }

        public static void GetAccessToken()
        {
            string state = Guid.NewGuid().ToString("N");

            var authorizeUrl = $"{AUTHORIZE_ENDPOINT}?client_id={clientId}&response_type=code&redirect_uri={HttpUtility.UrlEncode(REDIRECT_URI)}&scope=user-modify-playback-state user-read-playback-state&state={state}";

            // Launch browser for user authentication
            Process.Start(new ProcessStartInfo
            {
                FileName = authorizeUrl,
                UseShellExecute = true
            });

            var listener = new HttpListener();
            listener.Prefixes.Add(REDIRECT_URI);
            listener.Start();

            var context = listener.GetContext();
            var code = HttpUtility.ParseQueryString(context.Request.Url.Query).Get("code");

            if (!string.IsNullOrEmpty(code))
            {
                Console.WriteLine($"Authorization code received: {code}");
                var responseString = "<html><body>You can close this tab now.</body></html>";
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
                listener.Stop();

                var tokenClient = new RestClient(TOKEN_ENDPOINT);
                var request = new RestRequest
                {
                    Method = Method.Post,
                    RequestFormat = DataFormat.Json
                };

                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("grant_type", "authorization_code");
                request.AddParameter("code", code);
                request.AddParameter("redirect_uri", REDIRECT_URI);
                request.AddParameter("client_id", clientId);
                request.AddParameter("client_secret", clientSecret);

                var response = tokenClient.Execute(request);
                if (response.IsSuccessful)
                {
                    var tokenResponse = JObject.Parse(response.Content);
                    accessToken = tokenResponse["access_token"].ToString();
                    File.WriteAllText(TOKEN_FILE, accessToken);
                    Console.WriteLine($"Access Token: {accessToken}");
                }
                else
                {
                    Console.WriteLine($"Failed to obtain access token: {response.Content}");
                }
            }
            else
            {
                Console.WriteLine("Failed to obtain authorization code.");
            }
        }

        public static void Authenticate()
        {
            LoadCredentials();

            if (!CheckAccessToken())
            {
                GetAccessToken();
            }

            client = new RestClient(BASE_URL);
            client.AddDefaultHeader("Authorization", $"Bearer {accessToken}");
        }

        public static void Play()
        {
            try
            {
                var request = new RestRequest("me/player/play", Method.Put);
                var response = client.Execute(request);

                if (!response.IsSuccessful)
                {
                    Console.WriteLine($"Failed to play: {response.Content}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing track: {ex.Message}");
            }
        }

        public static void Pause()
        {
            try
            {
                var request = new RestRequest("me/player/pause", Method.Put);
                var response = client.Execute(request);

                if (!response.IsSuccessful)
                {
                    Console.WriteLine($"Failed to pause: {response.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pausing track: {ex.Message}");
            }
        }

        public static void NextTrack()
        {
            try
            {
                var request = new RestRequest("me/player/next", Method.Post);
                var response = client.Execute(request);

                if (!response.IsSuccessful)
                {
                    Console.WriteLine($"Failed to skip to next track: {response.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error skipping to next track: {ex.Message}");
            }
        }

        public static void PreviousTrack()
        {
            try
            {
                var request = new RestRequest("me/player/previous", Method.Post);
                var response = client.Execute(request);

                if (!response.IsSuccessful)
                {
                    Console.WriteLine($"Failed to skip to previous track: {response.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error skipping to previous track: {ex.Message}");
            }
        }

        public static JObject GetCurrentPlayback()
        {
            try
            {
                var request = new RestRequest("me/player", Method.Get);
                var response = client.Execute(request);

                if (response.IsSuccessful)
                {
                    return JObject.Parse(response.Content);
                }
                else
                {
                    Console.WriteLine($"Failed to get current playback: {response.ErrorMessage}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting current playback: {ex.Message}");
                return null;
            }
        }
    }
}
