using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aurora.SignalR.Hub
{

    public class ManagedHub : Hub
    {
        #region CLientClass
        private class Client
        {
            public readonly string ClientName;
            public readonly int Version;
            public readonly string ConnectionId;

            public Client(string clientName, int version, string connectionId)
            {
                ClientName = clientName;
                Version = version;
                ConnectionId = connectionId;
            }
        }
        #endregion
        #region Private Members
        private static readonly ConcurrentDictionary<string, Client> m_RegisteredClients = new ConcurrentDictionary<string, Client>();
        private static readonly Logger m_Log = LogManager.GetCurrentClassLogger();
        private string m_ManagementGroupName = "ManamgementGroup";
        #endregion
        #region Public Members
        #endregion
        #region Public Methods
        /// <summary>
        /// Called when a connection disconnects from this hub gracefully or due to a timeout.
        /// </summary>
        /// <param name="stopCalled">true, if stop was called on the client closing the connection gracefully;
        ///             false, if the connection has been lost for longer than the
        ///             <see cref="P:Microsoft.AspNet.SignalR.Configuration.IConfigurationManager.DisconnectTimeout"/>.
        ///             Timeouts can be caused by clients reconnecting to another SignalR server in scaleout.
        ///             </param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task"/>
        /// </returns>
        public override Task OnDisconnected(bool stopCalled)
        {
            Client client = GetRegisteredClientFromConnectionId();
            if (client != null)
            {
                m_Log.Debug(stopCalled ? "Client issued disconnect {0} removed with connectionID {1}" : "Client timed out {0} removed with connectionID {1}", client.ClientName, client.ConnectionId);

                Groups.Remove(Context.ConnectionId, client.ClientName);
                m_RegisteredClients.TryRemove(client.ConnectionId, out client);
                Clients.Group(m_ManagementGroupName).ClientDisconnected(client.ClientName);
            }
            return base.OnDisconnected(stopCalled);
        }
        #region Client Initiated Methods

        /// <summary>
        /// Register the client in Groups and in tracking dictionary
        /// </summary>
        /// <param name="clientName">code to identify the client</param>
        /// <returns>ConnectionId to return to client</returns>
        public string Register(string clientName)
        {
            return (Register(clientName, 0));
        }
        /// <summary>
        /// Register the client in Groups and in tracking dictionary with the current ClientVersion
        /// </summary>
        /// <param name="clientName">code to identify the client</param>
        /// <param name="version">Protocol Version</param>
        /// <returns>ConnectionId to return to client</returns>
        public string Register(string clientName, int version)
        {
            m_Log.Warn(">> Register {0} {1}", clientName, version);
            if (clientName.Equals(m_ManagementGroupName))
                throw (new Exception($"Invalid clientName {clientName}"));
            
            Client client = new Client(clientName, version, Context.ConnectionId);
            if (!m_RegisteredClients.ContainsKey(Context.ConnectionId))
            {
                m_Log.Debug("Client {0} added with connectionID {1}", clientName, Context.ConnectionId);
                m_RegisteredClients.AddOrUpdate(Context.ConnectionId, client, (key, oldValue) => client);
                Groups.Add(Context.ConnectionId, clientName);
                Clients.Group(m_ManagementGroupName).ClientConnected(clientName);
            }
            m_Log.Warn("<< Register");
            return (Context.ConnectionId);
        }
        /// <summary>
        /// Remove client from groups and the tracking dictionary
        /// </summary>
        /// <param name="clientName">code to identify the client</param>
        /// <returns>ConnectionId to return to client</returns>
        public string DeRegister(string clientName)
        {
            m_Log.Warn(">> DeRegister {0}", clientName);
            clientName = TranslateClientName(clientName);
            if (m_RegisteredClients.ContainsKey(Context.ConnectionId))
            {
                m_Log.Debug("Client {0} removed with connectionID {1}", clientName, Context.ConnectionId);
                Client client;
                m_RegisteredClients.TryRemove(Context.ConnectionId, out client);
            }
            Clients.Group(m_ManagementGroupName).ClientDisconnected(clientName);
            Groups.Remove(Context.ConnectionId, clientName);
            m_Log.Warn("<< Client called DeRegister");

            return (Context.ConnectionId);
        }
        #endregion
        #endregion
        #region Private Methods 
        private Client GetRegisteredClientFromConnectionId()
        {
            Client retVal = null;
            if (m_RegisteredClients.ContainsKey(Context.ConnectionId))
                retVal = m_RegisteredClients[Context.ConnectionId];
            return (retVal);
        }

        private static string TranslateClientName(string clientName)
        {
            int found = clientName.IndexOf("_", StringComparison.InvariantCultureIgnoreCase);
            if (found > 0)
                clientName = clientName.Substring(0, found);
            return clientName;
        }

        private Client GetRegisteredClientfromClientName(string clientName, int version = 0)
        {
            Client retVal = null;
            if (m_RegisteredClients.Values.FirstOrDefault((item) => item.ClientName == clientName && item.Version >= version) != null)
            {
                KeyValuePair<string, Client> clientKvp = m_RegisteredClients.FirstOrDefault((item) => item.Value.ClientName == clientName && item.Value.Version >= version);
                retVal = clientKvp.Value;
            }
            return retVal;
        }
        #endregion
    }
}
