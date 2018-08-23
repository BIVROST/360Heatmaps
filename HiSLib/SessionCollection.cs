using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using System;
using System.Diagnostics;

namespace BivrostHeatmapViewer
{
	public class SessionCollection
	{
		public List<Session> sessions;

		public static async Task<SessionCollection> FromFileAsync(StorageFile file)
		{
			string json;
			try
			{
				json = await FileIO.ReadTextAsync(file);
			}
			catch(Exception e)
			{
				Debug.WriteLine($"SessionCollection.FromFileAsync: could not read file ${file.Path}: ${e.Message}");
				return null;
			}

			try
			{
				var session = JsonConvert.DeserializeObject<Session>(json);
				if (session.history == null)	// TODO: proper content handling with JSON.net attributes
					throw new JsonSerializationException();
				return new SessionCollection()
				{
					sessions = new List<Session>()
					{
						session
					}
				};
			}
			catch(JsonException) {; }

			try
			{
				var sc = JsonConvert.DeserializeObject<SessionCollection>(json);
				if(sc.sessions == null || sc.sessions.Count == 0)   // TODO: proper content handling with JSON.net attributes
					throw new JsonSerializationException();
				return sc;
			}
			catch(JsonException) {; }

			Debug.WriteLine($"SessionCollection.FromFileAsync: could not parse file ${file.Path}");

			return null;
		}


		// TODO: remove legacy extensions
		public static string[] SupportedExtensions => new string[] 
		{
			".360Session", ".bvr", ".txt", ".js", ".json"
		};


		public static bool IsSupportedFileExtension(string ext)
		{
			return Array.IndexOf(SupportedExtensions, ext) >= 0;
		}
	}
}
