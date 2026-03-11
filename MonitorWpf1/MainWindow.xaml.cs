using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static OrefAlertsService;

namespace MonitorWpf1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

		private DispatcherTimer timer_news;
		private DispatcherTimer timer_clock;
		private DispatcherTimer timer_alerts;
		DateTime lastNewsFetchTime = new DateTime(2026,1,1);

		private readonly NewsService _newsService = new NewsService();
		private readonly OrefAlertsService _alertService = new OrefAlertsService();

		private DispatcherTimer updateTimer;
		private DispatcherTimer scrollTimer;

		private MapWindow _mapWindowInstance = null;

		public MainWindow()
        {
            InitializeComponent();
			this.DataContext = this;

			// Initialize the news timer
			timer_news = new DispatcherTimer();
			timer_news.Interval = TimeSpan.FromSeconds(150); 
			timer_news.Tick += TimerNews_Tick;
			timer_news.Start();

			// Initialize the clock timer
			timer_clock = new DispatcherTimer();
			timer_clock.Interval = TimeSpan.FromSeconds(1);
			timer_clock.Tick += TimerClock_Tick;
			timer_clock.Start();

			//run once:
			TimerNews_Tick(null, null);


			// Initialize the alerts timer
			timer_alerts = new DispatcherTimer();
			timer_alerts.Interval = TimeSpan.FromSeconds(2);
			timer_alerts.Tick += TimerAlerts_Tick;
			timer_alerts.Start();

			//run once:
			TimerAlerts_Tick(null, null);

			//run one time:
			//ProcessAlertFiles.MakeMasterFile();

		}

		private void Settings_Click(object sender, RoutedEventArgs e)
		{
			//SettingsWindow w = new SettingsWindow();
			//w.ShowDialog();
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		private void About_Click(object sender, RoutedEventArgs e)
		{

		}

		private void ShowMap_Click(object sender, RoutedEventArgs e)
		{
			if (MapControl.Visibility == Visibility.Visible)
			{
				MapControl.Visibility = Visibility.Collapsed;
				MapColumn.Width = new GridLength(0); // collapse column
			}
			else
			{
				MapControl.Visibility = Visibility.Visible;
				MapColumn.Width = new GridLength(355); // restore
			}

			//// If window doesn't exist or was closed/disposed
			//if (_mapWindowInstance == null || !Application.Current.Windows.Cast<Window>().Any(x => x == _mapWindowInstance))
			//{
			//	_mapWindowInstance = new MapWindow();

			//	// Ensure that if the user clicks 'X', we just hide it instead of destroying it
			//	// This keeps the JSON data and locations loaded in memory
			//	_mapWindowInstance.Closing += (s, ev) =>
			//	{
			//		ev.Cancel = true;
			//		_mapWindowInstance.Hide();
			//	};

			//	_mapWindowInstance.Show();
			//}
			//else
			//{
			//	// If it's already open (or hidden), show it and bring to front
			//	_mapWindowInstance.Show();
			//	_mapWindowInstance.Activate();
			//}
		}

		

		private async void TimerNews_Tick(object sender, EventArgs e)
		{
			var newsItems = await _newsService.GetYnetNewsAsync();
			NewsItemsControl.ItemsSource = newsItems.Take(30).ToList();


			lastNewsFetchTime = DateTime.Now;
			NewsScrollViewer.ScrollToTop();

			// Timer to update the "last updated" label every second
			if (updateTimer == null)
			{
				updateTimer = new DispatcherTimer();
				updateTimer.Interval = TimeSpan.FromSeconds(1);
				updateTimer.Tick += (s, ev) =>
				{
					TimeSpan elapsed = DateTime.Now - lastNewsFetchTime;
					string formatted = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
					LastUpdatedText.Text = $"Last updated: {formatted} ago";
				};
				updateTimer.Start();
			}


			// Start auto-scroll timer only once
			if (scrollTimer == null)
			{
				scrollTimer = new DispatcherTimer();
				scrollTimer.Interval = TimeSpan.FromMilliseconds(25); // adjust speed
				scrollTimer.Tick += (s, ev) =>
				{
					if (NewsScrollViewer.ScrollableHeight == 0) return;

					double offset = NewsScrollViewer.VerticalOffset + 1; // pixels per tick


					//if (offset >= NewsScrollViewer.ScrollableHeight)
					//{
					//	NewsScrollViewer.ScrollToTop();
					//}
					
					if (offset >= NewsScrollViewer.ScrollableHeight)
					{
						scrollTimer.Stop();
						NewsScrollViewer.ScrollToTop();

						_ = Task.Run(async () =>
						{
							await Task.Delay(TimeSpan.FromSeconds(6));
							Application.Current.Dispatcher.Invoke(() => scrollTimer.Start());
						});
					}
					else
					{
						NewsScrollViewer.ScrollToVerticalOffset(offset);
					}
				};

				// Optional: delay first scroll
				_ = Task.Run(async () =>
				{
					await Task.Delay(TimeSpan.FromSeconds(8));
					Application.Current.Dispatcher.Invoke(() => scrollTimer.Start());
				});
			}
		}


		private void TimerClock_Tick(object sender, EventArgs e)
		{
			SystemTimeText.Text = DateTime.Now.ToString("MMMM dd, HH:mm:ss");
		}


		private async void TimerAlerts_Tick(object sender, EventArgs e)
		{
			// new way with logic in OrefAlertService:
			await _alertService.UpdateAlerts();

			if(_alertService.lastOrefAlerts.Count == 0)
			{
				LabelNoData.Text = "No data.";
			}
			else
			{
				LabelNoData.Text = "";

				//bind the results to the UI:
				AlertsControl.ItemsSource = new ObservableCollection<AlertGroup>(_alertService.GroupedAlerts); ;
				ReleaseLocationsControl.ItemsSource = new ObservableCollection<AlertGroup>(_alertService.GroupedFinishedAlerts); ;
			}

			//always show the updated time:
			AlertsLastUpdatedText.Text = $"Last updated: {_alertService.lastAlertReceiveDate:dd/MM/yyyy HH:mm:ss}";

			// Push to Map
			//if (_mapWindowInstance != null && _mapWindowInstance.IsVisible)
			//{
			//	_mapWindowInstance.SyncWithService(_alertService);
			//}
			if (MapControl.Visibility == Visibility.Visible)
			{
				MapControl.SyncWithService(_alertService);
			}
		}

		
	}
}