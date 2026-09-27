namespace Tsinswreng.CsSql;

/// 事務
/// Transaction
public partial interface ITxn : IDisposable{
/// 原始事務對象
/// Raw transaction object
	public object? RawTxn{get;}
	public Task<nil> Begin(CT Ct);
	public Task<nil> Commit(CT Ct);
	public Task<nil> Rollback(CT Ct);

	public Task<nil> Rollback(Func<Exception, nil> OnErr, CT Ct);
}


public static class ITxnExtn{
	/// run a function in a transaction
	public static async Task<TRet> RunTxn<TRet>(
		this ITxn Txn
		,Func<
			CT, Task<TRet>
		> FnAsy
		,CT Ct
	){
		try{
			await Txn.Begin(Ct);
			TRet R = await FnAsy(Ct);
			await Txn.Commit(Ct);
			return R;
		}
		catch (Exception) {
			await Txn.Rollback(Ct);
			throw;
		}
	}
}
