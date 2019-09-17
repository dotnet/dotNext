using System.Threading.Tasks;

namespace RaftNode
{
    internal interface IValueProvider
    {
        Task<long> GetValueAsync();
    }
}
