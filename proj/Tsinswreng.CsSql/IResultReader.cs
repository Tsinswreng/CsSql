using System.Runtime.CompilerServices;

namespace Tsinswreng.CsSql;


public interface IResultReader{
	[Doc(@$"the SqlCmd who generated this result set")]
	public ISqlCmd Cmd{get;set;}
	[Doc(@$"
	#Sum[Execute and return 2D asynchronous enumerable result sets]
	#Params([Cancellation token])
	#Rtn[Asynchronous stream of result sets]
	")]
	public IAsyncEnumerable<
		IAsyncEnumerable<IDictionary<str, obj?>>
	> AsyE2d(
		CT Ct
	);
	[Doc(@$"
#Sum[Execute and materialize all result sets as 2D collections]
#Params([Cancellation token])
#Rtn[All result sets in memory]
")]
	public Task<IList<
		IList<IDictionary<str, obj?>>>
	> All2d(CT Ct);
	[Doc(@$"
#Sum[Execute and return a flattened asynchronous row stream]
#Params([Cancellation token])
#Rtn[Asynchronous stream containing rows from all result sets]
")]
	public IAsyncEnumerable<IDictionary<str, obj?>> AsyE1dSkipNull(CT Ct);
	[Doc(@$"
#Sum[Execute and return a flattened async stream; keep one slot per result-set, null for empty]
#Params([Cancellation token])
#Rtn[Asynchronous stream containing first row per result-set or null if empty]
")]
	public IAsyncEnumerable<IDictionary<str, obj?>?> AsyE1dWithNull(CT Ct);
	[Doc(@$"
#Sum[Execute and materialize flattened rows as a list]
#Params([Cancellation token])
#Rtn[In-memory rows combined from all result sets]
")]
	public Task<IList<IDictionary<str, obj?>>> All1dSkipNull(CT Ct);
}


public static class TaskIResultReaderExtn{
	extension(Task<IResultReader> z){
		public async IAsyncEnumerable<
			IAsyncEnumerable<IDictionary<str, obj?>>
		> AsyE2d(
			[EnumeratorCancellation]
			CT Ct
		){
			var r = await z;
			var itbl = r.AsyE2d(Ct);
			await foreach(var e in itbl){
				yield return e;
			}
		}

		public async Task<IList<IList<IDictionary<str, obj?>>>> All2d(CT Ct){
			var r = await z;
			return await r.All2d(Ct);
		}

		public async IAsyncEnumerable<IDictionary<str, obj?>> AsyE1dSkipNull(
			[EnumeratorCancellation] CT Ct
		){
			var r = await z;
			var itbl = r.AsyE1dSkipNull(Ct);
			await foreach(var row in itbl){
				yield return row;
			}
		}
		public async IAsyncEnumerable<IDictionary<str, obj?>?> AsyE1dWithNull(
			[EnumeratorCancellation] CT Ct
		){
			var r = await z;
			var itbl = r.AsyE1dWithNull(Ct);
			await foreach(var row in itbl){
				yield return row;
			}
		}

		public async Task<IList<IDictionary<str, obj?>>> All1dSkipNull(CT Ct){
			var r = await z;
			return await r.All1dSkipNull(Ct);
		}
	}
}



// public interface IResultReader<T>{
	
// }
