using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace BivrostHeatmapViewer
{
    public class TextAddConverter : IValueConverter
    {
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			String result = String.Empty;
			if (parameter != null)
			{
				result = parameter.ToString();
			}

			if (value != null)
			{
				result += value.ToString();
			}

			return result;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new NotImplementedException();
		}
	}
}
