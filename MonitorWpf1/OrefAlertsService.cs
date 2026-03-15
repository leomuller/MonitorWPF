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
	private List<MapLocation> storedMapLocations;
	public List<MapDisplayLocation> DisplayMapLocations;
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
		storedMapLocations = new List<MapLocation>();
		lastOrefAlerts = new List<OrefAlert>();
		missingLocations = new List<String>();
		DisplayMapLocations = new List<MapDisplayLocation>();

		LoadMapFiles();
	}

	private void LoadMapFiles()
	{
		//string MapLocationsFilePath = @"C:\DevLeo\PR2025\MonitorWpf1\MonitorWpf1\Data\MapLocations.json";
		//string MissingLocationsFilePath = @"C:\DevLeo\PR2025\MonitorWpf1\MonitorWpf1\Data\MissingLocations.json";

		string baseDir = AppDomain.CurrentDomain.BaseDirectory;
		string MapLocationsFilePath = System.IO.Path.Combine(baseDir, "Data", "MapLocations.json");
		string MissingLocationsFilePath = System.IO.Path.Combine(baseDir, "Data", "MissingLocations.json");


		//load the MapLocations
		if (System.IO.File.Exists(MapLocationsFilePath) == true)
		{
			string jsonMapLocations = System.IO.File.ReadAllText(MapLocationsFilePath, Encoding.UTF8);
			storedMapLocations = JsonConvert.DeserializeObject<List<MapLocation>>(jsonMapLocations);
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
		foreach (MapLocation loc in storedMapLocations)
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


	public void UpdateMapDisplayStatuses()
	{
		DisplayMapLocations.Clear();	//not sure if this is needed.

		var statusMap = new Dictionary<string, AlertStatus>();
		DateTime threshold = DateTime.Now.AddMinutes(-10);

		// Filter by time and take the LATEST event for every city
		var latestPerCity = lastOrefAlerts
			.Where(a => a.AlertDate >= threshold)
			.GroupBy(a => a.Location)
			.Select(g => g.OrderByDescending(a => a.AlertDate).First());


		//now convert this to a list per MapLocation (includes ALT trigger names)
		//List<MapLocation> displayLocation = new List<MapLocation>();
		foreach (OrefAlert alert in latestPerCity) {
			//find the area.
			if (dicMapLocations.ContainsKey(alert.Location))
			{
				MapLocation curMapLocation = dicMapLocations[alert.Location];

				Brush curBrush = Brushes.White;
				var ageSeconds = (DateTime.Now - alert.AlertDate).TotalSeconds;
				int curZindex = 25;

				if (alert.Category == 13)
				{
					//return Brushes.LightGreen;  // let out messages
					curBrush = new SolidColorBrush(Colors.LightGreen) { Opacity = 0.45 };
					curZindex = 5;
				}
				else if (alert.Category == 14)
				{
					//return Brushes.Gold;    // early warning
					curBrush = new SolidColorBrush(Colors.Gold) { Opacity = 0.45 };
					curZindex = 20;
				}
				else if (ageSeconds <= 90)
				{
					//return Brushes.DarkRed;         // very recent
					curBrush = new SolidColorBrush(Colors.DarkRed) { Opacity = 0.45 };
					curZindex = 60;
				}
				else if (ageSeconds <= 600)
				{
					//return Brushes.DarkOrange;           // slightly older
					curBrush = new SolidColorBrush(Colors.DarkOrange) { Opacity = 0.45 };
					curZindex = 40;
				}

				MapDisplayLocation curMapDisplayLocation = new MapDisplayLocation
				{
					BaseLocation = curMapLocation,
					DisplayColorBrush = curBrush,
					ZindexNum = curZindex
				};

				//this wasn't working, since it looks for that specific object, not the values.
				////check if it alread is in the list to show on map:
				//if (DisplayMapLocations.Contains(curMapDisplayLocation) == false)
				//{
				//	DisplayMapLocations.Add(curMapDisplayLocation);
				//}
				//else
				//{
				//	//it is alread showing (maybe with a different color) so no point showing it again.
				//	//nothing for now. 
				//}

				// "Only add if there isn't ALREADY an entry with this Name AND this Status/ZIndex"
				bool alreadyShown = DisplayMapLocations.Any(d =>
					d.BaseLocation.Name == curMapDisplayLocation.BaseLocation.Name &&
					d.ZindexNum == curMapDisplayLocation.ZindexNum);

				if (alreadyShown == false)
				{
					DisplayMapLocations.Add(curMapDisplayLocation);
				}

			}
			else
			{
				//does not have the location, so it should be logged. 
				//need to be added:
				missingLocations.Add(alert.Location); //to list in this app.
				LogMissingCity(alert.Location);
			}

			//if the area not in list yet, add it. 
		}

		
	}


	private void LogMissingCity(string cityName)
	{
		try
		{
			//string folderPath = @"C:\DevLeo\PR2025\MonitorWpf1\MonitorWpf1\Data\";
			//string filePath = System.IO.Path.Combine(folderPath, "MissingLocations.json");

			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string MissingLocationsFilePath = System.IO.Path.Combine(baseDir, "Data", "MissingLocations.json");

			// Format it as a JSON object for easy copy-pasting into your main file
			// We use \" to include quotes inside the string

			var entry = new
			{
				Name = cityName,
				Label = cityName,
				Triggers = new[] { cityName },
				X = 0,
				Y = 0,
				BaseRadius = 20
			};

			// serialize single entry
			string jsonEntry = JsonConvert.SerializeObject(entry) + "," + Environment.NewLine;

			//string jsonEntry = $"  {{ \"Name\": \"{cityName}\",\"Label\": \"{cityName}\",\"Triggers\": [\"{cityName}\"], \"X\": 0, \"Y\": 0, \"BaseRadius\": 20 }},{Environment.NewLine}";

			// Use Encoding.UTF8 to protect Hebrew characters
			System.IO.File.AppendAllText(MissingLocationsFilePath, jsonEntry, Encoding.UTF8);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Could not log: {ex.Message}");
		}
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

				return string.Join(", ", displayed) + extra + " " + AlertDate.ToString("dd/MM HH:mm");
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

				if (Category == 13)
				{
					//return Brushes.LightGreen;  // let out messages
					return new SolidColorBrush(Colors.LightGreen) { Opacity = 0.45 };
				}
				else if (Category == 14)
				{
					//return Brushes.Gold;    // early warning
					return new SolidColorBrush(Colors.Gold) { Opacity = 0.45 };
				}
				else if (ageSeconds <= 90)
				{
					//return Brushes.DarkRed;         // very recent
					return new SolidColorBrush(Colors.Red) { Opacity = 0.45 };
				}
				else if (ageSeconds <= 600)
				{
					//return Brushes.DarkOrange;           // slightly older
					return new SolidColorBrush(Colors.Orange) { Opacity = 0.45 };
				}
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



