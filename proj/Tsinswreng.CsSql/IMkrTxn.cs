namespace Tsinswreng.CsSql;

public partial interface IMkrTxn{
	// [Obsolete]
	// public Task<ITxn> GetTxnAsy(CT Ct);

	[Doc(@$"if {nameof(Ctx)} has no transaction,
	make and bind transaction to `{nameof(Ctx)}`")]
	public Task<ITxn> EnsureTxn(
		IDbFnCtx Ctx, CT Ct
	);

}
