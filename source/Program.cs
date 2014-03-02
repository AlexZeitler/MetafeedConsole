using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using MetaFeedConsole.Properties;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Logging;


namespace MetaFeedConsole {
	internal class Program {
		private static void Main() {
			IConfigurationSource configurationSource = ConfigurationSourceFactory.Create();
			LogWriterFactory logWriterFactory = new LogWriterFactory(configurationSource);
			Logger.SetLogWriter(logWriterFactory.Create());
			var watch = new Stopwatch();
			watch.Start();
			try {
				MainAsync().Wait();
			}
			catch (Exception e) {
				//Console.WriteLine(e);
			}
			watch.Stop();
			string.Format("Parsed all feeds in {0}s.", watch.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture))
				.ConsoleWrite(ConsoleColor.White);
			Console.ReadLine();
		}

		private static async Task MainAsync() {
			try {
				Logger.Write(new string("-".ToCharArray()[0], 80));

				"DotNetGermanBloggers Meta Feed Console".ConsoleWriteLine(ConsoleColor.Yellow, false);
				Console.WriteLine();

				// read Application Settings
				"Reading application configuration ".ConsoleWrite(ConsoleColor.White);

				var inputBlogFilePath = Settings.Default.InputBlogFilePath;
				var outputRssFeedFilePath = Settings.Default.OutputRssFeedFilePath;
				var outputAtomFeedFilePath = Settings.Default.OutputAtomFeedFilePath;
				string outputFeedTitle;
				string outputFeedLink;
				string outputFeedDescription;
				string outputFeedCopyright;
				string outputFeedGenerator;
				int outputItemsNumber;


				if (string.IsNullOrEmpty(inputBlogFilePath)) {
					throw new ConfigurationErrorsException("InputBlogFilePath is missing.");
				}

				if (string.IsNullOrEmpty(outputRssFeedFilePath)) {
					throw new ConfigurationErrorsException("OutputRssFeedFilePath is missing.");
				}

				if (string.IsNullOrEmpty(outputAtomFeedFilePath)) {
					throw new ConfigurationErrorsException("OutputAtomFeedFilePath is missing.");
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


				"succeeded.".ConsoleWriteLine(ConsoleColor.Green);

				// create List for all items 
				var feedItems = new ConcurrentBag<SyndicationItem>();

				// load xml with all bloggers
				string.Format("Reading {0} ", inputBlogFilePath).ConsoleWrite(ConsoleColor.White);

				var xml = XDocument.Load(inputBlogFilePath);

				"succeeded.".ConsoleWriteLine(ConsoleColor.Green);

				// select all bloggers from xml
				"Selecting Bloggers ".ConsoleWrite(ConsoleColor.White);

				var bloggers = (from b in xml.Descendants("blogger")
					orderby b.Element("name").Value
					select new Blogger {
						Name = b.Element("name").Value,
						BlogUrl = b.Element("blogurl").Value,
						BlogfeedUrl = b.Element("blogfeedurl").Value,
						FeedType = b.Element("feedtype").Value
					}).ToList();


				try {
					var test = bloggers.Count();
					"succeeded".ConsoleWriteLine(ConsoleColor.Green);
				}
				catch (NullReferenceException ex) {
					throw new ConfigurationErrorsException(string.Format("{0} schema invalid.", inputBlogFilePath));
				}


				var client = new HttpClient();

				// read all blogs
				var blogsRead = bloggers.Select(async blogger => {
					var items = await GetFeedItemsForBlogger(blogger, client);
					items.ForEach(feedItems.Add);
				});
				await Task.WhenAll(blogsRead);

				var feedItemsList = feedItems.ToList();

				// sort items by date descending
				"Sorting items ".ConsoleWrite(ConsoleColor.White);

				feedItemsList.Sort(
					(x, y) => DateTime.Compare(y.PublishDate.DateTime, x.PublishDate.DateTime));

				"succeeded.".ConsoleWriteLine(ConsoleColor.Green);

				// create List for items being added to the final meta feed
				List<SyndicationItem> metaFeedItems;

				// get the configured number of items
				if (feedItems.Count >= outputItemsNumber) {
					string.Format("Selecting {0} items ", outputItemsNumber).ConsoleWrite(ConsoleColor.White);
					metaFeedItems = feedItemsList.GetRange(0, outputItemsNumber);
				}
				else {
					string.Format("Selecting {0} items ", feedItems.Count).ConsoleWrite(ConsoleColor.White);
					metaFeedItems = feedItemsList;
				}

				"succeeded.".ConsoleWriteLine(ConsoleColor.Green);


				"Instantiating meta feed ".ConsoleWrite(ConsoleColor.White);
				var metaFeed = new SyndicationFeed(metaFeedItems);
				"succeeded.".ConsoleWriteLine(ConsoleColor.Green);


				// set meta feed title
				string.Format("Setting meta feed title to \"{0}\" ", outputFeedTitle).ConsoleWrite(ConsoleColor.White);
				metaFeed.Title =
					new TextSyndicationContent(outputFeedTitle, TextSyndicationContentKind.Plaintext);
				" succeeded.".ConsoleWriteLine(ConsoleColor.Green);

				//  set meta feed link
				string.Format("Setting meta feed link to \"{0}\" ", outputFeedLink).ConsoleWrite(ConsoleColor.White);
				metaFeed.Links.Add(new SyndicationLink(new Uri(outputFeedLink)));
				"succeeded.".ConsoleWriteLine(ConsoleColor.Green);


				// set meta feed description
				string.Format("Setting meta feed description to \"{0}\" ", outputFeedDescription).ConsoleWrite(ConsoleColor.White);
				metaFeed.Description = new TextSyndicationContent(outputFeedDescription,
					TextSyndicationContentKind.Plaintext);
				"succeeded.".ConsoleWriteLine(ConsoleColor.Green);


				// set meta feed copyright
				string.Format("Setting meta feed copyright to \"{0}\" ", outputFeedCopyright).ConsoleWrite(ConsoleColor.White);
				metaFeed.Copyright = new TextSyndicationContent(outputFeedCopyright,
					TextSyndicationContentKind.Plaintext);
				"succeeded.".ConsoleWriteLine(ConsoleColor.Green);


				// set meta feed generator
				string.Format("Setting meta feed generator to \"{0}\" ", outputFeedGenerator).ConsoleWrite(ConsoleColor.White);
				metaFeed.Generator = outputFeedGenerator;
				"succeeded.".ConsoleWriteLine(ConsoleColor.Green);

				var settings = new XmlWriterSettings {Encoding = new UTF8Encoding(), Indent = true};
				using (var writer = XmlWriter.Create(outputRssFeedFilePath, settings)) {
					string.Format("Writing RSS meta feed to \"{0}\" ", outputRssFeedFilePath).ConsoleWrite(ConsoleColor.White);
					metaFeed.SaveAsRss20(writer);
					"succeeded.".ConsoleWriteLine(ConsoleColor.Green);
				}

				using (var writer = XmlWriter.Create(outputAtomFeedFilePath, settings)) {
					string.Format("Writing ATOM meta feed to \"{0}\" ", outputAtomFeedFilePath).ConsoleWrite(ConsoleColor.White);
					metaFeed.SaveAsAtom10(writer);
					"succeeded.".ConsoleWriteLine(ConsoleColor.Green);
				}

				"completed".ConsoleWriteLine(ConsoleColor.Green);
				string.Empty.ConsoleWriteLine(ConsoleColor.Gray);
			}
			catch (Exception ex) {
				"failed with exception:".ConsoleWriteLine(ConsoleColor.Red);
				if (null != ex.InnerException) {
					ex.InnerException.ToString().ConsoleWriteLine(ConsoleColor.Red);
					ex.Message.ConsoleWriteLine(ConsoleColor.Red);
					// throw;
				}
				else {
					ex.Message.ConsoleWriteLine(ConsoleColor.Red);
					// throw;
				}
			}
		}

		private static async Task<List<SyndicationItem>> GetFeedItemsForBlogger(Blogger blogger, HttpClient client) {
			var feedItems = new List<SyndicationItem>();
			var timer = Stopwatch.StartNew();
			String.Format("Parsing / adding Items from \"{0}\" ", blogger.Name).ConsoleWriteLine(ConsoleColor.White);

			try {
				// check feed type
				switch (blogger.FeedType.ToLower()) {
					case "rss":


						var rssItems = await GetRssItemsForBlogger(blogger, client);
						rssItems.ForEach(feedItems.Add);
						break;


					case "atom":
						var atomItems = await GetAtomItemsForBlogger(blogger, client);
						atomItems.ForEach(feedItems.Add);
						break;
					default:
						break;
				}
				String.Format("Successfully parsed Items from \"{0}\" in {1}s. ", blogger.Name, timer.Elapsed.TotalSeconds)
					.ConsoleWriteLine(ConsoleColor.Green);
				return feedItems;
			}
			catch (Exception ex) {
				timer.Stop();
				String.Format("Failed Parsing / adding Items from \"{0}\"", blogger.Name).ConsoleWriteLine(ConsoleColor.White);
				string.Format("With exception {0}", ex.Message).ConsoleWriteLine(ConsoleColor.Red);
				return feedItems;
				// throw;
			}
		}

		private static async Task<List<SyndicationItem>> GetRssItemsForBlogger(Blogger blogger, HttpClient client) {
			var rssItems = new List<SyndicationItem>();
			// parse Rss feed
			var response = await client.GetAsync(blogger.BlogfeedUrl);
			var stream = await response.Content.ReadAsStreamAsync();


			var rssStreamReader = new StreamReader(stream, Encoding.UTF8);


			var rssReader = XmlReader.Create(rssStreamReader);
			var rssSerializer = new Rss20FeedFormatter();
			rssSerializer.ReadFrom(rssReader);
			var rssFeed = rssSerializer.Feed;
			foreach (var item in rssFeed.Items) {
				var newItem = new SyndicationItem {BaseUri = item.BaseUri, Content = item.Content};

				var copyright =
					new TextSyndicationContent(blogger.Name);

				newItem.Copyright = copyright;

				newItem.Id = item.Id;
				newItem.LastUpdatedTime = item.LastUpdatedTime;

				foreach (var link in item.Links) {
					newItem.Links.Add(link);
				}
				newItem.PublishDate = item.PublishDate;
				newItem.Summary = item.Summary;
				newItem.Title = item.Title;

				if (item.ElementExtensions.Count > 0) {
					var reader = item.ElementExtensions.GetReaderAtElementExtensions();
					while (reader.Read()) {
						if ("content:encoded" == reader.Name) {
							SyndicationContent content =
								SyndicationContent.CreateHtmlContent(reader.ReadString());
							newItem.Content = content;
						}
					}
				}


				//assign author name explicitly because email is
				//used by default
				var author = new SyndicationPerson {Name = blogger.Name};
				newItem.Authors.Add(author);

				newItem.Contributors.Add(author);

				var doc = new XmlDocument();
				var creator = String.Format(
					"<dc:creator xmlns:dc=\"http://purl.org/dc/elements/1.1/\">{0}</dc:creator>",
					blogger.Name);
				doc.LoadXml(creator);
				var insertext = new SyndicationElementExtension(new XmlNodeReader(doc.DocumentElement));

				newItem.ElementExtensions.Add(insertext);

				rssItems.Add(newItem);
			}
			return rssItems;
		}

		private static async Task<List<SyndicationItem>> GetAtomItemsForBlogger(Blogger blogger, HttpClient client) {
			var atomItems = new List<SyndicationItem>();
// parse Atom feed
			var atomResponse = await client.GetAsync(blogger.BlogfeedUrl);
			var atomStream = await atomResponse.Content.ReadAsStreamAsync();

			var atomStreamReader = new StreamReader(atomStream, Encoding.UTF8);

			var atomReader = XmlReader.Create(atomStreamReader);
			var atomSerializer = new Atom10FeedFormatter();
			atomSerializer.ReadFrom(atomReader);
			var atomFeed = atomSerializer.Feed;

			foreach (var item in atomFeed.Items) {
				var newItem = new SyndicationItem {
					BaseUri = item.BaseUri,
					Content = item.Content,
					Id = item.Id,
					LastUpdatedTime = item.LastUpdatedTime
				};

				foreach (var link in item.Links) {
					newItem.Links.Add(link);
				}
				newItem.PublishDate = item.PublishDate;
				newItem.Summary = item.Summary;
				newItem.Title = item.Title;

				var copyright =
					new TextSyndicationContent(blogger.Name);

				newItem.Copyright = copyright;


				if (item.ElementExtensions.Count > 0) {
					var reader = item.ElementExtensions.GetReaderAtElementExtensions();
					while (reader.Read()) {
						if ("content:encoded" == reader.Name) {
							SyndicationContent content =
								SyndicationContent.CreatePlaintextContent(reader.ReadString());
							newItem.Content = content;
						}
					}
				}


				// assign author name explicitly because email is
				// used by default
				var author = new SyndicationPerson {Name = blogger.Name};
				newItem.Authors.Add(author);

				newItem.Contributors.Add(author);

				var doc = new XmlDocument();
				var creator = String.Format(
					"<dc:creator xmlns:dc=\"http://purl.org/dc/elements/1.1/\">{0}</dc:creator>",
					blogger.Name);
				doc.LoadXml(creator);
				var insertext = new SyndicationElementExtension(new XmlNodeReader(doc.DocumentElement));

				newItem.ElementExtensions.Add(insertext);

				atomItems.Add(newItem);
			}
			return atomItems;
		}
	}
}