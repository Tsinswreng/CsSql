namespace Tsinswreng.CsSql;

public static partial class ExtnIDbFnCtx{
	extension<TSelf>(TSelf z)
		where TSelf: IDbFnCtx
	{
		private TSelf _AddToDispose(
			obj? Disposable
		){
			z.ObjsToDispose??=new List<obj?>();
			z.ObjsToDispose.Add(Disposable);
			return z;
		}

		public TSelf AddToDispose(
			IDisposable Disposable
		){
			return z._AddToDispose(Disposable);
		}

		public TSelf AddToAsyDispose(
			IAsyncDisposable Disposable
		){
			return z._AddToDispose(Disposable);
		}

		public TSelf AddToDispose(
			IEnumerable<obj?> DisposableObjs
		){
			z.ObjsToDispose??=new List<obj?>();
			foreach(var obj in DisposableObjs){
				z.ObjsToDispose.Add(obj);
			}
			return z;
		}
	}

	extension<TSelf>(TSelf z)
		where TSelf: IDbFnCtx
	{
		/// Prepare並AddToDispose
		[Obsolete(@$"use {nameof(ISqlCmdMkr.Prepare)} directly")]
		public async Task<ISqlCmd> PrepareToDispose(
			ISqlCmdMkr CmdMkr
			,str Sql
			,CT Ct
		){
			var R = await CmdMkr.Prepare(z, Sql, Ct);
			//z?.AddToDispose(R); //2025_1225_105738 MkCmd中已有AddToDispose
			return R;
		}
		
		[Doc(@$"Cmd.AttachCtxTxn(DbFnCtx).Args(Arg);")]
		public ISqlCmd RunCmd(
			ISqlCmd Cmd
			,IArgDict Arg
		){
			Cmd.AttachCtxTxn(z).Args(Arg);
			return Cmd;
		}
	}


	// public static async Task<ISqlCmd> Prepare(
	// 	this IBaseDbFnCtx z
	// 	,ISqlCmdMkr SqlCmdMkr
	// 	,str Sql
	// 	,CT Ct
	// ){
	// 	var R = await SqlCmdMkr.Prepare(z, Sql, Ct);
	// 	z.AddToDispose(R);
	// 	return R;
	// }
}
