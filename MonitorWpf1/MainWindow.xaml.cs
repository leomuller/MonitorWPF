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

		public MainWindow()
        {
            InitializeComponent();

			// Initialize the news timer
			timer_news = new DispatcherTimer();
			timer_news.Interval = TimeSpan.FromSeconds(90); 
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
			//TimerAlerts_Tick(null, null);

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
				scrollTimer.Interval = TimeSpan.FromMilliseconds(20); // adjust speed
				scrollTimer.Tick += (s, ev) =>
				{
					if (NewsScrollViewer.ScrollableHeight == 0) return;

					double offset = NewsScrollViewer.VerticalOffset + 1; // pixels per tick
					if (offset >= NewsScrollViewer.ScrollableHeight)
					{
						NewsScrollViewer.ScrollToTop();
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
			var alertGroups = await _alertService.GetOrefAlertsAsync();
			if(alertGroups != null && alertGroups.Count > 0)
			{
				LabelNoData.Text = "";
				AlertsControl.ItemsSource = alertGroups.OrderByDescending(g => g.AlertDate).ToList();
			}
			else
			{
				LabelNoData.Text = "No data.";
			}

			AlertsLastUpdatedText.Text = $"Last updated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
		}
	}
}