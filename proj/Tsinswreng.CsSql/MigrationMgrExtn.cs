namespace Tsinswreng.CsSql;

/// 與業務無關的遷移管理輔助函數。
/// 用於按 CreatedMs 去重註冊遷移，避免 Local/Biz 重複寫相同邏輯。
public static class MigrationMgrExtn{
	extension(IMigrationMgr z){
		/// 判斷某個 `CreatedMs` 的遷移是否已註冊。
		///
		/// `CreatedMs` 在這套設計中等價於「遷移版本號」。
		/// 只要它相同，就視作同一條遷移。
		public bool HasMigrationCreatedMs(i64 CreatedMs){
			foreach(var Info in z.SqlMigrationInfos){
				if(Info.CreatedMs == CreatedMs){
					return true;
				}
			}
			return false;
		}

		/// 只在未註冊時添加單個遷移。
		///
		/// 這樣 Local/Biz 只需要聲明「有哪些遷移」，
		/// 不需要自己再寫一份去重邏輯。
		public IMigrationMgr AddMigrationIfAbsent(ISqlMigrationInfo Info){
			if(!z.HasMigrationCreatedMs(Info.CreatedMs)){
				z.AddMigration(Info);
			}
			return z;
		}

		/// 批量註冊遷移（按 `CreatedMs` 去重）。
		///
		/// 約定由調用方按時間順序傳入，從而使遷移執行順序保持可預期。
		public IMigrationMgr AddMigrationsIfAbsent(IEnumerable<ISqlMigrationInfo> Infos){
			foreach(var Info in Infos){
				z.AddMigrationIfAbsent(Info);
			}
			return z;
		}
	}
}
