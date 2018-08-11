using System.Collections.ObjectModel;
using System.Text;
using Windows.Media.MediaProperties;

namespace BivrostHeatmapViewer
{
	public struct SavingResolutions
	{
		public uint Width;
		public uint Height;
	}

	public class Resolutions
	{
		public SavingResolutions Resolution
		{
			get;
			set;
		}
		public Resolutions (SavingResolutions resolution, bool orig)
		{
			Resolution = resolution;
			IsOrigResolution = orig;
		}

		public bool IsOrigResolution
		{
			get;
		}

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Resolution.Width);
			stringBuilder.Append("x");
			stringBuilder.Append(Resolution.Height);

			if (IsOrigResolution)
			{
				stringBuilder.Append(" (orig)");
			}

			return stringBuilder.ToString();
		}

	}

	

	//4096,3840.2560,2048,1920,1280
	public class SavingResolutionsCollection : ObservableCollection<Resolutions>
	{
		private readonly uint[] _resolutions = new uint[] { 4096, 3840, 2560, 2048, 1920, 1280 };

		public SavingResolutionsCollection(VideoEncodingProperties enc) : base()
		{
			uint width = enc.Width;
			uint height = enc.Height;

			Add(new Resolutions(new SavingResolutions { Width = width, Height = height }, true));

			for (int i = 0; i < _resolutions.Length; i++)
			{
				if (_resolutions[i] != width)
				{
					uint calcHeight = _resolutions[i] * height / width;

					if (calcHeight % 2 != 0)
					{
						calcHeight = calcHeight + 1;
					}

					Add(new Resolutions(new SavingResolutions {Width=_resolutions[i], Height = calcHeight }, false));
				}
				else
				{
				}
			}
		}

	}
}
