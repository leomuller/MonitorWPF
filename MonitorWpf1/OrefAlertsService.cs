using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;


public class OrefAlertsService
{
	private static readonly HttpClient httpClient = new HttpClient();
	private static List<AlertGroup> prevAlerts = new List<AlertGroup>();
	public DateTime lastAlertReceiveDate = new DateTime(2026, 1, 1);

	public async Task<List<AlertGroup>> GetOrefAlertsAsync()
	{
		try
		{
			string url = "https://www.oref.org.il/warningMessages/alert/History/AlertsHistory.json";
			string json = await httpClient.GetStringAsync(url);

			lastAlertReceiveDate = DateTime.Now;

			if (json == "")
			{
				return new List<AlertGroup>();
			}

			var alerts = JsonConvert.DeserializeObject<List<OrefAlert>>(json);

			// Group the alerts
			var groupedAlerts = GroupAlerts(alerts, prevAlerts);

			return groupedAlerts; // <- return groups for binding

		}
		catch (Exception ex)
		{
			Console.WriteLine("Error fetching Oref alerts: " + ex.Message);
			return new List<AlertGroup>();
		}
	}

	public List<AlertGroup> GroupAlerts(List<OrefAlert> alerts, List<AlertGroup> lastGroups)
	{
		DateTime lastTimestamp = (lastGroups != null && lastGroups.Any())
			? lastGroups.Max(g => g.AlertDate)
			: DateTime.MinValue;

		var grouped = alerts
			.GroupBy(a => new { a.AlertDate, a.Category, a.Title })
			.Select(g => new AlertGroup
			{
				AlertDate = g.Key.AlertDate,
				Category = g.Key.Category,
				Title = g.Key.Title,
				Locations = g.Select(a => a.Location).ToList()
			})
			.OrderByDescending(g => g.AlertDate)
			.ToList();

		foreach (var group in grouped)
		{
			group.IsNew = group.AlertDate > lastTimestamp;
		}

		return grouped;
	}

	public class OrefAlert
	{
		[JsonProperty("alertDate")]
		public DateTime AlertDate { get; set; }

		[JsonProperty("title")]
		public string Title { get; set; }

		[JsonProperty("data")]
		public string Location { get; set; }  // maps "data" in JSON

		[JsonProperty("category")]
		public int Category { get; set; }
	}

	public class AlertGroup
	{
		public DateTime AlertDate { get; set; }
		public int Category { get; set; }
		public string Title { get; set; }
		public List<string> Locations { get; set; } = new List<string>();

		// computed for display
		public string LocationsText => string.Join(", ", Locations);

		// new/old coloring
		public bool IsNew { get; set; }

		public Brush GroupColor
		{
			get
			{

				var ageSeconds = (DateTime.Now - AlertDate).TotalSeconds;

				if (Category == 13 && ageSeconds <= 600) return Brushes.CornflowerBlue; // finished messages
				if (Category == 14) return Brushes.Salmon;				// early warning
				if (ageSeconds <= 90) return Brushes.OrangeRed;			// very recent
				if (ageSeconds <= 600) return Brushes.Orange;           // slightly older
				return Brushes.Silver;									// old
			}
		}
	}
}



