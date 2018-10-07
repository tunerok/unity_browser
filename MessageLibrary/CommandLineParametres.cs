using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace MessageLibrary {
    [Serializable]
    public class CommandLineParametres {
        public int kWidth;
        public int kHeight;
        public string SharedFileName;
        public string outCommFile;
        public string inCommFile;
        public string InitialUrl;
        public string ResourceBundle;
        public bool _enableWebRTC;
        public bool _enableGPU;

        public int TextureSize {
            get { return kWidth * kHeight * 4; }
        }

        public string Encode() {
            BinaryFormatter bf = new BinaryFormatter();
            using (var stream = new MemoryStream()) {
                bf.Serialize(stream,this);
                stream.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        public static CommandLineParametres Decode(string base64) {
            BinaryFormatter bf = new BinaryFormatter();
            var decoded = Convert.FromBase64String(base64);
            using (var stream=new MemoryStream(decoded)) {
                return bf.Deserialize(stream) as CommandLineParametres;
            }
        }
    }
}