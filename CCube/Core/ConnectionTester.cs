// ConnectionTester.cs
// -----------------------------------------------------------------------------
// Backs the "Test connection" buttons on the Settings screen. Each test simply
// tries to reach the thing and reports back a friendly yes/no message. None of
// these ever throw - they catch their own errors and return the message.
//
// These are harmless read-only checks, so they DON'T require the admin password.
// -----------------------------------------------------------------------------

using Microsoft.Data.SqlClient;

namespace CCube.Core;

public static class ConnectionTester
{
    /// <summary>Try to open a SQL connection and run a trivial query.</summary>
    public static (bool ok, string message) TestSql(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT 1", connection) { CommandTimeout = 5 };
            command.ExecuteScalar();
            return (true, "Connected successfully.");
        }
        catch (Exception ex)
        {
            AppLog.Warn("SQL connection test failed.", ex);
            return (false, ex.Message);
        }
    }

    /// <summary>Check the PLC is reachable on the S7 port (102). The full S7
    /// handshake happens when the app actually runs; this just proves the network
    /// path is open.</summary>
    public static (bool ok, string message) TestPlc(string ipAddress, int port = 102)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            if (!client.ConnectAsync(ipAddress, port).Wait(TimeSpan.FromSeconds(3)))
            {
                return (false, $"Timed out reaching {ipAddress}:{port}.");
            }
            return (true, $"Reachable on port {port}.");
        }
        catch (Exception ex)
        {
            AppLog.Warn("PLC reachability test failed.", ex);
            return (false, ex.Message);
        }
    }
}
