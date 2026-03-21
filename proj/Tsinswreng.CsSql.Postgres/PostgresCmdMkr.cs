namespace Tsinswreng.CsSql.Postgres;
using System.Data;
using Npgsql;
using Tsinswreng.CsCore;
using IDbFnCtx = Tsinswreng.CsSql.IDbFnCtx;

public partial class PostgresCmdMkr
	:ISqlCmdMkr
	,IMkrTxn
{
	public EDbSrcType DbSrcType => EDbSrcType.Postgres;
	public IDbConnMgr DbConnGetter{get;set;}
	public PostgresCmdMkr(IDbConnMgr DbConnGetter){
		this.DbConnGetter = DbConnGetter;
	}

	[Impl]
	public async Task<ISqlCmd> MkCmd(
		IDbFnCtx? DbFnCtx
		,str Sql
		,CT Ct
	){
		var DbConnection = DbFnCtx?.DbConn??await DbConnGetter.GetConnAsy(Ct);
		if(DbConnection is not NpgsqlConnection sqlConn){
			throw new InvalidOperationException("DbConnection is not NpgsqlConnection");
		}
		var RawCmd = sqlConn.CreateCommand();
		RawCmd.CommandText = Sql;
		var R = new PostgresCmd(RawCmd);
		R.Sql = Sql;
		if(DbFnCtx!= null){
			R.WithCtx(DbFnCtx);
		}
		R.FnsOnDispose.Add(async()=>{
			return await DbConnGetter.AfterUsingConnAsy(DbConnection, default);
		});
		return R;
	}

	/// Npgsql叵 先Prepare後傳參數。若需效sqlite芝prepare後多次傳異ʹ參數 宜用Batch
	[Impl]
	public async Task<ISqlCmd> Prepare(ISqlCmd Cmd, CT Ct){

		return Cmd;
		// if(Cmd is not PostgresCmd SqlCmd){
		// 	throw new InvalidOperationException("ISqlCmd is not SqliteCmd");
		// }
		// SqlCmd.RawCmd.Prepare();
		// return Cmd;
	}


	/// Prepare叵用于CREATE TABLE
	public async Task<ISqlCmd> Prepare(
		IDbFnCtx? DbFnCtx
		,str Sql
		, CT Ct
	){
		var Cmd = await MkCmd(DbFnCtx, Sql, Ct);
		return await Prepare(Cmd, Ct);
	}


	public async Task<ITxn> EnsureTxn(
		IDbFnCtx Ctx, CT Ct
	){
		// pg中同一交易中 ʹ多條命令 須屬于同一連接。後MkCmd旹所用ʹ連接 亦 優先從Ctx.DbConn中取
		//事務過後 Ctx會Dispose 故每次開Ctx旹其DbConn必取自DbConnGetter
		if(Ctx.Txn is not null){
			return Ctx.Txn;
		}
		var DbConnection = Ctx.DbConn??await DbConnGetter.GetConnAsy(Ct);
		//var DbConnection = await DbConnGetter.GetConnAsy(Ct);
		Ctx.DbConn = DbConnection;
		var Tx = DbConnection.BeginTransaction();
		var Ans = new AdoTxn(Tx);
		Ctx.Txn = Ans;
		return Ans;
	}
}
