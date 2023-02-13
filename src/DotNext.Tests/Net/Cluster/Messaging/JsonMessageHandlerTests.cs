using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNext.Buffers;

namespace DotNext.Net.Cluster.Messaging;

[ExcludeFromCodeCoverage]
public sealed partial class JsonMessageHandlerTests : Test
{
   private class AddDto : IJsonMessageSerializable<AddDto>
   {
      public int X { get; set; }
      public int Y { get; set; }

      public int Execute() => X + Y;

      public static JsonSerializerOptions Options => null;
      public static JsonTypeInfo<AddDto> TypeInfo => MyJsonContext.Default.AddDto;
      public static MemoryAllocator<byte> Allocator => null;

   }

   private class ResultDto : IJsonMessageSerializable<ResultDto>
   {
      public int Result { get; set; }
      public static implicit operator ResultDto(int value) => new() { Result = value };

      public static JsonSerializerOptions Options => null;
      public static JsonTypeInfo<ResultDto> TypeInfo => MyJsonContext.Default.ResultDto;
      public static MemoryAllocator<byte> Allocator => null;
   }

   [JsonSerializable(typeof(AddDto))]
   [JsonSerializable(typeof(ResultDto))]
   private partial class MyJsonContext : JsonSerializerContext
   {
   }

   private class OneWayTest1 : JsonMessageHandler<AddDto, OneWayTest1>, INameOfMessageHandler
   {
      public override Task OnMessage(AddDto message, ISubscriber sender, object context, CancellationToken token) =>
         Task.FromResult<ResultDto>(message.Execute());

      public static string Name => "Tester";
   }
   private class TwoWayTest1 : JsonMessageHandler<AddDto, ResultDto, TwoWayTest1>, INameOfMessageHandler
   {
      public override Task<ResultDto> OnMessage(AddDto message, ISubscriber sender, object context, CancellationToken token) =>
         Task.FromResult<ResultDto>(message.Execute());

      public static string Name => "Tester2";
   }

   [Fact]
   public static void OneWay_IsOneWay()
   {
      var sut = new OneWayTest1();

      False(sut.As<IInputChannel>().IsSupported(OneWayTest1.Name, false));
      False(sut.As<IInputChannel>().IsSupported(TwoWayTest1.Name, false));
      True(sut.As<IInputChannel>().IsSupported(OneWayTest1.Name, true));
      False(sut.As<IInputChannel>().IsSupported(TwoWayTest1.Name, true));
   }

   [Fact]
   public static void OneWay_throwsIfCalledAsTwoWay()
   {
      var sut = new OneWayTest1();

      ThrowsAsync<UnreachableException>(()=>sut.As<IInputChannel>().ReceiveMessage(null!, null!, null, default));
   }

   [Fact]
   public static void TwoWay_IsTwoWay()
   {
      var sut = new TwoWayTest1();

      False(sut.As<IInputChannel>().IsSupported(OneWayTest1.Name, false));
      True(sut.As<IInputChannel>().IsSupported(TwoWayTest1.Name, false));
      False(sut.As<IInputChannel>().IsSupported(OneWayTest1.Name, true));
      False(sut.As<IInputChannel>().IsSupported(TwoWayTest1.Name, true));
   }

   [Fact]
   public static void TwoWay_throwsIfCalledAsOneWay()
   {
      var sut = new OneWayTest1();

      ThrowsAsync<UnreachableException>(()=>sut.As<IInputChannel>().ReceiveSignal(null!, null!, null, default));
   }

   [Fact]
   public static async Task TwoWay_Executes()
   {
      var sut = new TwoWayTest1();

      var onMessage = await sut.OnMessage(new AddDto() { X = 123, Y = 123 }, null, null, default);
      Equal(123+123, onMessage.Result);
   }
}