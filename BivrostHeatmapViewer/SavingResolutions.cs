using System.Collections.ObjectModel;
using System.Text;
using Windows.Media.MediaProperties;

namespace BivrostHeatmapViewer
{
	public struct SavingResolutions
	{
		public uint width;
		public uint height;
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
			IsOrigResulution = orig;
		}

		public bool IsOrigResulution
		{
			get;
		}

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Resolution.width);
			stringBuilder.Append("x");
			stringBuilder.Append(Resolution.height);

			if (IsOrigResulution)
			{
				stringBuilder.Append(" (orig)");
			}

			return stringBuilder.ToString();
		}

	}

	

	//4096,3840.2560,2048,1920,1280
	public class SavingResolutionsCollection : ObservableCollection<Resolutions>
	{
		private readonly uint[] resolutions = new uint[] { 4096, 3840, 2560, 2048, 1920, 1280 };

		public SavingResolutionsCollection(VideoEncodingProperties enc) : base()
		{
			uint width = enc.Width;
			uint height = enc.Height;

			Add(new Resolutions(new SavingResolutions { width = width, height = height }, true));

			for (int i = 0; i < resolutions.Length; i++)
			{
				if (resolutions[i] != width)
				{
					uint calcHeight = resolutions[i] * height / width;

					if (calcHeight % 2 != 0)
					{
						calcHeight = calcHeight + 1;
					}

					Add(new Resolutions(new SavingResolutions {width=resolutions[i], height = calcHeight }, false));
				}
				else
				{
				}
			}
		}

	}
}
