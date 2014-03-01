using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.Logging;


namespace MetaFeedConsole {
    class Program {

		static void Main()
		{
			var watch = new Stopwatch();
			watch.Start();
			MainAsync().Wait();
			watch.Stop();
			ConsoleWrite(watch.Elapsed.Seconds.ToString(), ConsoleColor.Red);
			Console.ReadLine();
		}

        static async Task MainAsync()
        {
            try {
				//// Logger.Write(new string("-".ToCharArray()[0], 80));

                ConsoleWriteLine("DotNetGermanBloggers Meta Feed Console", ConsoleColor.Yellow, false);
                Console.WriteLine();

                // read Application Settings
                ConsoleWrite("Reading application configuration ", ConsoleColor.White);

                string inputBlogFilePath = string.Empty;
                string outputRssFeedFilePath = string.Empty;
                string outputAtomFeedFilePath = string.Empty;
                string outputFeedTitle = string.Empty;
                string outputFeedLink = string.Empty;
                string outputFeedDescription = string.Empty;
                string outputFeedCopyright = string.Empty;
                string outputFeedGenerator = string.Empty;
                string outputFeedImageUrl = string.Empty;
                int outputItemsNumber = 0;


                if (string.IsNullOrEmpty(ConfigurationSettings.AppSettings["InputBlogFilePath"])) {
                    throw new ConfigurationErrorsException("InputBlogFilePath is missing.");
                }
                else {
                    inputBlogFilePath =
                        ConfigurationSettings.AppSettings["InputBlogFilePath"];
                }

                if (string.IsNullOrEmpty(ConfigurationSettings.AppSettings["OutputRssFeedFilePath"])) {
                    throw new ConfigurationErrorsException("OutputRssFeedFilePath is missing.");
                }
                else {
                    outputRssFeedFilePath =
                        ConfigurationSettings.AppSettings["OutputRssFeedFilePath"];
                }

                if (string.IsNullOrEmpty(ConfigurationSettings.AppSettings["OutputAtomFeedFilePath"])) {
                    throw new ConfigurationErrorsException("OutputAtomFeedFilePath is missing.");
                }
                else {
                    outputAtomFeedFilePath =
                    ConfigurationSettings.AppSettings["OutputAtomFeedFilePath"];
                }

                if (string.IsNullOrEmpty(ConfigurationSettings.AppSettings["OutputFeedTitle"])) {
                    throw new ConfigurationErrorsException("OutputFeedTitle is missing.");
                }
                else {
                    outputFeedTitle = ConfigurationSettings.AppSettings["OutputFeedTitle"];
                }


                if (string.IsNullOrEmpty(ConfigurationSettings.AppSettings["OutputFeedLink"])) {
                    throw new ConfigurationErrorsException("OutputFeedLink is missing.");
                }
                else {
                    outputFeedLink = ConfigurationSettings.AppSettings["OutputFeedLink"];
                }

                if (string.IsNullOrEmpty(ConfigurationSettings.AppSettings["OutputFeedDescription"])) {
                    throw new ConfigurationErrorsException("OutputFeedDescription is missing.");
                }
                else {
                    outputFeedDescription = ConfigurationSettings.AppSettings["OutputFeedDescription"];
                }


                if (string.IsNullOrEmpty(ConfigurationSettings.AppSettings["OutputFeedCopyright"])) {
                    throw new ConfigurationErrorsException("OutputFeedCopyright is missing");
                }
                else {
                    outputFeedCopyright = ConfigurationSettings.AppSettings["OutputFeedCopyright"];
                }

                if (string.IsNullOrEmpty(ConfigurationSettings.AppSettings["OutputFeedGenerator"])) {
                    throw new ConfigurationErrorsException("OutputFeedGenerator is missing.");
                }
                else {
                    outputFeedGenerator = ConfigurationSettings.AppSettings["OutputFeedGenerator"];
                }

                if (string.IsNullOrEmpty(ConfigurationSettings.AppSettings["OutputFeedImageUrl"])) {
                    throw new ConfigurationErrorsException("OutputFeedImageUrl is missing.");
                }
                else {
                    outputFeedImageUrl = ConfigurationSettings.AppSettings["OutputFeedImageUrl"];
                }


                if (string.IsNullOrEmpty(
                    ConfigurationSettings.AppSettings["OutputItemsNumber"])) {
                    throw new ConfigurationErrorsException("OutputItemsNumber is missing.");
                }
                else {
                    if (!(int.TryParse(ConfigurationSettings.AppSettings["OutputItemsNumber"], out outputItemsNumber))) {
                        throw new ConfigurationErrorsException("OutputItemsNumber has an invalid format. Int32 expected.");
                    }
                }




                ConsoleWriteLine("succeeded.", ConsoleColor.Green);

                // create List for all items 
                var feedItems = new ConcurrentBag<SyndicationItem>();

                // load xml with all bloggers
                ConsoleWrite(string.Format("Reading {0} ", inputBlogFilePath), ConsoleColor.White);

                XDocument xml = XDocument.Load(inputBlogFilePath);

                ConsoleWriteLine("succeeded.", ConsoleColor.Green);

                // select all bloggers from xml
                ConsoleWrite("Selecting Bloggers ", ConsoleColor.White);

                IEnumerable<Blogger> bloggers = from b in xml.Descendants("blogger")
                               orderby b.Element("name").Value ascending
                               select new Blogger
                               {
                                   name = b.Element("name").Value,
                                   blogurl = b.Element("blogurl").Value,
                                   blogfeedurl = b.Element("blogfeedurl").Value,
                                   feedtype = b.Element("feedtype").Value
                               };


                try {
                    int test = bloggers.Count();
                    ConsoleWriteLine("succeeded", ConsoleColor.Green);
                }
                catch (Exception ex) {
                    if (ex.GetType() == typeof(NullReferenceException)) {
                        throw new ConfigurationErrorsException(string.Format("{0} schema invalid.", inputBlogFilePath));
                    }
                }

                // iterate through bloggers

	            var client = new HttpClient();

	            var tasks = bloggers.Select(async item =>
	            {
		            // some pre stuff
		            await GetItems(item, client, feedItems);

		            // some post stuff
	            });
	            await Task.WhenAll(tasks);
				

				//foreach (var blogger in bloggers)
				//{
				//	await GetItems(blogger, client, feedItems);
				//}


	            // sort items by date descending

                ConsoleWrite("Sorting items ", ConsoleColor.White);

	            var feedItemsList = feedItems.ToList();

				feedItemsList.Sort(
                    delegate(SyndicationItem x, SyndicationItem y) {
                        return DateTime.Compare(y.PublishDate.DateTime, x.PublishDate.DateTime);
                    });

                ConsoleWriteLine("succeeded.", ConsoleColor.Green);

                // create List for items being added to the final meta feed
                List<SyndicationItem> metaFeedItems = new List<SyndicationItem>();

                // get the configured number of items
                if (feedItems.Count >= outputItemsNumber) {
                    ConsoleWrite(string.Format("Selecting {0} items ", outputItemsNumber), ConsoleColor.White);
					metaFeedItems = feedItemsList.GetRange(0, outputItemsNumber);
                }
                else {
                    ConsoleWrite(string.Format("Selecting {0} items ", feedItems.Count), ConsoleColor.White);
					metaFeedItems = feedItemsList;
                }

                ConsoleWriteLine("succeeded.", ConsoleColor.Green);


                //foreach (SyndicationItem item in metaFeedItems) {
                //    SyndicationElementExtension delext = null;
                //    XmlDocument doc = new XmlDocument();
                //    string author = string.Format("<dc:creator xmlns:dc=\"http://purl.org/dc/elements/1.1/\">{0}</dc:creator>", item.Authors[0].Name);
                //    doc.LoadXml(author);
                //    SyndicationElementExtension insertext = new SyndicationElementExtension(new XmlNodeReader(doc.DocumentElement));
                //    bool foundcreator = false;
                //    foreach (SyndicationElementExtension ext in item.ElementExtensions) {
                //        if (false == foundcreator) {
                //            XmlReader reader = ext.GetReader();
                //            while (reader.Read()) {
                //                if (("creator" == reader.LocalName) && ("dc" == reader.Prefix)) {
                //                    delext = ext;
                //                    foundcreator = true;
                //                }

                //            }
                //            if (foundcreator == true) {
                //                break;
                //            }
                //        }
                //    }
                //    if ((null != delext) && (true == foundcreator)) {

                //        item.ElementExtensions.Remove(delext);
                //    }
                //    item.ElementExtensions.Add(insertext);
                //    //}



                //}


                // create meta feed with the selected items

                ConsoleWrite("Instantiating meta feed ", ConsoleColor.White);
                SyndicationFeed metaFeed = new SyndicationFeed(metaFeedItems);
                ConsoleWriteLine("succeeded.", ConsoleColor.Green);


                // set meta feed title
                ConsoleWrite(string.Format("Setting meta feed title to \"{0}\" ", outputFeedTitle), ConsoleColor.White);
                metaFeed.Title =
                    new TextSyndicationContent(outputFeedTitle, TextSyndicationContentKind.Plaintext);
                ConsoleWriteLine(" succeeded.", ConsoleColor.Green);

                //  set meta feed link
                ConsoleWrite(string.Format("Setting meta feed link to \"{0}\" ", outputFeedLink), ConsoleColor.White);
                metaFeed.Links.Add(new SyndicationLink(new Uri(outputFeedLink)));
                ConsoleWriteLine("succeeded.", ConsoleColor.Green);


                // set meta feed description
                ConsoleWrite(string.Format("Setting meta feed description to \"{0}\" ", outputFeedDescription), ConsoleColor.White);
                metaFeed.Description = new TextSyndicationContent(outputFeedDescription,
                    TextSyndicationContentKind.Plaintext);
                ConsoleWriteLine("succeeded.", ConsoleColor.Green);


                // set meta feed copyright
                ConsoleWrite(string.Format("Setting meta feed copyright to \"{0}\" ", outputFeedCopyright), ConsoleColor.White);
                metaFeed.Copyright = new TextSyndicationContent(outputFeedCopyright,
                    TextSyndicationContentKind.Plaintext);
                ConsoleWriteLine("succeeded.", ConsoleColor.Green);


                // set meta feed generator
                ConsoleWrite(string.Format("Setting meta feed generator to \"{0}\" ", outputFeedGenerator), ConsoleColor.White);
                metaFeed.Generator = outputFeedGenerator;
                ConsoleWriteLine("succeeded.", ConsoleColor.Green);


                // set meta feed image url
                //ConsoleWrite(string.Format("Setting meta feed image url to \"{0}\" ", outputFeedImageUrl), ConsoleColor.White);
                //metaFeed.ImageUrl = new Uri(outputFeedImageUrl);
                //ConsoleWriteLine("succeeded.", ConsoleColor.Green);

                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Encoding = new UTF8Encoding();
                settings.Indent = true;
                using (XmlWriter writer = XmlWriter.Create(outputRssFeedFilePath, settings)) {
                    ConsoleWrite(string.Format("Writing RSS meta feed to \"{0}\" ", outputRssFeedFilePath), ConsoleColor.White);
                    metaFeed.SaveAsRss20(writer);
                    ConsoleWriteLine("succeeded.", ConsoleColor.Green);

                }

                using (XmlWriter writer = XmlWriter.Create(outputAtomFeedFilePath, settings)) {
                    ConsoleWrite(string.Format("Writing ATOM meta feed to \"{0}\" ", outputAtomFeedFilePath), ConsoleColor.White);
                    metaFeed.SaveAsAtom10(writer);
                    ConsoleWriteLine("succeeded.", ConsoleColor.Green);

                }

                ConsoleWriteLine("completed", ConsoleColor.Green);
                // Logger.Write(new string("-".ToCharArray()[0], 80));
                ConsoleWriteLine(string.Empty, ConsoleColor.Gray);
            }
            catch (Exception ex) {
                ConsoleWriteLine("failed with exception:", ConsoleColor.Red);
                if (null != ex.InnerException) {
                    ConsoleWriteLine(ex.InnerException.ToString(), ConsoleColor.Red);
                    ConsoleWriteLine(ex.Message, ConsoleColor.Red);
                }
                else {
                    ConsoleWriteLine(ex.Message, ConsoleColor.Red);
                }
            }
            finally {
            }
            //Console.ReadLine();

        }

