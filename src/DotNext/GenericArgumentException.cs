using System;

namespace DotNext
{
    public class GenericArgumentException: ArgumentException
    {
        public GenericArgumentException(Type genericParam, string message, string paramName = "")
            : base(message, paramName)
        {
            Argument = genericParam;
        }

        public Type Argument { get; }
    }

	public class GenericArgumentException<G>: GenericArgumentException
	{
		public GenericArgumentException(string message, string paramName = "")
			: base(typeof(G), message, paramName)
		{
		}
	}
}