using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace MonitorWpf1
{

	/// <summary>
	/// Interaction logic for MapWindow.xaml
	/// </summary>
	public partial class MapWindow : Window
    {
		private Canvas overlayCanvas;

		public MapWindow()
        {
            InitializeComponent();
			MapImage.SizeChanged += MapImage_SizeChanged;
		}

		private void MapImage_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (overlayCanvas != null)
			{
				(MapImage.Parent as Grid).Children.Remove(overlayCanvas);
			}

			overlayCanvas = new Canvas
			{
				Width = MapImage.ActualWidth,
				Height = MapImage.ActualHeight,
				IsHitTestVisible = false
			};

			(MapImage.Parent as Grid).Children.Add(overlayCanvas);

			// Now call with colors!
			PlaceMarker(500, 300, Brushes.Yellow, 10);
			PlaceMarker(700, 1850, Brushes.Green, 20);
			PlaceMarker(300, 900, Brushes.Red, 30);
			PlaceMarker(320, 910, Brushes.Red, 30);
		}


		private void PlaceMarker(double mapX, double mapY, Brush color, double radius)
		{

			double opacity = 0.4;

			if (MapImage.Source is BitmapSource bmp)
			{
				double diameter = radius * 2;

				// 1. Create the Ellipse
				Ellipse newMarker = new Ellipse
				{
					Width = diameter,
					Height = diameter,
					Fill = color,
					Stroke = Brushes.White,
					StrokeThickness = 0,
					Opacity = opacity,
					// Setup the center point for the animation to scale from
					RenderTransformOrigin = new Point(0.5, 0.5)
				};

				// 2. Setup the Scale Transform (required for the pulse)
				ScaleTransform scale = new ScaleTransform(1.0, 1.0);
				newMarker.RenderTransform = scale;

				// 3. Create the "Pulse" Animation
				// It will scale from 1.0 (100%) to 1.2 (120% size)
				DoubleAnimation pulseAnimation = new DoubleAnimation
				{
					From = 1.0,
					To = 1.2,
					Duration = TimeSpan.FromSeconds(0.8),
					AutoReverse = true,           // Shrink back down
					RepeatBehavior = RepeatBehavior.Forever // Loop it
				};

				// 4. Start the animation on both X and Y axes
				scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
				scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);

				// --- Positioning Logic (Same as before) ---
				double imgWidth = bmp.PixelWidth;
				double imgHeight = bmp.PixelHeight;
				double currentScale = Math.Min(MapImage.ActualWidth / imgWidth, MapImage.ActualHeight / imgHeight);

				double left = (mapX * currentScale) - radius;
				double top = (mapY * currentScale) - radius;

				Canvas.SetLeft(newMarker, left);
				Canvas.SetTop(newMarker, top);

				overlayCanvas.Children.Add(newMarker);
			}
		}





	}



}
