using System.Runtime.CompilerServices;

namespace Tsinswreng.CsSql;
public static class ExtnSqlCmd{
	extension(ISqlCmd z){
		
		public async IAsyncEnumerable<IDictionary<str, obj?>> AsyE1d(
			[EnumeratorCancellation]
			CT Ct
		){
			var R = await z.ExeReader(Ct);
			var itbl = R.AsyE1dSkipNull(Ct);
			await foreach(var e in itbl){
				yield return e;
			}
		}
		/// 保留每個結果集一個位置；空結果集輸出null
		public async IAsyncEnumerable<IDictionary<str, obj?>?> AsyE1dWithNull(
			[EnumeratorCancellation]
			CT Ct
		){
			var R = await z.ExeReader(Ct);
			var itbl = R.AsyE1dWithNull(Ct);
			await foreach(var e in itbl){
				yield return e;
			}
		}
		public async Task<IList<IDictionary<str, obj?>>> All1d(CT Ct){
			var R = await z.ExeReader(Ct);
			return await R.All1dSkipNull(Ct);
		}
		public async IAsyncEnumerable<IAsyncEnumerable<IDictionary<str, obj?>>> AsyE2d(
			[EnumeratorCancellation]
			CT Ct
		){
			var R = await z.ExeReader(Ct);
			var itbl = R.AsyE2d(Ct);
			await foreach(var e in itbl){
				yield return e;
			}
		}
		public async Task<IList<IList<IDictionary<str, obj?>>>> All2d(CT Ct){
			var R = await z.ExeReader(Ct);
			return await R.All2d(Ct);
		}
			
		
		public async Task<IDictionary<str, obj?>> DictFirst(
			CT Ct
		){
			var AsyE = z.AsyE1d(Ct);
			var i = 0;
			IDictionary<str, obj?> R = null!;
			await foreach(var e in AsyE){
				if(i >= 1){
					break;
				}
				R = e;
				i++;
			}
			if(i == 0){
				throw new InvalidOperationException("no result");
			}
			return R;
		}
		
		public ISqlCmd WithCtx(IDbFnCtx? Ctx){
			if(Ctx is null){
				return z;
			}
			z.AttachCtxTxn(Ctx);
			Ctx.AddToDispose(z);
			return z;
		}

		public async Task<T?> FirstOrDefault<T>(
			ITable Tbl, CT Ct
		)where T:new(){
			var AsyE = z.AsyE1d(Ct);
			var i = 0;
			IDictionary<str, obj?> Dict = null!;
			await foreach(var e in AsyE){
				if(i >= 1){
					break;
				}
				Dict = e;
				i++;
			}
			if(i == 0){
				return default;
			}
			var R = Tbl.DbDictToEntity<T>(Dict);
			return R;
		}

		public async Task<T?> First<T>(
			ITable Tbl, CT Ct
		)where T:new(){
			var R = await z.FirstOrDefault<T>(Tbl, Ct);
			if(R is null){
				throw new InvalidOperationException("no result");
			}
			return R;
		}



		public async Task<IDictionary<str, obj?>?> DictFirstOrDefault(
			CT Ct
		){
			var AsyE = z.AsyE1d(Ct);
			var i = 0;
			IDictionary<str, obj?> R = null!;
			await foreach(var e in AsyE){
				if(i >= 1){
					break;
				}
				R = e;
				i++;
			}
			if(i == 0){
				return null;
			}
			return R;
		}
	}
}
