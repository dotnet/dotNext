using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Messaging
{
    public sealed class MessageHandlerTests : Test
    {
        [Fact]
        public static void MessageHandlerBuilder1()
        {
            var handler = new MessageHandler.Builder()
                .Add<AddMessage, ResultMessage>(static (sender, input, context, token) => Task.FromResult<ResultMessage>(input.Execute()))
                .Add<ResultMessage>(static (sender, input, context, token) => Task.CompletedTask)
                .Build();

            False(handler.As<IInputChannel>().IsSupported(SubtractMessage.Name, false));
            True(handler.As<IInputChannel>().IsSupported(AddMessage.Name, false));
            False(handler.As<IInputChannel>().IsSupported(AddMessage.Name, true));

            False(handler.As<IInputChannel>().IsSupported(ResultMessage.Name, false));
            True(handler.As<IInputChannel>().IsSupported(ResultMessage.Name, true));
        }

        [Fact]
        public static void MessageHandlerBuilder2()
        {
            var handler = new MessageHandler.Builder()
                .Add<AddMessage, ResultMessage>(static (input, context, token) => Task.FromResult<ResultMessage>(input.Execute()))
                .Add<ResultMessage>(static (ResultMessage input, object context, CancellationToken token) => Task.CompletedTask)
                .Build();

            False(handler.As<IInputChannel>().IsSupported(SubtractMessage.Name, false));
            True(handler.As<IInputChannel>().IsSupported(AddMessage.Name, false));
            False(handler.As<IInputChannel>().IsSupported(AddMessage.Name, true));

            False(handler.As<IInputChannel>().IsSupported(ResultMessage.Name, false));
            True(handler.As<IInputChannel>().IsSupported(ResultMessage.Name, true));
        }

        [Fact]
        public static void MessageHandlerBuilder3()
        {
            var handler = new MessageHandler.Builder()
                .Add<AddMessage, ResultMessage>(static (sender, input, token) => Task.FromResult<ResultMessage>(input.Execute()))
                .Add<ResultMessage>(static (ISubscriber sender, ResultMessage input, CancellationToken token) => Task.CompletedTask)
                .Build();

            False(handler.As<IInputChannel>().IsSupported(SubtractMessage.Name, false));
            True(handler.As<IInputChannel>().IsSupported(AddMessage.Name, false));
            False(handler.As<IInputChannel>().IsSupported(AddMessage.Name, true));

            False(handler.As<IInputChannel>().IsSupported(ResultMessage.Name, false));
            True(handler.As<IInputChannel>().IsSupported(ResultMessage.Name, true));
        }

        [Fact]
        public static void MessageHandlerBuilder4()
        {
            var handler = new MessageHandler.Builder()
                .Add<AddMessage, ResultMessage>(static (input, token) => Task.FromResult<ResultMessage>(input.Execute()))
                .Add<ResultMessage>(static (input, token) => Task.CompletedTask)
                .Build();

            False(handler.As<IInputChannel>().IsSupported(SubtractMessage.Name, false));
            True(handler.As<IInputChannel>().IsSupported(AddMessage.Name, false));
            False(handler.As<IInputChannel>().IsSupported(AddMessage.Name, true));

            False(handler.As<IInputChannel>().IsSupported(ResultMessage.Name, false));
            True(handler.As<IInputChannel>().IsSupported(ResultMessage.Name, true));
        }
    }
}