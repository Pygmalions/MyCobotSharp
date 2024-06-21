using System.IO.Ports;

namespace MyCobotSharp;

public static class MyCobotSerialPortConnector
{
    public static MyCobot Connect(string port, int baud = 115200)
    {
        var connection = new SerialPort(port, baud)
        {
            RtsEnable = true,
            DtrEnable = true
        };
        connection.Open();
        return new MyCobot(connection.BaseStream, connection.BaseStream);
    }
}