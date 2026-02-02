namespace Psxbox.TBGateway
{
    public class GatewayInfo
    {
        public string? Token { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; } = 1883;
    }
}