	    private static async Task GetItems(Blogger blogger, HttpClient client, ConcurrentBag<SyndicationItem> feedItems)
	    {
		    ConsoleWrite(String.Format((string) "Parsing / adding Items from \"{0}\" ",  blogger.name),
			    ConsoleColor.White);

		    try
		    {
			    // check feed type
			    switch (blogger.feedtype.ToLower())
			    {
				    case "rss":

					    // parse Rss feed
					    var rssSerializer = new Rss20FeedFormatter();
					    WebResponse rssWebResponse;
					    //HttpWebRequest rssWebRequest = (HttpWebRequest) System.Net.WebRequest.Create(blogger.blogfeedurl);
					    //rssWebRequest.UserAgent = "DotNetGerman Bloggers";
					    //rssWebResponse = rssWebRequest.GetResponse();

					    var response = await client.GetAsync((string) blogger.blogfeedurl);
					    var stream = await response.Content.ReadAsStreamAsync();


					    StreamReader rssStreamReader = new StreamReader(stream, Encoding.UTF8);


					    XmlReader rssReader = XmlReader.Create(rssStreamReader);
					    rssSerializer.ReadFrom(rssReader);
					    SyndicationFeed rssFeed = rssSerializer.Feed;
					    foreach (SyndicationItem item in rssFeed.Items)
					    {
						    SyndicationItem newItem = new SyndicationItem();
						    newItem.BaseUri = item.BaseUri;
						    //newItem.Categories = item.Categories;

						    newItem.Content = item.Content;
						    //newItem.ElementExtensions = item.ElementExtensions;

						    TextSyndicationContent copyright =
							    new TextSyndicationContent(blogger.name);

						    newItem.Copyright = copyright;

						    newItem.Id = item.Id;
						    newItem.LastUpdatedTime = item.LastUpdatedTime;
						    //newItem.Links = item.Links;
						    foreach (SyndicationLink link in item.Links)
						    {
							    newItem.Links.Add(link);
						    }
						    newItem.PublishDate = item.PublishDate;
						    newItem.Summary = item.Summary;
						    newItem.Title = item.Title;

						    if (item.ElementExtensions.Count > 0)
						    {
							    XmlReader reader = item.ElementExtensions.GetReaderAtElementExtensions();
							    while (reader.Read())
							    {
								    if ("content:encoded" == reader.Name)
								    {
									    SyndicationContent content =
										    SyndicationContent.CreateHtmlContent(reader.ReadString());
									    newItem.Content = content;
								    }
							    }
						    }


						    //assign author name explicitly because email is
						    //used by default
						    SyndicationPerson author = new SyndicationPerson();
						    author.Name = blogger.name;
						    newItem.Authors.Add(author);

						    newItem.Contributors.Add(author);

						    XmlDocument doc = new XmlDocument();
						    string creator = String.Format((string) "<dc:creator xmlns:dc=\"http://purl.org/dc/elements/1.1/\">{0}</dc:creator>",
							    (object) blogger.name);
						    doc.LoadXml(creator);
						    SyndicationElementExtension insertext = new SyndicationElementExtension(new XmlNodeReader(doc.DocumentElement));

						    newItem.ElementExtensions.Add(insertext);

						    feedItems.Add(newItem);
					    }
					    break;


				    case "atom":

					    // parse Atom feed
					    Atom10FeedFormatter atomSerializer = new Atom10FeedFormatter();
					    WebResponse atomWebResponse;
					    HttpWebRequest atomWebRequest = (HttpWebRequest) WebRequest.Create((string) blogger.blogfeedurl);
					    atomWebRequest.UserAgent = "DotNetGerman Bloggers";
					    atomWebResponse = atomWebRequest.GetResponse();

					    StreamReader atomStreamReader = new StreamReader(atomWebResponse.GetResponseStream(), Encoding.UTF8);

					    XmlReader atomReader = XmlReader.Create(atomStreamReader);
					    atomSerializer.ReadFrom(atomReader);
					    SyndicationFeed atomFeed = atomSerializer.Feed;

					    foreach (SyndicationItem item in atomFeed.Items)
					    {
						    SyndicationItem newItem = new SyndicationItem();
						    newItem.BaseUri = item.BaseUri;
						    //newItem.Categories = item.Categories;

						    newItem.Content = item.Content;
						    //newItem.ElementExtensions = item.ElementExtensions;

						    newItem.Id = item.Id;
						    newItem.LastUpdatedTime = item.LastUpdatedTime;
						    //newItem.Links = item.Links;
						    foreach (SyndicationLink link in item.Links)
						    {
							    newItem.Links.Add(link);
						    }
						    newItem.PublishDate = item.PublishDate;
						    newItem.Summary = item.Summary;
						    newItem.Title = item.Title;

						    TextSyndicationContent copyright =
							    new TextSyndicationContent(blogger.name);

						    newItem.Copyright = copyright;


						    if (item.ElementExtensions.Count > 0)
						    {
							    XmlReader reader = item.ElementExtensions.GetReaderAtElementExtensions();
							    while (reader.Read())
							    {
								    if ("content:encoded" == reader.Name)
								    {
									    SyndicationContent content =
										    SyndicationContent.CreatePlaintextContent(reader.ReadString());
									    newItem.Content = content;
								    }
							    }
						    }


						    // assign author name explicitly because email is
						    // used by default
						    SyndicationPerson author = new SyndicationPerson();
						    author.Name = blogger.name;
						    newItem.Authors.Add(author);

						    newItem.Contributors.Add(author);

						    XmlDocument doc = new XmlDocument();
						    string creator = String.Format((string) "<dc:creator xmlns:dc=\"http://purl.org/dc/elements/1.1/\">{0}</dc:creator>",
							    (object) blogger.name);
						    doc.LoadXml(creator);
						    SyndicationElementExtension insertext = new SyndicationElementExtension(new XmlNodeReader(doc.DocumentElement));

						    newItem.ElementExtensions.Add(insertext);

						    feedItems.Add(newItem);
					    }
					    break;
				    default:
					    break;
			    }
			    ConsoleWriteLine("succeeded.", ConsoleColor.Green);
		    }
		    catch (Exception ex)
		    {
			    ConsoleWriteLine(string.Format("failed with exception {0}", ex.Message), ConsoleColor.Red);
		    }
	    }

