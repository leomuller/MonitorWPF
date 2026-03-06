using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class NewsService
{
	private static readonly HttpClient httpClient = new HttpClient();

	public async Task<List<YnetItem>> GetYnetNewsAsync()
	{
		try
		{
			string url = "https://www.ynet.co.il/news/category/184";
			string html = await httpClient.GetStringAsync(url);

			// Find the widget block
			var pattern = @"<script>window\.YITSiteWidgets\.push\(\['(\w+)',\s*'Accordion',";
			var match = Regex.Match(html, pattern);

			if (!match.Success)
			{
				return new List<YnetItem>();
			}

			string variablePart = match.Groups[1].Value;
			string startMarker = $"<script>window.YITSiteWidgets.push(['{variablePart}','Accordion',";

			int startIndex = html.IndexOf(startMarker);
			if (startIndex < 0)
			{
				return new List<YnetItem>();
			}

			startIndex += startMarker.Length;

			int endIndex = html.IndexOf("}]);", startIndex);
			if (endIndex < 0)
			{
				return new List<YnetItem>();
			}

			string jsonSnippet = html.Substring(startIndex, endIndex - startIndex + 1);

			// Deserialize JSON
			YnetResponse data = JsonConvert.DeserializeObject<YnetResponse>(jsonSnippet);

			if (data != null && data.items != null)
			{
				return data.items;
			}

			return new List<YnetItem>();
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error fetching Ynet news: " + ex.Message);
			return new List<YnetItem>();
		}
	}

	public class YnetResponse
	{
		public List<YnetItem> items { get; set; }
	}

	public class YnetItem
	{
		public string title { get; set; }
		public string text { get; set; }
		public string shareUrl { get; set; }
		public DateTime date { get; set; }
	}




}