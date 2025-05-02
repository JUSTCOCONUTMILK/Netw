using System.Net;
using System.Net.Sockets;
using System.Text;

var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

var ipAddress = IPAddress.Parse("127.0.0.1");
var endPoint = new IPEndPoint(ipAddress, 3003);

serverSocket.Bind(endPoint);
serverSocket.Listen(1);

Console.WriteLine("Server is listening");

var clientSocket = serverSocket.Accept();
Console.WriteLine("Client connected");

var buffer = new byte[1024];

try
{
    while (true)
    {
        int receivedBytes = clientSocket.Receive(buffer);
        string message = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
        Console.WriteLine($"Received: {message}");

        if (message.ToLower() == "quit")
        {
            string goodbye = "Goodbye!";
            byte[] goodbyeBytes = Encoding.UTF8.GetBytes(goodbye);
            clientSocket.Send(goodbyeBytes);
            
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            break;
        }

        string response = $"Server received: {message}";
        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        clientSocket.Send(responseBytes);
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}
finally
{
    serverSocket.Close();
}
