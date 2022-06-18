using System;
using System.Configuration;
using System.IO;

namespace Aurora.SignalR.Config
{
    public class SignalRConfig : ConfigurationSection
    {
        /// <summary>
        /// url to initiate the selfhosting with (startoption url)
        /// </summary>
        [ConfigurationProperty("serverSocket", DefaultValue = "", IsRequired = true)]
        public string ServerSocket
        {
            get { return (string) this["serverSocket"]; }
            set { this["serverSocket"] = value; }
        }
        /// <summary>
        /// Hubname to mao signalR with (in Configuration of self hosting)
        /// </summary>
        [ConfigurationProperty("serverHub", DefaultValue = "", IsRequired = true)]
        public string ServerHub
        {
            get { return (string)this["serverHub"]; }
            set { this["serverHub"] = value; }
        }
        [ConfigurationProperty("useSystemProxy", DefaultValue = false, IsRequired = false)]
        public Boolean UseSystemProxy
        {
            get { return Convert.ToBoolean(this["useSystemProxy"]); }
            set { this["useSystemProxy"] = value; }
        }
        /// <summary>
        /// Url to signalR Proxy to connect to from Client
        /// </summary>
        public string HubUrl => ServerSocket.TrimEnd('/') + "/" + ServerHub.TrimStart('/');
        /// <summary>
        /// Name of the Proxy Hub class 
        /// </summary>
        public string ProxyName => Path.GetFileName(ServerHub);

        /// <summary>
        /// Gibt einen <see cref="T:System.String"/> zurück, der das aktuelle <see cref="T:System.Object"/> darstellt.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.String"/>, der das aktuelle <see cref="T:System.Object"/> darstellt.
        /// </returns>
        public override string ToString()
        {
            return string.Format($"ServerSocket:{ServerSocket} ServerHub:{ServerHub} ConnectTo:{HubUrl} ProxyName:{ProxyName} UseSystemProxy:{UseSystemProxy}");
        }
    }
}
