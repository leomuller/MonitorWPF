using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MonitorWpf1
{
	public enum AlertStatus { PreWarning, NewAlert, PostAlert, Finished, None }

	public partial class MapWindow : Window
	{
		private class FireAlarm
		{
			public string ID { get; set; }
			public string Label { get; set; }
			public double MapX { get; set; }
			public double MapY { get; set; }
			public double BaseRadius { get; set; } 
			public AlertStatus Status { get; set; }
			public Ellipse Marker { get; set; }
		}

		public class MapLocation
		{
			public string Name { get; set; } 
			public string Label { get; set; }
			public double X { get; set; }
			public double Y { get; set; }
			public double BaseRadius { get; set; }
			public List<string> Triggers { get; set; }
		}

		private List<FireAlarm> _alarms = new List<FireAlarm>();
		private const double BaseRadius = 25.0; // Radius at 100% map scale
		private readonly Dictionary<string, MapLocation> _locationRegistry = new Dictionary<string, MapLocation>();
		private HashSet<string> _loggedMissingCities = new HashSet<string>();

		public MapWindow()
		{
			InitializeComponent();
			MapImage.SizeChanged += (s, e) => UpdateAllPositions();

			LoadLocationsFromJson(@"C:\DevLeo\PR2025\MonitorWpf1\MonitorWpf1\Data\MapLocations.json");

			//this.Loaded += (s, e) => {
			//	MarkAlarm("אילת", AlertStatus.NewAlert);
			//};

			//MarkAlarm("נהריה", AlertStatus.NewAlert);


			return;
			//rest below is for manual load.

			//try
			//{
			//	// Make sure "AlertData.json" is in your bin/Debug folder
			//	string path = @"C:\DevLeo\PR2025\MonitorWpf1\MonitorWpf1\Data\sample1.json";
			//	if (File.Exists(path))
			//	{
			//		string jsonFromFile = File.ReadAllText(path, Encoding.UTF8);
			//		ProcessOrefJson(jsonFromFile);
			//	}
			//}
			//catch (Exception ex)
			//{
			//	System.Diagnostics.Debug.WriteLine($"Initial load failed: {ex.Message}");
			//}

		}

		private readonly Dictionary<string, MapLocation> _triggerCache = new Dictionary<string, MapLocation>();

		private void LoadLocationsFromJson(string filePath)
		{
			if (!File.Exists(filePath)) return;

			try
			{
				string json = File.ReadAllText(filePath, Encoding.UTF8);
				var list = JsonConvert.DeserializeObject<List<MapLocation>>(json);

				_locationRegistry.Clear();
				foreach (var item in list)
				{
					_locationRegistry[item.Name] = item;

					// Build the fast-lookup cache
					if (item.Triggers != null)
					{
						foreach (var t in item.Triggers)
							_triggerCache[t.Trim()] = item;
					}
				}


				//System.Diagnostics.Debug.WriteLine($"Loaded {_locationRegistry.Count} cities from JSON.");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to load dictionary JSON: {ex.Message}");
			}
		}

		public void MarkAlarm(string locName, AlertStatus status)
		{

			// Optional: Clear UI before processing new update
			//OverlayCanvas.Children.Clear();
			//_alarms.Clear();

			//// 1. Look up the city coordinates in your "Database" (Dictionary)
			//if (_locationRegistry.TryGetValue(locName, out var loc))
			//{
			//	// 2. Found it! Now tell the UI worker to draw/update it.
			//	// Notice we pass loc.X, loc.Y, and loc.BaseRadius from the dictionary
			//	UpdateAlarm(locName, loc.X, loc.Y, status, loc.BaseRadius, loc.Label);
			//}

			// Scan all locations to see if any Trigger matches exactly
			//var loc = _locationRegistry.Values.FirstOrDefault(l =>
			//	l.Triggers != null && l.Triggers.Any(t => t.Trim() == locName));

			//if (loc != null)

			if (_triggerCache.TryGetValue(locName, out var loc))
			{
				UpdateAlarm(loc.Name, loc.X, loc.Y, status, loc.BaseRadius, loc.Label);
			}
			else
			{
				//System.Diagnostics.Debug.WriteLine($"No match found for: {locName}");

				// Only log it if we haven't already logged it during this session
				if (!_loggedMissingCities.Contains(locName))
				{
					LogMissingCity(locName);
					_loggedMissingCities.Add(locName);

					// Also keep the debug line so you see it in real-time
					//System.Diagnostics.Debug.WriteLine($"MISSING CITY LOGGED: {locName}");
				}
			}
		}

		private void LogMissingCity(string cityName)
		{
			try
			{
				string folderPath = @"C:\DevLeo\PR2025\MonitorWpf1\MonitorWpf1\Data\";
				string filePath = System.IO.Path.Combine(folderPath, "MissingCities.json");

				// Format it as a JSON object for easy copy-pasting into your main file
				// We use \" to include quotes inside the string
				string jsonEntry = $"  {{ \"Name\": \"{cityName}\", \"X\": 0, \"Y\": 0, \"BaseRadius\": 30 }},{Environment.NewLine}";

				// Use Encoding.UTF8 to protect Hebrew characters
				File.AppendAllText(filePath, jsonEntry, Encoding.UTF8);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Could not log: {ex.Message}");
			}
		}
		public void UpdateAlarm(string id, double x, double y, AlertStatus status, double baseRadius, string label)
		{
			var alarm = _alarms.FirstOrDefault(a => a.ID == id);

			if (alarm == null)
			{
				// New Alarm: Store the baseRadius so we can scale it later
				alarm = new FireAlarm
				{
					ID = id,
					Label = label,
					MapX = x,
					MapY = y,
					Status = status,
					BaseRadius = baseRadius
				};
				alarm.Marker = CreateMarker(label, baseRadius);
				_alarms.Add(alarm);
				OverlayCanvas.Children.Add(alarm.Marker);
			}
			else
			{
				// Existing Alarm: Just update the status/position
				alarm.MapX = x;
				alarm.MapY = y;
				alarm.Status = status;
				alarm.BaseRadius = baseRadius;
			}

			UpdateMarkerVisual(alarm);
			PositionMarker(alarm);
		}

		private Ellipse CreateMarker(string labelText, double radius)
		{
			double showSize = radius / 30;
			Ellipse e = new Ellipse
			{
				Opacity = 0.35,
				RenderTransformOrigin = new Point(0.5, 0.5),
				RenderTransform = new ScaleTransform(1.0, 1.0)
			};

			// Start the breathing pulse animation
			DoubleAnimation pulse = new DoubleAnimation
			{
				From = 1.0 * showSize,
				To = 1.25 * showSize,
				Duration = TimeSpan.FromSeconds(0.9),
				AutoReverse = true,
				RepeatBehavior = RepeatBehavior.Forever
			};
			e.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
			e.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);

			return e;
		}

		private void UpdateMarkerVisual(FireAlarm alarm)
		{
			// Set Color based on the Enum
			alarm.Marker.Fill = alarm.Status switch
			{
				AlertStatus.PreWarning => Brushes.Gold,
				AlertStatus.NewAlert => Brushes.Red,
				AlertStatus.PostAlert => Brushes.DarkOrange,
				AlertStatus.Finished => Brushes.LightGreen,             //Brushes.LightGreen,
				_ => Brushes.Gray
			};
		}

		private void UpdateAllPositions()
		{
			foreach (var alarm in _alarms) PositionMarker(alarm);
		}

		private void PositionMarker(FireAlarm alarm)
		{
			if (MapImage.Source is BitmapSource bmp)
			{
				// Calculate scale of the displayed image relative to the file pixels
				double scale = Math.Min(MapImage.ActualWidth / bmp.PixelWidth,
										MapImage.ActualHeight / bmp.PixelHeight);

				// Scale the dot size based on the map scale
				double currentRadius = BaseRadius * scale;
				alarm.Marker.Width = currentRadius * 2;
				alarm.Marker.Height = currentRadius * 2;

				// Position centered on the coordinate
				double left = (alarm.MapX * scale) - currentRadius;
				double top = (alarm.MapY * scale) - currentRadius;

				Canvas.SetLeft(alarm.Marker, left);
				Canvas.SetTop(alarm.Marker, top);
			}
		}

		private void MapImage_MouseDown(object sender, MouseButtonEventArgs e)
		{
			// 1. Get the position of the click relative to the Image control
			Point clickPoint = e.GetPosition(MapImage);

			if (MapImage.Source is BitmapSource bmp)
			{
				// 2. Calculate the ratio between the actual file pixels and the displayed size
				double ratioX = bmp.PixelWidth / MapImage.ActualWidth;
				double ratioY = bmp.PixelHeight / MapImage.ActualHeight;

				// 3. Convert click coordinates to JPG pixel coordinates
				double pixelX = clickPoint.X * ratioX;
				double pixelY = clickPoint.Y * ratioY;

				// 4. Log it to the Output Window in a format you can copy-paste!
				string logEntry = $"{{ \"CityName\", new MapLocation {{ X = {pixelX:F0}, Y = {pixelY:F0}, BaseRadius = 30 }} }},";

				System.Diagnostics.Debug.WriteLine(logEntry);
			}
		}

		public void SyncWithService(List<OrefAlertsService.OrefAlert> rawAlerts)
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();

			if (rawAlerts == null) return;

			// 1. Check Service Logic Speed
			var service = new OrefAlertsService();
			var statuses = service.GetMapStatuses(rawAlerts);

			long serviceTime = sw.ElapsedMilliseconds;
			System.Diagnostics.Debug.WriteLine($"[PERF] GetMapStatuses took: {serviceTime}ms");
			sw.Restart();

			// 2. Check UI/Marker Creation Speed
			int count = 0;
			foreach (var entry in statuses)
			{
				MarkAlarm(entry.Key, entry.Value);
				count++;
			}

			long uiTime = sw.ElapsedMilliseconds;
			System.Diagnostics.Debug.WriteLine($"[PERF] MarkAlarm (x{count}) took: {uiTime}ms");
			sw.Stop();
		}


	}
}