using System;

namespace ExpressionsEditor.Models
{
    [System.Serializable]
    public class VersionModel
    {
        public string Version = "0.0.0";

        public Version Ver
        {
            get
            {
                if (System.Version.TryParse(Version, out Version curVer))
                    return curVer;
                return new Version(0,0,0);
            }
        }
    }
}