using System;
using System.Collections.Generic;
using System.Text;

namespace MissingPieces.Metaprogramming
{
	public interface IOperator<out D>
		where D: Delegate
	{

	}
}
