using Microsoft.EntityFrameworkCore;

namespace Tsinswreng.CsSql.EFCore;

public partial class EfTxnMkr(
	DbContext DbContext
)

	:IMkrTxn
{
	public async Task<ITxn> EnsureTxn(IDbFnCtx Ctx, CT Ct){
		var Tx = await DbContext.Database.BeginTransactionAsync(Ct);
		var R = new EfTxn(Tx);
		return R;
	}
}
