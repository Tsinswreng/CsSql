using Tsinswreng.CsCore;

namespace Tsinswreng.CsSql;

public interface I_CreatedMs{
	public i64 CreatedMs{get;}
}


public partial interface IMigration:I_CreatedMs{
	public Task<Func<
		CT, Task<nil>
	>> FnUpAsy(IDbFnCtx Ctx, CT Ct);

	public Task<Func<
		CT, Task<nil>
	>> FnDownAsy(IDbFnCtx Ctx, CT Ct);
}

public interface ISqlMigrationInfo:I_CreatedMs{
	public IList<str> SqlsUp{get;}
	public IList<str> SqlsDown{get;}
}

public class SqlMigrationInfo:ISqlMigrationInfo{
	public virtual i64 CreatedMs{get;set;}
	public virtual IList<str> SqlsUp{get;set;} = [];
	public virtual IList<str> SqlsDown{get;set;} = [];
}



public class SqlMigration
	:ISqlMigrationInfo
	,IMigration
{

	//PassArgsByNameBecauseArgsCntMayChange
	public static SqlMigration MkSqlMigration(
		ISqlCmdMkr SqlCmdMkr
		,IMkrTxn MkrTxn
		,ISqlMigrationInfo SqlMigrationInfo
	){
		var R = new SqlMigration(
			SqlCmdMkr: SqlCmdMkr
			,MkrTxn: MkrTxn
		);
		R.CreatedMs = SqlMigrationInfo.CreatedMs;
		R.SqlsUp = SqlMigrationInfo.SqlsUp;
		R.SqlsDown = SqlMigrationInfo.SqlsDown;
		return R;
	}

	public i64 CreatedMs{get;set;}
	public IList<str> SqlsUp{get;set;} = [];
	public IList<str> SqlsDown{get;set;} = [];

	ISqlCmdMkr SqlCmdMkr;
	IMkrTxn MkrTxn;
	public SqlMigration(
		ISqlCmdMkr SqlCmdMkr
		,IMkrTxn MkrTxn
	){
		this.SqlCmdMkr = SqlCmdMkr;
		this.MkrTxn = MkrTxn;
	}

	async Task<nil> RunSql(IDbFnCtx Ctx, str Sql, CT Ct){
		//IBaseDbFnCtx Ctx = new BaseDbFnCtx();
		try{
			var Cmd = await SqlCmdMkr.MkCmd(Ctx, Sql, Ct);
			await Cmd.All1d(Ct);
			return NIL;
		}catch(Exception ex){
			throw new MigrationExecutionException(
				message: "Migration SQL execution failed."
				,innerException: ex
				,migrationCreatedMs: CreatedMs
				,sqlText: Sql
			);
		}
	}

	[Impl(typeof(IMigration))]
	public async Task<Func<
		CT, Task<nil>
	>> FnUpAsy(IDbFnCtx Ctx, CT Ct){
		return async(Ct)=>{
			foreach(var sql in SqlsUp){
				await RunSql(Ctx, sql, Ct);
			}
			return NIL;
		};
	}

	[Impl(typeof(IMigration))]
	public Task<Func<
		CT, Task<nil>
	>> FnDownAsy(IDbFnCtx Ctx, CT Ct){
		throw new NotImplementedException();
	}
}
