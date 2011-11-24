using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Web;
using System.Xml;

namespace NugetDownloadCountFeed
{
    public class FeedHandler : IHttpHandler
    {
        private const string NugetServiceUri = "http://packages.nuget.org/v1/FeedService.svc";
        private readonly IDictionary<string, IList<SyndicationItem>> packageDownloadCounts = new ConcurrentDictionary<string, IList<SyndicationItem>>();

        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            var packageName = context.Request.QueryString["packageId"];

            var nugetContext = new Nuget.GalleryFeedContext(new Uri(NugetServiceUri));
            var last = (from x in nugetContext.Packages where x.Id == packageName && x.IsLatestVersion select new { x.DownloadCount, x.Version }).First();

            var items = GetSyndicationItems(packageName, last.DownloadCount);
            var nugetUrl = string.Format(
                "{0}/Packages(Id='{1}',Version='{2}')", NugetServiceUri, packageName, last.Version);

            var feed = new SyndicationFeed("Nuget Download Count Feed",
                                           "Provides the current total download count for a Nuget Package",
                                           new Uri(nugetUrl), nugetUrl, items.Last().LastUpdatedTime,
                                           items);
            using (var xmlWriter = XmlWriter.Create(context.Response.OutputStream))
            {
                feed.SaveAsRss20(xmlWriter);
                xmlWriter.Flush();
                xmlWriter.Close();
            }

            context.Response.ContentType = "text/xml";
            context.Response.End();
        }

        private IList<SyndicationItem> GetSyndicationItems(string packageName, int count)
        {
            IList<SyndicationItem> items;
            lock (packageName)
            {
                if (packageDownloadCounts.ContainsKey(packageName))
                    items = packageDownloadCounts[packageName];
                else
                {
                    items = new List<SyndicationItem>();
                    packageDownloadCounts.Add(packageName, items);
                }
                var title = string.Format("{0} has {1} total downloads", packageName, count);

                if (!items.Any(x => x.Title.Text == title))
                    items.Add(new SyndicationItem(
                                                               title,
                                                               "",
                                                               new Uri(string.Format("http://nuget.org/packages/{0}",
                                                                                     packageName)), Guid.NewGuid().ToString(),
                                                               new DateTimeOffset(DateTime.UtcNow)));
                while (items.Count > 20)
                    items.RemoveAt(0);
            }

            return items;
        }
    }
}
