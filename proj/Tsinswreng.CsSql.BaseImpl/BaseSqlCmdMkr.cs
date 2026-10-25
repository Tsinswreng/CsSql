namespace Tsinswreng.CsSql.BaseImpl;

public abstract class BaseSqlCmdMkr:ISqlCmdMkr{
	public virtual Func<IDbFnCtx?, Task<IDbFnCtx>> FnInitDbFnCtx{get;set;} = async(Ctx)=>{
		return Ctx??new DbFnCtx();
	};

	public abstract EDbSrcType DbSrcType{get;}

	public abstract Task<ITxn> EnsureTxn(IDbFnCtx Ctx, CT Ct);

	public abstract Task<ISqlCmd> MkCmd(IDbFnCtx? DbFnCtx, string Sql, CT Ct);

	public abstract Task<ISqlCmd> Prepare(ISqlCmd Cmd, CT Ct);

	public abstract Task<ISqlCmd> Prepare(IDbFnCtx? DbFnCtx, string Sql, CT Ct);
}
