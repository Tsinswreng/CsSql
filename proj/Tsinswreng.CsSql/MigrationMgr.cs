using Tsinswreng.CsCore;
using Tsinswreng.CsPage;

namespace Tsinswreng.CsSql;

/// `IMigrationMgr` 的默認實現。
///
/// 它只做通用遷移編排：
/// - 查最後一次已執行的遷移
/// - 找出尚未執行的遷移
/// - 依次執行並寫入歷史表
public class MigrationMgr: IMigrationMgr{
	/// 當前生效的遷移清單。
	public IList<ISqlMigrationInfo> SqlMigrationInfos{get;set;} = new List<ISqlMigrationInfo>();

	/// 用於獲取 `SchemaHistory` 表元數據並生成查詢 SQL。
	ITblMgr TblMgr;
	/// 用於執行查詢最後一次歷史記錄的 SQL。
	ISqlCmdMkr SqlCmdMkr;

	/// 建立一個通用遷移管理器。
	public MigrationMgr(
		ITblMgr TblMgr
		,ISqlCmdMkr SqlCmdMkr
	){
		this.TblMgr = TblMgr;
		this.SqlCmdMkr = SqlCmdMkr;
	}

	/// 生成「查詢最後一條遷移歷史」的函數。
	///
	/// 不直接返回結果，而是與現有庫內其它 `FnXxx` 風格保持一致，
	/// 先綁定上下文，再返回最終可執行函數。
	public async Task<Func<
		CT
		,Task<SchemaHistory?>
	>> FnGetLastHistory(IDbFnCtx Ctx, CT Ct){
		var T = TblMgr.GetTbl<SchemaHistory>();
		var PCreatedMs = T.Prm(nameof(SchemaHistory.CreatedMs));
		var Sql =
$"""
SELECT * FROM {T.Qt(T.DbTblName)}
WHERE 1=1
ORDER BY {T.QtCol(PCreatedMs)} DESC
{T.SqlMkr.ParamLimOfst(out var Lim, out var Ofst)}
""";
		var Cmd = await SqlCmdMkr.Prepare(Ctx, Sql, Ct);
		return async (Ct)=>{
			//相當於limit 1、但屏蔽sql方言ʹ異
			var PageQry = new PageQry{
				PageSize = 1,
				PageIdx = 0,
			};
			var R = await Cmd
			.Args(
				ArgDict.Mk(T).AddPageQry(PageQry, Lim, Ofst)
			)
			.FirstOrDefault<SchemaHistory>(T, Ct);
			return R;
		};
	}

	/// 便捷方法：在獨立上下文中取最後一條遷移歷史。
	/// 主要用於升級場景判斷「目前庫版本到哪裏了」。
	public async Task<SchemaHistory?> GetLastHistory(CT Ct){
		var Ctx = new DbFnCtx();
		var Fn = await FnGetLastHistory(Ctx, Ct);
		var R = await Fn(Ct);
		return R;
	}

	/// 根據最後已執行的 CreatedMs，找出仍未執行的遷移。
	///
	/// 約定：
	/// - CreatedMs 越大表示越新的遷移
	/// - 所有遷移均只允許向前追加，不允許覆蓋舊遷移
	IList<ISqlMigrationInfo> GetUndeployedInfos(
		[See(nameof(ISqlMigrationInfo.CreatedMs))]
		i64 LastCreatedMs
	){
		var Undeployed = new List<ISqlMigrationInfo>();
		foreach(var Info in SqlMigrationInfos){
			if(Info.CreatedMs > LastCreatedMs){
				Undeployed.Add(Info);
			}
		}
		return Undeployed;
	}

	[Impl(typeof(IMigrationMgr))]
	/// 執行所有未應用遷移。
	///
	/// 流程：
	/// 1. 先查歷史表，得到最後一次已執行的 CreatedMs
	/// 2. 篩出所有更晚的遷移
	/// 3. 依次執行 Up
	/// 4. 每成功一條，就把該遷移寫入 SchemaHistory
	public async Task<nil> RunPendingMigrations(
		IDbFnCtx Ctx
		,ISqlCmdMkr SqlCmdMkr
		,IMkrTxn MkrTxn
		,IRepo<SchemaHistory, i64> RepoHistory
		,CT Ct
	){
		var LastHistory = await GetLastHistory(Ct);
		var LastCreatedMs = LastHistory?.CreatedMs ?? 0;
		var Undeployed = GetUndeployedInfos(LastCreatedMs);
		if(Undeployed.Count == 0){ return NIL; }
		// 這裏沿用現有 repo 舊 API，因爲遷移場景需要在建表/升級同一上下文內立即寫歷史表。
		var InsertHistory = await RepoHistory.FnInsertManyNoPrepare(Ctx, Ct);
		foreach(var Info in Undeployed){
			try{
				// 把純遷移描述轉成可執行遷移對象。
				var Migration = SqlMigration.MkSqlMigration(
					SqlCmdMkr: SqlCmdMkr
					,MkrTxn: MkrTxn
					,SqlMigrationInfo: Info
				);
				var FnUp = await Migration.FnUpAsy(Ctx, Ct);
				await FnUp(Ct);
				// 只有 Up 成功後才記錄歷史，避免「記錄成功但實際未執行」的不一致。
				var History = new SchemaHistory{
					Id = Info.CreatedMs
					,CreatedMs = Info.CreatedMs
					,Name = Info.GetType().Name
				};
				await InsertHistory([History], Ct);
			}catch(MigrationExecutionException ex){
				throw new MigrationExecutionException(
					message: $"Migration execution failed: {Info.GetType().Name} ({Info.CreatedMs})."
					,innerException: ex
					,migrationName: Info.GetType().Name
					,migrationCreatedMs: Info.CreatedMs
					,sqlText: ex.SqlText
				);
			}catch(Exception ex){
				throw new MigrationExecutionException(
					message: $"Migration execution failed: {Info.GetType().Name} ({Info.CreatedMs})."
					,innerException: ex
					,migrationName: Info.GetType().Name
					,migrationCreatedMs: Info.CreatedMs
				);
			}
		}
		return NIL;
	}

	[Impl(typeof(IMigrationMgr))]
	/// 將當前所有已註冊遷移直接標記爲已執行。
	///
	/// 適用於「全新安裝 + 直接按最新版建庫」場景。
	/// 這種情況下不需要逐條執行舊遷移，只要把歷史補齊即可。
	public async Task<nil> MarkAllApplied(
		IDbFnCtx Ctx
		,IRepo<SchemaHistory, i64> RepoHistory
		,CT Ct
	){
		// 同上：這裏需要在同一上下文中立即寫入歷史表。
		var InsertHistory = await RepoHistory.FnInsertManyNoPrepare(Ctx, Ct);
		foreach(var Info in SqlMigrationInfos){
			var History = new SchemaHistory{
				Id = Info.CreatedMs
				,CreatedMs = Info.CreatedMs
				,Name = Info.GetType().Name
			};
			await InsertHistory([History], Ct);
		}
		return NIL;
	}
}

