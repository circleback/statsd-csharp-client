using System.Threading.Tasks;

namespace StatsdClient
{
    public interface IMetricsSender
    {
        Task Send(string command);
    }
    public interface IStatsdUDP : IMetricsSender // legacy support
    {
    }
}