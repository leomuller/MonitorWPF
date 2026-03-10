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
using static MonitorWpf1.MapWindow;

#nullable disable	//supresses nullable warnings. 

public class OrefAlertsService
{
	private static HttpClient httpClient;
	public DateTime lastAlertReceiveDate;
	public Dictionary<string, MapLocation> dicMapLocations;
	private List<MapLocation> mapLocations;
	public List<String> missingLocations;
	public List<OrefAlert> lastOrefAlerts;

	public List<AlertGroup> GroupedAlerts
	{
		get
		{
			return GroupAlerts(lastOrefAlerts);
		}
	}

	public List<AlertGroup> GroupedFinishedAlerts
	{
		get
		{
			return GroupedAlerts
				.Where(x => x.Category == 13)
				.OrderByDescending(a => a.AlertDate)
				.ToList();
		}
	}

	public OrefAlertsService()
	{
		//constructor.
		httpClient  = new HttpClient();
		lastAlertReceiveDate = new DateTime(2026, 1, 1);
		dicMapLocations = new Dictionary<string, MapLocation>();
		mapLocations = new List<MapLocation>();
		lastOrefAlerts = new List<OrefAlert>();
		missingLocations = new List<String>();

		LoadMapFiles();
	}

	private void LoadMapFiles()
	{
		string MapLocationsFilePath = @"C:\DevLeo\PR2025\MonitorWpf1\MonitorWpf1\Data\MapLocations.json";
		string MissingLocationsFilePath = @"C:\DevLeo\PR2025\MonitorWpf1\MonitorWpf1\Data\MissingLocations.json";


		//load the MapLocations
		if (System.IO.File.Exists(MapLocationsFilePath) == true)
		{
			string jsonMapLocations = System.IO.File.ReadAllText(MapLocationsFilePath, Encoding.UTF8);
			mapLocations = JsonConvert.DeserializeObject<List<MapLocation>>(jsonMapLocations);
		}
		else
		{
			System.Diagnostics.Debug.WriteLine("Failed to load dictionary JSON");
		}


		//load the Missing Locations (so we can log only new ones)
		if (System.IO.File.Exists(MissingLocationsFilePath) == true)
		{
			string jsonMissingLocations = System.IO.File.ReadAllText(MissingLocationsFilePath, Encoding.UTF8);
			//missingLocations = JsonConvert.DeserializeObject<List<MapLocation>>(jsonMissingLocations);
			missingLocations = JsonConvert.DeserializeObject<List<MapLocation>>("[" + jsonMissingLocations.TrimEnd(',') + "]").Select(x => x.Name).ToList();

		}
		else
		{
			//no missing locations file yet, that's fine. 
			missingLocations = new List<String>();
		}


		//make a dictionary for quick lookup of 'Triggers'
		foreach (MapLocation loc in mapLocations)
		{
			foreach(string str in loc.Triggers)
			{
				if(dicMapLocations.ContainsKey(str) == false)
				{
					dicMapLocations.Add(str, loc);
				}
			}
		}
	}

	public async Task UpdateAlerts()
	{
		try
		{
			string url = "https://www.oref.org.il/warningMessages/alert/History/AlertsHistory.json";
			string json = await httpClient.GetStringAsync(url);
			lastAlertReceiveDate = DateTime.Now;

			if (string.IsNullOrWhiteSpace(json) || json == "[]")
			{
				//no alerts at all. 
				lastOrefAlerts = new List<OrefAlert>();
			}
			else
			{
				//process the alerts:
				lastOrefAlerts =  JsonConvert.DeserializeObject<List<OrefAlert>>(json);
			}
				
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error fetching Oref alerts: " + ex.Message);
			lastOrefAlerts = new List<OrefAlert>();
			lastOrefAlerts.Add(new OrefAlert
			{
				Title = "error fetching data",
				AlertDate = DateTime.Now,
				Category = 1,
				Location = "nowhere"
			});
		}
	}

	private async Task<List<OrefAlert>> GetOrefAlertsAsync()
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

	private List<AlertGroup> GroupAlerts(List<OrefAlert> alerts)
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
	

	public Dictionary<string, AlertStatus> GetMapStatuses()
	{
		var statusMap = new Dictionary<string, AlertStatus>();
		DateTime threshold = DateTime.Now.AddMinutes(-100);

		// Filter by time and take the LATEST event for every city
		var latestPerCity = lastOrefAlerts
			.Where(a => a.AlertDate >= threshold)
			.GroupBy(a => a.Location)
			.Select(g => g.OrderByDescending(a => a.AlertDate).First());

		foreach (var alert in latestPerCity)
		{
			AlertStatus status;
			var ageSeconds = (DateTime.Now - alert.AlertDate).TotalSeconds;

			if (alert.Category == 13)
			{
				status = AlertStatus.Finished; // Green
			}
			else if (alert.Category == 14)
			{
				status = AlertStatus.PreWarning;// Blue
			}
			else if (ageSeconds <= 90)
			{
				status = AlertStatus.NewAlert; // Red
			}
			else
			{
				status = AlertStatus.PostAlert; // Orange
			}

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
			//for the text UI, this is used to display for us important locations first. 

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



