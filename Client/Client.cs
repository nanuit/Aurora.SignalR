using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Aurora.SignalR.Config;
using Microsoft.AspNet.SignalR.Client;
using NLog;

namespace Aurora.SignalR.Client
{
    public class Client
    {
        #region Enum

        public enum TraceLevel
        {
            None = TraceLevels.None,
            Messages = TraceLevels.Messages,
            Events = TraceLevels.Events,
            StateChanges = TraceLevels.StateChanges,
            All = TraceLevels.All

        }
        #endregion
        #region Events
        public delegate void ConnectionEventHandler();

        public event ConnectionEventHandler Connected;
        public event ConnectionEventHandler Disconnected;

        private void OnConnected()
        {
            Connected?.Invoke();
        }

        private void OnDisconnected()
        {
            Disconnected?.Invoke();
        }
        #endregion
        #region Private Static Members
        /// <summary>
        /// Nlog Class
        /// </summary>
        private static Logger m_Log = LogManager.GetCurrentClassLogger();
        #endregion
        #region Private Members
        /// <summary>                                                                                          
        /// lock object to serialize access to the specific resources
        /// </summary>                                                                                         
        private readonly object m_Padlock = new object();
        /// <summary>
        /// SignalR Hub connection
        /// </summary>
        private HubConnection m_HubConnection;
        /// <summary>
        /// SignalR Hub Proxy
        /// </summary>
        private IHubProxy m_Proxy;
        /// <summary>
        /// Url of the SignalRServer Hub
        /// </summary>
        private readonly string m_HubUrl;
        /// <summary>
        /// Name of the proxy to connect to an the given SignalR server 
        /// </summary>
        private readonly string m_ProxyName;
        /// <summary>
        /// indicates if the system proxy should be used for connection to serverv hub
        /// </summary>
        private readonly bool m_UseSystemProxy;
        /// <summary>
        /// Task is launched when connection is lost
        /// </summary>
        private Task m_ReconnectionTask;
        /// <summary>
        /// Tracelevel to set for Hubtraces
        /// </summary>
        private TraceLevels m_Trace = TraceLevels.None;
        /// <summary>
        /// filepath to write the trace to
        /// </summary>
        private string m_TraceFile;
        #endregion
        #region Properties
        /// <summary>
        /// returns if the connection to the signalR server is established
        /// </summary>
        public bool IsConnected => (m_HubConnection != null && m_HubConnection.State == ConnectionState.Connected);
        /// <summary>
        /// Contains the last error occurred
        /// </summary>
        public Exception LastError { get; set; }
        /// <summary>
        /// seconds to wait in the reconnection loop before next try
        /// </summary>
        public int ConnectionRetryWait { get; set; } = 60;
        #endregion
        #region To Life and Die in Starlight          
        /// <summary>
        /// create the client class to connect to a SignalR Host
        /// </summary>
        /// <param name="config">SignalR Config instance</param>
        /// <param name="proxyName">SignalR proxy to access to</param>
        public Client(SignalRConfig config) : this(config.HubUrl, config.ProxyName, config.UseSystemProxy)
        {
        }
        /// <summary>
        /// create the client class to connect to a SignalR Host
        /// </summary>
        /// <param name="hubUrl">Url to access the signalR Host</param>
        /// <param name="proxyName">SignalR proxy to access to</param>
        public Client(string hubUrl, string proxyName) : this(hubUrl, proxyName, false)
        {
        }

