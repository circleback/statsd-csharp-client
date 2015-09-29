namespace StatsdClient
{
    public interface IMetricsSender
    {
        void Send(string command);
    }
    public interface IStatsdUDP : IMetricsSender // legacy support
    {
    }
}