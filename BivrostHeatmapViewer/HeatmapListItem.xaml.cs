using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace BivrostHeatmapViewer
{
	public sealed partial class HeatmapListItem : UserControl
	{
		public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register("DisplayName", typeof(string), typeof(HeatmapListItem), new PropertyMetadata(""));
		public static readonly DependencyProperty DisplayDescriptionProperty = DependencyProperty.Register("DisplayDescription", typeof(string), typeof(HeatmapListItem), new PropertyMetadata(""));

		private Session _heatmap;
		public Session HeatmapObject {
			get { return _heatmap; }
			set
			{
				_heatmap = value;
				Name.Text = "Session " + _heatmap.time_start.ToString();
				Description.Text = _heatmap.Length.ToString() + "; sample rate " + _heatmap.sample_rate + "Hz; source: " + _heatmap.lookprovider;
			}
		}

		public string DisplayName
		{
			get { return Name.Text; }
			set { Name.Text = value; }
		}

		public string DisplayDescription
		{
			get { return Description.Text; }
			set { Description.Text = value; }
		}

		public HeatmapListItem()
		{
			this.InitializeComponent();
			this.Loaded += HeatmapListItem_Loaded;
		}

		private void HeatmapListItem_Loaded(object sender, RoutedEventArgs e)
		{
			HeatmapObject = _heatmap;
		}

		public HeatmapListItem(Session session)
		{
			this.InitializeComponent();
			this.Loaded += HeatmapListItem_Loaded;
			_heatmap = session;
		}
	}
}
