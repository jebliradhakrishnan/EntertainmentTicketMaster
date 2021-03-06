﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace Twitter
{
    public class TwitterFeedsManipulator
    {
        private WidgetGroupItemList _widgetGroupItems;
        private string _groupActionText;
        private string _groupActionUrl;
        private string _groupHeaderText;
        private string _cacheKey;
        private string _cacheTimeSecs;
        private Cache _cache;

        public TwitterFeedsManipulator(string groupActionText,string groupActionUrl,string groupHeaderText,string cacheKey,string cacheTimeSecs,Cache cache)
        {
            _groupActionText = groupActionText;
            _groupActionUrl = groupActionUrl;
            _groupHeaderText = groupHeaderText;
            _cacheKey = cacheKey;
            _cacheTimeSecs = cacheTimeSecs;
            _cache = cache;
        }

        public string GetOauthToken(OauthAuthentication oauthAuthentication)
        {
            StreamReader reader = null;
            HttpWebRequest request = null;

            var oauthHeader = GetOauthHeader(oauthAuthentication, OauthHeaderType.Basic);
            try
            {
                request = WebRequest.Create("https://api.twitter.com/oauth2/token") as HttpWebRequest;

                request.Headers.Add("Authorization", oauthHeader);
                request.Method = "POST";

                if (request != null)
                {
                    request.ContentType = "application/x-www-form-urlencoded";
                    var bytes = UTF8Encoding.ASCII.GetBytes("grant_type=client_credentials");
                    request.ContentLength = bytes.Length;
                    request.GetRequestStream().Write(bytes, 0, bytes.Length);

                    using (var response = request.GetResponse() as HttpWebResponse)
                    {
                        using (Stream respStream = response.GetResponseStream())
                        {
                            if (respStream != null)
                            {
                                reader = new StreamReader(respStream);

                                string xmlString = reader.ReadToEnd();
                                reader.Close();
                                var tokenResult = xmlString.Split(new char[] { ',' });
                                if (tokenResult[0].Contains("bearer"))
                                    return tokenResult[1].Split(new char[] { ':' })[1].Split(new char[] { '}', '"' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                                else return tokenResult[0].Split(new char[] { ':' })[1].Split(new char[] { '}', '"' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                if (reader != null)
                    reader.Close();
                throw e;
            }
        }


        public WidgetGroupItemList GetProfileTwitterFeeds(string twitterProfileUrl, OauthAuthentication oauthAuthentication)
        {
            //Using Twitter Oauth: http://www.codeproject.com/Articles/247336/Twitter-OAuth-authentication-using-Net
            _widgetGroupItems = _cache[_cacheKey] == null ? null : _cache[_cacheKey] as WidgetGroupItemList;

            if (_widgetGroupItems == null)
            {
                _widgetGroupItems = new WidgetGroupItemList();

                var oauthToken = GetOauthToken(oauthAuthentication);
                var oauthHeader = GetOauthHeader(OauthHeaderType.Bearer, oauthToken);
                StreamReader reader = null;

                ServicePointManager.Expect100Continue = false;

                try
                {
                    HttpWebRequest request = null;

                    request = WebRequest.Create(twitterProfileUrl) as HttpWebRequest;

                    if (request != null)
                    {

                        request.Headers.Add("Authorization", oauthHeader);
                        request.Method = "GET";
                        request.ContentType = "application/x-www-form-urlencoded";

                        using (var response = request.GetResponse() as HttpWebResponse)
                        {
                            using (Stream respStream = response.GetResponseStream())
                            {
                                if (respStream != null)
                                {
                                    reader = new StreamReader(respStream);

                                    string xmlString = reader.ReadToEnd();
                                    reader.Close();
                                    return ConvertRawTweetsToXmlConsumables(xmlString);
                                }
                            }
                        }
                    }
                    return new WidgetGroupItemList();
                }
                catch (Exception e)
                {
                    if (reader != null)
                        reader.Close();
                    throw e;
                }
            }
            else return _cache[_cacheKey] as WidgetGroupItemList;
        }


        public WidgetGroupItemList GetSearchTwitterFeeds(string twitterSearchUrl, OauthAuthentication oauthAuthentication, HttpServerUtility Server)
        {
            //Using Twitter Oauth: http://www.codeproject.com/Articles/247336/Twitter-OAuth-authentication-using-Net
            _widgetGroupItems = _cache[_cacheKey] as WidgetGroupItemList;

            if (_widgetGroupItems == null)
            {
                _widgetGroupItems = new WidgetGroupItemList();

                var oauthToken = GetOauthToken(oauthAuthentication);
                var oauthHeader = GetOauthHeader(OauthHeaderType.Bearer, oauthToken);
                StreamReader reader = null;

                ServicePointManager.Expect100Continue = false;

                try
                {
                    HttpWebRequest request = null;

                    request = WebRequest.Create(twitterSearchUrl) as HttpWebRequest;

                    if (request != null)
                    {

                        request.Headers.Add("Authorization", oauthHeader);
                        request.Method = "GET";
                        request.ContentType = "application/x-www-form-urlencoded";

                        using (var response = request.GetResponse() as HttpWebResponse)
                        {
                            using (Stream respStream = response.GetResponseStream())
                            {
                                if (respStream != null)
                                {
                                    reader = new StreamReader(respStream);

                                    string xmlString = reader.ReadToEnd();
                                    reader.Close();
                                    return ConvertRawSearchTweetsToXmlConsumables(xmlString);
                                }
                            }
                        }
                    }
                    return _widgetGroupItems;
                }
                catch (Exception e)
                {
                    if (reader != null)
                        reader.Close();
                    throw e;
                }
            }
            return _widgetGroupItems;
        }
        private string GetOauthHeader(OauthAuthentication oauthentication, string resource_url)
        {

            var oauth_version = "1.0";
            var oauth_signature_method = "HMAC-SHA1";
            var oauth_nonce = Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));

            var timeSpan = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0,
                                                   DateTimeKind.Utc);
            var oauth_timestamp = Convert.ToInt64(timeSpan.TotalSeconds).ToString();

            var baseFormat = "oauth_consumer_key={0}&oauth_nonce={1}&oauth_signature_method={2}" +
                "&oauth_timestamp={3}&oauth_token={4}&oauth_version={5}&oauth_verifier={6}";
            var oauth_token = GetOauthToken(oauthentication);

            var baseString = string.Format(baseFormat,
                                        oauthentication.ConsumerKey,
                                        oauth_nonce,
                                        oauth_signature_method,
                                        oauth_timestamp,
                                        oauthentication.TokenKey,
                                        oauth_version
                                        );

            baseString = string.Concat("GET&", Uri.EscapeDataString(resource_url),
                         "&", Uri.EscapeDataString(baseString));

            var compositeKey = string.Concat(Uri.EscapeDataString(oauthentication.ConsumerSecret),
                        "&", Uri.EscapeDataString(oauthentication.TokenSecret));

            string oauth_signature;

            using (HMACSHA1 hasher = new HMACSHA1(ASCIIEncoding.ASCII.GetBytes(compositeKey)))
            {
                oauth_signature = Convert.ToBase64String(
                    hasher.ComputeHash(ASCIIEncoding.ASCII.GetBytes(baseString)));
            }

            var headerFormat = "OAuth oauth_nonce=\"{0}\", oauth_signature_method=\"{1}\", " +
               "oauth_timestamp=\"{2}\", oauth_consumer_key=\"{3}\", " +
               "oauth_token=\"{4}\", oauth_signature=\"{5}\", " +
               "oauth_version=\"{6}\",oauth_callback=oob";

            var authHeader = string.Format(headerFormat,
            Uri.EscapeDataString(oauth_nonce),
            Uri.EscapeDataString(oauth_signature_method),
            Uri.EscapeDataString(oauth_timestamp),
            Uri.EscapeDataString(oauthentication.ConsumerKey),
            Uri.EscapeDataString(oauthentication.TokenKey),
            Uri.EscapeDataString(oauth_signature),
            Uri.EscapeDataString(oauth_version)
    );

            return authHeader;
        }

        public enum OauthHeaderType { Basic = 0, Bearer = 1 }

        private string GetOauthHeader(OauthAuthentication oauthentication, OauthHeaderType oauthHeaderType)
        {

            var oauth_version = "1.0";
            var oauth_signature_method = "HMAC-SHA1";
            var oauth_nonce = Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));

            var timeSpan = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0,
                                                   DateTimeKind.Utc);
            var oauth_timestamp = Convert.ToInt64(timeSpan.TotalSeconds).ToString();

            var baseFormat = "oauth_consumer_key={0}&oauth_nonce={1}&oauth_signature_method={2}" +
                "&oauth_timestamp={3}&oauth_token={4}&oauth_version={5}";

            var baseString = string.Format(baseFormat,
                                        oauthentication.ConsumerKey,
                                        oauth_nonce,
                                        oauth_signature_method,
                                        oauth_timestamp,
                                        oauthentication.TokenKey,
                                        oauth_version
                                        );


            var compositeKey = string.Concat(Uri.EscapeDataString(oauthentication.ConsumerSecret),
                        "&", Uri.EscapeDataString(oauthentication.TokenSecret));

            string oauth_signature;

            using (HMACSHA1 hasher = new HMACSHA1(ASCIIEncoding.ASCII.GetBytes(compositeKey)))
            {
                oauth_signature = Convert.ToBase64String(
                    hasher.ComputeHash(ASCIIEncoding.ASCII.GetBytes(baseString)));
            }

            var headerFormat = oauthentication.ConsumerKey + ":" + oauthentication.ConsumerSecret;
            headerFormat = Convert.ToBase64String(ASCIIEncoding.UTF8.GetBytes(headerFormat));

            return oauthHeaderType.ToString() + " " + headerFormat;
        }


        private string GetOauthHeader(OauthHeaderType oauthHeaderType, string bearerToken)
        {
            return oauthHeaderType.ToString() + " " + bearerToken;
        }
        public class TweetObject
        {
            public string AvatarUrl { get; set; }
            public string Tweet { get; set; }
            public string StatusId { get; set; }
            public string TimeCreated { get; set; }
            public string Username { get; set; }
            public string GroupUrl { get; set; }
        }

        private IEnumerable<TweetObject> GetTweetsFromXdoc(XDocument xdoc)
        {
            var tweetResults = from tweets in xdoc.Descendants("status")
                               select new TweetObject
                               {
                                   Tweet = tweets.Element("text").Value,
                                   AvatarUrl =
                        tweets.Element("user").Element("profile_image_url").Value,
                                   Username =
                        tweets.Element("user").Element("screen_name").Value,
                                   StatusId = tweets.Element("id_str").Value,
                                   TimeCreated = tweets.Element("created_at").Value,
                                   GroupUrl = tweets.Descendants("entities").Descendants("urls").Any() ? tweets.Descendants("entities").Descendants("urls").FirstOrDefault().Element("url").Value : "http://www.martinlayooinc.co.uk"
                               };
            return tweetResults;
        }

        private void GetTimeSinceTweeted(TimeSpan period, out string timeSince)
        {
            string timePeriod = string.Empty;

            if (period.Days > 0)
            {
                timePeriod = period.Days.ToString();

                if (period.Days == 1)
                    timeSince = string.Format("{0} day ago", timePeriod);
                else timeSince = string.Format("{0} days ago", timePeriod);
            }
            else if (period.Hours > 0)
            {
                timePeriod = period.Hours.ToString();
                if (period.Hours == 1)
                    timeSince = string.Format("{0} hour ago", timePeriod);
                else timeSince = string.Format("{0} hours ago", timePeriod);
            }
            else if (period.Minutes > 0)
            {
                timePeriod = period.Minutes.ToString();
                if (period.Minutes == 1)
                    timeSince = string.Format("{0} minute ago", timePeriod);
                else timeSince = string.Format("{0} minutes ago", timePeriod);
            }
            else
            {
                timePeriod = period.Seconds.ToString();
                if (period.Seconds == 1)
                    timeSince = string.Format("{0} second ago", timePeriod);
                else timeSince = string.Format("{0} seconds ago", timePeriod);
            }
        }
        private WidgetGroupItemList ConvertRawTweetsToXmlConsumables(string jsonString)
        {
            // To convert JSON text contained in string json into an XML node

            var xmldoc = JsonConvert.DeserializeXNode("{\"status\":" + jsonString + "}", "root");


            var ProfileTweets = GetTweetsFromXdoc(xmldoc);

            foreach (var profileTweet in ProfileTweets)
            {

                var dateCreated = profileTweet.TimeCreated;
                string[] timeComponents = dateCreated.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                var dateComponents = timeComponents[0].Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                var year = timeComponents[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1];
                dateCreated = string.Concat(dateComponents[0].Trim(), " ", dateComponents[1], " ", dateComponents[2], " ", dateComponents[3], " ", year);

                string timeSince = string.Empty;
                try
                {
                    DateTime createdDate = DateTime.ParseExact(dateCreated, "ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None);
                    DateTime current = DateTime.Now;

                    TimeSpan period = current - createdDate;

                    GetTimeSinceTweeted(period, out timeSince);
                }
                catch (Exception e)
                {

                }

                _widgetGroupItems.Add(new GroupObject { GroupActionText = _groupActionText, GroupActionUrl = _groupActionUrl, GroupDescription = AddColourToTwitterKnownWords(profileTweet.Tweet), GroupAvatarUrl = profileTweet.AvatarUrl, GroupId = -1, GroupHeaderText = _groupHeaderText, GroupUrl = profileTweet.GroupUrl, Username = profileTweet.Username, Duration = timeSince, TweetStatusId = profileTweet.StatusId });
            }


            int _cacheTime = 180;
            Int32.TryParse(_cacheTimeSecs, out _cacheTime);
            _cache.Insert(_cacheKey, _widgetGroupItems, null, DateTime.UtcNow.AddSeconds(_cacheTime), System.Web.Caching.Cache.NoSlidingExpiration);
            //return WidgetGroupItems.ToSerializerXml();
            return _widgetGroupItems;
        }
        private WidgetGroupItemList GenerateWidgetItems(IEnumerable<TweetObject> profileTweets, int pageSize)
        {
            int tweetIndex = 0;
            var gotLastId = false;

            if (profileTweets.Count() == 0) throw new Exception("There are no results for the search criteria!");

            foreach (var searchedTweet in profileTweets)
            {
                //dateFormat: Wed, 19 Jan 2011 21:16:37 +0000"

                string dateCreated = searchedTweet.TimeCreated;
                string[] timeComponents = dateCreated.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                var dateComponents = timeComponents[0].Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                var year = timeComponents[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1];
                dateCreated = string.Concat(dateComponents[0].Trim(), " ", dateComponents[1], " ", dateComponents[2], " ", dateComponents[3], " ", year);

                string timeSince = string.Empty;
                try
                {
                    DateTime createdDate = DateTime.ParseExact(dateCreated, "ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None);
                    DateTime current = DateTime.Now;

                    TimeSpan period = current - createdDate;

                    GetTimeSinceTweeted(period, out timeSince);
                }
                catch (Exception e)
                {

                }
                _widgetGroupItems.Add(new GroupObject { GroupActionText = _groupActionText, GroupActionUrl = _groupActionUrl, GroupDescription = searchedTweet.Tweet, GroupAvatarUrl = searchedTweet.AvatarUrl, GroupId = -1, GroupHeaderText = _groupHeaderText, GroupUrl = _groupActionUrl, Username = searchedTweet.Username, TweetStatusId = searchedTweet.StatusId, Duration = timeSince });
                tweetIndex++;

                if (tweetIndex > pageSize - 1)
                {
                    long lastResultId = -1;

                    long.TryParse(_widgetGroupItems[_widgetGroupItems.Count - 1].TweetStatusId, out lastResultId);
                    if (lastResultId > 0)
                    {
                        //Application["LastResultId"] = lastResultId;
                    }
                    gotLastId = true;
                    break;
                }
            }
            if (!gotLastId)
            {
                long lastResultId = -1;

                long.TryParse(_widgetGroupItems[_widgetGroupItems.Count - 1].TweetStatusId, out lastResultId);
                if (lastResultId > 0)
                {
                    //Application["LastResultId"] = lastResultId;
                }
            }
            return _widgetGroupItems;
        }
        private WidgetGroupItemList ConvertRawSearchTweetsToXmlConsumables(string jsonString)
        {
            // To convert JSON text contained in string json into an XML node

            var xmldoc = JsonConvert.DeserializeXNode("{\"status\":" + jsonString + "}", "root");


            var ProfileTweets = from tweets in xmldoc.Descendants("statuses")
                                select new
                                {
                                    Tweet = tweets.Element("text").Value,
                                    AvatarUrl =
                         tweets.Element("user").Descendants("profile_image_url").SingleOrDefault().Value,
                                    Username =
                         tweets.Element("user").Descendants("screen_name").SingleOrDefault().Value,
                                    TweetStatusId = tweets.Element("id").Value
                                };

            foreach (var profileTweet in ProfileTweets)
            {
                _widgetGroupItems.Add(new GroupObject { GroupActionText = _groupActionText, GroupActionUrl = _groupActionUrl, GroupDescription = AddColourToTwitterKnownWords(profileTweet.Tweet), GroupAvatarUrl = profileTweet.AvatarUrl, GroupId = -1, GroupHeaderText = _groupHeaderText, GroupUrl = _groupActionUrl, Username = profileTweet.Username, TweetStatusId = profileTweet.TweetStatusId });
            }


            int cacheTime = 180;
            Int32.TryParse(_cacheTimeSecs, out cacheTime);
            _cache.Insert(_cacheKey, _widgetGroupItems, null, DateTime.UtcNow.AddSeconds(cacheTime), System.Web.Caching.Cache.NoSlidingExpiration);
            return _widgetGroupItems;
        }
        private string AddColourToTwitterKnownWords(string sentence)
        {
            var pattern = @"((?:http://|www\.)\S+\b)|(?:\@\S+)|(?:\#\S+)";

            string wrapper = @"<span class='greentext'>{0}</span>";
            string linkWrapper = @"<a class='greentext' href='{0}' target='_blank'>{0}</a>";
            MatchCollection collection = Regex.Matches(sentence, pattern);
            foreach (Match match in collection)
            {
                string value = match.Value;

                string wrappedText = string.Empty;
                if (value.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    wrappedText = string.Format(linkWrapper, value);
                }
                else
                {
                    wrappedText = string.Format(wrapper, value);
                }

                sentence = sentence.Replace(value, wrappedText);
            }
            return sentence;
        }
    }
}
