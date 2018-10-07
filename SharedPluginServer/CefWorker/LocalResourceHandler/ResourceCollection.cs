using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms.VisualStyles;
using MimeTypes;
namespace ResourceCollection
{
    public class ResourceInfo
    {
        public bool IsStatic;
        public string Mime;
        public byte[] Resource;

        public ResourceInfo(byte[] resource, string mime)
        {
            Resource = resource;
            Mime = mime;
        }

        public static string MimeByName(string name)
        {
            var extension = Path.GetExtension(name);
            var mime = MimeTypeMap.GetMimeType(extension);
            return mime;
        }

        internal void Transfer(Stream outputStream)
        {
            try {
                outputStream.Write(Resource, 0, Resource.Length);
                
           }
            catch (Exception e) {
                
            }
            //using (var inputthread = new MemoryStream(Resource))
                //await inputthread.CopyToAsync(outputStream);
        }
    }

    public interface IResourceSource
    {
        byte [] TryLoad(string filepath);
    }

    public class HttpResources
    {
        private readonly IDictionary<string, ResourceInfo> _dynamicCollection = new Dictionary<string, ResourceInfo>();
        private readonly ICollection<IResourceSource> _sources = new List<IResourceSource>();
        private readonly IDictionary<string, ResourceInfo> _staticCollection = new Dictionary<string, ResourceInfo>();
        private readonly Dictionary<string,string> _relocation=new Dictionary<string, string>();

        public ResourceInfo Search(string name)
        {
            var result = SearchDynamic(name);

            if (result == null)
                result = SearchStatic(name);

            return result;
        }

        private ResourceInfo SearchDynamic(string name)
        {
            ResourceInfo result;
            lock (_dynamicCollection)
            {
                _dynamicCollection.TryGetValue(name, out result);
            }
            return result;
        }

        private string SearchRelocation(string name) {
            string result;
            lock(_relocation)
            _relocation.TryGetValue(name,out result);
            return result;
        }

        private void AddRelocation(string name, string realname) {
            lock(_relocation)
                _relocation.Add(name, realname);
        }
        private ResourceInfo DirectSearchStatic(string name)
        {
            ResourceInfo result;
            lock (_staticCollection) //found exact sentence in static collection
            {
                _staticCollection.TryGetValue(name, out result);
            }
            return result;
        }

        private byte[] TryLoadFromSources(string cleanedname)
        {
            lock (_sources) {
                foreach (var source in _sources) {
                    var task = source.TryLoad(cleanedname);
                    if (task != null)
                        return task;
                }
            }
            return null;
        }
        private ResourceInfo SearchStatic(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (!name.StartsWith("/"))
            {
                throw new Exception("searching malformed string");
            }
            
            var result = DirectSearchStatic(name);
            if (result != null)
                return result; //exactly this resource found
            var relocation = SearchRelocation(name);
            if (relocation != null)
                return DirectSearchStatic(relocation);  //return using existing relocation
            //try to remove workplace parameter before path and search again
            var withoutworkplace = string.Empty;
            var sequencepos = name.IndexOf("/", 1, StringComparison.Ordinal);
            if (sequencepos != -1)
            {
                withoutworkplace = name.Substring(sequencepos);
                result = DirectSearchStatic(withoutworkplace); //workplace index removed and result found
                if (result != null) {
                    AddRelocation(name, withoutworkplace); //resource found and relocation addded
                    return result;
                }
            }
            var sourcecontent = TryLoadFromSources(withoutworkplace);
            return sourcecontent!=null ? UploadStatic(withoutworkplace, sourcecontent) : null;
        }

        public ResourceInfo Upload(string path, byte[] resource, bool isStatic)
        {
            var resBind = new ResourceInfo(resource, ResourceInfo.MimeByName(path)) {IsStatic = isStatic};
            if (isStatic)
                lock (_staticCollection)
                {
                    _staticCollection[path] = resBind;
                }
            else
                lock (_dynamicCollection)
                {
                    _dynamicCollection[path] = resBind;
                }
            return resBind;
        }

        public ResourceInfo UploadStatic(string path, byte[] resource) => Upload(path, resource, true);

        public void Upload(string path, string resource)
        {
            using (var streamWriter = new MemoryStream())
            {
                using (var stringWriter = new StreamWriter(streamWriter))
                {
                    stringWriter.Write(resource);
                }
                Upload(path, streamWriter.ToArray(), false);
            }
        }

        public void AddSource(IResourceSource source)
        {
            lock (_sources)
            {
                _sources.Add(source);
            }
        }

        public void UploadPackage(string path) {
            if (Directory.Exists(path))
                UploadDirectory(path);
            else if (File.Exists(path))
                UploadArchivedPackage(path);
            else {
                
            }
        }

        private void UploadArchivedPackage(string path) {
            throw new NotImplementedException();
        }

        IEnumerable<string> RecursiveEnumerate(string path) {
            //Directory.EnumerateFiles requeired .NET 4.0+ so use this
            var queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0) {
                path = queue.Dequeue();
                try {
                    foreach (var subDir in Directory.GetDirectories(path)) {
                        queue.Enqueue(subDir);
                    }
                }
                catch(Exception ex) {
                    Console.Error.WriteLine(ex);
                }
                string[] files = null;
                try {
                    files = Directory.GetFiles(path);
                }
                catch (Exception ex) {
                    Console.Error.WriteLine(ex);
                }
                if (files != null) {
                    foreach (var t in files) {
                        yield return t;
                    }
                }
            }
            
        }

        string MakePathRelativeWithBase(string absoultepath, string basepath) {
            var relative = absoultepath.Replace(basepath,string.Empty);
            relative = relative.Replace('\\','/');
            return relative;
        }
        private void UploadDirectory(string path) {
            var absolutepath = Path.GetFullPath(path);
            foreach (var file in RecursiveEnumerate(absolutepath)) {
                var content = File.ReadAllBytes(file);
                var relativepath = MakePathRelativeWithBase(file, absolutepath);
                UploadStatic(relativepath, content);
            }
        }

        public void Clear() {
            lock(_dynamicCollection)
                _dynamicCollection.Clear();
            lock(_staticCollection)
                _staticCollection.Clear();
            lock(_relocation)
                _relocation.Clear();
            lock (_sources)
                _sources.Clear();
        }
    }
}