	    static void ConsoleWrite(string Text, ConsoleColor ForegroundColor)
        {
            ConsoleWrite(Text, ForegroundColor, ConsoleColor.Black, false, true);
        }

        static void ConsoleWrite(string Text, ConsoleColor ForegroundColor, bool WriteToLog)
        {
            ConsoleWrite(Text, ForegroundColor, ConsoleColor.Black, false, WriteToLog);
        }

        static void ConsoleWrite(string Text, ConsoleColor ForegroundColor, ConsoleColor BackgroundColor, bool WriteToLog)
        {
            ConsoleWrite(Text, ForegroundColor, BackgroundColor, false, WriteToLog);
        }

        static void ConsoleWriteLine(string Text, ConsoleColor ForegroundColor, ConsoleColor BackgroundColor, bool WriteToLog)
        {
            ConsoleWrite(Text, ForegroundColor, BackgroundColor, true, WriteToLog);
        }

        static void ConsoleWriteLine(string Text, ConsoleColor ForegroundColor, bool WriteToLog)
        {
            ConsoleWrite(Text, ForegroundColor, ConsoleColor.Black, true, WriteToLog);
        }


        static void ConsoleWriteLine(string Text, ConsoleColor ForegroundColor)
        {
            ConsoleWrite(Text, ForegroundColor, ConsoleColor.Black, true, true);
        }

        static void ConsoleWrite(string Text, ConsoleColor ForegroundColor, ConsoleColor BackgroundColor, bool WriteLine, bool WriteToLog)
        {
            Console.BackgroundColor = BackgroundColor;
            Console.ForegroundColor = ForegroundColor;
            if (true == WriteLine) {
                Console.WriteLine(Text);
            }
            else {
                Console.Write(Text);
            }
            if (true == WriteToLog) {
                // Logger.Write(Text);
            }
        }
    }

	 class Blogger {
		 public string name { get; set; }
		 public string blogurl { get; set; }
		 public string blogfeedurl { get; set; }
		 public string feedtype { get; set; }
	 }
}
