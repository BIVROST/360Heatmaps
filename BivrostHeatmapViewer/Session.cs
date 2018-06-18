using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml.Controls;

namespace BivrostHeatmapViewer
{
	public struct Session
	{

		public string version;
		public Guid guid;
		public string uri;
		public int sample_rate;
		public Guid installation_id;
		public DateTime time_start;
		public DateTime time_end;
		public string lookprovider;
		public string history;
		public string media_id;

		readonly static string CURRENT_VERSION = "0.20170321";

		[JsonIgnore]
		public TimeSpan Length { get { return time_end - time_start; } }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

		public override string ToString()
		{
			return uri.Substring(uri.LastIndexOf('\\') + 1).ToString();
		}

	}

public struct SessionCollection
	{
		public string vid;
		public List<Session> sessions;
	}
}
