using System.Runtime.CompilerServices;

namespace Tsinswreng.CsSql;

internal sealed class RefEqComparer: IEqualityComparer<object>{
	public bool Equals(object? X, object? Y){
		return ReferenceEquals(X, Y);
	}

	public int GetHashCode(object Obj){
		return RuntimeHelpers.GetHashCode(Obj);
	}
}

