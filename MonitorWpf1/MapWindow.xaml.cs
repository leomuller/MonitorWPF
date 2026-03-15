using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using static MonitorWpf1.MapWindow;

namespace MonitorWpf1
{
	public enum AlertStatus { PreWarning, NewAlert, PostAlert, Finished, None }

	public partial class MapWindow : UserControl
	{
		private class FireAlarm
		{
			public string ID { get; set; }
			public string Label { get; set; }
			public double MapX { get; set; }
			public double MapY { get; set; }
			public double BaseRadius { get; set; } 
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


		public class MapDisplayLocation
		{
			public MapLocation BaseLocation { get; set; }
			public Brush DisplayColorBrush { get; set; } = Brushes.White;
			public int ZindexNum { get; set; } = 10;
		}

		private const double BaseRadius = 25.0; // Radius at 100% map scale

		public MapWindow()
		{
			InitializeComponent();
			PaintMainPoints();
			MapImage.SizeChanged += (s, e) => UpdateAllPositions();



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

		
		private void PaintMainPoints()
		{
			Ellipse dotModiin = new Ellipse
			{
				Width = 8, 
				Height = 8,
				Fill = Brushes.Transparent,
				Stroke = Brushes.Black,
				StrokeThickness = 1,
				IsHitTestVisible = false
			};

			Ellipse dotTelAviv = new Ellipse
			{
				Width = 8,
				Height = 8,
				Fill = Brushes.Transparent,
				Stroke = Brushes.Black,
				StrokeThickness = 1,
				IsHitTestVisible = false
			};

			Ellipse dotJerusalem = new Ellipse
			{
				Width = 8,
				Height = 8,
				Fill = Brushes.Transparent,
				Stroke = Brushes.Black,
				StrokeThickness = 1,
				IsHitTestVisible = false
			};

			// Add to canvas
			PointsCanvas.Children.Add(dotModiin);
			PointsCanvas.Children.Add(dotTelAviv);
			PointsCanvas.Children.Add(dotJerusalem);

			Canvas.SetLeft(dotModiin, 160);
			Canvas.SetTop(dotModiin, 348);

			Canvas.SetLeft(dotTelAviv, 114);
			Canvas.SetTop(dotTelAviv, 302);

			Canvas.SetLeft(dotJerusalem, 201);
			Canvas.SetTop(dotJerusalem, 375);
		
		}

		
		
		public void UpdateAlarmOnMap(MapDisplayLocation loc)
		{
			FireAlarm alarm = new FireAlarm
			{
				ID = loc.BaseLocation.Name,
				Label = loc.BaseLocation.Label,
				MapX = loc.BaseLocation.X,
				MapY = loc.BaseLocation.Y,
				BaseRadius = loc.BaseLocation.BaseRadius
			};

			alarm.Marker = CreateMarker(loc.BaseLocation.Label, loc.BaseLocation.BaseRadius, loc.DisplayColorBrush);
			OverlayCanvas.Children.Add(alarm.Marker);
			PositionMarker(alarm);

			Debug.WriteLine("Mark area {0} with color {1} zindex {2} opacity {3}", loc.BaseLocation.Name, loc.DisplayColorBrush.ToString(), loc.ZindexNum, loc.DisplayColorBrush.Opacity);
		}

		private Ellipse CreateMarker(string labelText, double radius, Brush fillBrush)
		{
			double showSize = radius / 30;

			Brush markerBrush = fillBrush.Clone();
			markerBrush.Opacity = 0.45;

			Ellipse e = new Ellipse
			{
				Fill = markerBrush,
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

		

		private void UpdateAllPositions()
		{
			//TBD think how this can be done.
			//foreach (var alarm in _alarms) PositionMarker(alarm);

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

		public void SyncWithService(OrefAlertsService alertService)
		{

			// update what needs to be displayed.
			alertService.UpdateMapDisplayStatuses();

			//clear existing alarms.
			OverlayCanvas.Children.Clear();

			// Check UI/Marker Creation Speed
			int count = 0;
			foreach (var entry in alertService.DisplayMapLocations.OrderBy(entry => entry.ZindexNum))
			{
				UpdateAlarmOnMap(entry);
				count++;
			}
		}


	}
}