using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace BivrostHeatmapViewer
{

	public class CheckboxToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			bool isChecked = false;

			if (bool.TryParse(value.ToString(), out isChecked))
			{
				return isChecked ? Visibility.Visible : Visibility.Collapsed;	
			}

			return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new NotImplementedException();
		}
	}
}
