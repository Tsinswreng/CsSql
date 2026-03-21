namespace Tsinswreng.CsSql.Sqlite;

using Microsoft.Data.Sqlite;
using Tsinswreng.CsCore;


using IDbFnCtx = Tsinswreng.CsSql.IDbFnCtx;
public partial class SqliteCmdMkr
	:ISqlCmdMkr
	,IMkrTxn
{
	// [Obsolete]
	// public IDbConnection? DbConnection{get;set;}

	//#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	
	public EDbSrcType DbSrcType => EDbSrcType.Sqlite;
	public IDbConnMgr DbConnGetter{get;set;}

	public SqliteCmdMkr(IDbConnMgr DbConnGetter){
		this.DbConnGetter = DbConnGetter;
	}


	[Doc($"""

	""")]
	[Impl(typeof(ISqlCmdMkr))]
	public async Task<ISqlCmd> MkCmd(
		IDbFnCtx? DbFnCtx
		,str Sql
		,CT Ct
	){
		var DbConnection = DbFnCtx?.DbConn?? await DbConnGetter.GetConnAsy(Ct);
		if(DbConnection is not SqliteConnection sqlConn){
			throw new InvalidOperationException("DbConnection is not SqlConnection");
		}
		var RawCmd = sqlConn.CreateCommand();
		RawCmd.CommandText = Sql;
		var R = new SqliteCmd(RawCmd);
		R.Sql = Sql;
		if(DbFnCtx != null){
			R.WithCtx(DbFnCtx);
		}
		R.FnsOnDispose.Add(async()=>{
			return await DbConnGetter.AfterUsingConnAsy(DbConnection, default);
		});
		return R;
	}

	[Impl(typeof(ISqlCmdMkr))]
	public async Task<ISqlCmd> Prepare(ISqlCmd Cmd, CT Ct){
		if(Cmd is not SqliteCmd SqlCmd){
			throw new InvalidOperationException("ISqlCmd is not SqliteCmd");
		}
		try{
			SqlCmd.RawCmd.Prepare();
			return Cmd;
		}
		catch (System.Exception e){
			throw new Exception(
				"Prepare failed"
				+$" Sql:\n{Cmd.Sql}\n"
				,e
			);
		}

	}


	/// Prepare叵用于CREATE TABLE
	[Impl(typeof(ISqlCmdMkr))]
	public async Task<ISqlCmd> Prepare(
		IDbFnCtx? DbFnCtx
		,str Sql
		,CT Ct
	){
		var Cmd = await MkCmd(DbFnCtx, Sql, Ct);
		return await Prepare(Cmd, Ct);
	}


	[Impl(typeof(IMkrTxn))]
	public async Task<ITxn> EnsureTxn(
		IDbFnCtx Ctx, CT Ct
	){
		if(Ctx.Txn is not null){
			return Ctx.Txn;
		}
		var DbConnection = Ctx.DbConn??await DbConnGetter.GetConnAsy(Ct); //事務過後 Ctx會Dispose 故每次開Ctx旹其DbConn必取自DbConnGetter
		//var DbConnection = await DbConnGetter.GetConnAsy(Ct);
		Ctx.DbConn = DbConnection;
		var Tx = DbConnection.BeginTransaction();
		var Ans = new AdoTxn(Tx);
		Ctx.Txn = Ans;
		return Ans;
	}
}
