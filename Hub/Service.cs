using System;
using Aurora.SignalR.Config;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Hosting;
using NLog;
using Owin;

namespace Aurora.SignalR.Hub
{
    [System.Runtime.InteropServices.Guid("E0674628-7048-4EEC-9E37-87F5D839A27F")]
    public class Service
    {
        #region Private Members
        private static readonly Logger m_Log = LogManager.GetCurrentClassLogger();
        private static string m_ServerSocket;
        private static string m_ServerHub;

        private static IDisposable m_Service;
        #endregion
        public Service()
        {

        }

        public static IDisposable Start(SignalRConfig config)
        {
            return (Start(config.ServerSocket, config.ServerHub));
        }
        public static IDisposable Start(string serverSocket, string serverHub)
        {
            try
            {
                Init(serverSocket, serverHub);
                m_Log.Warn(">> Start {0} {1}", m_ServerSocket, m_ServerHub);
                var options = new StartOptions();
                options.Urls.Add(m_ServerSocket);
                options.ServerFactory = "Microsoft.Owin.Host.HttpListener";
                m_Service = WebApp.Start<Service>(options);
                m_Log.Warn("<< Start {0}", m_Service != null ? "success" : "failure");
            }
            catch (System.Exception ex)
            {
                m_Log.Error(ex, $" Error starting service {ex}");
            }
            return (m_Service);
        }

        public static void Stop()
        {
            m_Log.Warn(">> Stop");
            m_Service.Dispose();
            m_Log.Warn("<< Stop");
        }

        public void Configuration(IAppBuilder app)
        {
            m_Log.Debug(">> SignalR ServerConfig");
            GlobalHost.HubPipeline.AddModule(new ErrorHandlingPipelineModule());
            var hubConfiguration = new HubConfiguration
            {
                EnableDetailedErrors = true,
                EnableJavaScriptProxies = false
            };

            app.UseCors(CorsOptions.AllowAll);

            app.MapSignalR(m_ServerHub, hubConfiguration);
            m_Log.Debug("<< SignalR ServerConfig");
        }

        private static void Init(string serverSocket, string serverHub)
        {
            if (m_ServerSocket != null)
                return;
            try
            {
                m_Log.Debug(">> Init SelfHost");
                m_ServerSocket = serverSocket;
                m_ServerHub = serverHub;
                m_Log.Warn("<< Init SelfHost");
            }
            catch (Exception ex)
            {
                m_Log.Error(ex, "Error loading Configuration", ex.ToString());
            }
        }
    }
}
