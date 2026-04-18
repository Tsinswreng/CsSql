using Tsinswreng.CsCore;

namespace Tsinswreng.CsSql;
public partial class AdoTxnRunner(
	//IDbConnection DbConnection
)
	//:IRunInTxn
	:ITxnRunner
{
	static bool IsTxnCompletedException(Exception ex){
		return ex is InvalidOperationException && ex.Message.Contains("completed", StringComparison.OrdinalIgnoreCase);
	}

	[Impl]
	public async Task<TRet> RunTxn<TRet>(
		ITxn? Txn
		,Func<
			CT, Task<TRet>
		> FnAsy
		,CT Ct
	){
		if(Txn == null){
			TRet R = await FnAsy(Ct);
			return R;
		}
		try{
			await Txn.Begin(Ct);
			TRet R = await FnAsy(Ct);
			await Txn.Commit(Ct);
			return R;
		}
		catch (Exception) {
			try{
				await Txn.Rollback(Ct);
			}catch(Exception ex) when(IsTxnCompletedException(ex)){
				// transaction already completed; ignore rollback
			}
			throw;
		}

		// using var Tx = DbConnection.BeginTransaction(IsolationLevel.Serializable);
		// var AdoTx = new AdoTxn(Tx);
		// //var Ctx = new DbFnCtx{Txn = AdoTx};
		// DbFnCtx.Txn = AdoTx;
		// try{
		// 	var ans = await FnAsy(DbFnCtx, ct);
		// 	Tx.Commit();
		// 	return ans;
		// }
		// catch (System.Exception){
		// 	Tx.Rollback();
		// 	throw;
		// }
	}
}
