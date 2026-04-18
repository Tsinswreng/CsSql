namespace Tsinswreng.CsSql;

using Tsinswreng.CsSql;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;

public static class ExtnISqlCmdMkr{
	private static bool IsTxnCompletedException(Exception ex){
		return ex is InvalidOperationException && ex.Message.Contains("completed", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task TryRollbackSafely(ITxn txn, CT ct){
		try{
			await txn.Rollback(ct);
		}catch(Exception ex) when(IsTxnCompletedException(ex)){
			// transaction already completed; ignore rollback
		}
	}

	private static (
		IList<IParamAutoBinderMulti> ManyBinders,
		IList<IParamAutoBinderMultiAsy> ManyAsyncBinders,
		IList<IParamAutoBinder> OneBinders
	) SplitBinders(IList<IParamAutoBinder> binders){
		var many = binders
			.Where(x=>x is IParamAutoBinderMulti && x is not IParamAutoBinderMultiAsy)
			.Cast<IParamAutoBinderMulti>()
			.ToList();
		var manyAsync = binders
			.Where(x=>x is IParamAutoBinderMultiAsy)
			.Cast<IParamAutoBinderMultiAsy>()
			.ToList();
		var one = binders.Where(x=>x is not IParamAutoBinderMulti && x is not IParamAutoBinderMultiAsy).ToList();
		return (many, manyAsync, one);
	}

	private static async IAsyncEnumerable<IDictionary<str, obj?>> YieldRowsOrNull(
		ISqlCmd SqlCmd,
		IArgDict ArgDict,
		[EnumeratorCancellation] CT Ct
	){
		// Iterate per result-set to preserve one-slot output for empty result-sets.
		var d2 = SqlCmd.Args(ArgDict).AsyE2d(Ct);
		await foreach(var d1 in d2.WithCancellation(Ct)){
			var hasAny = false;
			await foreach(var row in d1.WithCancellation(Ct)){
				hasAny = true;
				yield return row;
			}
			if(!hasAny){
				yield return null!;
			}
		}
	}

	private static async IAsyncEnumerable<IDictionary<str, obj?>> YieldRows(
		ISqlCmd SqlCmd,
		IArgDict ArgDict,
		[EnumeratorCancellation] CT Ct
	){
		var d1 = SqlCmd.Args(ArgDict).AsyE1d(Ct);
		await foreach(var row in d1.WithCancellation(Ct)){
			yield return row;
		}
	}

	private static async Task<ISqlCmd> EnsureCmdForBatch(
		ISqlCmdMkr CmdMkr,
		IDbFnCtx Ctx,
		IAutoBindSqlDuplicator Sql,
		u64 BatchSize, // 批大小
		u64 Cnt, //入參數量 (或滿一批 亦或不滿批(末批))
		ISqlCmd? FullBatchCmd,
		CT Ct
	){
		if(Cnt == BatchSize){
			FullBatchCmd ??= await CmdMkr.Prepare(Ctx, Sql.DuplicateSql(BatchSize), Ct);
			return FullBatchCmd;
		}
		return await CmdMkr.Prepare(Ctx, Sql.DuplicateSql(Cnt), Ct);
	}

	private static IArgDict BuildArgsForBatch(
		IList<IParamAutoBinder> oneBinders,
		IList<IParamAutoBinderMulti> manyBinders,
		IList<IList> batches
	){
		var args = ArgDict.Mk();
		var repeatCnt = batches.Count == 0 ? 0ul : (u64)batches[0].Count;
		BindOneBinders(args, oneBinders, repeatCnt);
		for(var i=0; i<manyBinders.Count; i++){
			manyBinders[i].BindBatch(args, batches[i]);
		}
		return args;
	}

	private static void BindOneBinders(
		IArgDict args,
		IList<IParamAutoBinder> oneBinders,
		u64 repeatCnt
	){
		foreach(var binder in oneBinders){
			if(binder is IParamAutoBinderOneBatch oneBatch){
				oneBatch.BindBatch(args, repeatCnt == 0 ? 1ul : repeatCnt);
			}else{
				binder.Bind(args);
			}
		}
	}

	private static bool TryTakeAlignedBatches(
		IList<IParamAutoBinderMulti> MultiBinders,
		u64 BatchSize,
		out IList FirstBatchArgs,
		out IList<IList> ArgBatchList
	){
		FirstBatchArgs = new List<obj?>();
		ArgBatchList = new List<IList>();
		if(!MultiBinders[0].TryTakeBatchArgs(BatchSize, out FirstBatchArgs)){
			return false;
		}
		var cnt = (u64)FirstBatchArgs.Count;
		ArgBatchList = new List<IList>{FirstBatchArgs};
		for(var i=1; i<MultiBinders.Count; i++){
			if(!MultiBinders[i].TryTakeBatchArgs(cnt, out var batchI)){
				throw new InvalidOperationException("ParamAutoBinder.Many(...) length mismatch.");
			}
			if((u64)batchI.Count != cnt){
				throw new InvalidOperationException("ParamAutoBinder.Many(...) length mismatch.");
			}
			ArgBatchList.Add(batchI);
		}
		return true;
	}

	extension(ISqlCmdMkr z){
		[Doc(@$"
#Sum[Create auto batch helper with provider-based default size]
#Params([Db function context],[SQL duplicator],[Batch execution delegate],[Batch size, 0 means auto])
#TParams([Batch item type],[Batch return type])
#Rtn[{nameof(AutoBatch)} instance]
")]
		public AutoBatch<TItem, TRet> AutoBatch<TItem, TRet>(
			IDbFnCtx Ctx,
			ISqlDuplicator SqlDuplicator,
			Func<AutoBatch<TItem, TRet>, IList<TItem>, CT, Task<TRet>> FnAsy,
			u64 BatchSize = 0
		)
		{
			if(BatchSize == 0){
				if(z.DbSrcType.Eq(EDbSrcType.Sqlite)){
					BatchSize = 1;
				}else{
					BatchSize = 500;
				}
			}
			return CsSql.AutoBatch<TItem, TRet>.Mk(Ctx, z, SqlDuplicator, FnAsy, BatchSize);
		}
		
		// public async Task<IDbFnCtx> MkTxnDbFnCtx(CT Ct){
		// 	var R = new DbFnCtx();
		// 	await z.MkEtBindTxn(R, Ct);
		// 	return R;
		// }
		
		[Doc(@$"Exe function in transaction
		no need to provide {nameof(IDbFnCtx)}
		")]
		public async Task<TRtn> RunInTxn<TRtn>(
			CT Ct
			,Func<IDbFnCtx, Task<TRtn>> Fn
		){
			var Ctx = new DbFnCtx{};
			await z.EnsureTxn(Ctx, Ct);
			try{
				var R = await Fn(Ctx);
				if(Ctx.Txn is not null){
					await Ctx.Txn.Commit(Ct);
				}
				return R;
			}
			catch (System.Exception ex){
				if(Ctx.Txn is not null){
					await TryRollbackSafely(Ctx.Txn, Ct);
				}
				throw;
			}
			finally{
				await ((IAsyncDisposable)Ctx).DisposeAsync();
			}
		}
		
		[Doc(@$"ensure transaction and Exe function.
		if{nameof(Ctx)} is null or not have {nameof(Ctx.Txn)},
		it will ensure {nameof(Ctx)} with {nameof(Ctx.Txn)}
		
		suitable for making API that don't need the caller to provide {nameof(IDbFnCtx)}.
		that is, if you call the function(FnA) outside,
		set {nameof(Ctx)} to null to start transaction,
		if the function is call by another function(FnB) with {nameof(IDbFnCtx)},
		pass FnB's Ctx to FnA to combine in one transaction.
		")]
		public async Task<TRtn> EnsureTxn<TRtn>(
			IDbFnCtx? Ctx
			,CT Ct
			,Func<IDbFnCtx, Task<TRtn>> Fn
		){
			var hasOuterCtx = Ctx is not null;
			var hasOuterConn = Ctx?.DbConn is not null;
			Ctx ??= new DbFnCtx();
			var hasOuterTxn = Ctx.Txn is not null;
			if(!hasOuterTxn){
				await z.EnsureTxn(Ctx, Ct);
			}
			try{
				var R = await Fn(Ctx);
				if(!hasOuterTxn && Ctx.Txn is not null){
					await Ctx.Txn.Commit(Ct);
				}
				return R;
			}
			catch{
				if(!hasOuterTxn && Ctx.Txn is not null){
					await TryRollbackSafely(Ctx.Txn, Ct);
				}
				throw;
			}
			finally{
				if(!hasOuterCtx || (!hasOuterTxn && !hasOuterConn)){
					await ((IAsyncDisposable)Ctx).DisposeAsync();
				}
			}
		}
		
		[Doc(@$"
		#See[{nameof(RunDupliSql)}] Without param type.
		#Rtn[entities. would emits null placeholder if the corresponding arg got null]
		")]
		public IAsyncEnumerable<T?> RunDupliSql<T>(
			IDbFnCtx Ctx
			,ITable<T> Tbl
			,IAutoBindSqlDuplicator Sql
			,CT Ct
		)where T: new()
		{
			return Impl(Ct);
			
			// Keep null placeholders to preserve 1:1 positional alignment with input args.
			async IAsyncEnumerable<T?> Impl([EnumeratorCancellation] CT Ct2){
				await foreach(var row in z.RunDupliSql(Ctx, Sql, Ct2).WithCancellation(Ct2)){
					if(row is null){
						yield return default;
						continue;
					}
					yield return Tbl.DbDictToEntity(row);
				}
			}
		}
		
		
		[Doc(@$"
#Sum[Execute auto-bound duplicated SQL lazily and flatten as row stream]
#Params([Db function context],[SQL splicer carrying auto binders],[Cancellation token])
#Rtn[Flattened async row stream; emits null placeholder for empty result-set]
#See([{nameof(IAutoBindSqlDuplicator.ParamAutoBinders)}],[{nameof(IParamAutoBinderMultiAsy)}])
")]
		public async IAsyncEnumerable<
			IDictionary<str, obj?>
		> RunDupliSql(
			IDbFnCtx Ctx
			,IAutoBindSqlDuplicator Sql
			,[EnumeratorCancellation] CT Ct
		){
			var BatchSize = z.DbSrcType.Eq(EDbSrcType.Sqlite) ? 1ul : 500ul; //TODO
			var (multiBinders, manyAsyncBinders, oneBinders) = SplitBinders(Sql.ParamAutoBinders);

			ISqlCmd? fullBatchCmd = null;
			ISqlCmd? finalBatchCmd = null;
			try{
				// 无 Multi binder 和异步 binder：执行一次固定参数
				if(multiBinders.Count == 0 && manyAsyncBinders.Count == 0){
					var cmd = await z.Prepare(Ctx, Sql.DuplicateSql(1), Ct);
					finalBatchCmd = cmd;
					var args = ArgDict.Mk();
					foreach(var binder in oneBinders){
						binder.Bind(args);
					}
					await foreach(var row in YieldRows(cmd, args, Ct)){
						yield return row;
					}
					yield break;
				}

				// 只有异步 binder，无同步 binder
				if(multiBinders.Count == 0 && manyAsyncBinders.Count > 0){
					await foreach(var row in z.RunDupliSql(Ctx, Sql, oneBinders, manyAsyncBinders, BatchSize, Ct)){
						yield return row;
					}
					yield break;
				}

				// 同时有同步和异步 binder（不支持混合，报错）
				if(manyAsyncBinders.Count > 0){
					throw new NotSupportedException("Cannot mix sync and async binders in same query");
				}

				// 仅有同步 Multi binder
				while(true){
					if(!TryTakeAlignedBatches(multiBinders, BatchSize, out var firstBatch, out var batches)){
						break;
					}
					var cnt = (u64)firstBatch.Count;

					ISqlCmd curCmd;
					if(cnt == BatchSize){
						fullBatchCmd ??= await EnsureCmdForBatch(z, Ctx, Sql, BatchSize, cnt, fullBatchCmd, Ct);
						curCmd = fullBatchCmd;
					}else{
						if(finalBatchCmd != null && !ReferenceEquals(finalBatchCmd, fullBatchCmd)){
							await finalBatchCmd.DisposeAsync();
						}
						finalBatchCmd = await EnsureCmdForBatch(z, Ctx, Sql, BatchSize, cnt, fullBatchCmd, Ct);
						curCmd = finalBatchCmd;
					}

					var args = BuildArgsForBatch(oneBinders, multiBinders, batches);

					await foreach(var row in YieldRowsOrNull(curCmd, args, Ct)){
						yield return row;
					}
				}
			}finally{
				if(finalBatchCmd != null && !ReferenceEquals(finalBatchCmd, fullBatchCmd)){
					await finalBatchCmd.DisposeAsync();
				}
				if(fullBatchCmd != null){
					await fullBatchCmd.DisposeAsync();
				}
			}
		}

		/// 处理仅有异步 binder 的情况
		private async IAsyncEnumerable<IDictionary<str, obj?>> RunDupliSql(
			IDbFnCtx Ctx,
			IAutoBindSqlDuplicator Sql,
			IList<IParamAutoBinder> oneBinders,
			IList<IParamAutoBinderMultiAsy> manyAsyncBinders,
			u64 BatchSize,
			[EnumeratorCancellation] CT Ct
		){
			ISqlCmd? fullBatchCmd = null;
			ISqlCmd? finalBatchCmd = null;
			try{
				while(true){
					// 从第一个异步 binder 取一批
					var (hasAny, firstBatch) = await manyAsyncBinders[0].TryTakeBatchArgsAsync(BatchSize, Ct);
					if(!hasAny){
						break;
					}

					var cnt = (u64)firstBatch.Count;
					var asyncBatches = new List<IList>{firstBatch};

					// 从其他异步 binder 取相同数量的值
					for(var i = 1; i < manyAsyncBinders.Count; i++){
						var (hasAnyI, batchI) = await manyAsyncBinders[i].TryTakeBatchArgsAsync(cnt, Ct);
						if(!hasAnyI || (u64)batchI.Count != cnt){
							throw new InvalidOperationException("ParamAutoBinderAsync.Many(...) length mismatch.");
						}
						asyncBatches.Add(batchI);
					}

					// 准备 SQL 命令
					ISqlCmd curCmd;
					if(cnt == BatchSize){
						fullBatchCmd ??= await z.Prepare(Ctx, Sql.DuplicateSql(BatchSize), Ct);
						curCmd = fullBatchCmd;
					}else{
						if(finalBatchCmd != null && !ReferenceEquals(finalBatchCmd, fullBatchCmd)){
							await finalBatchCmd.DisposeAsync();
						}
						finalBatchCmd = await z.Prepare(Ctx, Sql.DuplicateSql(cnt), Ct);
						curCmd = finalBatchCmd;
					}

					// 绑定参数
					var args = ArgDict.Mk();
					BindOneBinders(args, oneBinders, cnt);
					for(var i = 0; i < manyAsyncBinders.Count; i++){
						manyAsyncBinders[i].BindBatch(args, asyncBatches[i]);
					}

					// 执行并返回结果
					await foreach(var row in YieldRowsOrNull(curCmd, args, Ct)){
						yield return row;
					}
				}
			}finally{
				if(finalBatchCmd != null && !ReferenceEquals(finalBatchCmd, fullBatchCmd)){
					await finalBatchCmd.DisposeAsync();
				}
				if(fullBatchCmd != null){
					await fullBatchCmd.DisposeAsync();
				}
			}
		}
		
	}
}
