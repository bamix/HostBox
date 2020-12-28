using System.Threading.Tasks;

namespace HostBox
{
    public interface IHosting
    {
        Task Run();
    }
}