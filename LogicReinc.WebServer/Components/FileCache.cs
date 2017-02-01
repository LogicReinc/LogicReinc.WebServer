using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Components
{
    public class FileCache
    {
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);
        private Dictionary<string, CachedFile> Cache { get; } = new Dictionary<string, CachedFile>();



        public byte[] LoadFile(string path)
        {
            string lPath = path.ToLower();

            if (!Cache.ContainsKey(lPath))
                Cache.Add(lPath, new CachedFile(this, path));
            return Cache[lPath].Get();
        }


        public void Clear() => Cache.Clear();

        public class CachedFile
        {
            private FileCache Container { get; set; }
            public FileInfo Info { get; private set; }
            public string Path { get; private set; }
            public DateTime LastUpdate { get; private set; }
            public DateTime LastWrite { get; private set; }
            public byte[] Data { get; private set; }

            public CachedFile(FileCache container, string path)
            {
                Container = container;
                Path = path;
            }

            public byte[] Get()
            {
                if (Data == null || (DateTime.Now.Subtract(LastUpdate) > Container.CacheDuration))
                    Update();
                return Data;
            }

            public void Update()
            {
                LastUpdate = DateTime.Now;
                FileInfo f = new FileInfo(Path);
                if (!f.Exists)
                    throw new FileNotFoundException($"File {System.IO.Path.GetFileName(Path)} could not be found");
                if(LastWrite < f.LastWriteTime)
                    Data = File.ReadAllBytes(Path);
                LastWrite = f.LastWriteTime;
            }
        }
    }
}
