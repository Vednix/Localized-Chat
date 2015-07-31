using Newtonsoft.Json;
using System.IO;

namespace LocalisedChat
{
	public class Config
	{
		public float RadiusInFeet = 100;


		public void Write(string path)
		{
			using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
			{
				Write(fs);
			}
		}

		public void Write(Stream stream)
		{
			string str = JsonConvert.SerializeObject(this, Formatting.Indented);
			using (StreamWriter sw = new StreamWriter(stream))
			{
				sw.Write(str);
			}
		}

		public void Read(string path)
		{
			if (!File.Exists(path))
				return;
			using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				Read(fs);
			}
		}

		public void Read(Stream stream)
		{
			using (StreamReader sr = new StreamReader(stream))
			{
				Config c = JsonConvert.DeserializeObject<Config>(sr.ReadToEnd());
				RadiusInFeet = c.RadiusInFeet;
			}
		}
	}
}
