using MonitorWpf1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
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

	public async Task<List<OrefAlert>> GetOrefAlertsAsync()
	{
		try
		{
			string url = "https://www.oref.org.il/warningMessages/alert/History/AlertsHistory.json";
			string json = await httpClient.GetStringAsync(url);
			lastAlertReceiveDate = DateTime.Now;

			if (string.IsNullOrWhiteSpace(json) || json == "[]") return new List<OrefAlert>();

			return JsonConvert.DeserializeObject<List<OrefAlert>>(json);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error fetching Oref alerts: " + ex.Message);
			return new List<OrefAlert>();
		}
	}

	//public async Task<List<AlertGroup>> GetOrefAlertsAsync()
	//{
	//	try
	//	{


	//		string url = "https://www.oref.org.il/warningMessages/alert/History/AlertsHistory.json";
	//		string json = await httpClient.GetStringAsync(url);

	//		lastAlertReceiveDate = DateTime.Now;

	//		if (json == "")
	//		{
	//			return new List<AlertGroup>();
	//		}

	//		var alerts = JsonConvert.DeserializeObject<List<OrefAlert>>(json);

	//		// Group the alerts
	//		var groupedAlerts = GroupAlerts(alerts, prevAlerts);

	//		return groupedAlerts; // <- return groups for binding

	//	}
	//	catch (Exception ex)
	//	{
	//		Console.WriteLine("Error fetching Oref alerts: " + ex.Message);
	//		return new List<AlertGroup>();
	//	}
	//}

	public List<AlertGroup> GroupAlerts(List<OrefAlert> alerts)
	{
		if (alerts == null) return new List<AlertGroup>();

		// 1. Group the raw alerts by Date, Category, and Title
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

		// 2. Determine "IsNew" based on time (e.g., last 90 seconds)
		// This removes the need to track "what we saw last time"
		foreach (var group in grouped)
		{
			var ageSeconds = (DateTime.Now - group.AlertDate).TotalSeconds;
			group.IsNew = ageSeconds <= 90;
		}

		return grouped;
	}

	//public List<AlertGroup> GroupAlerts(List<OrefAlert> alerts, List<AlertGroup> lastGroups)
	//{
	//	DateTime lastTimestamp = (lastGroups != null && lastGroups.Any())
	//		? lastGroups.Max(g => g.AlertDate)
	//		: DateTime.MinValue;

	//	var grouped = alerts
	//		.GroupBy(a => new { a.AlertDate, a.Category, a.Title })
	//		.Select(g => new AlertGroup
	//		{
	//			AlertDate = g.Key.AlertDate,
	//			Category = g.Key.Category,
	//			Title = g.Key.Title,
	//			Locations = g.Select(a => a.Location).ToList()
	//		})
	//		.OrderByDescending(g => g.AlertDate)
	//		.ToList();

	//	foreach (var group in grouped)
	//	{
	//		group.IsNew = group.AlertDate > lastTimestamp;
	//	}

	//	return grouped;
	//}

	//old funct.
	private Dictionary<string, AlertStatus> GetMapStatuses(List<AlertGroup> groups)
	{
		var statusMap = new Dictionary<string, AlertStatus>();

		// 1. Map Logic: Flatten and find the "Current State" of each city
		var latestCityStatus = groups
			.SelectMany(g => g.Locations.Select(loc => new {
				Name = loc,
				Date = g.AlertDate,
				Cat = g.Category
			}))
			.GroupBy(x => x.Name)
			.Select(g => g.OrderByDescending(x => x.Date).First()); // Newest info for this city wins

		// We process from oldest to newest so that the NEWEST status for a city wins
		foreach (var group in groups.OrderBy(g => g.AlertDate))
		{
			AlertStatus groupStatus;
			var ageSeconds = (DateTime.Now - group.AlertDate).TotalSeconds;

			if (group.Category == 13) groupStatus = AlertStatus.Finished;
			else if (group.Category == 14) groupStatus = AlertStatus.PreWarning;
			else if (ageSeconds <= 90) groupStatus = AlertStatus.NewAlert;
			else groupStatus = AlertStatus.PostAlert;

			foreach (var loc in group.Locations)
			{
				statusMap[loc] = groupStatus;
			}
		}
		return statusMap;
	}

	public Dictionary<string, AlertStatus> GetMapStatuses(List<OrefAlert> rawAlerts)
	{
		var statusMap = new Dictionary<string, AlertStatus>();
		if (rawAlerts == null) return statusMap;

		DateTime threshold = DateTime.Now.AddMinutes(-10);

		// Filter by time and take the LATEST event for every city
		var latestPerCity = rawAlerts
			.Where(a => a.AlertDate >= threshold)
			.GroupBy(a => a.Location)
			.Select(g => g.OrderByDescending(a => a.AlertDate).First());

		foreach (var alert in latestPerCity)
		{
			AlertStatus status;
			var ageSeconds = (DateTime.Now - alert.AlertDate).TotalSeconds;

			if (alert.Category == 13) status = AlertStatus.Finished; // Green
			else if (alert.Category == 14) status = AlertStatus.PreWarning; // Blue
			else if (ageSeconds <= 90) status = AlertStatus.NewAlert; // Red
			else status = AlertStatus.PostAlert; // Orange

			statusMap[alert.Location] = status;
		}
		return statusMap;
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
		private List<string> LocPrio1 = new List<string>();
		private List<string> LocPrio2 = new List<string>();
		public string LocationsText => string.Join(", ", Locations);
		public string DisplayLocations
		{
			get
			{
				FillPriorityLocations();

				var sortedLocs = Locations
					.OrderByDescending(loc => LocPrio1.Any(p => loc.Contains(p)))
					.ThenByDescending(loc => LocPrio2.Any(p => loc.Contains(p)))
					.ThenBy(l => l) // optional: alphabetical for the rest
					.ToList();

				int maxDisplay = 16;
				var displayed = sortedLocs.Take(maxDisplay).ToList();

				// calculate how many are not shown
				int remaining = sortedLocs.Count - displayed.Count;

				string extra = remaining > 0 ? $" +  עוד {remaining}" : "";

				return string.Join(", ", displayed) + extra;
			}
		}

		public List<string> ReleaseLocations
		{
			get
			{
				FillPriorityLocations();

				var sortedLocs = Locations
					.OrderByDescending(loc => LocPrio1.Any(p => loc.Contains(p)))
					.ThenBy(l => l) // rest alphabetically
					.ToList();

				return sortedLocs;
			}
		}
		public string ReleaseLocationsText => string.Join(", ", ReleaseLocations);

		// new/old coloring
		public bool IsNew { get; set; }

		public Brush GroupColor
		{
			get
			{

				var ageSeconds = (DateTime.Now - AlertDate).TotalSeconds;

				if (Category == 13 && ageSeconds <= 300) return Brushes.GreenYellow;	// let out messages
				if (Category == 14 && ageSeconds <= 600) return Brushes.CornflowerBlue;	// early warning
				if (ageSeconds <= 90) return Brushes.OrangeRed;			// very recent
				if (ageSeconds <= 600) return Brushes.Orange;           // slightly older
				return Brushes.Silver;                                  // old 
			}
		}



		private void FillPriorityLocations()
		{
			if (LocPrio1.Count == 0)
			{
				LocPrio1.Add("מודיעין מכבים רעות");
				//LocPrio1.Add("כפר סבא");
			}

			if (LocPrio2.Count == 0)
			{
				LocPrio2.Add("ירושלים");
				LocPrio2.Add("תל אביב");
				LocPrio2.Add("פתח תקווה");
				LocPrio2.Add("שילת");
				LocPrio2.Add("ראשון לציון");
				LocPrio2.Add("רמלה");
				LocPrio2.Add("לוד");
				LocPrio2.Add("מודיעין");

			}
		}
	}
}



