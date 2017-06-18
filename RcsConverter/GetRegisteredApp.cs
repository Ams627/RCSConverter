using Microsoft.Win32;
using System.IO;

namespace RcsConverter
{
    class GetRegisteredApp
    {
        string ext;
        public string  Appname { get; private set; }
        public GetRegisteredApp(string ext)
        {
            this.ext = ext;
            TryGetRegisteredApplication(ext);
        }

        private void TryGetRegisteredApplication(string extension)
        {
            string extensionId = GetClassesRootKeyDefaultValue(extension);
            if (extensionId != null)
            {

                string openCommand = GetClassesRootKeyDefaultValue(Path.Combine(new[] { extensionId, "shell", "open", "command" }));

                if (openCommand != null)
                {

                    Appname = openCommand
                                     .Replace("%1", string.Empty)
                                     .Replace("\"", string.Empty)
                                     .Trim();
                }
            }
        }

        private static string GetClassesRootKeyDefaultValue(string keyPath)
        {
            string result = "";
            using (var key = Registry.ClassesRoot.OpenSubKey(keyPath))
            {
                if (key != null)
                {
                    var defaultValue = key.GetValue(null);
                    if (defaultValue != null)
                    {
                        result = defaultValue.ToString();
                    }
                }
            }
            return result;
        }
    }
}