        /// <summary>
        /// create the client class to connect to a SignalR Host
        /// </summary>
        /// <param name="hubUrl">Url to access the signalR Host</param>
        /// <param name="proxyName">SignalR proxy to access to</param>
        /// <param name="useSystemProxy">use the Systemproxy to access the SignalR Host</param>
        public Client(string hubUrl, string proxyName, bool useSystemProxy)
        {
            m_HubUrl = hubUrl;
            m_ProxyName = proxyName;
            m_UseSystemProxy = useSystemProxy;
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Terminate the connection to the signalR hub
        /// </summary>
        public void Close()
        {
            m_HubConnection.Stop();
        }
        /// <summary>
        /// set Tracelevel and tracefile to be used with hub trace
        /// </summary>
        /// <param name="level">tracelevel for hubtrace</param>
        /// <param name="traceFile">filepath to be used for5 hubtrace</param>
        public void SetTrace(TraceLevel level, string traceFile)
        {
            m_Trace = (TraceLevels)level;
            m_TraceFile = traceFile.Replace("#", "-");
            SetTrace();
        }
        /// <summary>
        /// Invoke a Method with the connected singalR proxy
        /// </summary>
        /// <typeparam name="T">cass of server reply</typeparam>
        /// <param name="methodName">Name of the method</param>
        /// <param name="methodParams">parameters for the method</param>
        /// <returns></returns>
        public async Task<T> InvokeMethod<T>(string methodName, params object[] methodParams)
        {
            if (!CheckConnectionState())
                return (default(T));
            try
            {
                if (m_Log.IsTraceEnabled)
                {
                    if (methodParams != null)
                    {
                        string values = methodParams.Aggregate("", (current, param) => current + "," + (param?.ToString() ?? "null"));
                        m_Log.Trace(">> Invoke:{0}, ParamValues:{1}", methodName, values);
                    }
                    else
                    {
                        m_Log.Trace(">> Invoke:{0}, with no parameter", methodName);
                    }

                }
                T reply = await m_Proxy.Invoke<T>(methodName, methodParams);
                m_Log.Trace($"<< Invoked:{methodName} {reply}");
                return (reply);
            }
            catch (Exception e)
            {
                m_Log.Error(e.ToString());
                LastError = e;
                return (default(T));
            }
        }
        /// <summary>
        /// Register for an event on the SignalR Server 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="action"></param>
        public IDisposable RegisterEvent<T1, T2, T3, T4, T5, T6, T7>(string eventName, Action<T1, T2, T3, T4, T5, T6, T7> action)
        {
            IDisposable retVal = null;
            if (CheckConnectionState())
                retVal = m_Proxy.On(eventName, action);
            return (retVal);
        }
        /// <summary>
        /// Register for an event on the SignalR Server 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="action"></param>
        public IDisposable RegisterEvent<T1, T2, T3, T4, T5, T6>(string eventName, Action<T1, T2, T3, T4, T5, T6> action)
        {
            IDisposable retVal = null;
            if (CheckConnectionState())
                retVal = m_Proxy.On(eventName, action);
            return (retVal);
        }
        /// <summary>
        /// Register for an event on the SignalR Server 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="action"></param>
        public IDisposable RegisterEvent<T1, T2, T3, T4, T5>(string eventName, Action<T1, T2, T3, T4, T5> action)
        {
            IDisposable retVal = null;
            if (CheckConnectionState())
                retVal = m_Proxy.On(eventName, action);
            return (retVal);
        }
        /// <summary>
        /// Register for an event on the SignalR Server 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="action"></param>
        public IDisposable RegisterEvent<T1, T2, T3, T4>(string eventName, Action<T1, T2, T3, T4> action)
        {
            IDisposable retVal = null;
            if (CheckConnectionState())
                retVal = m_Proxy.On(eventName, action);
            return (retVal);
        }
        /// <summary>
        /// Register for an event on the SignalR Server 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="action"></param>
        public IDisposable RegisterEvent<T1, T2, T3>(string eventName, Action<T1, T2, T3> action)
        {
            IDisposable retVal = null;
            if (CheckConnectionState())
                retVal = m_Proxy.On(eventName, action);
            return (retVal);
        }
        /// <summary>
        /// Register for an event on the SignalR Server 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="action"></param>
        public IDisposable RegisterEvent<T1, T2>(string eventName, Action<T1, T2> action)
        {
            IDisposable retVal = null;
            if (CheckConnectionState())
                retVal = m_Proxy.On(eventName, action);
            return (retVal);
        }
        /// <summary>
        /// Register for an event on the SignalR Server 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="action"></param>
        public IDisposable RegisterEvent<T>(string eventName, Action<T> action)
        {
            IDisposable retVal = null;
            if (CheckConnectionState())
                retVal = m_Proxy.On(eventName, action);
            return (retVal);
        }
        /// <summary>
        /// Register for an event on the SignalR Server 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="action"></param>
        public IDisposable RegisterEvent(string eventName, Action action)
        {
            IDisposable retVal = null;
            if (CheckConnectionState())
                retVal = m_Proxy.On(eventName, action);
            return (retVal);
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// establish connection to the proxy on the SignalR Server 
        /// </summary>
        /// <returns>success</returns>
        private bool Connect()
        {
            bool retVal = true;
            m_Log.Warn(">> Connect: {0} {1}", m_HubUrl, m_ProxyName);
            lock (m_Padlock)
            {
                try
                {
                    LastError = null;
                    m_HubConnection?.TraceWriter?.Close();
                    m_HubConnection = new HubConnection(m_HubUrl);
                    if (m_UseSystemProxy)
                        m_HubConnection.Proxy = WebRequest.GetSystemWebProxy();
                    else
                        m_HubConnection.Proxy = new WebProxy();
                    m_HubConnection.Headers.Add("myauthtoken", "token data");
                    m_HubConnection.ConnectionSlow += () => m_Log.Error("** SlowConnection ");
                    m_HubConnection.StateChanged += HubConnectionOnStateChanged;
                    m_HubConnection.Error += ex => m_Log.Error(ex, "** SignalR error: {0}", ex);
                    m_HubConnection.Closed += HubConnectionOnClosed;
                    m_Proxy = m_HubConnection.CreateHubProxy(m_ProxyName);

                    if (m_HubConnection.Proxy == null)
                        m_Log.Trace("No Proxy");
                    else
                        m_Log.Trace("Use Proxy to Connect:{0}", !m_HubConnection.Proxy.IsBypassed(new Uri(m_HubUrl)));

                    SetTrace();
                    m_HubConnection.Start().Wait();
                }
                catch (Exception e)
                {
                    LastError = e;
                    m_Log.Error(e, "Connect Error {0}", LastError);
                    m_HubConnection = null;
                    retVal = false;
                    StartReconnectLoop();
                }
                finally
                {
                    m_Log.Warn("<< Connect ");
                }
            }
            return (retVal);
        }

        private void SetTrace()
        {
            if (m_Trace > TraceLevels.None)
            {
                m_Log.Warn("Activate Trace with level {0}", m_Trace);
                if (!string.IsNullOrEmpty(m_TraceFile))
                {
                    var traceStream = new StreamWriter(m_TraceFile);
                    traceStream.AutoFlush = true;
                    m_HubConnection.TraceWriter = traceStream;
                    m_HubConnection.TraceLevel = TraceLevels.All;
                }
                else
                {
                    m_Log.Error("TraceLevel set but no Tracefile specified");
                }
            }
        }

        private void HubConnectionOnClosed()
        {
            m_Log.Error("** Connection Closed");
            StartReconnectLoop();

        }
        /// <summary>
        /// start the reconnection thread if not already running
        /// </summary>
        private void StartReconnectLoop()
        {
            if (m_ReconnectionTask != null && m_ReconnectionTask.Status == TaskStatus.Running || ConnectionRetryWait == 0)
                return;
            m_Log.Trace("** connection failed start connectionLoop");
            m_ReconnectionTask = Task.Run(() =>
            {
                do
                {
                    m_Log.Trace(">> ConnectionLoop wait for {0} seconds", ConnectionRetryWait);
                    Thread.Sleep(ConnectionRetryWait * 1000);
                    m_Log.Trace("<< ConnectionLoop");
                } while (!CheckConnectionState());
            });
        }
        /// <summary>
        /// Eventmethod to handle a connection close event from SignalR
        /// </summary>
        /// <param name="stateChange"></param>
        private void HubConnectionOnStateChanged(StateChange stateChange)
        {
            m_Log.Debug("State Change from {0} to {1}", stateChange.OldState.ToString(), stateChange.NewState.ToString());
            switch (stateChange.NewState)
            {
                case ConnectionState.Connected:
                    OnConnected();
                    break;
                case ConnectionState.Reconnecting:
                case ConnectionState.Connecting:
                case ConnectionState.Disconnected:
                    OnDisconnected();
                    break;
            }

        }

        /// <summary>
        /// check the connection state to the signalR proxy and reconnect if necessary
        /// </summary>
        /// <returns>true if connected or reconnect was successful</returns>
        private bool CheckConnectionState()
        {
            bool connected = true;
            lock (m_Padlock)
            {
                if (m_HubConnection == null || m_HubConnection.State != ConnectionState.Connected)
                    return Connect();
            }

            if (IsConnected)
                return (connected);

            lock (m_Padlock)
            {
                if (m_HubConnection.State == ConnectionState.Disconnected)
                {
                    m_Log.Debug("Hub connection state disconnected. Connecting ...");
                    try
                    {
                        m_HubConnection.Start().Wait();
                        m_Log.Debug("Reconnection Done");
                    }
                    catch (Exception e)
                    {
                        LastError = e;
                        m_Log.Error(e.ToString());
                        connected = false;
                    }
                }
            }
            return (connected);
        }
        #endregion     
    }
}